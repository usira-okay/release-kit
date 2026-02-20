using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Application.Common;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 套用 TeamMapping 將 User Story 資料中的原始團隊名稱轉換為顯示名稱
/// </summary>
public class MapUserStoryTeamTask : ITask
{
    private readonly IRedisService _redisService;
    private readonly ILogger<MapUserStoryTeamTask> _logger;
    private readonly IReadOnlyList<TeamMappingOptions> _teamMappings;

    public MapUserStoryTeamTask(
        IRedisService redisService,
        IOptions<AzureDevOpsOptions> azureDevOpsOptions,
        ILogger<MapUserStoryTeamTask> logger)
    {
        _redisService = redisService ?? throw new ArgumentNullException(nameof(redisService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _teamMappings = azureDevOpsOptions?.Value.TeamMapping
                        ?? throw new ArgumentNullException(nameof(azureDevOpsOptions));
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始套用 User Story Team Mapping");

        var sourceJson = await _redisService.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems);
        if (string.IsNullOrWhiteSpace(sourceJson))
        {
            _logger.LogWarning("Redis Key {Key} 中無 User Story 資料，略過映射", RedisKeys.AzureDevOpsUserStoryWorkItems);
            return;
        }

        var fetchResult = sourceJson.ToTypedObject<UserStoryFetchResult>();
        if (fetchResult == null || fetchResult.WorkItems.Count == 0)
        {
            _logger.LogWarning("無法解析 User Story 資料或資料為空，略過映射");
            return;
        }

        if (_teamMappings.Count == 0)
        {
            _logger.LogWarning("TeamMapping 未設定，略過映射");
            return;
        }

        var mappedWorkItems = fetchResult.WorkItems
            .Select(MapWorkItem)
            .ToList();

        var mappedResult = fetchResult with { WorkItems = mappedWorkItems };
        var targetJson = mappedResult.ToJson();

        await _redisService.SetAsync(RedisKeys.AzureDevOpsUserStoryTeamMappedWorkItems, targetJson);
        _logger.LogInformation("映射完成，結果已寫入 Redis Key {Key}", RedisKeys.AzureDevOpsUserStoryTeamMappedWorkItems);
    }

    private UserStoryWorkItemOutput MapWorkItem(UserStoryWorkItemOutput workItem)
    {
        var mappedTeamName = MapTeamName(workItem.OriginalTeamName);
        var mappedOriginalWorkItem = workItem.OriginalWorkItem is null
            ? null
            : workItem.OriginalWorkItem with { OriginalTeamName = MapTeamName(workItem.OriginalWorkItem.OriginalTeamName) };

        return workItem with
        {
            OriginalTeamName = mappedTeamName,
            OriginalWorkItem = mappedOriginalWorkItem
        };
    }

    private string? MapTeamName(string? originalTeamName)
    {
        if (string.IsNullOrWhiteSpace(originalTeamName))
        {
            return originalTeamName;
        }

        var mapping = _teamMappings.FirstOrDefault(m =>
            !string.IsNullOrWhiteSpace(m.OriginalTeamName) &&
            originalTeamName.Contains(m.OriginalTeamName, StringComparison.OrdinalIgnoreCase));

        return mapping?.DisplayName ?? originalTeamName;
    }
}
