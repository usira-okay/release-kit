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

        // 遞迴查詢 Parent Work Item
        // 首先需要取得完整的 Work Item 資訊（包含 ParentId）
        var currentWorkItemResult = await _azureDevOpsRepository.GetWorkItemAsync(workItem.WorkItemId);
        
        if (!currentWorkItemResult.IsSuccess)
        {
            // 無法取得完整資訊，視為 NotFound
            return new UserStoryWorkItemOutput
            {
                WorkItemId = workItem.WorkItemId,
                Title = workItem.Title,
                Type = workItem.Type,
                State = workItem.State,
                Url = workItem.Url,
                OriginalTeamName = workItem.OriginalTeamName,
                IsSuccess = true,
                ErrorMessage = $"無法取得完整 Work Item 資訊: {currentWorkItemResult.Error?.Message}",
                ResolutionStatus = UserStoryResolutionStatus.NotFound,
                OriginalWorkItem = workItem
            };
        }

        var currentWorkItem = currentWorkItemResult.Value!;
        
        // 檢查是否有 Parent
        if (!currentWorkItem.ParentId.HasValue)
        {
            // 沒有 Parent，無法找到 User Story
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

        // 從 Parent 開始遞迴查詢
        var visited = new HashSet<int> { workItem.WorkItemId };
        var userStoryResult = await FindUserStoryRecursivelyAsync(currentWorkItem.ParentId.Value, visited, depth: 0);

        if (userStoryResult.IsSuccess)
        {
            // 成功找到 User Story
            return new UserStoryWorkItemOutput
            {
                WorkItemId = userStoryResult.UserStoryWorkItemId,
                Title = userStoryResult.Title,
                Type = userStoryResult.Type,
                State = userStoryResult.State,
                Url = userStoryResult.Url,
                OriginalTeamName = userStoryResult.OriginalTeamName,
                IsSuccess = true,
                ErrorMessage = null,
                ResolutionStatus = UserStoryResolutionStatus.FoundViaRecursion,
                OriginalWorkItem = workItem
            };
        }
        else
        {
            // 未找到 User Story（無 Parent、API 失敗、循環參照、超深度）
            return new UserStoryWorkItemOutput
            {
                WorkItemId = workItem.WorkItemId,
                Title = workItem.Title,
                Type = workItem.Type,
                State = workItem.State,
                Url = workItem.Url,
                OriginalTeamName = workItem.OriginalTeamName,
                IsSuccess = true,
                ErrorMessage = userStoryResult.ErrorMessage,
                ResolutionStatus = UserStoryResolutionStatus.NotFound,
                OriginalWorkItem = workItem
            };
        }
    }

    /// <summary>
    /// 遞迴查詢 Parent Work Item，尋找 User Story 層級
    /// </summary>
    /// <param name="workItemId">當前 Work Item ID</param>
    /// <param name="visited">已訪問的 Work Item ID 集合（用於偵測循環參照）</param>
    /// <param name="depth">目前遞迴深度</param>
    /// <returns>User Story 查詢結果</returns>
    private async Task<UserStoryQueryResult> FindUserStoryRecursivelyAsync(int workItemId, HashSet<int> visited, int depth)
    {
        // 檢查是否超過最大遞迴深度
        if (depth >= DefaultMaxDepth)
        {
            _logger.LogWarning("Work Item {WorkItemId} 超過最大遞迴深度 {MaxDepth}", workItemId, DefaultMaxDepth);
            return UserStoryQueryResult.Failure("超過最大遞迴深度");
        }

        // 呼叫 Azure DevOps API 取得 Work Item（包含 Relations）
        var result = await _azureDevOpsRepository.GetWorkItemAsync(workItemId);
        
        if (!result.IsSuccess)
        {
            _logger.LogWarning("無法取得 Work Item {WorkItemId}: {Error}", workItemId, result.Error?.Message);
            return UserStoryQueryResult.Failure($"Parent work item not found: {result.Error?.Message}");
        }

        var currentWorkItem = result.Value!;

        // 檢查是否為 User Story 層級
        if (WorkItemTypeConstants.IsUserStoryLevel(currentWorkItem.Type))
        {
            return UserStoryQueryResult.Success(currentWorkItem);
        }

        // 檢查是否有 Parent
        if (!currentWorkItem.ParentId.HasValue)
        {
            _logger.LogInformation("Work Item {WorkItemId} 沒有 Parent，無法找到 User Story", workItemId);
            return UserStoryQueryResult.Failure("未找到對應的 User Story");
        }

        var parentId = currentWorkItem.ParentId.Value;

        // 檢查是否偵測到循環參照
        if (visited.Contains(parentId))
        {
            _logger.LogWarning("偵測到循環參照：Work Item {WorkItemId} -> Parent {ParentId}", workItemId, parentId);
            return UserStoryQueryResult.Failure("偵測到循環參照");
        }

        // 加入 visited 集合，繼續遞迴查詢
        visited.Add(parentId);
        return await FindUserStoryRecursivelyAsync(parentId, visited, depth + 1);
    }

    /// <summary>
    /// User Story 查詢結果（內部使用）
    /// </summary>
    private sealed record UserStoryQueryResult
    {
        public bool IsSuccess { get; init; }
        public int UserStoryWorkItemId { get; init; }
        public string? Title { get; init; }
        public string? Type { get; init; }
        public string? State { get; init; }
        public string? Url { get; init; }
        public string? OriginalTeamName { get; init; }
        public string? ErrorMessage { get; init; }

        public static UserStoryQueryResult Success(Domain.Entities.WorkItem workItem)
        {
            return new UserStoryQueryResult
            {
                IsSuccess = true,
                UserStoryWorkItemId = workItem.WorkItemId,
                Title = workItem.Title,
                Type = workItem.Type,
                State = workItem.State,
                Url = workItem.Url,
                OriginalTeamName = workItem.OriginalTeamName,
                ErrorMessage = null
            };
        }

        public static UserStoryQueryResult Failure(string errorMessage)
        {
            return new UserStoryQueryResult
            {
                IsSuccess = false,
                UserStoryWorkItemId = 0,
                Title = null,
                Type = null,
                State = null,
                Url = null,
                OriginalTeamName = null,
                ErrorMessage = errorMessage
            };
        }
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
