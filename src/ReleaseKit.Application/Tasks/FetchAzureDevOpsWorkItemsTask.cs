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
    private readonly Dictionary<int, Result<WorkItem>> _workItemCache;

    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="logger">日誌記錄器</param>
    /// <param name="redisService">Redis 服務</param>
    /// <param name="azureDevOpsRepository">Azure DevOps Repository</param>
    public FetchAzureDevOpsWorkItemsTask(
        ILogger<FetchAzureDevOpsWorkItemsTask> logger,
        IRedisService redisService,
        IAzureDevOpsRepository azureDevOpsRepository)
    {
        _logger = logger;
        _redisService = redisService;
        _azureDevOpsRepository = azureDevOpsRepository;
        _workItemCache = new Dictionary<int, Result<WorkItem>>();
    }

    /// <summary>
    /// 執行拉取 Azure DevOps Work Item 資訊任務
    /// </summary>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始拉取 Azure DevOps Work Item 資訊");

        // 清除舊的 Azure DevOps Work Item 資料
        await _redisService.HashDeleteAsync(RedisKeys.AzureDevOpsHash, RedisKeys.Fields.WorkItems);

        // 從 Redis 讀取 PR 資料
        var allPullRequests = await LoadPullRequestsFromRedisAsync();
        
        if (allPullRequests.Count == 0)
        {
            _logger.LogWarning("無可用的 PR 資料，任務結束");
            return;
        }

        // 從 PR 中收集所有 Work Item ID
        var workItemIds = ExtractWorkItemIdsFromPRs(allPullRequests);
        
        if (workItemIds.Count == 0)
        {
            _logger.LogInformation("未從 PR 來源分支中解析到任何 Work Item ID，任務結束");
            return;
        }

        _logger.LogInformation("從 {PRCount} 個 PR 中解析出 {WorkItemCount} 個 Work Item ID（含重複）", allPullRequests.Count, workItemIds.Count);

        // 逐一查詢 Work Item
        var workItemOutputs = await FetchWorkItemsAsync(workItemIds);

        // 統計結果
        var successCount = workItemOutputs.Count(w => w.IsSuccess);
        var failureCount = workItemOutputs.Count(w => !w.IsSuccess);

        // 組裝結果
        var result = new WorkItemFetchResult
        {
            WorkItems = workItemOutputs,
            TotalPRsAnalyzed = allPullRequests.Count,
            TotalWorkItemsFound = workItemIds.Count,
            SuccessCount = successCount,
            FailureCount = failureCount
        };

        // 寫入 Redis
        await _redisService.HashSetAsync(RedisKeys.AzureDevOpsHash, RedisKeys.Fields.WorkItems, result.ToJson());

        _logger.LogInformation(
            "完成 Work Item 查詢：總計 {Total} 個，成功 {Success} 個，失敗 {Failure} 個", 
            workItemIds.Count, successCount, failureCount);
    }

    /// <summary>
    /// 從 Redis 載入 PR 資料
    /// </summary>
    private async Task<List<(MergeRequestOutput PR, string ProjectName)>> LoadPullRequestsFromRedisAsync()
    {
        var allPullRequests = new List<(MergeRequestOutput PR, string ProjectName)>();

        // 定義要讀取的 Redis Hash
        var redisKeys = new[]
        {
            (HashKey: RedisKeys.GitLabHash, Field: RedisKeys.Fields.PullRequestsByUser, Platform: "GitLab"),
            (HashKey: RedisKeys.BitbucketHash, Field: RedisKeys.Fields.PullRequestsByUser, Platform: "Bitbucket")
        };

        // 迴圈處理所有平台
        foreach (var (hashKey, field, platform) in redisKeys)
        {
            var json = await _redisService.HashGetAsync(hashKey, field);
            if (json is not null)
            {
                var result = json.ToTypedObject<FetchResult>();
                if (result is not null)
                {
                    foreach (var project in result.Results)
                    {
                        var projectName = project.ProjectPath.Split('/').Last();
                        foreach (var pr in project.PullRequests)
                        {
                            allPullRequests.Add((pr, projectName));
                        }
                    }
                }
            }
            else
            {
                _logger.LogWarning("Redis Hash {HashKey} Field {Field} 不存在或為空", hashKey, field);
            }
        }

        return allPullRequests;
    }

    /// <summary>
    /// 從 PR 中提取 Work Item ID 與 PR 關聯
    /// </summary>
    /// <param name="pullRequests">PR 與專案名稱的對應清單</param>
    /// <returns>PR ID、Work Item ID 與專案名稱的對應清單（保留重複）</returns>
    /// <remarks>
    /// 直接使用 PR 的 WorkItemId 欄位（已從 SourceBranch 解析）。
    /// 不去重複，保留每個 PR 與 Work Item 的對應關係。
    /// </remarks>
    private List<(string prId, int workItemId, string projectName)> ExtractWorkItemIdsFromPRs(List<(MergeRequestOutput PR, string ProjectName)> pullRequests)
    {
        var workItemPairs = pullRequests
            .Where(item => item.PR.WorkItemId.HasValue)
            .Select(item => (item.PR.PrId, item.PR.WorkItemId!.Value, item.ProjectName))
            .ToList();

        return workItemPairs;
    }

    /// <summary>
    /// 逐一查詢 Work Item
    /// </summary>
    /// <param name="workItemPairs">Work Item ID、PR ID 與專案名稱的對應清單</param>
    /// <returns>Work Item 輸出清單</returns>
    private async Task<List<WorkItemOutput>> FetchWorkItemsAsync(IReadOnlyList<(string prId, int workItemId, string projectName)> workItemPairs)
    {
        var outputs = new List<WorkItemOutput>();

        _logger.LogInformation("開始查詢 {WorkItemCount} 個 Work Item", workItemPairs.Count);
        var processedCount = 0;
        foreach (var (prId, workItemId, projectName) in workItemPairs)
        {
            processedCount++;
            
            // 檢查快取
            if (!_workItemCache.TryGetValue(workItemId, out var result))
            {
                _logger.LogInformation("查詢 Work Item {CurrentCount}/{TotalCount}：{WorkItemId}", processedCount, workItemPairs.Count, workItemId);
                result = await _azureDevOpsRepository.GetWorkItemAsync(workItemId);
                _workItemCache[workItemId] = result;
            }
            else
            {
                _logger.LogInformation("從快取讀取 Work Item {CurrentCount}/{TotalCount}：{WorkItemId}", processedCount, workItemPairs.Count, workItemId);
            }

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
                    PrId = prId,
                    ProjectName = projectName,
                    IsSuccess = true,
                    ErrorMessage = null
                });
            }
            else
            {
                _logger.LogWarning("查詢 Work Item {WorkItemId} 失敗：{ErrorMessage}", workItemId, result.Error.Message);
                outputs.Add(new WorkItemOutput
                {
                    WorkItemId = workItemId,
                    Title = null,
                    Type = null,
                    State = null,
                    Url = null,
                    OriginalTeamName = null,
                    PrId = prId,
                    ProjectName = projectName,
                    IsSuccess = false,
                    ErrorMessage = result.Error.Message
                });
            }
        }

        return outputs;
    }
}
