using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Abstractions;

/// <summary>
/// 情境專家型 Copilot 風險分析調度器介面
/// </summary>
public interface ICopilotScenarioDispatcher
{
    /// <summary>
    /// 啟動三層 Agent Pipeline（Coordinator → Expert × 5 → Synthesis）對指定專案進行風險分析
    /// </summary>
    /// <param name="runId">本次執行 ID</param>
    /// <param name="projectPath">專案路徑</param>
    /// <param name="commitSummaries">各 Commit 的異動摘要</param>
    /// <param name="localPath">本地 clone 路徑</param>
    /// <param name="scenarios">要分析的風險情境清單</param>
    /// <param name="ct">取消標記</param>
    /// <returns>分析結果</returns>
    Task<ProjectRiskAnalysis> DispatchAsync(
        string runId,
        string projectPath,
        IReadOnlyList<CommitSummary> commitSummaries,
        string localPath,
        IReadOnlyList<RiskScenario> scenarios,
        CancellationToken ct = default);
}
