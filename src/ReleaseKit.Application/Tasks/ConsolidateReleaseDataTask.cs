using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Application.Common;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 整合 PR 與 Work Item 資料任務
/// </summary>
/// <remarks>
/// 從 Redis 讀取 Bitbucket/GitLab ByUser PR 資料與 UserStories Work Item 資料，
/// 透過 PrId 配對後依專案分組、依團隊顯示名稱與 Work Item ID 排序，
/// 並將整合結果存入 Redis Key <c>ConsolidatedReleaseData</c>。
/// </remarks>
public class ConsolidateReleaseDataTask : ITask
{
    private readonly IRedisService _redisService;
    private readonly IOptions<ConsolidateReleaseDataOptions> _options;
    private readonly ILogger<ConsolidateReleaseDataTask> _logger;

    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="redisService">Redis 服務</param>
    /// <param name="options">整合任務設定選項</param>
    /// <param name="logger">日誌記錄器</param>
    public ConsolidateReleaseDataTask(
        IRedisService redisService,
        IOptions<ConsolidateReleaseDataOptions> options,
        ILogger<ConsolidateReleaseDataTask> logger)
    {
        _redisService = redisService ?? throw new ArgumentNullException(nameof(redisService));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始整合 Release 資料");

        // 1. 從 Redis 讀取 Bitbucket + GitLab ByUser PR 資料
        var bitbucketJson = await _redisService.GetAsync(RedisKeys.BitbucketPullRequestsByUser);
        var gitLabJson = await _redisService.GetAsync(RedisKeys.GitLabPullRequestsByUser);

        var bitbucketFetchResult = bitbucketJson?.ToTypedObject<FetchResult>();
        var gitLabFetchResult = gitLabJson?.ToTypedObject<FetchResult>();

        // 2. 驗證 PR 資料
        var bitbucketHasData = bitbucketFetchResult?.Results?.Count > 0;
        var gitLabHasData = gitLabFetchResult?.Results?.Count > 0;

        if (!bitbucketHasData && !gitLabHasData)
        {
            var missingKeys = new List<string>();
            if (bitbucketFetchResult == null)
                missingKeys.Add(RedisKeys.BitbucketPullRequestsByUser);
            if (gitLabFetchResult == null)
                missingKeys.Add(RedisKeys.GitLabPullRequestsByUser);

            if (missingKeys.Count == 0)
            {
                missingKeys.Add(RedisKeys.BitbucketPullRequestsByUser);
                missingKeys.Add(RedisKeys.GitLabPullRequestsByUser);
            }

            throw new InvalidOperationException(
                $"缺少必要的 PR 資料，請先執行 filter-bitbucket-pr-by-user 或 filter-gitlab-pr-by-user。" +
                $"缺少的 Redis Key: {string.Join(", ", missingKeys)}");
        }

        // 3. 建立 PrId → List<(MergeRequestOutput, ProjectName)> 查詢字典
        var prLookup = new Dictionary<string, List<(MergeRequestOutput Pr, string ProjectName)>>(StringComparer.Ordinal);

        foreach (var fetchResult in new[] { bitbucketFetchResult, gitLabFetchResult })
        {
            if (fetchResult?.Results == null) continue;
            foreach (var projectResult in fetchResult.Results)
            {
                var projectName = GetProjectName(projectResult.ProjectPath);
                foreach (var pr in projectResult.PullRequests)
                {
                    if (string.IsNullOrEmpty(pr.PrId)) continue;
                    if (!prLookup.TryGetValue(pr.PrId, out var list))
                    {
                        list = new List<(MergeRequestOutput, string)>();
                        prLookup[pr.PrId] = list;
                    }
                    list.Add((pr, projectName));
                }
            }
        }

        _logger.LogInformation("建立 PR 查詢字典完成，共 {Count} 筆", prLookup.Count);

        // 4. 從 Redis 讀取 UserStories Work Item 資料
        var userStoriesJson = await _redisService.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems);
        var userStoryFetchResult = userStoriesJson?.ToTypedObject<UserStoryFetchResult>();

        // 5. 驗證 Work Item 資料
        if (userStoryFetchResult == null || userStoryFetchResult.WorkItems.Count == 0)
        {
            throw new InvalidOperationException(
                $"缺少必要的 Work Item 資料，請先執行 get-user-story。" +
                $"缺少的 Redis Key: {RedisKeys.AzureDevOpsUserStoryWorkItems}");
        }

        _logger.LogInformation("從 Redis 讀取到 {Count} 筆 Work Item", userStoryFetchResult.WorkItems.Count);

        // 6. 建立 TeamMapping 查詢字典（忽略大小寫）
        var teamMapping = _options.Value.TeamMapping
            .ToDictionary(
                t => t.OriginalTeamName,
                t => t.DisplayName,
                StringComparer.OrdinalIgnoreCase);

