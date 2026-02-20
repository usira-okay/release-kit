using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Application.Common;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 對 User Story 進行團隊名稱映射任務
/// </summary>
/// <remarks>
/// 從 Redis 讀取 User Story 資料，依照 appsettings 中的 TeamMapping 進行團隊名稱映射，
/// 將 OriginalTeamName 替換為對應的 DisplayName，並存入新的 Redis Key。
/// </remarks>
public class MapUserStoryTeamTask : ITask
{
    private readonly IRedisService _redisService;
    private readonly ILogger<MapUserStoryTeamTask> _logger;
    private readonly IReadOnlyDictionary<string, string> _teamNameToDisplayName;

    public MapUserStoryTeamTask(
        IRedisService redisService,
        ILogger<MapUserStoryTeamTask> logger,
        IOptions<TeamMappingOptions> teamMappingOptions)
    {
        _redisService = redisService;
        _logger = logger;
        _teamNameToDisplayName = ExtractTeamNameToDisplayName(teamMappingOptions.Value);
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始對 User Story 進行團隊名稱映射");

        // 1. 從 Redis 讀取 User Story 資料
        var sourceJson = await _redisService.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems);
        if (string.IsNullOrWhiteSpace(sourceJson))
        {
            _logger.LogWarning("Redis Key {Key} 中無 User Story 資料，略過處理", RedisKeys.AzureDevOpsUserStoryWorkItems);
            return;
        }

        var fetchResult = sourceJson.ToTypedObject<UserStoryFetchResult>();
        if (fetchResult == null || fetchResult.WorkItems.Count == 0)
        {
            _logger.LogWarning("無法解析 User Story 資料或資料為空，略過處理");
            return;
        }

        _logger.LogInformation("從 Redis 讀取到 {Count} 筆 User Story", fetchResult.WorkItems.Count);

        // 2. 檢查團隊映射清單
        if (_teamNameToDisplayName.Count == 0)
        {
            _logger.LogWarning("TeamMapping 設定為空，略過處理");
            return;
        }

        _logger.LogInformation("TeamMapping 包含 {Count} 個映射規則", _teamNameToDisplayName.Count);

        // 3. 對每個 User Story 進行團隊名稱映射
        var mappedWorkItems = new List<UserStoryWorkItemOutput>();
        var mappedCount = 0;
        var notMappedCount = 0;

        foreach (var workItem in fetchResult.WorkItems)
        {
            // 若 OriginalTeamName 為空，保留原樣
            if (string.IsNullOrWhiteSpace(workItem.OriginalTeamName))
            {
                mappedWorkItems.Add(workItem);
                notMappedCount++;
                continue;
            }

            // 尋找匹配的團隊映射（忽略大小寫的 Contains 比對）
            var matchedEntry = _teamNameToDisplayName.FirstOrDefault(kvp =>
                workItem.OriginalTeamName.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(matchedEntry.Key))
            {
                // 替換為 DisplayName
                var mappedWorkItem = workItem with { OriginalTeamName = matchedEntry.Value };
                mappedWorkItems.Add(mappedWorkItem);
                mappedCount++;
            }
            else
            {
                // 未找到匹配，保留原樣
                mappedWorkItems.Add(workItem);
                notMappedCount++;
            }
        }

        _logger.LogInformation("團隊名稱映射完成。總數: {Total}, 已映射: {Mapped}, 未映射: {NotMapped}",
            fetchResult.WorkItems.Count, mappedCount, notMappedCount);

        // 4. 建立映射後的結果
        var mappedResult = new UserStoryFetchResult
        {
            WorkItems = mappedWorkItems,
            TotalWorkItems = fetchResult.TotalWorkItems,
            AlreadyUserStoryCount = fetchResult.AlreadyUserStoryCount,
            FoundViaRecursionCount = fetchResult.FoundViaRecursionCount,
            NotFoundCount = fetchResult.NotFoundCount,
            OriginalFetchFailedCount = fetchResult.OriginalFetchFailedCount
        };

        // 5. 寫入新的 Redis Key
        var targetJson = mappedResult.ToJson();
        await _redisService.SetAsync(RedisKeys.AzureDevOpsUserStoryWorkItemsTeamMapped, targetJson);

        _logger.LogInformation("團隊映射結果已寫入 Redis Key {Key}", RedisKeys.AzureDevOpsUserStoryWorkItemsTeamMapped);

        // 6. 輸出至 stdout
        Console.WriteLine(targetJson);
    }

    /// <summary>
    /// 從 TeamMappingOptions 中提取團隊名稱與 DisplayName 的對應字典
    /// </summary>
    /// <param name="options">團隊映射設定</param>
    /// <returns>團隊名稱與 DisplayName 的對應字典</returns>
    private static IReadOnlyDictionary<string, string> ExtractTeamNameToDisplayName(TeamMappingOptions options)
    {
        return options.Mappings
            .Where(m => !string.IsNullOrWhiteSpace(m.OriginalTeamName) && !string.IsNullOrWhiteSpace(m.DisplayName))
            .GroupBy(m => m.OriginalTeamName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().DisplayName, StringComparer.OrdinalIgnoreCase);
    }
}
