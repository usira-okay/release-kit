using Microsoft.Extensions.Logging;
using ReleaseKit.Application.Common;
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
/// 讀取 Stage 3 專案結構與 Stage 4 AI 風險分析結果，建立相依性圖後交叉確認受影響專案，
/// 並調整風險等級與產生通知對象清單，最後將 <see cref="CrossProjectCorrelation"/> 寫入 Stage 5 Redis Hash。
/// </remarks>
public class CrossProjectCorrelationTask : ITask
{
    private readonly IRedisService _redisService;
    private readonly ILogger<CrossProjectCorrelationTask> _logger;

    /// <summary>
    /// 初始化 <see cref="CrossProjectCorrelationTask"/> 類別的新執行個體
    /// </summary>
    /// <param name="redisService">Redis 快取服務</param>
    /// <param name="logger">日誌記錄器</param>
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

        var structures = await LoadStage3StructuresAsync(runId);
        var analyses = await LoadStage4AnalysesAsync(runId);

        var dependencyEdges = BuildDependencyEdges(structures);
        var correlatedFindings = CorrelateFindings(analyses, dependencyEdges, structures);
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
    /// 從 Redis Stage 3 Hash 載入所有專案結構
    /// </summary>
    private async Task<IReadOnlyList<ProjectStructure>> LoadStage3StructuresAsync(string runId)
    {
        var stage3Data = await _redisService.HashGetAllAsync(RiskAnalysisRedisKeys.Stage3Hash(runId));
        return stage3Data.Values
            .Select(json => json.ToTypedObject<ProjectStructure>())
            .Where(s => s != null)
            .Cast<ProjectStructure>()
            .ToList();
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
    /// 從所有專案的推斷相依性建立相依性邊清單
    /// </summary>
    /// <remarks>
    /// 對每個專案的每條 <see cref="ServiceDependency"/>，尋找擁有相同相依目標的其他專案，
    /// 建立有向邊（SourceProject → TargetProject）。
    /// </remarks>
    internal static IReadOnlyList<DependencyEdge> BuildDependencyEdges(IReadOnlyList<ProjectStructure> structures)
    {
        var edges = new List<DependencyEdge>();

        foreach (var source in structures)
        {
            foreach (var dep in source.InferredDependencies)
            {
                foreach (var target in structures)
                {
                    if (target.ProjectPath == source.ProjectPath)
                        continue;

                    var targetHasSameDep = target.InferredDependencies
                        .Any(d => d.DependencyType == dep.DependencyType && d.Target == dep.Target);

                    if (targetHasSameDep)
                    {
                        edges.Add(new DependencyEdge
                        {
                            SourceProject = source.ProjectPath,
                            TargetProject = target.ProjectPath,
                            DependencyType = dep.DependencyType,
                            Target = dep.Target
                        });
                    }
                }
            }
        }

        return edges;
    }

    /// <summary>
    /// 交叉比對風險發現與相依性圖，確認真正受影響的專案並調整風險等級
    /// </summary>
    /// <remarks>
    /// 對每個 <see cref="RiskFinding"/>：
    /// <list type="bullet">
    ///   <item>從 <c>PotentiallyAffectedProjects</c> 篩選出確實出現在相依性圖目標節點的專案</item>
    ///   <item>若有確認受影響專案且原始等級為 <see cref="RiskLevel.Medium"/>，則提升至 <see cref="RiskLevel.High"/></item>
    /// </list>
    /// </remarks>
    internal static IReadOnlyList<CorrelatedRiskFinding> CorrelateFindings(
        IReadOnlyList<ProjectRiskAnalysis> analyses,
        IReadOnlyList<DependencyEdge> dependencyEdges,
        IReadOnlyList<ProjectStructure> structures)
    {
        var dependencyTargetProjects = dependencyEdges
            .Select(e => e.TargetProject)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = new List<CorrelatedRiskFinding>();

        foreach (var analysis in analyses)
        {
            foreach (var finding in analysis.Findings)
            {
                var confirmed = finding.PotentiallyAffectedProjects
                    .Where(p => dependencyTargetProjects.Contains(p))
                    .ToList();

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
    /// <remarks>
    /// 對每個有確認受影響專案的 <see cref="CorrelatedRiskFinding"/>，
    /// 以風險發現的 <c>ChangedBy</c> 作為通知對象，每個確認專案產生一筆通知。
    /// </remarks>
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
}
