using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Application.Common;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 整合 Release 資料任務
/// </summary>
/// <remarks>
/// 從 Redis 讀取已過濾的 PR 資料（Bitbucket/GitLab ByUser）與 Work Item 資料（UserStories），
/// 透過 PR ID 配對後，依專案路徑分組、依團隊顯示名稱與 Work Item ID 排序，
/// 並將整合結果存入新的 Redis Key。
/// </remarks>
public class ConsolidateReleaseDataTask : ITask
{
    private readonly IRedisService _redisService;
    private readonly IOptions<ConsolidateReleaseDataOptions> _options;
    private readonly ILogger<ConsolidateReleaseDataTask> _logger;

    /// <summary>
    /// 初始化 <see cref="ConsolidateReleaseDataTask"/> 類別的新執行個體
    /// </summary>
    /// <param name="redisService">Redis 服務</param>
    /// <param name="options">整合任務配置選項</param>
    /// <param name="logger">日誌記錄器</param>
    public ConsolidateReleaseDataTask(
        IRedisService redisService,
        IOptions<ConsolidateReleaseDataOptions> options,
        ILogger<ConsolidateReleaseDataTask> logger)
    {
        _redisService = redisService;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// 執行整合 Release 資料任務
    /// </summary>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始整合 Release 資料");

        // 1. 從 Redis 讀取 PR 資料
        var prLookup = await LoadPullRequestsAsync();

        // 2. 從 Redis 讀取 Work Item 資料
        var userStoryResult = await LoadUserStoriesAsync();

        // 3. 建立 TeamMapping 查詢字典（忽略大小寫）
        var teamMapping = BuildTeamMapping();

        // 4. 整合資料
        var consolidated = ConsolidateData(prLookup, userStoryResult, teamMapping);

        // 5. 序列化並寫入 Redis
        var json = consolidated.ToJson();
        await _redisService.SetAsync(RedisKeys.ConsolidatedReleaseData, json);

        _logger.LogInformation("整合 Release 資料完成，共 {ProjectCount} 個專案",
            consolidated.Projects.Count);
    }

    /// <summary>
    /// 從 Redis 讀取 Bitbucket 與 GitLab ByUser PR 資料，並建立以 PrId 為 Key 的查詢字典
    /// </summary>
    private async Task<Dictionary<string, List<(MergeRequestOutput PR, string ProjectName)>>> LoadPullRequestsAsync()
    {
        var bitbucketJson = await _redisService.GetAsync(RedisKeys.BitbucketPullRequestsByUser);
        var gitLabJson = await _redisService.GetAsync(RedisKeys.GitLabPullRequestsByUser);

        var bitbucketResult = bitbucketJson?.ToTypedObject<FetchResult>();
        var gitLabResult = gitLabJson?.ToTypedObject<FetchResult>();

        // US2: 驗證 PR 資料是否存在
        var hasBitbucketData = bitbucketResult?.Results?.Any(r => r.PullRequests.Count > 0) == true;
        var hasGitLabData = gitLabResult?.Results?.Any(r => r.PullRequests.Count > 0) == true;

        if (!hasBitbucketData && !hasGitLabData)
        {
            throw new InvalidOperationException(
                $"缺少 PR 資料：Redis Key '{RedisKeys.BitbucketPullRequestsByUser}' 與 '{RedisKeys.GitLabPullRequestsByUser}' 均無有效資料");
        }

        var prLookup = new Dictionary<string, List<(MergeRequestOutput PR, string ProjectName)>>();

        AddPullRequestsToLookup(prLookup, bitbucketResult);
        AddPullRequestsToLookup(prLookup, gitLabResult);

        _logger.LogInformation("載入 PR 資料完成，共 {Count} 個不重複 PrId", prLookup.Count);

        return prLookup;
    }

    /// <summary>
    /// 將 FetchResult 中的 PR 加入查詢字典
    /// </summary>
    private static void AddPullRequestsToLookup(
        Dictionary<string, List<(MergeRequestOutput PR, string ProjectName)>> lookup,
        FetchResult? fetchResult)
    {
        if (fetchResult?.Results == null) return;

        foreach (var project in fetchResult.Results)
        {
            var projectName = project.ProjectPath.Split('/').Last();

            foreach (var pr in project.PullRequests)
            {
                if (string.IsNullOrEmpty(pr.PrId)) continue;

                if (!lookup.ContainsKey(pr.PrId))
                {
                    lookup[pr.PrId] = new List<(MergeRequestOutput, string)>();
                }

                lookup[pr.PrId].Add((pr, projectName));
            }
        }
    }

    /// <summary>
    /// 從 Redis 讀取 UserStories Work Item 資料
    /// </summary>
    private async Task<UserStoryFetchResult> LoadUserStoriesAsync()
    {
        var json = await _redisService.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems);
        var result = json?.ToTypedObject<UserStoryFetchResult>();

        // US3: 驗證 Work Item 資料是否存在
        if (result == null || result.WorkItems.Count == 0)
        {
            throw new InvalidOperationException(
                $"缺少 Work Item 資料：Redis Key '{RedisKeys.AzureDevOpsUserStoryWorkItems}' 無有效資料");
        }

