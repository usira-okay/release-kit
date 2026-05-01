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
/// Stage 4 替代方案：情境專家型 Copilot 風險分析
/// </summary>
/// <remarks>
/// 使用三層 Agent Pipeline（Coordinator → Expert × 5 → Synthesis）對每個專案進行風險分析。
/// 與 <see cref="CopilotRiskAnalysisTask"/> 共存，使用者依場景選用。
/// </remarks>
public class CopilotScenarioAnalysisTask : ITask
{
    private readonly ICopilotScenarioDispatcher _dispatcher;
    private readonly IRedisService _redisService;
    private readonly IOptions<RiskAnalysisOptions> _riskOptions;
    private readonly ILogger<CopilotScenarioAnalysisTask> _logger;

    /// <summary>
    /// 初始化 <see cref="CopilotScenarioAnalysisTask"/>
    /// </summary>
    public CopilotScenarioAnalysisTask(
        ICopilotScenarioDispatcher dispatcher,
        IRedisService redisService,
        IOptions<RiskAnalysisOptions> riskOptions,
        ILogger<CopilotScenarioAnalysisTask> logger)
    {
        _dispatcher = dispatcher;
        _redisService = redisService;
        _riskOptions = riskOptions;
        _logger = logger;
    }

    /// <summary>
    /// 執行情境專家型風險分析
    /// </summary>
    public async Task ExecuteAsync()
    {
        var runId = await _redisService.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey);
        if (string.IsNullOrEmpty(runId))
        {
            _logger.LogWarning("找不到 RunId，請先執行 Stage 1");
            return;
        }

        _logger.LogInformation("開始情境專家型風險分析, RunId={RunId}", runId);

        var stage1Data = await _redisService.HashGetAllAsync(RiskAnalysisRedisKeys.Stage1Hash(runId));
        var stage2Data = await _redisService.HashGetAllAsync(RiskAnalysisRedisKeys.Stage2Hash(runId));

        var scenarios = _riskOptions.Value.Scenarios
            .Select(s => Enum.Parse<RiskScenario>(s, ignoreCase: true))
            .ToList();

        foreach (var (projectPath, diffJson) in stage2Data)
        {
            var diffResult = diffJson.ToTypedObject<ProjectDiffResult>();
            if (diffResult == null || diffResult.CommitSummaries.Count == 0)
            {
                _logger.LogInformation("專案 {ProjectPath} 無 CommitSummary 資料，跳過", projectPath);
                continue;
            }

            if (!stage1Data.TryGetValue(projectPath, out var cloneJson))
            {
                _logger.LogWarning("專案 {ProjectPath} 無 Stage 1 clone 記錄，跳過", projectPath);
                continue;
            }

            var cloneResult = cloneJson.ToTypedObject<CloneStageResult>();
            if (cloneResult?.Status != "Success" || string.IsNullOrEmpty(cloneResult.LocalPath))
            {
                _logger.LogWarning("專案 {ProjectPath} clone 狀態非 Success，跳過", projectPath);
                continue;
            }

            _logger.LogInformation("開始分析專案 {ProjectPath}，共 {CommitCount} 個 commit",
                projectPath, diffResult.CommitSummaries.Count);

            var analysis = await _dispatcher.DispatchAsync(
                runId, projectPath, diffResult.CommitSummaries,
                cloneResult.LocalPath, scenarios);

            await _redisService.HashSetAsync(
                RiskAnalysisRedisKeys.Stage4ScenarioSynthesisHash(runId),
                projectPath,
                analysis.ToJson());
        }

        _logger.LogInformation("情境專家型風險分析完成, RunId={RunId}", runId);
    }

    private sealed record CloneStageResult
    {
        public string LocalPath { get; init; } = "";
        public string Status { get; init; } = "";
    }
}
