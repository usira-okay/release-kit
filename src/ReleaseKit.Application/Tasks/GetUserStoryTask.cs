using Microsoft.Extensions.Logging;
using ReleaseKit.Application.Common;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Common;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 取得 User Story 層級的 Work Item 任務
/// </summary>
/// <remarks>
/// 將 Redis 中低於 User Story 層級的 Azure Work Item（如 Bug、Task）遞迴轉換為其對應的 User Story，
/// 並存入新的 Redis Key（`AzureDevOps:WorkItems:UserStories`）。
/// </remarks>
public class GetUserStoryTask : ITask
{
    private readonly IAzureDevOpsRepository _azureDevOpsRepository;
    private readonly IRedisService _redisService;
    private readonly ILogger<GetUserStoryTask> _logger;
    private const int DefaultMaxDepth = 10;

    public GetUserStoryTask(
        IAzureDevOpsRepository azureDevOpsRepository,
        IRedisService redisService,
        ILogger<GetUserStoryTask> logger)
    {
        _azureDevOpsRepository = azureDevOpsRepository;
        _redisService = redisService;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始取得 User Story 層級的 Work Item");

        // 1. 從 Redis 讀取原始 Work Item 資料
        var workItemData = await LoadWorkItemsFromRedisAsync();
        if (workItemData == null || workItemData.WorkItems.Count == 0)
        {
            _logger.LogWarning("Redis 中無 Work Item 資料");
            await SaveEmptyResultAsync();
            return;
        }

        _logger.LogInformation("從 Redis 讀取到 {Count} 筆 Work Item", workItemData.WorkItems.Count);

        // 2. 處理每個 Work Item
        var userStoryWorkItems = new List<UserStoryWorkItemOutput>();
        foreach (var workItem in workItemData.WorkItems)
        {
            var userStoryWorkItem = await ProcessWorkItemAsync(workItem);
            userStoryWorkItems.Add(userStoryWorkItem);
        }

        // 3. 統計結果
        var result = new UserStoryFetchResult
        {
            WorkItems = userStoryWorkItems,
            TotalWorkItems = workItemData.WorkItems.Count,
            AlreadyUserStoryCount = userStoryWorkItems.Count(w => w.ResolutionStatus == UserStoryResolutionStatus.AlreadyUserStoryOrAbove),
            FoundViaRecursionCount = userStoryWorkItems.Count(w => w.ResolutionStatus == UserStoryResolutionStatus.FoundViaRecursion),
            NotFoundCount = userStoryWorkItems.Count(w => w.ResolutionStatus == UserStoryResolutionStatus.NotFound),
            OriginalFetchFailedCount = userStoryWorkItems.Count(w => w.ResolutionStatus == UserStoryResolutionStatus.OriginalFetchFailed)
        };

        // 4. 寫入 Redis
        await SaveResultAsync(result);

        _logger.LogInformation("完成 User Story 解析。總數: {Total}, 已是 User Story: {Already}, 透過遞迴找到: {Found}, 未找到: {NotFound}, 原始失敗: {Failed}",
            result.TotalWorkItems,
            result.AlreadyUserStoryCount,
            result.FoundViaRecursionCount,
            result.NotFoundCount,
            result.OriginalFetchFailedCount);
    }

    /// <summary>
    /// 從 Redis 讀取 Work Item 資料
    /// </summary>
    private async Task<WorkItemFetchResult?> LoadWorkItemsFromRedisAsync()
    {
        var json = await _redisService.GetAsync(RedisKeys.AzureDevOpsWorkItems);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return json.ToTypedObject<WorkItemFetchResult>();
    }

    /// <summary>
    /// 處理單一 Work Item，將其轉換為 User Story 層級
    /// </summary>
    private async Task<UserStoryWorkItemOutput> ProcessWorkItemAsync(WorkItemOutput workItem)
    {
        // 如果原始 Work Item 已經失敗，直接回傳失敗狀態
        if (!workItem.IsSuccess)
        {
            return new UserStoryWorkItemOutput
            {
                WorkItemId = workItem.WorkItemId,
                Title = null,
                Type = null,
                State = null,
                Url = null,
                OriginalTeamName = null,
                IsSuccess = false,
                ErrorMessage = workItem.ErrorMessage,
                ResolutionStatus = UserStoryResolutionStatus.OriginalFetchFailed,
                OriginalWorkItem = null
            };
        }

        // 檢查是否已經是 User Story 層級
        if (WorkItemTypeConstants.IsUserStoryLevel(workItem.Type))
        {
            return new UserStoryWorkItemOutput
            {
                WorkItemId = workItem.WorkItemId,
                Title = workItem.Title,
                Type = workItem.Type,
                State = workItem.State,
                Url = workItem.Url,
                OriginalTeamName = workItem.OriginalTeamName,
                IsSuccess = true,
                ErrorMessage = null,
                ResolutionStatus = UserStoryResolutionStatus.AlreadyUserStoryOrAbove,
                OriginalWorkItem = null
            };
        }

        // TODO: 實作遞迴查詢邏輯（User Story 2 & 3）
        // 目前暫時回傳 NotFound
        return new UserStoryWorkItemOutput
        {
            WorkItemId = workItem.WorkItemId,
            Title = workItem.Title,
            Type = workItem.Type,
            State = workItem.State,
            Url = workItem.Url,
            OriginalTeamName = workItem.OriginalTeamName,
            IsSuccess = true,
            ErrorMessage = "未找到對應的 User Story",
            ResolutionStatus = UserStoryResolutionStatus.NotFound,
            OriginalWorkItem = workItem
        };
    }

    /// <summary>
    /// 儲存空結果至 Redis
    /// </summary>
    private async Task SaveEmptyResultAsync()
    {
        var emptyResult = new UserStoryFetchResult
        {
            WorkItems = new List<UserStoryWorkItemOutput>(),
            TotalWorkItems = 0,
            AlreadyUserStoryCount = 0,
            FoundViaRecursionCount = 0,
            NotFoundCount = 0,
            OriginalFetchFailedCount = 0
        };

        await SaveResultAsync(emptyResult);
    }

    /// <summary>
    /// 儲存結果至 Redis
    /// </summary>
    private async Task SaveResultAsync(UserStoryFetchResult result)
    {
        var json = result.ToJson();
        await _redisService.SetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems, json);
    }
}
