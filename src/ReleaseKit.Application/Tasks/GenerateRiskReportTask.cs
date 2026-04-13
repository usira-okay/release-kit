using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 產生最終風險分析報告
/// </summary>
/// <remarks>
/// 從 Redis 載入所有 Intermediate 中間報告，透過 Copilot 產生 Markdown 最終報告。
/// </remarks>
public class GenerateRiskReportTask : ITask
{
    private readonly IRedisService _redisService;
    private readonly IRiskAnalyzer _riskAnalyzer;
    private readonly RiskAnalysisOptions _options;
    private readonly INow _now;
    private readonly ILogger<GenerateRiskReportTask> _logger;

    /// <summary>
    /// 初始化 <see cref="GenerateRiskReportTask"/> 類別的新執行個體
    /// </summary>
    public GenerateRiskReportTask(
        IRedisService redisService,
        IRiskAnalyzer riskAnalyzer,
        IOptions<RiskAnalysisOptions> options,
        INow now,
        ILogger<GenerateRiskReportTask> logger)
    {
        _redisService = redisService;
        _riskAnalyzer = riskAnalyzer;
        _options = options.Value;
        _now = now;
        _logger = logger;
    }

    /// <summary>執行最終報告產生任務</summary>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始產生最終風險分析報告");

        var reports = await LoadIntermediateReportsAsync();
        _logger.LogInformation("載入 {Count} 份中間報告", reports.Count);

        var markdown = await _riskAnalyzer.GenerateFinalReportAsync(reports);

        await _redisService.HashSetAsync(
            RedisKeys.RiskAnalysisHash, RedisKeys.Fields.FinalReport, markdown);
        _logger.LogInformation("最終報告已存入 Redis");

        await WriteReportToFileAsync(markdown);
        _logger.LogInformation("最終風險分析報告產生完成");
    }

    /// <summary>從 Redis 載入所有中間分析報告（Markdown 格式）</summary>
    private async Task<IReadOnlyList<string>> LoadIntermediateReportsAsync()
    {
        var entries = await _redisService.HashGetByPrefixAsync(
            RedisKeys.RiskAnalysisHash, RedisKeys.Fields.IntermediatePrefix);

        return entries
            .OrderBy(e => GetIntermediateReportSequence(e.Key))
            .Select(e => e.Value)
            .ToList();
    }

    private static int GetIntermediateReportSequence(string key)
    {
        var prefix = RedisKeys.Fields.IntermediatePrefix;
        if (key.StartsWith(prefix, StringComparison.Ordinal) &&
            int.TryParse(key[prefix.Length..], out var sequence))
        {
            return sequence;
        }

        return int.MaxValue;
    }

    /// <summary>將報告寫入檔案系統</summary>
    private async Task WriteReportToFileAsync(string markdown)
    {
        Directory.CreateDirectory(_options.ReportOutputPath);

        var fileName = $"risk-report-{_now.UtcNow:yyyy-MM-dd}.md";
        var filePath = Path.Combine(_options.ReportOutputPath, fileName);

        await File.WriteAllTextAsync(filePath, markdown);
        _logger.LogInformation("報告已寫入 {FilePath}", filePath);
    }
}
