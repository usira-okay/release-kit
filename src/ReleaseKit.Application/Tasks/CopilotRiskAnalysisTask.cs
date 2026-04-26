using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Application.Common;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// Stage 4：Copilot AI 風險分析
/// </summary>
/// <remarks>
/// 從 Redis 讀取 Stage 2 diff 結果與 Stage 3 專案結構，對每個有 diff 的專案呼叫
/// Copilot AI 進行風險分析，並將 <see cref="ProjectRiskAnalysis"/> 寫入 Stage 4 Redis Hash。
/// </remarks>
public class CopilotRiskAnalysisTask : ITask
{
    private readonly ICopilotRiskAnalyzer _analyzer;
    private readonly IRedisService _redisService;
    private readonly IOptions<RiskAnalysisOptions> _riskOptions;
    private readonly ILogger<CopilotRiskAnalysisTask> _logger;

    /// <summary>
    /// 初始化 <see cref="CopilotRiskAnalysisTask"/> 類別的新執行個體
    /// </summary>
    /// <param name="analyzer">Copilot 風險分析器</param>
    /// <param name="redisService">Redis 快取服務</param>
    /// <param name="riskOptions">風險分析設定選項</param>
    /// <param name="logger">日誌記錄器</param>
    public CopilotRiskAnalysisTask(
        ICopilotRiskAnalyzer analyzer,
        IRedisService redisService,
        IOptions<RiskAnalysisOptions> riskOptions,
        ILogger<CopilotRiskAnalysisTask> logger)
    {
        _analyzer = analyzer;
        _redisService = redisService;
        _riskOptions = riskOptions;
        _logger = logger;
    }

    /// <summary>
    /// 執行 Stage 4：讀取 Stage 2/3 資料並對每個有 diff 的專案執行 Copilot 風險分析
    /// </summary>
    public async Task ExecuteAsync()
    {
        var runId = await _redisService.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey);
        if (string.IsNullOrEmpty(runId))
        {
            _logger.LogWarning("找不到 RunId，請先執行 Stage 1");
            return;
        }
        _logger.LogInformation("開始 Stage 4: Copilot 風險分析, RunId={RunId}", runId);

        var stage2Data = await _redisService.HashGetAllAsync(RiskAnalysisRedisKeys.Stage2Hash(runId));
        var stage3Data = await _redisService.HashGetAllAsync(RiskAnalysisRedisKeys.Stage3Hash(runId));

        var scenarios = _riskOptions.Value.Scenarios
            .Select(s => Enum.Parse<RiskScenario>(s, ignoreCase: true))
            .ToList();

        var totalFindings = 0;
        var totalSessions = 0;

        foreach (var (projectPath, diffJson) in stage2Data)
        {
            var diffResult = diffJson.ToTypedObject<ProjectDiffResult>();
            if (diffResult == null || diffResult.FileDiffs.Count == 0)
            {
                _logger.LogInformation("專案 {ProjectPath} 無 diff 資料，跳過", projectPath);
                continue;
            }

            ProjectStructure? structure = null;
            if (stage3Data.TryGetValue(projectPath, out var structureJson))
                structure = structureJson.ToTypedObject<ProjectStructure>();

            var (findings, sessionCount) = await _analyzer.AnalyzeAsync(
                projectPath,
                diffResult.FileDiffs,
                structure,
                scenarios,
                changedBy: string.Empty);

            var analysis = new ProjectRiskAnalysis
            {
                ProjectPath = projectPath,
                Findings = findings,
                SessionCount = sessionCount
            };

            await _redisService.HashSetAsync(
                RiskAnalysisRedisKeys.Stage4Hash(runId),
                projectPath,
                analysis.ToJson());

            totalFindings += findings.Count;
            totalSessions += sessionCount;

            _logger.LogInformation("專案 {ProjectPath} 分析完成: {FindingCount} 個風險, {SessionCount} 個 session",
                projectPath, findings.Count, sessionCount);
        }

        _logger.LogInformation("Stage 4 完成: {TotalFindings} 個風險, {TotalSessions} 個 session, RunId={RunId}",
            totalFindings, totalSessions, runId);
    }
}
