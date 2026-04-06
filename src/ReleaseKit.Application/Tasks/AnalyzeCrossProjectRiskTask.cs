using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Entities;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// Pass 2~10 跨專案動態深度風險分析任務
/// </summary>
/// <remarks>
/// 載入 Pass 1 的中間報告，透過 <see cref="IRiskAnalyzer"/> 進行多層動態分析，
/// AI 每層判斷是否需要繼續更深入的分析，直到 AI 決定停止或達到硬上限。
/// </remarks>
public class AnalyzeCrossProjectRiskTask : ITask
{
    private readonly IRedisService _redisService;
    private readonly IRiskAnalyzer _riskAnalyzer;
    private readonly RiskAnalysisOptions _options;
    private readonly INow _now;
    private readonly ILogger<AnalyzeCrossProjectRiskTask> _logger;

    /// <summary>
    /// 初始化 <see cref="AnalyzeCrossProjectRiskTask"/> 類別的新執行個體
    /// </summary>
    /// <param name="redisService">Redis 服務</param>
    /// <param name="riskAnalyzer">AI 風險分析器</param>
    /// <param name="options">風險分析組態</param>
    /// <param name="now">時間服務</param>
    /// <param name="logger">日誌記錄器</param>
    public AnalyzeCrossProjectRiskTask(
        IRedisService redisService,
        IRiskAnalyzer riskAnalyzer,
        IOptions<RiskAnalysisOptions> options,
        INow now,
        ILogger<AnalyzeCrossProjectRiskTask> logger)
    {
        _redisService = redisService;
        _riskAnalyzer = riskAnalyzer;
        _options = options.Value;
        _now = now;
        _logger = logger;
    }

    /// <summary>
    /// 執行 Pass 2~10 跨專案動態深度分析
    /// </summary>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始 Pass 2~{MaxPass} 跨專案動態深度分析", _options.MaxAnalysisPasses);

        var previousReports = await LoadPass1ReportsAsync();

        if (previousReports.Count == 0)
        {
            _logger.LogInformation("無 Pass 1 報告，跳過跨專案分析");
            return;
        }

        _logger.LogInformation("載入 {Count} 份 Pass 1 報告", previousReports.Count);

        for (var currentPass = 2; currentPass <= _options.MaxAnalysisPasses; currentPass++)
        {
            _logger.LogInformation("開始 Pass {Pass} 分析", currentPass);

            var result = await _riskAnalyzer.AnalyzeDeepAsync(
                currentPass, previousReports);

            // 儲存此層的中間報告
            await StoreIntermediateReportsAsync(result.Reports);

            // 儲存此層的 metadata
            await StorePassMetadataAsync(currentPass, result);

            _logger.LogInformation(
                "Pass {Pass} 完成：策略={Strategy}，報告數={Count}，繼續={Continue}",
                currentPass, result.AnalysisStrategy, result.Reports.Count, result.ContinueAnalysis);

            if (!result.ContinueAnalysis)
            {
                _logger.LogInformation("AI 決定停止分析（Pass {Pass}）", currentPass);
                break;
            }

            if (currentPass == _options.MaxAnalysisPasses)
            {
                _logger.LogWarning("已達動態分析硬上限 {MaxPass} 層", _options.MaxAnalysisPasses);
            }

            previousReports = result.Reports;
        }

        _logger.LogInformation("跨專案動態深度分析完成");
    }

    /// <summary>
    /// 從 Redis 載入 Pass 1 中間報告
    /// </summary>
    private async Task<IReadOnlyList<RiskAnalysisReport>> LoadPass1ReportsAsync()
    {
        var fields = await _redisService.HashGetByPrefixAsync(
            RedisKeys.RiskAnalysisHash, "Intermediate:1-");

        return fields.Values
            .Select(json => json.ToTypedObject<RiskAnalysisReport>())
            .Where(report => report is not null)
            .Cast<RiskAnalysisReport>()
            .ToList();
    }

    /// <summary>
    /// 儲存中間報告至 Redis
    /// </summary>
    private async Task StoreIntermediateReportsAsync(IReadOnlyList<RiskAnalysisReport> reports)
    {
        foreach (var report in reports)
        {
            await _redisService.HashSetAsync(
                RedisKeys.RiskAnalysisHash,
                report.PassKey.ToRedisField(),
                report.ToJson());
        }
    }

    /// <summary>
    /// 儲存分析層 metadata 至 Redis
    /// </summary>
    private async Task StorePassMetadataAsync(int pass, DynamicAnalysisResult result)
    {
        var metadata = new
        {
            Strategy = result.AnalysisStrategy,
            ContinueReason = result.ContinueReason,
            ReportCount = result.Reports.Count
        };

        await _redisService.HashSetAsync(
            RedisKeys.RiskAnalysisHash,
            $"PassMetadata:{pass}",
            metadata.ToJson());
    }
}
