using Microsoft.Extensions.Logging;
using ReleaseKit.Application.Common;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Entities;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 取得 User Story 層級資訊任務
/// </summary>
/// <remarks>
/// 從 Redis 讀取既有的 Work Item 資料，對每個 Work Item 判斷是否為 User Story 以上層級。
/// 若不是，透過 Azure DevOps API 遞迴查找 Parent 直到找到 User Story 或更高層級。
/// 處理結果寫入 Redis Key `AzureDevOps:WorkItems:UserStories`。
/// </remarks>
public class GetUserStoryTask : ITask
{
    private readonly ILogger<GetUserStoryTask> _logger;
    private readonly IRedisService _redisService;
    private readonly IAzureDevOpsRepository _azureDevOpsRepository;

    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="logger">日誌記錄器</param>
    /// <param name="redisService">Redis 服務</param>
    /// <param name="azureDevOpsRepository">Azure DevOps Repository</param>
    public GetUserStoryTask(
        ILogger<GetUserStoryTask> logger,
        IRedisService redisService,
        IAzureDevOpsRepository azureDevOpsRepository)
    {
        _logger = logger;
        _redisService = redisService;
        _azureDevOpsRepository = azureDevOpsRepository;
    }

    /// <summary>
    /// 執行任務
    /// </summary>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始取得 User Story 層級資訊");

        // 從 Redis 讀取 Work Item 資料
        var workItemDataJson = await _redisService.GetAsync(RedisKeys.AzureDevOpsWorkItems);
        
        if (string.IsNullOrWhiteSpace(workItemDataJson))
        {
            _logger.LogWarning("Redis 中無 Work Item 資料，任務結束");
            await SaveEmptyResultAsync();
            return;
        }

        var workItemFetchResult = workItemDataJson.ToTypedObject<WorkItemFetchResult>();
        if (workItemFetchResult?.WorkItems == null || workItemFetchResult.WorkItems.Count == 0)
        {
            _logger.LogInformation("Work Item 清單為空，任務結束");
            await SaveEmptyResultAsync();
            return;
        }

        _logger.LogInformation("從 Redis 讀取到 {Count} 個 Work Item", workItemFetchResult.WorkItems.Count);

        // 用於快取 API 查詢結果，避免重複查詢同一個 Work Item
        var workItemCache = new Dictionary<int, WorkItem>();

        // 處理每個 Work Item
        var resolutionOutputs = new List<UserStoryResolutionOutput>();
        
        foreach (var workItemOutput in workItemFetchResult.WorkItems)
        {
            var resolutionOutput = await ResolveUserStoryAsync(workItemOutput, workItemCache);
            resolutionOutputs.Add(resolutionOutput);
        }

        // 統計結果
        var result = new UserStoryResolutionResult
        {
            Items = resolutionOutputs,
            TotalCount = resolutionOutputs.Count,
            AlreadyUserStoryCount = resolutionOutputs.Count(r => r.ResolutionStatus == UserStoryResolutionStatus.AlreadyUserStoryOrAbove),
            FoundViaRecursionCount = resolutionOutputs.Count(r => r.ResolutionStatus == UserStoryResolutionStatus.FoundViaRecursion),
            NotFoundCount = resolutionOutputs.Count(r => r.ResolutionStatus == UserStoryResolutionStatus.NotFound),
            OriginalFetchFailedCount = resolutionOutputs.Count(r => r.ResolutionStatus == UserStoryResolutionStatus.OriginalFetchFailed)
        };

        // 寫入 Redis
        await _redisService.SetAsync(RedisKeys.AzureDevOpsUserStories, result.ToJson(), null);

        // 輸出 JSON 至 stdout
        Console.WriteLine(result.ToJson());

