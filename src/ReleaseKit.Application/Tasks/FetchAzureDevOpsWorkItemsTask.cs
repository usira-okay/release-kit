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
    /// Work Item 與 PR 的配對記錄
    /// </summary>
    /// <remarks>
    /// 用於追蹤 Work Item ID 與其來源 PR 的對應關係，包含 PR 所屬的專案路徑。
    /// </remarks>
    private record WorkItemPullRequestPair(int WorkItemId, MergeRequestOutput PullRequest, string ProjectPath);

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
    }

    /// <summary>
    /// 執行拉取 Azure DevOps Work Item 資訊任務
    /// </summary>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始拉取 Azure DevOps Work Item 資訊");

        // 清除舊的 Azure DevOps Work Item 資料
        await _redisService.DeleteAsync(RedisKeys.AzureDevOpsWorkItems);

        // 從 Redis 讀取 PR 資料及其專案資訊
        var (allPullRequests, pullRequestsByProjectPath) = await LoadPullRequestsWithProjectPathAsync();
        
        if (allPullRequests.Count == 0)
        {
            _logger.LogWarning("無可用的 PR 資料，任務結束");
            return;
        }

        // 建立 (WorkItemId, PR) 配對
        var workItemPrPairs = BuildWorkItemPullRequestPairs(allPullRequests, pullRequestsByProjectPath);
        
        if (workItemPrPairs.Count == 0)
        {
            _logger.LogInformation("未從 PR 標題中解析到任何 VSTS ID，任務結束");
            return;
        }

        _logger.LogInformation("從 {PRCount} 個 PR 中解析出 {PairCount} 個 Work Item-PR 配對", allPullRequests.Count, workItemPrPairs.Count);

        // 查詢 Work Item 並生成輸出
        var workItemOutputs = await FetchAndGenerateWorkItemOutputsAsync(workItemPrPairs);

        // 統計結果
        var successCount = workItemOutputs.Count(w => w.IsSuccess);
        var failureCount = workItemOutputs.Count(w => !w.IsSuccess);

        // 組裝結果
        var result = new WorkItemFetchResult
        {
            WorkItems = workItemOutputs,
            TotalPRsAnalyzed = allPullRequests.Count,
            TotalWorkItemsFound = workItemPrPairs.Count,
            SuccessCount = successCount,
            FailureCount = failureCount
        };

        // 寫入 Redis
        await _redisService.SetAsync(RedisKeys.AzureDevOpsWorkItems, result.ToJson(), null);

        _logger.LogInformation(
            "完成 Work Item 查詢：總計 {Total} 個，成功 {Success} 個，失敗 {Failure} 個", 
            workItemPrPairs.Count, successCount, failureCount);
    }

    /// <summary>
    /// 從 Redis 載入 PR 資料及其所屬專案路徑
    /// </summary>
    /// <returns>PR 清單與 PR 對應的專案路徑對應表</returns>
    private async Task<(List<MergeRequestOutput> AllPRs, Dictionary<MergeRequestOutput, string> PullRequestsByProjectPath)> LoadPullRequestsWithProjectPathAsync()
    {
        var allPullRequests = new List<MergeRequestOutput>();
        var pullRequestsByProjectPath = new Dictionary<MergeRequestOutput, string>();

        // 定義要讀取的 Redis Key
        var redisKeys = new[]
        {
            (Key: RedisKeys.GitLabPullRequestsByUser, Platform: "GitLab"),
            (Key: RedisKeys.BitbucketPullRequestsByUser, Platform: "Bitbucket")
        };

        // 迴圈處理所有平台
        foreach (var (key, platform) in redisKeys)
        {
            var json = await _redisService.GetAsync(key);
            if (json is not null)
            {
                var result = json.ToTypedObject<FetchResult>();
                if (result is not null)
                {
                    foreach (var project in result.Results)
                    {
                        foreach (var pr in project.PullRequests)
                        {
                            allPullRequests.Add(pr);
                            pullRequestsByProjectPath[pr] = project.ProjectPath;
                        }
                    }
                }
            }
            else
            {
                _logger.LogWarning("Redis Key {RedisKey} 不存在或為空", key);
            }
        }

        return (allPullRequests, pullRequestsByProjectPath);
    }

    /// <summary>
    /// 建立 Work Item-PR 配對清單
    /// </summary>
    /// <remarks>
    /// 從所有 PR 中解析 VSTS ID，為每個 ID-PR 組合創建一個配對記錄。
    /// </remarks>
    /// <param name="pullRequests">PR 清單</param>
    /// <param name="pullRequestsByProjectPath">PR 與專案路徑的對應表</param>
    /// <returns>Work Item-PR 配對清單</returns>
    private List<WorkItemPullRequestPair> BuildWorkItemPullRequestPairs(
        List<MergeRequestOutput> pullRequests,
        Dictionary<MergeRequestOutput, string> pullRequestsByProjectPath)
    {
        var pairs = new List<WorkItemPullRequestPair>();
        var regex = new Regex(@"VSTS(\d+)", RegexOptions.None);

        foreach (var pr in pullRequests)
        {
            var matches = regex.Matches(pr.Title);
            
            if (!pullRequestsByProjectPath.TryGetValue(pr, out var projectPath))
            {
                _logger.LogWarning(
                    "無法找到 PR 所屬的專案路徑：PRUrl={PRUrl}，Title={Title}。使用預設值 'unknown'",
                    pr.PRUrl, pr.Title);
                projectPath = "unknown";
            }

            foreach (Match match in matches)
            {
                if (int.TryParse(match.Groups[1].Value, out var workItemId))
                {
                    pairs.Add(new WorkItemPullRequestPair(workItemId, pr, projectPath));
                }
            }
        }

        return pairs;
    }

    /// <summary>
    /// 查詢 Work Item 並為每個 Work Item-PR 配對生成輸出
    /// </summary>
    /// <remarks>
    /// 使用字典緩存 API 查詢結果以避免重複查詢相同的 Work Item ID。
    /// 對於每個配對，生成包含來源 PR 資訊的 WorkItemOutput。
    /// </remarks>
    /// <param name="workItemPrPairs">Work Item-PR 配對清單</param>
    /// <returns>Work Item 輸出清單</returns>
    private async Task<List<WorkItemOutput>> FetchAndGenerateWorkItemOutputsAsync(
        List<WorkItemPullRequestPair> workItemPrPairs)
    {
        var outputs = new List<WorkItemOutput>();
        
        // 收集所有唯一的 Work Item ID
        var uniqueWorkItemIds = workItemPrPairs.Select(p => p.WorkItemId).Distinct().ToList();
        
        _logger.LogInformation("開始查詢 {WorkItemCount} 個不重複的 Work Item（共 {PairCount} 個配對）", 
            uniqueWorkItemIds.Count, workItemPrPairs.Count);

        // 使用字典快取 API 查詢結果
        var workItemCache = new Dictionary<int, Result<WorkItem>>();
        
        var processedCount = 0;
        foreach (var workItemId in uniqueWorkItemIds)
        {
            processedCount++;
            _logger.LogInformation("查詢 Work Item {CurrentCount}/{TotalCount}：{WorkItemId}", 
                processedCount, uniqueWorkItemIds.Count, workItemId);
            
            var result = await _azureDevOpsRepository.GetWorkItemAsync(workItemId);
            workItemCache[workItemId] = result;
        }

        // 為每個 (WorkItemId, PR) 配對生成輸出
        foreach (var pair in workItemPrPairs)
        {
            var cachedResult = workItemCache[pair.WorkItemId];

            if (cachedResult.IsSuccess)
            {
                var workItem = cachedResult.Value;
                outputs.Add(new WorkItemOutput
                {
                    WorkItemId = workItem.WorkItemId,
                    Title = workItem.Title,
                    Type = workItem.Type,
                    State = workItem.State,
                    Url = workItem.Url,
                    OriginalTeamName = workItem.OriginalTeamName,
                    IsSuccess = true,
                    ErrorMessage = null,
                    SourcePullRequestId = pair.PullRequest.PullRequestId,
                    SourceProjectName = pair.ProjectPath,
                    SourcePRUrl = pair.PullRequest.PRUrl
                });
            }
            else
            {
                _logger.LogWarning("Work Item {WorkItemId} 查詢失敗：{ErrorMessage}", 
                    pair.WorkItemId, cachedResult.Error.Message);
                    
                outputs.Add(new WorkItemOutput
                {
                    WorkItemId = pair.WorkItemId,
                    Title = null,
                    Type = null,
                    State = null,
                    Url = null,
                    OriginalTeamName = null,
                    IsSuccess = false,
                    ErrorMessage = cachedResult.Error.Message,
                    SourcePullRequestId = pair.PullRequest.PullRequestId,
                    SourceProjectName = pair.ProjectPath,
                    SourcePRUrl = pair.PullRequest.PRUrl
                });
            }
        }

        return outputs;
    }
}