        _logger.LogInformation("載入 Work Item 資料完成，共 {Count} 筆", result.WorkItems.Count);

        return result;
    }

    /// <summary>
    /// 建立 TeamMapping 查詢字典（忽略大小寫）
    /// </summary>
    private Dictionary<string, string> BuildTeamMapping()
    {
        var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var team in _options.Value.TeamMapping)
        {
            if (!string.IsNullOrEmpty(team.OriginalTeamName))
            {
                mapping[team.OriginalTeamName] = team.DisplayName;
            }
        }

        return mapping;
    }

    /// <summary>
    /// 整合 PR 與 Work Item 資料
    /// </summary>
    private ConsolidatedReleaseResult ConsolidateData(
        Dictionary<string, List<(MergeRequestOutput PR, string ProjectName)>> prLookup,
        UserStoryFetchResult userStoryResult,
        Dictionary<string, string> teamMapping)
    {
        // 以 WorkItemId 為 Key 合併多筆記錄
        var workItemGroups = new Dictionary<int, (
            UserStoryWorkItemOutput WorkItem,
            List<MergeRequestOutput> PullRequests,
            HashSet<string> AuthorNames,
            string ProjectName,
            string? FirstPrTitle)>();

        foreach (var wi in userStoryResult.WorkItems)
        {
            List<(MergeRequestOutput PR, string ProjectName)>? matchedPrs = null;

            if (!string.IsNullOrEmpty(wi.PrId))
            {
                prLookup.TryGetValue(wi.PrId, out matchedPrs);
            }

            if (!workItemGroups.TryGetValue(wi.WorkItemId, out var group))
            {
                group = (
                    WorkItem: wi,
                    PullRequests: new List<MergeRequestOutput>(),
                    AuthorNames: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    ProjectName: matchedPrs?.FirstOrDefault().ProjectName ?? "unknown",
                    FirstPrTitle: matchedPrs?.FirstOrDefault().PR.Title
                );
                workItemGroups[wi.WorkItemId] = group;
            }

            if (matchedPrs != null)
            {
                foreach (var (pr, projectName) in matchedPrs)
                {
                    group.PullRequests.Add(pr);
                    if (!string.IsNullOrEmpty(pr.AuthorName))
                    {
                        group.AuthorNames.Add(pr.AuthorName);
                    }

                    // 若尚無 ProjectName（先前為 unknown），更新之
                    if (group.ProjectName == "unknown")
                    {
                        group = group with { ProjectName = projectName };
                        workItemGroups[wi.WorkItemId] = group;
                    }

                    // 若尚無 PrTitle，更新之
                    if (group.FirstPrTitle == null)
                    {
                        group = group with { FirstPrTitle = pr.Title };
                        workItemGroups[wi.WorkItemId] = group;
                    }
                }
            }
        }

        // 建立 ConsolidatedReleaseEntry 清單，以 ProjectName 分組
        var projectGroups = new Dictionary<string, List<ConsolidatedReleaseEntry>>();

        foreach (var (workItemId, group) in workItemGroups)
        {
            var teamDisplayName = GetTeamDisplayName(group.WorkItem.OriginalTeamName, teamMapping);

            var entry = new ConsolidatedReleaseEntry
            {
                PrTitle = group.FirstPrTitle ?? string.Empty,
                WorkItemId = workItemId,
                TeamDisplayName = teamDisplayName,
                Authors = group.AuthorNames
                    .Select(name => new ConsolidatedAuthorInfo { AuthorName = name })
                    .ToList(),
                PullRequests = group.PullRequests
                    .Select(pr => new ConsolidatedPrInfo { Url = pr.PRUrl })
                    .ToList(),
                OriginalData = new ConsolidatedOriginalData
                {
                    WorkItem = group.WorkItem,
                    PullRequests = group.PullRequests
                }
            };

            if (!projectGroups.ContainsKey(group.ProjectName))
            {
                projectGroups[group.ProjectName] = new List<ConsolidatedReleaseEntry>();
            }

            projectGroups[group.ProjectName].Add(entry);
        }

        // 排序：每組內依 TeamDisplayName 升冪 → WorkItemId 升冪
        var projects = projectGroups
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new ConsolidatedProjectGroup
            {
                ProjectName = g.Key,
                Entries = g.Value
                    .OrderBy(e => e.TeamDisplayName, StringComparer.Ordinal)
                    .ThenBy(e => e.WorkItemId)
                    .ToList()
            })
            .ToList();

        return new ConsolidatedReleaseResult { Projects = projects };
    }

    /// <summary>
    /// 取得團隊顯示名稱（忽略大小寫），找不到對映時使用原始名稱
    /// </summary>
    private static string GetTeamDisplayName(string? originalTeamName, Dictionary<string, string> teamMapping)
    {
        if (string.IsNullOrEmpty(originalTeamName))
        {
            return string.Empty;
        }

        return teamMapping.TryGetValue(originalTeamName, out var displayName)
            ? displayName
            : originalTeamName;
    }
}