        _logger.LogInformation(
            "完成 User Story 解析：總計 {Total} 個，已為 User Story {Already} 個，透過遞迴找到 {Found} 個，未找到 {NotFound} 個，原始失敗 {Failed} 個",
            result.TotalCount,
            result.AlreadyUserStoryCount,
            result.FoundViaRecursionCount,
            result.NotFoundCount,
            result.OriginalFetchFailedCount);
    }

    /// <summary>
    /// 解析單一 Work Item 的 User Story 資訊
    /// </summary>
    private async Task<UserStoryResolutionOutput> ResolveUserStoryAsync(
        WorkItemOutput workItemOutput,
        Dictionary<int, WorkItem> cache)
    {
        // 處理原始取得失敗的情況
        if (!workItemOutput.IsSuccess)
        {
            return new UserStoryResolutionOutput
            {
                WorkItemId = workItemOutput.WorkItemId,
                Title = workItemOutput.Title,
                Type = workItemOutput.Type,
                State = workItemOutput.State,
                Url = workItemOutput.Url,
                OriginalTeamName = workItemOutput.OriginalTeamName,
                IsSuccess = false,
                ErrorMessage = workItemOutput.ErrorMessage,
                ResolutionStatus = UserStoryResolutionStatus.OriginalFetchFailed,
                UserStory = null
            };
        }

        // 檢查是否已為 User Story 以上層級
        if (WorkItemTypeConstants.UserStoryOrAboveTypes.Contains(workItemOutput.Type ?? string.Empty))
        {
            return new UserStoryResolutionOutput
            {
                WorkItemId = workItemOutput.WorkItemId,
                Title = workItemOutput.Title,
                Type = workItemOutput.Type,
                State = workItemOutput.State,
                Url = workItemOutput.Url,
                OriginalTeamName = workItemOutput.OriginalTeamName,
                IsSuccess = true,
                ErrorMessage = null,
                ResolutionStatus = UserStoryResolutionStatus.AlreadyUserStoryOrAbove,
                UserStory = new UserStoryInfo
                {
                    WorkItemId = workItemOutput.WorkItemId,
                    Title = workItemOutput.Title ?? string.Empty,
                    Type = workItemOutput.Type ?? string.Empty,
                    State = workItemOutput.State ?? string.Empty,
                    Url = workItemOutput.Url ?? string.Empty
                }
            };
        }

        // 需要遞迴查找 Parent
        // 注意：原始的 WorkItemOutput 沒有 ParentWorkItemId，需要透過 API 查詢取得完整 WorkItem
        var currentWorkItemResult = await GetWorkItemWithCacheAsync(workItemOutput.WorkItemId, cache);
        
        if (!currentWorkItemResult.IsSuccess)
        {
            _logger.LogWarning("無法查詢 Work Item {Id}：{Error}", workItemOutput.WorkItemId, currentWorkItemResult.Error?.Message);
            return CreateNotFoundOutput(workItemOutput);
        }

        var currentWorkItem = currentWorkItemResult.Value!;

        // 如果沒有 Parent，無法往上查找
        if (currentWorkItem.ParentWorkItemId == null)
        {
            _logger.LogDebug("Work Item {Id} 沒有 Parent，無法找到 User Story", workItemOutput.WorkItemId);
            return CreateNotFoundOutput(workItemOutput);
        }

        // 遞迴查找 Parent
        var visited = new HashSet<int> { workItemOutput.WorkItemId };
        var userStory = await FindUserStoryRecursivelyAsync(currentWorkItem.ParentWorkItemId.Value, visited, cache, 0);

        if (userStory != null)
        {
            return new UserStoryResolutionOutput
            {
                WorkItemId = workItemOutput.WorkItemId,
                Title = workItemOutput.Title,
                Type = workItemOutput.Type,
                State = workItemOutput.State,
                Url = workItemOutput.Url,
                OriginalTeamName = workItemOutput.OriginalTeamName,
                IsSuccess = true,
                ErrorMessage = null,
                ResolutionStatus = UserStoryResolutionStatus.FoundViaRecursion,
                UserStory = userStory
            };
        }

        return CreateNotFoundOutput(workItemOutput);
    }

    /// <summary>
    /// 遞迴查找 User Story
    /// </summary>
    private async Task<UserStoryInfo?> FindUserStoryRecursivelyAsync(
        int workItemId,
        HashSet<int> visited,
        Dictionary<int, WorkItem> cache,
        int depth)
    {
        // 檢查遞迴深度限制
        if (depth >= WorkItemTypeConstants.MaxRecursionDepth)
        {
            _logger.LogWarning("達到最大遞迴深度 {Depth}，停止查找", depth);
            return null;
        }

        // 檢查循環參照
        if (visited.Contains(workItemId))
        {
            _logger.LogWarning("偵測到循環參照 Work Item {Id}，停止查找", workItemId);
            return null;
        }

        visited.Add(workItemId);

        // 查詢 Work Item
        var result = await GetWorkItemWithCacheAsync(workItemId, cache);
        
        if (!result.IsSuccess)
        {
            _logger.LogDebug("無法查詢 Work Item {Id}，停止查找", workItemId);
            return null;
        }

        var workItem = result.Value!;

        // 檢查是否為 User Story 以上層級
        if (WorkItemTypeConstants.UserStoryOrAboveTypes.Contains(workItem.Type))
        {
            return new UserStoryInfo
            {
                WorkItemId = workItem.WorkItemId,
                Title = workItem.Title,
                Type = workItem.Type,
                State = workItem.State,
                Url = workItem.Url
            };
        }

        // 如果有 Parent，繼續往上查找
        if (workItem.ParentWorkItemId.HasValue)
        {
            return await FindUserStoryRecursivelyAsync(
                workItem.ParentWorkItemId.Value,
                visited,
                cache,
                depth + 1);
        }

        // 沒有 Parent 且不是 User Story，查找失敗
        return null;
    }

    /// <summary>
    /// 從快取或 API 取得 Work Item
    /// </summary>
    private async Task<Domain.Common.Result<WorkItem>> GetWorkItemWithCacheAsync(
        int workItemId,
        Dictionary<int, WorkItem> cache)
    {
        if (cache.TryGetValue(workItemId, out var cachedWorkItem))
        {
            return Domain.Common.Result<WorkItem>.Success(cachedWorkItem);
        }

        var result = await _azureDevOpsRepository.GetWorkItemAsync(workItemId);
        
        if (result.IsSuccess && result.Value != null)
        {
            cache[workItemId] = result.Value;
        }

        return result;
    }

    /// <summary>
    /// 建立「未找到」狀態的輸出
    /// </summary>
    private static UserStoryResolutionOutput CreateNotFoundOutput(WorkItemOutput workItemOutput)
    {
        return new UserStoryResolutionOutput
        {
            WorkItemId = workItemOutput.WorkItemId,
            Title = workItemOutput.Title,
            Type = workItemOutput.Type,
            State = workItemOutput.State,
            Url = workItemOutput.Url,
            OriginalTeamName = workItemOutput.OriginalTeamName,
            IsSuccess = true,
            ErrorMessage = null,
            ResolutionStatus = UserStoryResolutionStatus.NotFound,
            UserStory = null
        };
    }

    /// <summary>
    /// 儲存空結果至 Redis
    /// </summary>
    private async Task SaveEmptyResultAsync()
    {
        var emptyResult = new UserStoryResolutionResult
        {
            Items = new List<UserStoryResolutionOutput>(),
            TotalCount = 0,
            AlreadyUserStoryCount = 0,
            FoundViaRecursionCount = 0,
            NotFoundCount = 0,
            OriginalFetchFailedCount = 0
        };

        await _redisService.SetAsync(RedisKeys.AzureDevOpsUserStories, emptyResult.ToJson(), null);
        Console.WriteLine(emptyResult.ToJson());
    }
}
