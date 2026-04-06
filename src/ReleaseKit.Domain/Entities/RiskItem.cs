using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Entities;

/// <summary>
/// 單一風險項目
/// </summary>
public sealed record RiskItem
{
    /// <summary>風險類別</summary>
    public required RiskCategory Category { get; init; }

    /// <summary>風險等級</summary>
    public required RiskLevel Level { get; init; }

    /// <summary>變更摘要（繁體中文）</summary>
    public required string ChangeSummary { get; init; }

    /// <summary>影響的檔案路徑</summary>
    public required IReadOnlyList<string> AffectedFiles { get; init; }

    /// <summary>可能受影響的外部服務或元件</summary>
    public required IReadOnlyList<string> PotentiallyAffectedServices { get; init; }

    /// <summary>來源專案（跨專案分析時填入）</summary>
    public string? SourceProject { get; init; }

    /// <summary>受影響專案（跨專案分析時填入）</summary>
    public string? AffectedProject { get; init; }

    /// <summary>影響描述（繁體中文）</summary>
    public required string ImpactDescription { get; init; }

    /// <summary>建議的驗證步驟</summary>
    public required IReadOnlyList<string> SuggestedValidationSteps { get; init; }
}
