using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Entities;

/// <summary>
/// 單一 Repository 的風險分析結果
/// </summary>
public sealed record RepositoryRiskResult
{
    /// <summary>Repository 名稱</summary>
    public required string RepositoryName { get; init; }

    /// <summary>所屬平台</summary>
    public required string Platform { get; init; }

    /// <summary>該 Repo 中所有 PR 的風險評估</summary>
    public required IReadOnlyList<PullRequestRisk> PullRequestRisks { get; init; }

    /// <summary>該 Repo 的最高風險等級</summary>
    public RiskLevel MaxRiskLevel =>
        PullRequestRisks.Count > 0
            ? PullRequestRisks.Max(r => r.RiskLevel)
            : RiskLevel.None;
}
