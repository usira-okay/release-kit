using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// Pass 1 專案風險分析任務
/// </summary>
/// <remarks>
/// 從 Redis 讀取各專案的 PR Diff 資料，
/// 透過 <see cref="IRiskAnalyzer"/> 對每個專案進行 AI 風險分析，
/// 大型 diff 會自動拆分為多個子代理分析後合併結果。
/// </remarks>
public class AnalyzeProjectRiskTask : ITask
{
    private readonly IRedisService _redisService;
    private readonly IRiskAnalyzer _riskAnalyzer;
    private readonly RiskAnalysisOptions _options;
    private readonly INow _now;
    private readonly ILogger<AnalyzeProjectRiskTask> _logger;

    /// <summary>
    /// 初始化 <see cref="AnalyzeProjectRiskTask"/> 類別的新執行個體
    /// </summary>
    /// <param name="redisService">Redis 服務</param>
    /// <param name="riskAnalyzer">AI 風險分析器</param>
    /// <param name="options">風險分析組態</param>
    /// <param name="now">時間服務</param>
    /// <param name="logger">日誌記錄器</param>
    public AnalyzeProjectRiskTask(
        IRedisService redisService,
        IRiskAnalyzer riskAnalyzer,
        IOptions<RiskAnalysisOptions> options,
        INow now,
        ILogger<AnalyzeProjectRiskTask> logger)
    {
        _redisService = redisService;
        _riskAnalyzer = riskAnalyzer;
        _options = options.Value;
        _now = now;
        _logger = logger;
    }

    /// <summary>
    /// 執行 Pass 1 專案風險分析
    /// </summary>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始 Pass 1 專案風險分析");

        var json = await _redisService.HashGetAsync(
            RedisKeys.RiskAnalysisHash, RedisKeys.Fields.PrDiffs);

        if (string.IsNullOrEmpty(json))
        {
            _logger.LogInformation("Redis 中無 PrDiffs 資料，跳過分析");
            return;
        }

        var diffsByProject = json.ToTypedObject<Dictionary<string, List<PrDiffContext>>>();
        if (diffsByProject is null || diffsByProject.Count == 0)
        {
            _logger.LogInformation("PrDiffs 資料為空，跳過分析");
            return;
        }

        // 按專案名稱排序以確保 Sequence 編號穩定
        var sortedProjects = diffsByProject.OrderBy(p => p.Key).ToList();
        var semaphore = new SemaphoreSlim(_options.MaxConcurrentClones);

        var tasks = sortedProjects.Select((entry, index) =>
            ProcessProjectAsync(entry.Key, entry.Value, index, semaphore));

        await Task.WhenAll(tasks);

        _logger.LogInformation("Pass 1 專案風險分析完成，共處理 {Count} 個專案",
            sortedProjects.Count);
    }

    /// <summary>
    /// 處理單一專案的風險分析
    /// </summary>
    private async Task ProcessProjectAsync(
        string projectName,
        List<PrDiffContext> diffs,
        int index,
        SemaphoreSlim semaphore)
    {
        await semaphore.WaitAsync();
        try
        {
            var passKey = new AnalysisPassKey { Pass = 1, Sequence = index + 1 };
            var totalDiffSize = diffs.Sum(d => d.DiffContent.Length);

            _logger.LogInformation("分析專案 {ProjectName}（Sequence={Sequence}，DiffSize={Size}）",
                projectName, passKey.Sequence, totalDiffSize);

            RiskAnalysisReport report;

            if (totalDiffSize <= _options.MaxTokensPerAiCall)
            {
                report = await _riskAnalyzer.AnalyzeProjectRiskAsync(projectName, diffs);
            }
            else
            {
                report = await AnalyzeInChunksAsync(projectName, diffs, passKey);
            }

            // 正規化 PassKey 與 ProjectName
            report = report with { PassKey = passKey, ProjectName = projectName };

            await _redisService.HashSetAsync(
                RedisKeys.RiskAnalysisHash, passKey.ToRedisField(), report.ToJson());

            _logger.LogInformation("專案 {ProjectName} 分析完成，識別 {Count} 個風險項目",
                projectName, report.RiskItems.Count);
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// 將超過 Token 限制的 diff 拆分為多個 chunk 分別分析後合併
    /// </summary>
    private async Task<RiskAnalysisReport> AnalyzeInChunksAsync(
        string projectName,
        List<PrDiffContext> diffs,
        AnalysisPassKey basePassKey)
    {
        var chunks = SplitDiffsIntoChunks(diffs, _options.MaxTokensPerAiCall);

        _logger.LogInformation("專案 {ProjectName} diff 超過限制，拆分為 {ChunkCount} 個子代理",
            projectName, chunks.Count);

        var chunkReports = new List<RiskAnalysisReport>();

        for (var i = 0; i < chunks.Count; i++)
        {
            var subSequence = ((char)('a' + (i % 26))).ToString();
            if (i >= 26)
                subSequence = $"{(char)('a' + (i / 26 - 1))}{(char)('a' + (i % 26))}";

            var subKey = new AnalysisPassKey
            {
                Pass = basePassKey.Pass,
                Sequence = basePassKey.Sequence,
                SubSequence = subSequence
            };

            _logger.LogInformation("分析子代理 {SubKey}（{DiffCount} 個 diff）",
                subKey.ToRedisField(), chunks[i].Count);

            var chunkReport = await _riskAnalyzer.AnalyzeProjectRiskAsync(
                projectName, chunks[i]);

            chunkReports.Add(chunkReport);
        }

        return MergeReports(chunkReports, basePassKey, projectName);
    }

    /// <summary>
    /// 將 diff 清單依 Token 限制拆分為多個 chunk
    /// </summary>
    internal static List<List<PrDiffContext>> SplitDiffsIntoChunks(
        List<PrDiffContext> diffs,
        int maxTokensPerChunk)
    {
        var chunks = new List<List<PrDiffContext>>();
        var currentChunk = new List<PrDiffContext>();
        var currentSize = 0;

        foreach (var diff in diffs)
        {
            var diffSize = diff.DiffContent.Length;

            // 目前 chunk 加上此 diff 會超過限制，且 chunk 不為空
            if (currentSize + diffSize > maxTokensPerChunk && currentChunk.Count > 0)
            {
                chunks.Add(currentChunk);
                currentChunk = new List<PrDiffContext>();
                currentSize = 0;
            }

            currentChunk.Add(diff);
            currentSize += diffSize;
        }

        if (currentChunk.Count > 0)
            chunks.Add(currentChunk);

        return chunks;
    }

    /// <summary>
    /// 合併多個 chunk 報告為單一專案報告
    /// </summary>
    private RiskAnalysisReport MergeReports(
        List<RiskAnalysisReport> reports,
        AnalysisPassKey passKey,
        string projectName)
    {
        return new RiskAnalysisReport
        {
            PassKey = passKey,
            ProjectName = projectName,
            RiskItems = reports.SelectMany(r => r.RiskItems).ToList(),
            Summary = string.Join("\n", reports.Select(r => r.Summary)),
            AnalyzedAt = _now.UtcNow
        };
    }
}
