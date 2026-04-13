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

    /// <inheritdoc />
    public bool Equals(ProjectAnalysisContext? other)
    {
        if (ReferenceEquals(this, other))
            return true;

        if (other is null)
            return false;

        return string.Equals(ProjectName, other.ProjectName, StringComparison.Ordinal)
               && string.Equals(RepoPath, other.RepoPath, StringComparison.Ordinal)
               && CommitShas.SequenceEqual(other.CommitShas, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ProjectName, StringComparer.Ordinal);
        hash.Add(RepoPath, StringComparer.Ordinal);
        foreach (var sha in CommitShas)
            hash.Add(sha, StringComparer.Ordinal);
        return hash.ToHashCode();
    }
}
