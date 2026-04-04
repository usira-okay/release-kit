using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Entities;

/// <summary>
/// 單一 PR 的風險評估結果
/// </summary>
public sealed record PullRequestRisk
{
    /// <summary>PR 識別資訊</summary>
    public required string PrId { get; init; }

    /// <summary>所屬 Repository 名稱</summary>
    public required string RepositoryName { get; init; }

    /// <summary>PR 標題</summary>
    public string PrTitle { get; init; } = string.Empty;

    /// <summary>PR 連結</summary>
    public string PrUrl { get; init; } = string.Empty;

    /// <summary>風險等級</summary>
    public required RiskLevel RiskLevel { get; init; }

    /// <summary>風險類別（可多個）</summary>
    public required IReadOnlyList<RiskCategory> RiskCategories { get; init; }

    /// <summary>風險描述</summary>
    public required string RiskDescription { get; init; }

    /// <summary>是否需要深度分析</summary>
    public required bool NeedsDeepAnalysis { get; init; }

    /// <summary>受影響的元件</summary>
    public required IReadOnlyList<string> AffectedComponents { get; init; }

    /// <summary>建議行動</summary>
    public required string SuggestedAction { get; init; }
}
