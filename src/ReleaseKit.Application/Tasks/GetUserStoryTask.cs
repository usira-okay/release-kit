using Microsoft.Extensions.Logging;
using ReleaseKit.Application.Common;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Common;
using ReleaseKit.Domain.Entities;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 解析 Work Item 至 User Story 層級任務
/// </summary>
public class GetUserStoryTask : ITask
{
    private const int MaxTraversalDepth = 10;

    private static readonly HashSet<string> HighLevelTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "User Story",
        "Feature",
        "Epic"
    };

    private readonly ILogger<GetUserStoryTask> _logger;
    private readonly IRedisService _redisService;
    private readonly IAzureDevOpsRepository _azureDevOpsRepository;

    /// <summary>
    /// 建構子
    /// </summary>
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
    /// 執行解析 Work Item 至 User Story 層級任務
    /// </summary>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始解析 Work Item 至 User Story 層級");

        await _redisService.DeleteAsync(RedisKeys.AzureDevOpsUserStories);

        var workItemFetchResult = await LoadWorkItemsFromRedisAsync();

        if (workItemFetchResult is null || workItemFetchResult.WorkItems.Count == 0)
        {
            _logger.LogWarning("無可用的 Work Item 資料，任務結束");
            return;
        }

        _logger.LogInformation("讀取到 {Count} 筆 Work Item，開始解析", workItemFetchResult.WorkItems.Count);

        var outputs = new List<UserStoryOutput>();
        var alreadyUserStoryCount = 0;
        var resolvedCount = 0;
        var keptOriginalCount = 0;

        foreach (var workItem in workItemFetchResult.WorkItems)
        {
            if (!workItem.IsSuccess)
            {
                _logger.LogInformation("Work Item {WorkItemId} 原始取得失敗，保留原始資料", workItem.WorkItemId);
                outputs.Add(CreateFromOriginal(workItem));
                keptOriginalCount++;
                continue;
            }

            if (IsHighLevelType(workItem.Type))
            {
                _logger.LogInformation("Work Item {WorkItemId} 已是 {Type}，直接保留", workItem.WorkItemId, workItem.Type);
                outputs.Add(CreateFromOriginal(workItem));
                alreadyUserStoryCount++;
                continue;
            }

            _logger.LogInformation("Work Item {WorkItemId} 類型為 {Type}，開始向上尋找 User Story", workItem.WorkItemId, workItem.Type);
            var resolved = await ResolveToUserStoryAsync(workItem);
            outputs.Add(resolved);

            if (resolved.WorkItemId != resolved.OriginalWorkItemId)
            {
                resolvedCount++;
            }
            else
            {
                keptOriginalCount++;
            }
        }

        var result = new UserStoryFetchResult
        {
            UserStories = outputs,
            TotalWorkItemsProcessed = workItemFetchResult.WorkItems.Count,
            AlreadyUserStoryCount = alreadyUserStoryCount,
            ResolvedCount = resolvedCount,
            KeptOriginalCount = keptOriginalCount
        };

        await _redisService.SetAsync(RedisKeys.AzureDevOpsUserStories, result.ToJson(), null);

        _logger.LogInformation(
            "完成 User Story 解析：共 {Total} 筆，已是 US 以上 {Already} 筆，成功解析 {Resolved} 筆，保留原始 {Kept} 筆",
            outputs.Count, alreadyUserStoryCount, resolvedCount, keptOriginalCount);
    }

    /// <summary>
    /// 從 Redis 載入 Work Item 資料
    /// </summary>
    private async Task<WorkItemFetchResult?> LoadWorkItemsFromRedisAsync()
    {
        var json = await _redisService.GetAsync(RedisKeys.AzureDevOpsWorkItems);
        if (json is null)
        {
            _logger.LogWarning("Redis Key {RedisKey} 不存在或為空", RedisKeys.AzureDevOpsWorkItems);
            return null;
        }

        return json.ToTypedObject<WorkItemFetchResult>();
    }

    /// <summary>
    /// 遞迴向上尋找 User Story / Feature / Epic
    /// </summary>
    private async Task<UserStoryOutput> ResolveToUserStoryAsync(WorkItemOutput originalWorkItem)
    {
        var currentResult = await _azureDevOpsRepository.GetWorkItemAsync(originalWorkItem.WorkItemId);

        for (var depth = 0; depth < MaxTraversalDepth; depth++)
        {
            if (!currentResult.IsSuccess || !currentResult.Value.ParentWorkItemId.HasValue)
            {
                _logger.LogInformation(
                    "Work Item {WorkItemId} 無法繼續向上查詢，保留原始資料",
                    originalWorkItem.WorkItemId);
                return CreateFromOriginal(originalWorkItem);
            }

            var parentId = currentResult.Value.ParentWorkItemId.Value;
            _logger.LogInformation("查詢 parent Work Item {ParentId}（深度 {Depth}）", parentId, depth + 1);

            var parentResult = await _azureDevOpsRepository.GetWorkItemAsync(parentId);

            if (!parentResult.IsSuccess)
            {
                _logger.LogWarning("查詢 parent Work Item {ParentId} 失敗，保留原始資料", parentId);
                return CreateFromOriginal(originalWorkItem);
            }

            if (IsHighLevelType(parentResult.Value.Type))
            {
                _logger.LogInformation(
                    "找到 {Type} {ParentId}（原始 Work Item {OriginalId}）",
                    parentResult.Value.Type, parentId, originalWorkItem.WorkItemId);
                return CreateFromParent(parentResult.Value, originalWorkItem.WorkItemId);
            }

            currentResult = parentResult;
        }

        _logger.LogWarning(
            "Work Item {WorkItemId} 超過最大遍歷深度 {MaxDepth}，保留原始資料",
            originalWorkItem.WorkItemId, MaxTraversalDepth);
        return CreateFromOriginal(originalWorkItem);
    }

    /// <summary>
    /// 判斷是否為 User Story 以上的類型
    /// </summary>
    private static bool IsHighLevelType(string? type)
    {
        return type is not null && HighLevelTypes.Contains(type);
    }

    /// <summary>
    /// 從原始 Work Item 建立 UserStoryOutput（保留原始資料）
    /// </summary>
    private static UserStoryOutput CreateFromOriginal(WorkItemOutput workItem)
    {
        return new UserStoryOutput
        {
            WorkItemId = workItem.WorkItemId,
            OriginalWorkItemId = workItem.WorkItemId,
            Title = workItem.Title,
            Type = workItem.Type,
            State = workItem.State,
            Url = workItem.Url,
            OriginalTeamName = workItem.OriginalTeamName,
            IsSuccess = workItem.IsSuccess,
            ErrorMessage = workItem.ErrorMessage
        };
    }

    /// <summary>
    /// 從 parent Work Item 建立 UserStoryOutput
    /// </summary>
    private static UserStoryOutput CreateFromParent(WorkItem parent, int originalWorkItemId)
    {
        return new UserStoryOutput
        {
            WorkItemId = parent.WorkItemId,
            OriginalWorkItemId = originalWorkItemId,
            Title = parent.Title,
            Type = parent.Type,
            State = parent.State,
            Url = parent.Url,
            OriginalTeamName = parent.OriginalTeamName,
            IsSuccess = true,
            ErrorMessage = null
        };
    }
}
