namespace ReleaseKit.Domain.Entities;

/// <summary>
/// 表示單一專案的所有 Commit 異動摘要
/// </summary>
public sealed record ProjectDiffResult
{
    /// <summary>
    /// 專案路徑
    /// </summary>
    public required string ProjectPath { get; init; }

    /// <summary>
    /// 各 Commit 的異動摘要清單
    /// </summary>
    public required IReadOnlyList<CommitSummary> CommitSummaries { get; init; }
}
