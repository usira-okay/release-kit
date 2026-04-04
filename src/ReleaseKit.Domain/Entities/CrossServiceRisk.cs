using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Entities;

/// <summary>
/// 跨服務風險關聯
/// </summary>
public sealed record CrossServiceRisk
{
    /// <summary>來源服務（發起變更的服務）</summary>
    public required string SourceService { get; init; }

    /// <summary>受影響的服務清單</summary>
    public required IReadOnlyList<string> AffectedServices { get; init; }

    /// <summary>風險等級</summary>
    public required RiskLevel RiskLevel { get; init; }

    /// <summary>影響描述</summary>
    public required string ImpactDescription { get; init; }

    /// <summary>建議行動</summary>
    public required string SuggestedAction { get; init; }

    /// <summary>關聯的 PR ID 清單</summary>
    public required IReadOnlyList<string> RelatedPrIds { get; init; }
}
