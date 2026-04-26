using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Application.Common;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Entities;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// Stage 6：產生風險報告
/// </summary>
/// <remarks>
/// 讀取 Stage 4 各專案風險分析結果與 Stage 5 跨專案交叉比對結果，
/// 組裝 <see cref="RiskReport"/>，產生 Markdown 報告並輸出至 Redis 與本機檔案。
/// </remarks>
public class GenerateRiskReportTask : ITask
{
    private readonly IMarkdownReportGenerator _reportGenerator;
    private readonly IRedisService _redisService;
    private readonly INow _now;
    private readonly IOptions<RiskAnalysisOptions> _riskOptions;
    private readonly ILogger<GenerateRiskReportTask> _logger;

    /// <summary>
    /// 初始化 <see cref="GenerateRiskReportTask"/> 類別的新執行個體
    /// </summary>
    /// <param name="reportGenerator">Markdown 報告產生器</param>
    /// <param name="redisService">Redis 快取服務</param>
    /// <param name="now">時間服務</param>
    /// <param name="riskOptions">風險分析設定選項</param>
    /// <param name="logger">日誌記錄器</param>
    public GenerateRiskReportTask(
        IMarkdownReportGenerator reportGenerator,
        IRedisService redisService,
        INow now,
        IOptions<RiskAnalysisOptions> riskOptions,
        ILogger<GenerateRiskReportTask> logger)
    {
        _reportGenerator = reportGenerator;
        _redisService = redisService;
        _now = now;
        _riskOptions = riskOptions;
        _logger = logger;
    }

    /// <summary>
    /// 執行 Stage 6：讀取 Stage 4/5 資料，組裝報告，產生 Markdown 並儲存至 Redis 與本機檔案
    /// </summary>
    public async Task ExecuteAsync()
    {
        var runId = await _redisService.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey);
        if (string.IsNullOrEmpty(runId))
        {
            _logger.LogWarning("找不到 RunId，請先執行 Stage 1");
            return;
        }
        _logger.LogInformation("開始 Stage 6: 產生風險報告, RunId={RunId}", runId);

        var projectAnalyses = await LoadStage4AnalysesAsync(runId);
        var correlation = await LoadStage5CorrelationAsync(runId);

        var report = new RiskReport
        {
            RunId = runId,
            ExecutedAt = _now.UtcNow,
            Correlation = correlation,
            ProjectAnalyses = projectAnalyses,
            MarkdownContent = string.Empty
        };

        var markdown = _reportGenerator.Generate(report);
        report = report with { MarkdownContent = markdown };

        await _redisService.HashSetAsync(
            RiskAnalysisRedisKeys.Stage6Hash(runId),
            RiskAnalysisRedisKeys.ReportField,
            report.ToJson());

        await WriteReportFileAsync(runId, markdown);

        _logger.LogInformation("Stage 6 完成: 報告已產生, RunId={RunId}", runId);
    }

    /// <summary>
    /// 從 Redis Stage 4 Hash 載入所有專案風險分析結果
    /// </summary>
    private async Task<IReadOnlyList<ProjectRiskAnalysis>> LoadStage4AnalysesAsync(string runId)
    {
        var stage4Data = await _redisService.HashGetAllAsync(RiskAnalysisRedisKeys.Stage4Hash(runId));
        return stage4Data.Values
            .Select(json => json.ToTypedObject<ProjectRiskAnalysis>())
            .Where(a => a != null)
            .Cast<ProjectRiskAnalysis>()
            .ToList();
    }

    /// <summary>
    /// 從 Redis Stage 5 Hash 載入跨專案交叉比對結果
    /// </summary>
    private async Task<CrossProjectCorrelation> LoadStage5CorrelationAsync(string runId)
    {
        var correlationJson = await _redisService.HashGetAsync(
            RiskAnalysisRedisKeys.Stage5Hash(runId),
            RiskAnalysisRedisKeys.CorrelationField);

        if (correlationJson != null)
        {
            var deserialized = correlationJson.ToTypedObject<CrossProjectCorrelation>();
            if (deserialized != null)
                return deserialized;
        }

        return new CrossProjectCorrelation
        {
            DependencyEdges = new List<DependencyEdge>(),
            CorrelatedFindings = new List<CorrelatedRiskFinding>(),
            NotificationTargets = new List<NotificationTarget>()
        };
    }

    /// <summary>
    /// 將 Markdown 報告寫入本機檔案
    /// </summary>
    private async Task WriteReportFileAsync(string runId, string markdown)
    {
        var outputPath = _riskOptions.Value.ReportOutputPath;
        Directory.CreateDirectory(outputPath);

        var filePath = Path.Combine(outputPath, $"{runId}-risk-report.md");
        await File.WriteAllTextAsync(filePath, markdown);

        _logger.LogInformation("風險報告已寫入: {FilePath}", filePath);
    }
}
