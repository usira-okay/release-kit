using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Application.Common;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 套用 Team 顯示名稱對應任務
/// </summary>
/// <remarks>
/// 從 Redis Key `AzureDevOps:WorkItems:UserStories` 讀取 User Story 資料，
/// 依 appsettings 中的 TeamMapping 對應 OriginalTeamName，
/// 將符合的項目替換為 DisplayName，並寫入新的 Redis Key `AzureDevOps:WorkItems:UserStories:TeamMapped`。
/// </remarks>
public class MapTeamDisplayNameTask : ITask
{
    private readonly IRedisService _redisService;
    private readonly ILogger<MapTeamDisplayNameTask> _logger;
    private readonly IReadOnlyList<TeamMapping> _teamMappings;

    public MapTeamDisplayNameTask(
        IRedisService redisService,
        IOptions<AzureDevOpsTeamMappingOptions> azureDevOpsTeamMappingOptions,
        ILogger<MapTeamDisplayNameTask> logger)
    {
        _redisService = redisService ?? throw new ArgumentNullException(nameof(redisService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _teamMappings = azureDevOpsTeamMappingOptions?.Value?.TeamMapping ?? new List<TeamMapping>();
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始套用 Team 顯示名稱對應");

        // 1. 從 Redis 讀取 User Story 資料
        var json = await _redisService.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems);
        if (string.IsNullOrWhiteSpace(json))
        {
            _logger.LogWarning("Redis 中無 User Story 資料（Key: {Key}），不寫入空結果", RedisKeys.AzureDevOpsUserStoryWorkItems);
            return;
        }

        var fetchResult = json.ToTypedObject<UserStoryFetchResult>();
        if (fetchResult == null || fetchResult.WorkItems.Count == 0)
        {
            _logger.LogWarning("Redis 中無 User Story Work Item 資料，不寫入空結果");
            return;
        }

        _logger.LogInformation("從 Redis 讀取到 {Count} 筆 User Story Work Item，套用 {MappingCount} 筆 TeamMapping",
            fetchResult.WorkItems.Count, _teamMappings.Count);

        // 2. 對每個 Work Item 套用 TeamMapping
        var mappedWorkItems = fetchResult.WorkItems
            .Select(MapWorkItem)
            .ToList();

        var mappedResult = fetchResult with { WorkItems = mappedWorkItems };

        // 3. 寫入新的 Redis Key
        await _redisService.SetAsync(RedisKeys.AzureDevOpsUserStoryWorkItemsTeamMapped, mappedResult.ToJson());

        _logger.LogInformation("完成 Team 顯示名稱對應，已寫入 Redis Key: {Key}", RedisKeys.AzureDevOpsUserStoryWorkItemsTeamMapped);
    }

    /// <summary>
    /// 套用 Team 顯示名稱對應至單一 Work Item
    /// </summary>
    private UserStoryWorkItemOutput MapWorkItem(UserStoryWorkItemOutput workItem)
    {
        var mappedTeamName = ResolveDisplayName(workItem.OriginalTeamName);

        WorkItemOutput? mappedOriginalWorkItem = null;
        if (workItem.OriginalWorkItem != null)
        {
            var mappedOriginalTeamName = ResolveDisplayName(workItem.OriginalWorkItem.OriginalTeamName);
            mappedOriginalWorkItem = workItem.OriginalWorkItem with { OriginalTeamName = mappedOriginalTeamName };
        }

        return workItem with
        {
            OriginalTeamName = mappedTeamName,
            OriginalWorkItem = mappedOriginalWorkItem
        };
    }

    /// <summary>
    /// 依 TeamMapping 找出對應的 DisplayName；若無匹配則回傳原始值
    /// </summary>
    private string? ResolveDisplayName(string? originalTeamName)
    {
        if (string.IsNullOrWhiteSpace(originalTeamName))
        {
            return originalTeamName;
        }

        foreach (var mapping in _teamMappings)
        {
            if (!string.IsNullOrWhiteSpace(mapping.OriginalTeamName) &&
                originalTeamName.Contains(mapping.OriginalTeamName, StringComparison.OrdinalIgnoreCase))
            {
                return mapping.DisplayName;
            }
        }

        return originalTeamName;
    }
}
