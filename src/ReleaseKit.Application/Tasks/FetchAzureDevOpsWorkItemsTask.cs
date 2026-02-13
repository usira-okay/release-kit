using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ReleaseKit.Application.Common;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;

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

        // 從 Redis 讀取 PR 資料
        var allPullRequests = await LoadPullRequestsFromRedisAsync();
        
        if (allPullRequests.Count == 0)
        {
            _logger.LogWarning("無可用的 PR 資料，任務結束");
            return;
        }

        // 解析所有 VSTS ID
        var workItemIds = ParseVSTSIdsFromPRs(allPullRequests);
        
        if (workItemIds.Count == 0)
        {
            _logger.LogInformation("未從 PR 標題中解析到任何 VSTS ID，任務結束");
            return;
        }

        _logger.LogInformation("從 {PRCount} 個 PR 中解析出 {WorkItemCount} 個不重複的 Work Item ID", allPullRequests.Count, workItemIds.Count);

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
        await _redisService.SetAsync(RedisKeys.AzureDevOpsWorkItems, result.ToJson(), null);

        _logger.LogInformation(
            "完成 Work Item 查詢：總計 {Total} 個，成功 {Success} 個，失敗 {Failure} 個", 
            workItemIds.Count, successCount, failureCount);
    }

    /// <summary>
    /// 從 Redis 載入 PR 資料
    /// </summary>
    private async Task<List<MergeRequestOutput>> LoadPullRequestsFromRedisAsync()
    {
        var allPullRequests = new List<MergeRequestOutput>();

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
                    allPullRequests.AddRange(result.Results.SelectMany(r => r.PullRequests));
                }
            }
            else
            {
                _logger.LogWarning($"Redis Key '{key}' 不存在或為空");
            }
        }

        return allPullRequests;
    }

    /// <summary>
    /// 從 PR 標題中解析 VSTS ID
    /// </summary>
    /// <param name="pullRequests">PR 清單</param>
    /// <returns>不重複的 Work Item ID 清單</returns>
    private HashSet<int> ParseVSTSIdsFromPRs(List<MergeRequestOutput> pullRequests)
    {
        var workItemIds = new HashSet<int>();
        var regex = new Regex(@"VSTS(\d+)", RegexOptions.None);

        foreach (var pr in pullRequests)
        {
            var matches = regex.Matches(pr.Title);
            foreach (Match match in matches)
            {
                if (int.TryParse(match.Groups[1].Value, out var workItemId))
                {
                    workItemIds.Add(workItemId);
                }
            }
        }

        return workItemIds;
    }

    /// <summary>
    /// 逐一查詢 Work Item
    /// </summary>
    /// <param name="workItemIds">Work Item ID 清單</param>
    /// <returns>Work Item 輸出清單</returns>
    private async Task<List<WorkItemOutput>> FetchWorkItemsAsync(HashSet<int> workItemIds)
    {
        var outputs = new List<WorkItemOutput>();

        foreach (var workItemId in workItemIds)
        {
            var result = await _azureDevOpsRepository.GetWorkItemAsync(workItemId);

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
                    ErrorMessage = null
                });
            }
            else
            {
                outputs.Add(new WorkItemOutput
                {
                    WorkItemId = workItemId,
                    Title = null,
                    Type = null,
                    State = null,
                    Url = null,
                    OriginalTeamName = null,
                    IsSuccess = false,
                    ErrorMessage = result.Error.Message
                });
            }
        }

        return outputs;
    }
}
