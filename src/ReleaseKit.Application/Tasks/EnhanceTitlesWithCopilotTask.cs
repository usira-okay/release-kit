using Microsoft.Extensions.Logging;
using ReleaseKit.Application.Common;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 使用 AI 增強 Release 標題任務
/// </summary>
/// <remarks>
/// 從 Redis 讀取整合後的 Release 資料，收集各項目的候選標題，
/// 透過 <see cref="ITitleEnhancer"/> 產生更有意義的標題，
/// 並將增強結果與原始資料一起寫入新的 Redis Key。
/// </remarks>
public class EnhanceTitlesWithCopilotTask : ITask
{
    private readonly IRedisService _redisService;
    private readonly ITitleEnhancer _titleEnhancer;
    private readonly ILogger<EnhanceTitlesWithCopilotTask> _logger;

    /// <summary>
    /// 初始化 <see cref="EnhanceTitlesWithCopilotTask"/> 類別的新執行個體
    /// </summary>
    /// <param name="redisService">Redis 服務</param>
    /// <param name="titleEnhancer">標題增強服務</param>
    /// <param name="logger">日誌記錄器</param>
    public EnhanceTitlesWithCopilotTask(
        IRedisService redisService,
        ITitleEnhancer titleEnhancer,
        ILogger<EnhanceTitlesWithCopilotTask> logger)
    {
        _redisService = redisService;
        _titleEnhancer = titleEnhancer;
        _logger = logger;
    }

    /// <summary>
    /// 執行增強標題任務
    /// </summary>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始增強 Release 標題");

        // 1. 從 Redis 讀取整合資料
        var consolidatedResult = await LoadConsolidatedDataAsync();
        if (consolidatedResult == null)
        {
            return;
        }

        // 2. 收集所有 Entry 及其候選標題
        var (allEntries, titleGroups) = CollectTitleGroups(consolidatedResult);

        if (allEntries.Count == 0)
        {
            _logger.LogInformation("沒有項目需要增強標題");
            return;
        }

        // 3. 呼叫 AI 增強標題
        _logger.LogInformation("正在透過 AI 增強 {Count} 個項目的標題", allEntries.Count);
        var enhancedTitles = await _titleEnhancer.EnhanceTitlesAsync(titleGroups);

        // 4. 組合結果
        var enhancedResult = BuildEnhancedResult(consolidatedResult, allEntries, enhancedTitles);

        // 5. 寫入 Redis
        var json = enhancedResult.ToJson();
        await _redisService.HashSetAsync(RedisKeys.ReleaseDataHash, RedisKeys.Fields.EnhancedTitles, json);

        _logger.LogInformation("增強標題完成，共處理 {Count} 個項目，{ProjectCount} 個專案",
            allEntries.Count, enhancedResult.Projects.Count);
    }

    /// <summary>
    /// 從 Redis 讀取整合後的 Release 資料
    /// </summary>
    private async Task<ConsolidatedReleaseResult?> LoadConsolidatedDataAsync()
    {
        var json = await _redisService.HashGetAsync(RedisKeys.ReleaseDataHash, RedisKeys.Fields.Consolidated);
        if (string.IsNullOrEmpty(json))
        {
            _logger.LogInformation("Redis 中無整合 Release 資料，跳過增強標題");
            return null;
        }

        var result = json.ToTypedObject<ConsolidatedReleaseResult>();
        if (result == null || result.Projects.Count == 0)
        {
            _logger.LogInformation("整合 Release 資料為空，跳過增強標題");
            return null;
        }

        _logger.LogInformation("載入整合資料完成，共 {ProjectCount} 個專案", result.Projects.Count);
        return result;
    }

    /// <summary>
    /// 收集所有 Entry 的候選標題群組
    /// </summary>
    /// <remarks>
    /// 優先順序：Entry.Title → OriginalData.WorkItem.Title → OriginalData.PullRequests[].Title，
    /// null 或空白的標題會被排除。
    /// </remarks>
    private static (List<(string ProjectName, int Index, ConsolidatedReleaseEntry Entry)> AllEntries,
        List<IReadOnlyList<string>> TitleGroups)
        CollectTitleGroups(ConsolidatedReleaseResult consolidatedResult)
    {
        var allEntries = new List<(string ProjectName, int Index, ConsolidatedReleaseEntry Entry)>();
        var titleGroups = new List<IReadOnlyList<string>>();

        foreach (var (projectName, entries) in consolidatedResult.Projects)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var candidateTitles = new List<string>();

                // 優先順序 1: Entry.Title
                if (!string.IsNullOrWhiteSpace(entry.Title))
                {
                    candidateTitles.Add(entry.Title);
                }

                // 優先順序 2: 原始 WorkItem.Title
                if (!string.IsNullOrWhiteSpace(entry.OriginalData.WorkItem.Title))
                {
                    candidateTitles.Add(entry.OriginalData.WorkItem.Title);
                }

                // 優先順序 3: PR Titles
                foreach (var pr in entry.OriginalData.PullRequests)
                {
                    if (!string.IsNullOrWhiteSpace(pr.Title))
                    {
                        candidateTitles.Add(pr.Title);
                    }
                }

                allEntries.Add((projectName, i, entry));
                titleGroups.Add(candidateTitles);
            }
        }

        return (allEntries, titleGroups);
    }

    /// <summary>
    /// 建構增強標題結果，保留原始資料結構，僅替換 Title
    /// </summary>
    private static ConsolidatedReleaseResult BuildEnhancedResult(
        ConsolidatedReleaseResult consolidatedResult,
        List<(string ProjectName, int Index, ConsolidatedReleaseEntry Entry)> allEntries,
        IReadOnlyList<string> enhancedTitles)
    {
        var projectEntries = new Dictionary<string, List<ConsolidatedReleaseEntry>>();

        // 初始化專案分組（保持原始順序）
        foreach (var projectName in consolidatedResult.Projects.Keys)
        {
            projectEntries[projectName] = new List<ConsolidatedReleaseEntry>();
        }

        // 將增強標題對應回各專案的 Entry，僅替換 Title
        for (var i = 0; i < allEntries.Count; i++)
        {
            var (projectName, _, entry) = allEntries[i];
            var enhancedTitle = enhancedTitles[i];

            projectEntries[projectName].Add(entry with { Title = enhancedTitle });
        }

        return new ConsolidatedReleaseResult { Projects = projectEntries };
    }
}
