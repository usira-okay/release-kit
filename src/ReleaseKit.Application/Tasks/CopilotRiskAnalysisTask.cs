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
/// 從 Redis 讀取 Stage 2 CommitSummaries 與 Stage 1 localPath，
/// 對每個有 Commit 記錄的專案透過 <see cref="ICopilotRiskDispatcher"/> 啟動雙層 SubAgent 分析流程。
/// SubAgent 2 完成後直接將 <see cref="ProjectRiskAnalysis"/> 寫入 Stage 4 Redis Hash。
/// </remarks>
public class CopilotRiskAnalysisTask : ITask
{
    private readonly ICopilotRiskDispatcher _dispatcher;
    private readonly IRedisService _redisService;
    private readonly IOptions<RiskAnalysisOptions> _riskOptions;
    private readonly ILogger<CopilotRiskAnalysisTask> _logger;

    /// <summary>
    /// 初始化 <see cref="CopilotRiskAnalysisTask"/> 類別的新執行個體
    /// </summary>
    public CopilotRiskAnalysisTask(
        ICopilotRiskDispatcher dispatcher,
        IRedisService redisService,
        IOptions<RiskAnalysisOptions> riskOptions,
        ILogger<CopilotRiskAnalysisTask> logger)
    {
        _dispatcher = dispatcher;
        _redisService = redisService;
        _riskOptions = riskOptions;
        _logger = logger;
    }

    /// <summary>
    /// 執行 Stage 4：讀取 Stage 1/2 資料，對每個有 Commit 的專案啟動雙層 SubAgent 風險分析
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

            await _dispatcher.DispatchAsync(
                runId,
                projectPath,
                diffResult.CommitSummaries,
                cloneResult.LocalPath,
                scenarios);
        }

        _logger.LogInformation("Stage 4 完成, RunId={RunId}", runId);
    }

    /// <summary>
    /// Stage 1 clone 結果的反序列化模型
    /// </summary>
    private sealed record CloneStageResult
    {
        public string LocalPath { get; init; } = "";
        public string Status { get; init; } = "";
    }
}
