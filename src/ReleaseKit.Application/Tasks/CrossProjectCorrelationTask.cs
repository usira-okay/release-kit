using Microsoft.Extensions.Logging;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// Stage 5：跨專案交叉比對
/// </summary>
/// <remarks>
/// 讀取 Stage 4 AI 風險分析結果，從 AI 推斷的 <see cref="RiskFinding.PotentiallyAffectedProjects"/>
/// 建立相依性圖，並以此作為確認受影響專案的依據，最後將 <see cref="CrossProjectCorrelation"/> 寫入 Stage 5 Redis Hash。
/// </remarks>
public class CrossProjectCorrelationTask : ITask
{
    private readonly IRedisService _redisService;
    private readonly ILogger<CrossProjectCorrelationTask> _logger;

    /// <summary>
    /// 初始化 <see cref="CrossProjectCorrelationTask"/> 類別的新執行個體
    /// </summary>
    public CrossProjectCorrelationTask(
        IRedisService redisService,
        ILogger<CrossProjectCorrelationTask> logger)
    {
        _redisService = redisService;
        _logger = logger;
    }

    /// <summary>
    /// 執行 Stage 5 跨專案交叉比對
    /// </summary>
    public async Task ExecuteAsync()
    {
        var runId = await _redisService.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey);
        if (string.IsNullOrEmpty(runId))
        {
            _logger.LogWarning("找不到 RunId，請先執行 Stage 1");
            return;
        }
        _logger.LogInformation("開始 Stage 5: 跨專案交叉比對, RunId={RunId}", runId);

        var analyses = await LoadStage4AnalysesAsync(runId);

        var dependencyEdges = BuildDependencyEdges(analyses);
        var correlatedFindings = CorrelateFindings(analyses, dependencyEdges);
        var notifications = BuildNotificationTargets(correlatedFindings);

        var correlation = new CrossProjectCorrelation
        {
            DependencyEdges = dependencyEdges,
            CorrelatedFindings = correlatedFindings,
            NotificationTargets = notifications
        };

        await _redisService.HashSetAsync(
            RiskAnalysisRedisKeys.Stage5Hash(runId),
            RiskAnalysisRedisKeys.CorrelationField,
            correlation.ToJson());

        _logger.LogInformation("Stage 5 完成: {EdgeCount} 條相依, {FindingCount} 個風險, {NotifyCount} 個通知",
            dependencyEdges.Count, correlatedFindings.Count, notifications.Count);
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
    /// 從 Stage 4 AI 風險發現中建立跨專案相依性邊清單
    /// </summary>
    /// <remarks>
    /// 以 AI 推斷的 <see cref="RiskFinding.PotentiallyAffectedProjects"/> 作為相依關係來源，
    /// 對每個風險發現中提及的受影響專案建立有向邊（變更專案 → 受影響專案）。
    /// </remarks>
    internal static IReadOnlyList<DependencyEdge> BuildDependencyEdges(IReadOnlyList<ProjectRiskAnalysis> analyses)
    {
        var edges = new List<DependencyEdge>();

        foreach (var analysis in analyses)
        {
            foreach (var finding in analysis.Findings)
            {
                foreach (var affectedProject in finding.PotentiallyAffectedProjects)
                {
                    var alreadyExists = edges.Any(e =>
                        e.SourceProject == analysis.ProjectPath &&
                        e.TargetProject == affectedProject &&
                        e.DependencyType == MapScenarioToDependencyType(finding.Scenario));

                    if (!alreadyExists)
                    {
                        edges.Add(new DependencyEdge
                        {
                            SourceProject = analysis.ProjectPath,
                            TargetProject = affectedProject,
                            DependencyType = MapScenarioToDependencyType(finding.Scenario),
                            Target = finding.AffectedFile
                        });
                    }
                }
            }
        }

        return edges;
    }

    /// <summary>
    /// 將 AI 推斷的 PotentiallyAffectedProjects 作為確認受影響專案，並依相依性圖調整風險等級
    /// </summary>
    /// <remarks>
    /// 由於 Stage 3 靜態分析已移除，此處直接採用 AI 推斷結果作為 <c>ConfirmedAffectedProjects</c>。
    /// 若有確認受影響專案且原始等級為 <see cref="RiskLevel.Medium"/>，則提升至 <see cref="RiskLevel.High"/>。
    /// </remarks>
    internal static IReadOnlyList<CorrelatedRiskFinding> CorrelateFindings(
        IReadOnlyList<ProjectRiskAnalysis> analyses,
        IReadOnlyList<DependencyEdge> dependencyEdges)
    {
        var result = new List<CorrelatedRiskFinding>();

        foreach (var analysis in analyses)
        {
            foreach (var finding in analysis.Findings)
            {
                var confirmed = finding.PotentiallyAffectedProjects.ToList();

                var finalRiskLevel = confirmed.Count > 0 && finding.RiskLevel == RiskLevel.Medium
                    ? RiskLevel.High
                    : finding.RiskLevel;

                result.Add(new CorrelatedRiskFinding
                {
                    OriginalFinding = finding,
                    ConfirmedAffectedProjects = confirmed,
                    FinalRiskLevel = finalRiskLevel
                });
            }
        }

        return result;
    }

    /// <summary>
    /// 從已確認受影響的風險發現建立通知對象清單
    /// </summary>
    internal static IReadOnlyList<NotificationTarget> BuildNotificationTargets(
        IReadOnlyList<CorrelatedRiskFinding> correlatedFindings)
    {
        var targets = new List<NotificationTarget>();

        foreach (var correlated in correlatedFindings)
        {
            if (correlated.ConfirmedAffectedProjects.Count == 0)
                continue;

            foreach (var affectedProject in correlated.ConfirmedAffectedProjects)
            {
                targets.Add(new NotificationTarget
                {
                    PersonName = correlated.OriginalFinding.ChangedBy,
                    RiskDescription = correlated.OriginalFinding.Description,
                    RelatedProject = affectedProject
                });
            }
        }

        return targets;
    }

    /// <summary>
    /// 將 <see cref="RiskScenario"/> 對應至最接近的 <see cref="DependencyType"/>
    /// </summary>
    private static DependencyType MapScenarioToDependencyType(RiskScenario scenario) => scenario switch
    {
        RiskScenario.ApiContractBreak => DependencyType.HttpCall,
        RiskScenario.DatabaseSchemaChange => DependencyType.SharedDb,
        RiskScenario.MessageQueueFormat => DependencyType.SharedMQ,
        _ => DependencyType.HttpCall
    };
}
