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
    private readonly IOptions<AzureDevOpsOptions> _azureDevOpsOptions;
    private readonly ILogger<ConsolidateReleaseDataTask> _logger;

    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="redisService">Redis 服務</param>
    /// <param name="azureDevOpsOptions">Azure DevOps 設定選項</param>
    /// <param name="logger">日誌記錄器</param>
    public ConsolidateReleaseDataTask(
        IRedisService redisService,
        IOptions<AzureDevOpsOptions> azureDevOpsOptions,
        ILogger<ConsolidateReleaseDataTask> logger)
    {
        _redisService = redisService ?? throw new ArgumentNullException(nameof(redisService));
        _azureDevOpsOptions = azureDevOpsOptions ?? throw new ArgumentNullException(nameof(azureDevOpsOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 執行整合 Release 資料任務
    /// </summary>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始整合 Release 資料");

        // 步驟 1：從 Redis 讀取 Bitbucket 與 GitLab ByUser PR 資料
        var bitbucketJson = await _redisService.GetAsync(RedisKeys.BitbucketPullRequestsByUser);
        var gitlabJson = await _redisService.GetAsync(RedisKeys.GitLabPullRequestsByUser);

        var bitbucketResult = bitbucketJson?.ToTypedObject<FetchResult>();
        var gitlabResult = gitlabJson?.ToTypedObject<FetchResult>();

        // 步驟 2：驗證 PR 資料存在
        var bitbucketEmpty = bitbucketResult == null || bitbucketResult.Results.Count == 0 ||
                             bitbucketResult.Results.All(r => r.PullRequests.Count == 0);
        var gitlabEmpty = gitlabResult == null || gitlabResult.Results.Count == 0 ||
                          gitlabResult.Results.All(r => r.PullRequests.Count == 0);

        if (bitbucketEmpty && gitlabEmpty)
        {
            throw new InvalidOperationException(
                $"缺少 PR 資料，以下 Redis Key 均不存在或為空：" +
                $"{RedisKeys.BitbucketPullRequestsByUser}、{RedisKeys.GitLabPullRequestsByUser}");
        }

        // 步驟 3：建立 PrId → (MergeRequestOutput, ProjectName) 查詢字典
        var prLookup = BuildPrLookup(bitbucketResult, gitlabResult);

        _logger.LogInformation("PR 查詢字典建立完成，共 {Count} 個 PrId", prLookup.Count);

        // 步驟 4：從 Redis 讀取 UserStories Work Item 資料
        var userStoriesJson = await _redisService.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems);
        var userStoriesResult = userStoriesJson?.ToTypedObject<UserStoryFetchResult>();

        // 步驟 5：驗證 Work Item 資料存在
        if (userStoriesResult == null || userStoriesResult.WorkItems.Count == 0)
        {
            throw new InvalidOperationException(
                $"缺少 Work Item 資料，Redis Key 不存在或為空：{RedisKeys.AzureDevOpsUserStoryWorkItems}");
        }

        _logger.LogInformation("從 Redis 讀取到 {Count} 筆 Work Item", userStoriesResult.WorkItems.Count);

        // 步驟 6：建立 TeamMapping 查詢字典（忽略大小寫）
        var teamMapping = BuildTeamMapping();

        // 步驟 7：遍歷 Work Items，配對 PR 資料，依 ProjectName 分組
        var projectGroups = BuildProjectGroups(userStoriesResult.WorkItems, prLookup, teamMapping);

        // 步驟 8：序列化結果並寫入 Redis
        var consolidatedResult = new ConsolidatedReleaseResult
        {
            Projects = projectGroups
        };

        var resultJson = consolidatedResult.ToJson();
        await _redisService.SetAsync(RedisKeys.ConsolidatedReleaseData, resultJson, null);

        _logger.LogInformation("整合 Release 資料完成，共 {Count} 個專案分組", projectGroups.Count);
    }

    /// <summary>
    /// 建立 PrId 查詢字典
    /// </summary>
    private static Dictionary<string, List<(MergeRequestOutput Pr, string ProjectName)>> BuildPrLookup(
        FetchResult? bitbucketResult,
        FetchResult? gitlabResult)
    {
        var lookup = new Dictionary<string, List<(MergeRequestOutput, string)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var fetchResult in new[] { bitbucketResult, gitlabResult })
        {
            if (fetchResult == null)
            {
                continue;
            }

            foreach (var projectResult in fetchResult.Results)
            {
                var projectName = GetProjectName(projectResult.ProjectPath);
                foreach (var pr in projectResult.PullRequests)
                {
                    if (string.IsNullOrEmpty(pr.PrId))
                    {
                        continue;
                    }

                    if (!lookup.TryGetValue(pr.PrId, out var list))
                    {
                        list = new List<(MergeRequestOutput, string)>();
                        lookup[pr.PrId] = list;
                    }

                    list.Add((pr, projectName));
                }
            }
        }

        return lookup;
    }

    /// <summary>
    /// 建立 TeamMapping 查詢字典（忽略大小寫）
    /// </summary>
    private Dictionary<string, string> BuildTeamMapping()
    {
        var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var teamMap in _azureDevOpsOptions.Value.TeamMapping)
        {
            if (!string.IsNullOrEmpty(teamMap.OriginalTeamName))
            {
                mapping[teamMap.OriginalTeamName] = teamMap.DisplayName;
            }
        }

        return mapping;
    }

    /// <summary>
    /// 依 Work Item 清單建立依 ProjectName 分組的整合結果
    /// </summary>
    private static List<ConsolidatedProjectGroup> BuildProjectGroups(
        List<UserStoryWorkItemOutput> workItems,
        Dictionary<string, List<(MergeRequestOutput Pr, string ProjectName)>> prLookup,
        Dictionary<string, string> teamMapping)
    {
        // 合併相同 WorkItemId 的多筆記錄
        var mergedWorkItems = new Dictionary<int, (UserStoryWorkItemOutput WorkItem, List<(MergeRequestOutput Pr, string ProjectName)> Prs)>();

        foreach (var workItem in workItems)
        {
            var matchedPrs = new List<(MergeRequestOutput Pr, string ProjectName)>();

            if (!string.IsNullOrEmpty(workItem.PrId) &&
                prLookup.TryGetValue(workItem.PrId, out var prsForId))
            {
                matchedPrs.AddRange(prsForId);
            }

            if (mergedWorkItems.TryGetValue(workItem.WorkItemId, out var existing))
            {
                existing.Prs.AddRange(matchedPrs);
            }
            else
            {
                mergedWorkItems[workItem.WorkItemId] = (workItem, matchedPrs);
            }
        }

        // 依 ProjectName 分組
        var projectDict = new Dictionary<string, List<ConsolidatedReleaseEntry>>();

        foreach (var (workItemId, (workItem, matchedPrs)) in mergedWorkItems)
        {
            var projectName = matchedPrs.Count > 0
                ? matchedPrs[0].ProjectName
                : "unknown";

            var teamDisplayName = !string.IsNullOrEmpty(workItem.OriginalTeamName) &&
                                  teamMapping.TryGetValue(workItem.OriginalTeamName, out var displayName)
                ? displayName
                : workItem.OriginalTeamName ?? string.Empty;

            var prTitle = matchedPrs.Count > 0 ? matchedPrs[0].Pr.Title : string.Empty;

            // 收集 Authors（依 AuthorName 去重）
            var authorNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var authors = new List<ConsolidatedAuthorInfo>();
            foreach (var (pr, _) in matchedPrs)
            {
                if (!string.IsNullOrEmpty(pr.AuthorName) && authorNames.Add(pr.AuthorName))
                {
                    authors.Add(new ConsolidatedAuthorInfo { AuthorName = pr.AuthorName });
                }
            }

            // 收集 PR URLs
            var pullRequests = matchedPrs
                .Where(p => !string.IsNullOrEmpty(p.Pr.PRUrl))
                .Select(p => new ConsolidatedPrInfo { Url = p.Pr.PRUrl })
                .ToList();

            var entry = new ConsolidatedReleaseEntry
            {
                PrTitle = prTitle,
                WorkItemId = workItemId,
                TeamDisplayName = teamDisplayName,
                Authors = authors,
                PullRequests = pullRequests,
                OriginalData = new ConsolidatedOriginalData
                {
                    WorkItem = workItem,
                    PullRequests = matchedPrs.Select(p => p.Pr).ToList()
                }
            };

            if (!projectDict.TryGetValue(projectName, out var entries))
            {
                entries = new List<ConsolidatedReleaseEntry>();
                projectDict[projectName] = entries;
            }

            entries.Add(entry);
        }

        // 每組內依 TeamDisplayName 升冪 → WorkItemId 升冪 排序
        return projectDict
            .Select(kvp => new ConsolidatedProjectGroup
            {
                ProjectName = kvp.Key,
                Entries = kvp.Value
                    .OrderBy(e => e.TeamDisplayName, StringComparer.Ordinal)
                    .ThenBy(e => e.WorkItemId)
                    .ToList()
            })
            .ToList();
    }

    /// <summary>
    /// 從 ProjectPath 取得專案名稱（split('/') 後取最後一段）
    /// </summary>
    private static string GetProjectName(string projectPath)
    {
        if (string.IsNullOrEmpty(projectPath))
        {
            return "unknown";
        }

        var parts = projectPath.Split('/');
        return parts[^1];
    }
}
