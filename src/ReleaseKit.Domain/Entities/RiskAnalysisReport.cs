using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Entities;

/// <summary>
/// 風險分析報告 — 聚合根
/// </summary>
public sealed record RiskAnalysisReport
{
    /// <summary>分析時間戳記</summary>
    public required DateTimeOffset AnalyzedAt { get; init; }

    /// <summary>各 Repository 的風險結果</summary>
    public required IReadOnlyList<RepositoryRiskResult> RepositoryResults { get; init; }

    /// <summary>跨服務風險關聯</summary>
    public required IReadOnlyList<CrossServiceRisk> CrossServiceRisks { get; init; }

    /// <summary>分析的 Repository 總數</summary>
    public int TotalRepositories => RepositoryResults.Count;

    /// <summary>分析的 PR 總數</summary>
    public int TotalPullRequests => RepositoryResults.Sum(r => r.PullRequestRisks.Count);

    /// <summary>依風險等級統計 PR 數量</summary>
    public Dictionary<RiskLevel, int> RiskLevelSummary =>
        RepositoryResults
            .SelectMany(r => r.PullRequestRisks)
            .GroupBy(pr => pr.RiskLevel)
            .ToDictionary(g => g.Key, g => g.Count());

    /// <summary>依風險類別統計數量</summary>
    public Dictionary<RiskCategory, int> RiskCategorySummary =>
        RepositoryResults
            .SelectMany(r => r.PullRequestRisks)
            .SelectMany(pr => pr.RiskCategories)
            .GroupBy(c => c)
            .ToDictionary(g => g.Key, g => g.Count());
}
