using Microsoft.Extensions.Logging;
using ReleaseKit.Application.Common;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Common;
using ReleaseKit.Domain.Entities;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 取得用戶故事任務
/// </summary>
/// <remarks>
/// 此任務負責將 Work Item 解析至高層級類型（User Story/Feature/Epic）。
/// 對於已是高層級類型的 Work Item，直接保留。
/// 對於較低層級的 Work Item（Task/Bug），遞迴查詢其父層，直至找到高層級類型或無更多父層為止。
/// </remarks>
public class GetUserStoryTask : ITask
{
    /// <summary>
    /// 日誌記錄器
    /// </summary>
    private readonly ILogger<GetUserStoryTask> _logger;

    /// <summary>
    /// Redis 服務
    /// </summary>
    private readonly IRedisService _redisService;

    /// <summary>
    /// Azure DevOps Repository
    /// </summary>
    private readonly IAzureDevOpsRepository _azureDevOpsRepository;

    /// <summary>
    /// 高層級類型集合（User Story, Feature, Epic）
    /// </summary>
    private static readonly HashSet<string> HigherLevelTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "User Story",
        "Feature",
        "Epic"
    };

    /// <summary>
    /// 遞迴查詢的最大深度
    /// </summary>
    private const int MaxRecursionDepth = 10;

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
    /// 執行取得用戶故事任務
    /// </summary>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始取得用戶故事");

        // 從 Redis 讀取 Work Item 資料
        var json = await _redisService.GetAsync(RedisKeys.AzureDevOpsWorkItems);
        if (json is null)
        {
            _logger.LogWarning("Redis Key {RedisKey} 不存在或為空", RedisKeys.AzureDevOpsWorkItems);
            return;
        }

        var workItemFetchResult = json.ToTypedObject<WorkItemFetchResult>();
        if (workItemFetchResult is null || workItemFetchResult.WorkItems.Count == 0)
        {
            _logger.LogWarning("未找到任何 Work Item 資料");
            return;
        }

        _logger.LogInformation("開始處理 {WorkItemCount} 個 Work Item", workItemFetchResult.WorkItems.Count);

        // 使用 Dictionary 快取已查詢的 Work Item
        var workItemCache = new Dictionary<int, Result<WorkItem>>();

        // 處理每個 Work Item
        var userStories = new List<UserStoryOutput>();
        var alreadyUserStoryCount = 0;
        var resolvedCount = 0;
        var keptOriginalCount = 0;

        foreach (var workItem in workItemFetchResult.WorkItems)
        {
            // 若原始抓取失敗，保留失敗記錄
            if (!workItem.IsSuccess)
            {
                _logger.LogWarning("Work Item {WorkItemId} 原始抓取失敗：{ErrorMessage}", 
                    workItem.WorkItemId, workItem.ErrorMessage);
                
                userStories.Add(new UserStoryOutput
                {
                    WorkItemId = workItem.WorkItemId,
                    OriginalWorkItemId = workItem.WorkItemId,
                    Title = workItem.Title,
                    Type = workItem.Type,
                    State = workItem.State,
                    Url = workItem.Url,
                    OriginalTeamName = workItem.OriginalTeamName,
                    IsSuccess = false,
                    ErrorMessage = workItem.ErrorMessage
                });
                keptOriginalCount++;
                continue;
            }

            // 若已是高層級類型，直接保留
            if (IsHigherLevelType(workItem.Type))
            {
                _logger.LogInformation("Work Item {WorkItemId} 已是 {Type}，直接保留", 
                    workItem.WorkItemId, workItem.Type);
                
                userStories.Add(new UserStoryOutput
                {
                    WorkItemId = workItem.WorkItemId,
                    OriginalWorkItemId = workItem.WorkItemId,
                    Title = workItem.Title,
                    Type = workItem.Type,
                    State = workItem.State,
                    Url = workItem.Url,
                    OriginalTeamName = workItem.OriginalTeamName,
                    IsSuccess = true,
                    ErrorMessage = null
                });
                alreadyUserStoryCount++;
                continue;
            }

            // 遞迴查詢父層
            var result = await ResolveToHigherLevelAsync(workItem.WorkItemId, workItem.WorkItemId, workItemCache);
            
            if (result.IsSuccess)
            {
                var resolvedWorkItem = result.Value;
                
                if (resolvedWorkItem.WorkItemId == workItem.WorkItemId)
                {
                    // 未能向上解析，保留原始資料
                    _logger.LogInformation("Work Item {WorkItemId} 無法向上解析至高層級類型，保留原始資料", 
                        workItem.WorkItemId);
                    
                    userStories.Add(new UserStoryOutput
                    {
                        WorkItemId = workItem.WorkItemId,
                        OriginalWorkItemId = workItem.WorkItemId,
                        Title = workItem.Title,
                        Type = workItem.Type,
                        State = workItem.State,
                        Url = workItem.Url,
                        OriginalTeamName = workItem.OriginalTeamName,
                        IsSuccess = true,
                        ErrorMessage = null
                    });
                    keptOriginalCount++;
                }
                else
                {
                    // 成功解析至高層級
                    _logger.LogInformation(
                        "Work Item {OriginalId} 成功解析至 {ResolvedId}（{Type}）", 
                        workItem.WorkItemId, resolvedWorkItem.WorkItemId, resolvedWorkItem.Type);
                    
                    userStories.Add(new UserStoryOutput
                    {
                        WorkItemId = resolvedWorkItem.WorkItemId,
                        OriginalWorkItemId = workItem.WorkItemId,
                        Title = resolvedWorkItem.Title,
                        Type = resolvedWorkItem.Type,
                        State = resolvedWorkItem.State,
                        Url = resolvedWorkItem.Url,
                        OriginalTeamName = resolvedWorkItem.OriginalTeamName,
                        IsSuccess = true,
                        ErrorMessage = null
                    });
                    resolvedCount++;
                }
            }
            else
            {
                // 解析失敗，保留失敗記錄
                _logger.LogWarning("Work Item {WorkItemId} 遞迴查詢失敗：{ErrorMessage}", 
                    workItem.WorkItemId, result.Error?.Message);
                
                userStories.Add(new UserStoryOutput
                {
                    WorkItemId = workItem.WorkItemId,
                    OriginalWorkItemId = workItem.WorkItemId,
                    Title = workItem.Title,
                    Type = workItem.Type,
                    State = workItem.State,
                    Url = workItem.Url,
                    OriginalTeamName = workItem.OriginalTeamName,
                    IsSuccess = false,
                    ErrorMessage = result.Error?.Message
                });
                keptOriginalCount++;
            }
        }

        // 組裝結果
        var userStoryFetchResult = new UserStoryFetchResult
        {
            UserStories = userStories,
            TotalWorkItemsProcessed = workItemFetchResult.WorkItems.Count,
            AlreadyUserStoryCount = alreadyUserStoryCount,
            ResolvedCount = resolvedCount,
            KeptOriginalCount = keptOriginalCount
        };

        // 寫入 Redis
        await _redisService.SetAsync(RedisKeys.AzureDevOpsUserStories, userStoryFetchResult.ToJson(), null);

        _logger.LogInformation(
            "完成用戶故事解析：總計 {Total} 個，已是高層級 {AlreadyHigher} 個，成功解析 {Resolved} 個，保留原始 {Kept} 個",
            userStoryFetchResult.TotalWorkItemsProcessed,
            alreadyUserStoryCount,
            resolvedCount,
            keptOriginalCount);
    }

    /// <summary>
    /// 檢查是否為高層級類型
    /// </summary>
    /// <param name="type">Work Item 類型</param>
    /// <returns>是否為高層級類型</returns>
    private bool IsHigherLevelType(string? type)
    {
        return type is not null && HigherLevelTypes.Contains(type);
    }

    /// <summary>
    /// 遞迴解析至高層級類型
    /// </summary>
    /// <param name="originalWorkItemId">原始 Work Item ID</param>
    /// <param name="currentWorkItemId">目前查詢的 Work Item ID</param>
    /// <param name="cache">Work Item 快取</param>
    /// <param name="depth">目前遞迴深度</param>
    /// <returns>解析結果，包含找到的高層級 Work Item 或原始 Work Item</returns>
    private async Task<Result<WorkItem>> ResolveToHigherLevelAsync(
        int originalWorkItemId,
        int currentWorkItemId,
        Dictionary<int, Result<WorkItem>> cache,
        int depth = 0)
    {
        // 檢查遞迴深度是否超過限制
        if (depth > MaxRecursionDepth)
        {
            _logger.LogWarning("Work Item {WorkItemId} 的遞迴深度超過 {MaxDepth}，停止查詢", 
                originalWorkItemId, MaxRecursionDepth);
            
            // 返回原始 Work Item
            return await GetOrCacheWorkItemAsync(originalWorkItemId, cache);
        }

        // 嘗試從快取取得 Work Item
        if (cache.TryGetValue(currentWorkItemId, out var result))
        {
            if (result.IsFailure)
            {
                return result;
            }

            var workItem = result.Value;

            // 若已是高層級類型，返回
            if (IsHigherLevelType(workItem.Type))
            {
                return result;
            }

            // 若無父層，返回原始 Work Item
            if (workItem.ParentWorkItemId is null)
            {
                // 返回原始 Work Item
                return await GetOrCacheWorkItemAsync(originalWorkItemId, cache);
            }

            // 遞迴查詢父層
            return await ResolveToHigherLevelAsync(originalWorkItemId, workItem.ParentWorkItemId.Value, cache, depth + 1);
        }

        // 查詢 Work Item
        var fetchResult = await GetOrCacheWorkItemAsync(currentWorkItemId, cache);

        if (fetchResult.IsFailure)
        {
            return fetchResult;
        }

        var currentWorkItem = fetchResult.Value;

        // 若已是高層級類型，返回
        if (IsHigherLevelType(currentWorkItem.Type))
        {
            return fetchResult;
        }

        // 若無父層，返回原始 Work Item
        if (currentWorkItem.ParentWorkItemId is null)
        {
            // 返回原始 Work Item
            return await GetOrCacheWorkItemAsync(originalWorkItemId, cache);
        }

        // 遞迴查詢父層
        return await ResolveToHigherLevelAsync(originalWorkItemId, currentWorkItem.ParentWorkItemId.Value, cache, depth + 1);
    }

    /// <summary>
    /// 取得或快取 Work Item
    /// </summary>
    /// <param name="workItemId">Work Item ID</param>
    /// <param name="cache">Work Item 快取</param>
    /// <returns>查詢結果</returns>
    private async Task<Result<WorkItem>> GetOrCacheWorkItemAsync(
        int workItemId,
        Dictionary<int, Result<WorkItem>> cache)
    {
        if (cache.TryGetValue(workItemId, out var cached))
        {
            return cached;
        }

        _logger.LogInformation("查詢 Work Item {WorkItemId}", workItemId);
        var result = await _azureDevOpsRepository.GetWorkItemAsync(workItemId);
        cache[workItemId] = result;

        return result;
    }
}
