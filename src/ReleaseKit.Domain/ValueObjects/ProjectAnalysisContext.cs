namespace ReleaseKit.Domain.ValueObjects;

/// <summary>
/// 專案分析輸入上下文
/// </summary>
/// <remarks>
/// 提供 Copilot agentic 分析所需的專案資訊，
/// 包含專案名稱、本地 clone 路徑與要分析的 commit SHA 列表。
/// </remarks>
public sealed record ProjectAnalysisContext
{
    /// <summary>專案名稱</summary>
    public required string ProjectName { get; init; }

    /// <summary>本地 clone 路徑</summary>
    public required string RepoPath { get; init; }

    /// <summary>要分析的 commit SHA 列表</summary>
    public required IReadOnlyList<string> CommitShas { get; init; }
}
