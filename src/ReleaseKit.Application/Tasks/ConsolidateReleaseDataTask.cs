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
    private const string UnknownProjectName = "unknown";
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
        if (prLookup == null)
        {
            return;
        }

        // 2. 從 Redis 讀取 Work Item 資料
        var userStoryResult = await LoadUserStoriesAsync();
        if (userStoryResult.WorkItems.Count == 0)
        {
            return;
        }

        // 3. 整合資料
        var consolidated = ConsolidateData(prLookup, userStoryResult, _options.Value.TeamMapping);

        // 5. 序列化並寫入 Redis
        var json = consolidated.ToJson();
        await _redisService.HashSetAsync(RedisKeys.ReleaseDataHash, RedisKeys.Fields.Consolidated, json);

        _logger.LogInformation("整合 Release 資料完成，共 {ProjectCount} 個專案",
            consolidated.Projects.Count);
    }

    /// <summary>
    /// 從 Redis 讀取 Bitbucket 與 GitLab ByUser PR 資料，並建立以 (PrId, ProjectName) 為 Key 的查詢字典
    /// </summary>
    private async Task<Dictionary<(string PrId, string ProjectName), List<(MergeRequestOutput PR, string ProjectName)>>?> LoadPullRequestsAsync()
    {
        var bitbucketJson = await _redisService.HashGetAsync(RedisKeys.BitbucketHash, RedisKeys.Fields.PullRequestsByUser);
        var gitLabJson = await _redisService.HashGetAsync(RedisKeys.GitLabHash, RedisKeys.Fields.PullRequestsByUser);

        var bitbucketResult = bitbucketJson?.ToTypedObject<FetchResult>();
        var gitLabResult = gitLabJson?.ToTypedObject<FetchResult>();

        // US2: 驗證 PR 資料是否存在
        var hasBitbucketData = bitbucketResult?.Results?.Any(r => r.PullRequests.Count > 0) == true;
        var hasGitLabData = gitLabResult?.Results?.Any(r => r.PullRequests.Count > 0) == true;

        if (!hasBitbucketData && !hasGitLabData)
        {
            _logger.LogInformation("沒有 PR 資料可供整合");
            return null;
        }

        var prLookup = new Dictionary<(string PrId, string ProjectName), List<(MergeRequestOutput PR, string ProjectName)>>();

        AddPullRequestsToLookup(prLookup, bitbucketResult);
        AddPullRequestsToLookup(prLookup, gitLabResult);

        _logger.LogInformation("載入 PR 資料完成，共 {Count} 個不重複 (PrId, ProjectName) 組合", prLookup.Count);

        return prLookup;
    }

    /// <summary>
    /// 將 FetchResult 中的 PR 加入查詢字典
    /// </summary>
    private static void AddPullRequestsToLookup(
        Dictionary<(string PrId, string ProjectName), List<(MergeRequestOutput PR, string ProjectName)>> lookup,
        FetchResult? fetchResult)
    {
        if (fetchResult?.Results == null) return;

        foreach (var project in fetchResult.Results)
        {
            var projectName = project.ProjectPath.Split('/').Last();

            foreach (var pr in project.PullRequests)
            {
                if (string.IsNullOrEmpty(pr.PrId)) continue;

                var key = (pr.PrId, projectName);

                if (!lookup.ContainsKey(key))
                {
                    lookup[key] = new List<(MergeRequestOutput, string)>();
                }

                lookup[key].Add((pr, projectName));
            }
        }
    }

    /// <summary>
    /// 從 Redis 讀取 UserStories Work Item 資料
    /// </summary>
    private async Task<UserStoryFetchResult> LoadUserStoriesAsync()
    {
        var json = await _redisService.HashGetAsync(RedisKeys.AzureDevOpsHash, RedisKeys.Fields.WorkItemsUserStories);
        var result = json?.ToTypedObject<UserStoryFetchResult>();

        // US3: 驗證 Work Item 資料是否存在
        if (result == null || result.WorkItems.Count == 0)
        {
            _logger.LogInformation("沒有 Work Item 資料可供整合");
            return new UserStoryFetchResult
            {
                WorkItems = new List<UserStoryWorkItemOutput>(),
                TotalWorkItems = 0,
                AlreadyUserStoryCount = 0,
                FoundViaRecursionCount = 0,
                NotFoundCount = 0,
                OriginalFetchFailedCount = 0
            };
        }

        _logger.LogInformation("載入 Work Item 資料完成，共 {Count} 筆", result.WorkItems.Count);

        return result;
    }

    /// <summary>
    /// 整合 PR 與 Work Item 資料
    /// </summary>
    private ConsolidatedReleaseResult ConsolidateData(
        Dictionary<(string PrId, string ProjectName), List<(MergeRequestOutput PR, string ProjectName)>> prLookup,
        UserStoryFetchResult userStoryResult,
        IReadOnlyList<TeamMappingOptions> teamMapping)
    {
        // 以 (WorkItemId, PrId) 為複合 Key 合併記錄（插件的 WorkItemId 可能為 0，需靠 PrId 區分）
        var workItemGroups = new Dictionary<(int WorkItemId, string PrId), (
            UserStoryWorkItemOutput WorkItem,
            List<MergeRequestOutput> PullRequests,
            HashSet<string> AuthorNames,
            string ProjectName,
            string? FirstPrTitle)>();

        foreach (var wi in userStoryResult.WorkItems)
        {
            if (string.IsNullOrEmpty(wi.PrId))
            {
                throw new InvalidOperationException(
                    $"Work Item {wi.WorkItemId} 缺少 PrId，無法配對 PR 資料");
            }

            var projectName = wi.ProjectName ?? UnknownProjectName;
            var lookupKey = (wi.PrId, projectName);

            if (!prLookup.TryGetValue(lookupKey, out var matchedPrs))
            {
                throw new InvalidOperationException(
                    $"Work Item {wi.WorkItemId} 的 PrId '{wi.PrId}' (ProjectName: '{projectName}') 在 PR 資料中找不到對應記錄");
            }

            var key = (wi.WorkItemId, wi.PrId);

            if (!workItemGroups.TryGetValue(key, out var group))
            {
                group = (
                    WorkItem: wi,
                    PullRequests: new List<MergeRequestOutput>(),
                    AuthorNames: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    ProjectName: projectName,
                    FirstPrTitle: matchedPrs.FirstOrDefault().PR?.Title
                );
                workItemGroups[key] = group;
            }

            foreach (var (pr, _) in matchedPrs)
            {
                group.PullRequests.Add(pr);
                if (!string.IsNullOrEmpty(pr.AuthorName))
                {
                    group.AuthorNames.Add(pr.AuthorName);
                }

                // 若尚無 FirstPrTitle，更新之
                if (group.FirstPrTitle == null)
                {
                    group = group with { FirstPrTitle = pr.Title };
                    workItemGroups[key] = group;
                }
            }
        }

        // 建立 ConsolidatedReleaseEntry 清單，以 ProjectName 分組
        var projectGroups = new Dictionary<string, List<ConsolidatedReleaseEntry>>();

        foreach (var (key, group) in workItemGroups)
        {
            var teamDisplayName = GetTeamDisplayName(group.WorkItem.OriginalTeamName, teamMapping);

            var entry = new ConsolidatedReleaseEntry
            {
                Title = group.WorkItem.Title ?? group.FirstPrTitle ?? string.Empty,
                WorkItemUrl = group.WorkItem.Url ?? string.Empty,
                WorkItemId = key.WorkItemId,
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
            .ToDictionary(
                g => g.Key,
                g => g.Value
                    .OrderBy(e => e.TeamDisplayName, StringComparer.Ordinal)
                    .ThenBy(e => e.WorkItemId)
                    .ToList());

        return new ConsolidatedReleaseResult { Projects = projects };
    }

    /// <summary>
    /// 取得團隊顯示名稱（以 Contains 忽略大小寫比對），找不到對映時使用原始名稱
    /// </summary>
    private static string GetTeamDisplayName(string? originalTeamName, IReadOnlyList<TeamMappingOptions> teamMapping)
    {
        if (string.IsNullOrEmpty(originalTeamName))
        {
            return string.Empty;
        }

        foreach (var team in teamMapping)
        {
            if (!string.IsNullOrEmpty(team.OriginalTeamName) &&
                originalTeamName.Contains(team.OriginalTeamName, StringComparison.OrdinalIgnoreCase))
            {
                return team.DisplayName;
            }
        }

        return originalTeamName;
    }
}
