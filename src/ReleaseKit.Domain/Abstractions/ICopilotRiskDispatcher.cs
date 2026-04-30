using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Abstractions;

/// <summary>
/// Copilot 雙層 SubAgent 風險分析調度器介面
/// </summary>
public interface ICopilotRiskDispatcher
{
    /// <summary>
    /// 啟動 SubAgent 1（Dispatcher），由 AI 決定如何分組 Commit 並派發 SubAgent 2（Analyzer）進行分析。
    /// SubAgent 2 完成後直接將分析結果寫入 Redis Stage 4。
    /// </summary>
    /// <param name="runId">本次執行 ID</param>
    /// <param name="projectPath">專案路徑</param>
    /// <param name="commitSummaries">各 Commit 的異動摘要（含 metadata，不含完整 diff）</param>
    /// <param name="localPath">本地 clone 路徑（供 get_diff 工具使用）</param>
    /// <param name="scenarios">要分析的風險情境清單</param>
    /// <param name="ct">取消標記</param>
    Task DispatchAsync(
        string runId,
        string projectPath,
        IReadOnlyList<CommitSummary> commitSummaries,
        string localPath,
        IReadOnlyList<RiskScenario> scenarios,
        CancellationToken ct = default);
}