        // 7. 遍歷 Work Items，依 PrId 配對 PR，合併相同 WorkItemId 的多筆記錄
        // 使用 WorkItemId → ConsolidatedReleaseEntry 的暫存字典來合併重複記錄
        var entryByWorkItemId = new Dictionary<int, (
            string PrTitle,
            string TeamDisplayName,
            string ProjectName,
            List<ConsolidatedAuthorInfo> Authors,
            HashSet<string> AuthorNames,
            List<ConsolidatedPrInfo> PrInfos,
            List<MergeRequestOutput> OriginalPrs,
            UserStoryWorkItemOutput WorkItem)>();

        foreach (var workItem in userStoryFetchResult.WorkItems)
        {
            // 取得對映的 TeamDisplayName
            var originalTeamName = workItem.OriginalTeamName ?? string.Empty;
            var teamDisplayName = teamMapping.TryGetValue(originalTeamName, out var mappedName)
                ? mappedName
                : originalTeamName;

            // 依 PrId 查詢配對的 PR 清單
            List<(MergeRequestOutput Pr, string ProjectName)>? matchedPrs = null;
            if (!string.IsNullOrEmpty(workItem.PrId))
            {
                prLookup.TryGetValue(workItem.PrId, out matchedPrs);
            }

            var projectName = matchedPrs?.Count > 0 ? matchedPrs[0].ProjectName : "unknown";
            var prTitle = matchedPrs?.Count > 0 ? matchedPrs[0].Pr.Title : string.Empty;

            if (!entryByWorkItemId.TryGetValue(workItem.WorkItemId, out var existing))
            {
                var authors = new List<ConsolidatedAuthorInfo>();
                var authorNames = new HashSet<string>(StringComparer.Ordinal);
                var prInfos = new List<ConsolidatedPrInfo>();
                var originalPrs = new List<MergeRequestOutput>();

                if (matchedPrs != null)
                {
                    foreach (var (pr, _) in matchedPrs)
                    {
                        if (!string.IsNullOrEmpty(pr.AuthorName) && authorNames.Add(pr.AuthorName))
                            authors.Add(new ConsolidatedAuthorInfo { AuthorName = pr.AuthorName });
                        if (!string.IsNullOrEmpty(pr.PRUrl))
                            prInfos.Add(new ConsolidatedPrInfo { Url = pr.PRUrl });
                        originalPrs.Add(pr);
                    }
                }

                entryByWorkItemId[workItem.WorkItemId] = (
                    prTitle,
                    teamDisplayName,
                    projectName,
                    authors,
                    authorNames,
                    prInfos,
                    originalPrs,
                    workItem);
            }
            else
            {
                // 同一 WorkItemId 有多筆記錄，合併 PR 資訊
                if (matchedPrs != null)
                {
                    foreach (var (pr, _) in matchedPrs)
                    {
                        if (!string.IsNullOrEmpty(pr.AuthorName) && existing.AuthorNames.Add(pr.AuthorName))
                            existing.Authors.Add(new ConsolidatedAuthorInfo { AuthorName = pr.AuthorName });
                        if (!string.IsNullOrEmpty(pr.PRUrl))
                            existing.PrInfos.Add(new ConsolidatedPrInfo { Url = pr.PRUrl });
                        existing.OriginalPrs.Add(pr);
                    }
                }
            }
        }

        // 8. 依 ProjectName 分組，組內依 TeamDisplayName 升冪 → WorkItemId 升冪 排序
        var projectGroups = entryByWorkItemId.Values
            .GroupBy(e => e.ProjectName)
            .Select(g => new ConsolidatedProjectGroup
            {
                ProjectName = g.Key,
                Entries = g
                    .OrderBy(e => e.TeamDisplayName, StringComparer.Ordinal)
                    .ThenBy(e => e.WorkItem.WorkItemId)
                    .Select(e => new ConsolidatedReleaseEntry
                    {
                        PrTitle = e.PrTitle,
                        WorkItemId = e.WorkItem.WorkItemId,
                        TeamDisplayName = e.TeamDisplayName,
                        Authors = e.Authors,
                        PullRequests = e.PrInfos,
                        OriginalData = new ConsolidatedOriginalData
                        {
                            WorkItem = e.WorkItem,
                            PullRequests = e.OriginalPrs
                        }
                    })
                    .ToList()
            })
            .ToList();

        // 9. 序列化並寫入 Redis
        var result = new ConsolidatedReleaseResult { Projects = projectGroups };
        var json = result.ToJson();
        await _redisService.SetAsync(RedisKeys.ConsolidatedReleaseData, json);

        _logger.LogInformation("整合 Release 資料完成，共 {Count} 個專案群組", projectGroups.Count);
        System.Console.WriteLine(json);
    }

    /// <summary>
    /// 從專案路徑取得專案名稱（split('/') 後取最後一段）
    /// </summary>
    /// <param name="projectPath">專案路徑</param>
    /// <returns>專案名稱</returns>
    private static string GetProjectName(string projectPath)
    {
        if (string.IsNullOrEmpty(projectPath))
            return "unknown";

        var parts = projectPath.Split('/');
        return parts[^1];
    }
}
