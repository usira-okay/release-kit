using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Entities;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 產生最終風險分析報告，從 Redis 載入最後一層中間報告，透過 AI 產生 Markdown 報告
/// </summary>
public class GenerateRiskReportTask : ITask
{
    private readonly IRedisService _redisService;
    private readonly IRiskAnalyzer _riskAnalyzer;
    private readonly RiskAnalysisOptions _options;
    private readonly INow _now;
    private readonly ILogger<GenerateRiskReportTask> _logger;

    /// <summary>
    /// 比對 PassMetadata:{N} 格式的正則表達式
    /// </summary>
    private static readonly Regex PassMetadataRegex = new(
        @"^PassMetadata:(\d+)$", RegexOptions.Compiled);

    /// <summary>
    /// 初始化 <see cref="GenerateRiskReportTask"/> 類別的新執行個體
    /// </summary>
    /// <param name="redisService">Redis 服務</param>
    /// <param name="riskAnalyzer">AI 風險分析器</param>
    /// <param name="options">風險分析組態</param>
    /// <param name="now">時間服務</param>
    /// <param name="logger">日誌記錄器</param>
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

    /// <summary>
    /// 執行最終報告產生任務
    /// </summary>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始產生最終風險分析報告");

        var lastPass = await DetermineLastPassAsync();
        _logger.LogInformation("使用第 {Pass} 層分析結果產生最終報告", lastPass);

        var reports = await LoadIntermediateReportsAsync(lastPass);
        _logger.LogInformation("載入 {Count} 份中間報告", reports.Count);

        var markdown = await _riskAnalyzer.GenerateFinalReportAsync(reports);

        await _redisService.HashSetAsync(
            RedisKeys.RiskAnalysisHash, RedisKeys.Fields.FinalReport, markdown);
        _logger.LogInformation("最終報告已存入 Redis");

        await WriteReportToFileAsync(markdown);
        _logger.LogInformation("最終風險分析報告產生完成");
    }

    /// <summary>
    /// 從 Redis Hash 欄位中判斷最後一層分析的 Pass 編號
    /// </summary>
    private async Task<int> DetermineLastPassAsync()
    {
        var fields = await _redisService.HashFieldsAsync(RedisKeys.RiskAnalysisHash);
        var maxPass = 0;

        foreach (var field in fields)
        {
            var match = PassMetadataRegex.Match(field);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var passNumber))
            {
                maxPass = Math.Max(maxPass, passNumber);
            }
        }

        // 若無 PassMetadata 欄位，預設使用 Pass 1
        return maxPass > 0 ? maxPass : 1;
    }

    /// <summary>
    /// 載入指定層數的中間分析報告
    /// </summary>
    private async Task<IReadOnlyList<RiskAnalysisReport>> LoadIntermediateReportsAsync(int pass)
    {
        var prefix = $"Intermediate:{pass}-";
        var entries = await _redisService.HashGetByPrefixAsync(
            RedisKeys.RiskAnalysisHash, prefix);

        var reports = entries.Values
            .Select(json => json.ToTypedObject<RiskAnalysisReport>()!)
            .OrderBy(r => r.PassKey.Sequence)
            .ThenBy(r => r.PassKey.SubSequence)
            .ToList();

        return reports;
    }

    /// <summary>
    /// 將報告寫入檔案系統
    /// </summary>
    private async Task WriteReportToFileAsync(string markdown)
    {
        Directory.CreateDirectory(_options.ReportOutputPath);

        var fileName = $"risk-report-{_now.UtcNow:yyyy-MM-dd}.md";
        var filePath = Path.Combine(_options.ReportOutputPath, fileName);

        await File.WriteAllTextAsync(filePath, markdown);
        _logger.LogInformation("報告已寫入 {FilePath}", filePath);
    }
}
