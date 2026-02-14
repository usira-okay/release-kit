using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ReleaseKit.Application.Common;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Common;
using ReleaseKit.Domain.Entities;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 拉取 Azure DevOps Work Item 資訊任務
/// </summary>
public class FetchAzureDevOpsWorkItemsTask : ITask
{
    private readonly ILogger<FetchAzureDevOpsWorkItemsTask> _logger;
    private readonly IRedisService _redisService;
    private readonly IAzureDevOpsRepository _azureDevOpsRepository;

    /// <summary>
    /// Work Item 與 PR 來源配對
    /// </summary>
    private sealed record WorkItemPullRequestPair(
        int WorkItemId,
        int PullRequestId,
        string ProjectName,
        string PRUrl);

    /// <summary>
    /// 建構子
    /// </summary>
    public FetchAzureDevOpsWorkItemsTask(
        ILogger<FetchAzureDevOpsWorkItemsTask> logger,
        IRedisService redisService,
        IAzureDevOpsRepository azureDevOpsRepository)
    {
        _logger = logger;
        _redisService = redisService;
        _azureDevOpsRepository = azureDevOpsRepository;
    }

    /// <summary>
    /// 執行拉取 Azure DevOps Work Item 資訊任務
    /// </summary>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始拉取 Azure DevOps Work Item 資訊");

        await _redisService.DeleteAsync(RedisKeys.AzureDevOpsWorkItems);

        var projectResults = await LoadProjectResultsFromRedisAsync();
        var allPRCount = projectResults.SelectMany(p => p.PullRequests).Count();

        if (allPRCount == 0)
        {
            _logger.LogWarning("無可用的 PR 資料，任務結束");
            return;
        }

        var pairs = ParseVSTSIdsWithPRInfo(projectResults);

        if (pairs.Count == 0)
        {
            _logger.LogInformation("未從 PR 標題中解析到任何 VSTS ID，任務結束");
            return;
        }

        var uniqueWorkItemCount = pairs.Select(p => p.WorkItemId).Distinct().Count();
        _logger.LogInformation(
            "從 {PRCount} 個 PR 中解析出 {PairCount} 筆 Work Item 配對（{UniqueCount} 個不重複）",
            allPRCount, pairs.Count, uniqueWorkItemCount);

        var workItemOutputs = await FetchWorkItemsAsync(pairs);

        var successCount = workItemOutputs.Count(w => w.IsSuccess);
        var failureCount = workItemOutputs.Count(w => !w.IsSuccess);

        var result = new WorkItemFetchResult
        {
            WorkItems = workItemOutputs,
            TotalPRsAnalyzed = allPRCount,
            TotalWorkItemsFound = uniqueWorkItemCount,
            SuccessCount = successCount,
            FailureCount = failureCount
        };

        await _redisService.SetAsync(RedisKeys.AzureDevOpsWorkItems, result.ToJson(), null);

        _logger.LogInformation(
            "完成 Work Item 查詢：共 {Total} 筆輸出（{UniqueCount} 個不重複），成功 {Success} 筆，失敗 {Failure} 筆",
            workItemOutputs.Count, uniqueWorkItemCount, successCount, failureCount);
    }

    /// <summary>
    /// 從 Redis 載入各平台的 ProjectResult 資料
    /// </summary>
    private async Task<List<ProjectResult>> LoadProjectResultsFromRedisAsync()
    {
        var projectResults = new List<ProjectResult>();

        var redisKeys = new[]
        {
            (Key: RedisKeys.GitLabPullRequestsByUser, Platform: "GitLab"),
            (Key: RedisKeys.BitbucketPullRequestsByUser, Platform: "Bitbucket")
        };

        foreach (var (key, platform) in redisKeys)
        {
            _logger.LogInformation("讀取 Redis Key: {RedisKey}", key);
            var json = await _redisService.GetAsync(key);
            if (json is not null)
            {
                var result = json.ToTypedObject<FetchResult>();
                if (result is not null)
                {
                    projectResults.AddRange(result.Results);
                }
            }
            else
            {
                _logger.LogWarning("Redis Key {RedisKey} 不存在或為空", key);
            }
        }

        return projectResults;
    }

    /// <summary>
    /// 從 PR 標題中解析 VSTS ID 並保留 PR 來源資訊
    /// </summary>
    private List<WorkItemPullRequestPair> ParseVSTSIdsWithPRInfo(List<ProjectResult> projectResults)
    {
        var pairs = new List<WorkItemPullRequestPair>();
        var regex = new Regex(@"VSTS(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        foreach (var project in projectResults)
        {
            foreach (var pr in project.PullRequests)
            {
                foreach (Match match in regex.Matches(pr.Title))
                {
                    if (int.TryParse(match.Groups[1].Value, out var id))
                    {
                        pairs.Add(new WorkItemPullRequestPair(
                            id, pr.PullRequestId, project.ProjectPath, pr.PRUrl));
                    }
                }
            }
        }

        return pairs;
    }

    /// <summary>
    /// 逐一查詢 Work Item 並組裝含 PR 來源資訊的輸出
    /// </summary>
    private async Task<List<WorkItemOutput>> FetchWorkItemsAsync(List<WorkItemPullRequestPair> pairs)
    {
        var outputs = new List<WorkItemOutput>();
        var cache = new Dictionary<int, Result<WorkItem>>();
        var uniqueIds = pairs.Select(p => p.WorkItemId).Distinct().ToList();

        _logger.LogInformation("開始查詢 {WorkItemCount} 個不重複的 Work Item", uniqueIds.Count);
        var processedCount = 0;

        foreach (var workItemId in uniqueIds)
        {
            processedCount++;
            _logger.LogInformation("查詢 Work Item {CurrentCount}/{TotalCount}：{WorkItemId}",
                processedCount, uniqueIds.Count, workItemId);
            cache[workItemId] = await _azureDevOpsRepository.GetWorkItemAsync(workItemId);
        }

        foreach (var pair in pairs)
        {
            var result = cache[pair.WorkItemId];

            if (result.IsSuccess)
            {
                outputs.Add(new WorkItemOutput
                {
                    WorkItemId = result.Value.WorkItemId,
                    Title = result.Value.Title,
                    Type = result.Value.Type,
                    State = result.Value.State,
                    Url = result.Value.Url,
                    OriginalTeamName = result.Value.OriginalTeamName,
                    IsSuccess = true,
                    ErrorMessage = null,
                    SourcePullRequestId = pair.PullRequestId,
                    SourceProjectName = pair.ProjectName,
                    SourcePRUrl = pair.PRUrl
                });
            }
            else
            {
                _logger.LogWarning("查詢 Work Item {WorkItemId} 失敗：{ErrorMessage}",
                    pair.WorkItemId, result.Error.Message);
                outputs.Add(new WorkItemOutput
                {
                    WorkItemId = pair.WorkItemId,
                    Title = null,
                    Type = null,
                    State = null,
                    Url = null,
                    OriginalTeamName = null,
                    IsSuccess = false,
                    ErrorMessage = result.Error.Message,
                    SourcePullRequestId = pair.PullRequestId,
                    SourceProjectName = pair.ProjectName,
                    SourcePRUrl = pair.PRUrl
                });
            }
        }

        return outputs;
    }
}
