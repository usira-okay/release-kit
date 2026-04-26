using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Entities;

/// <summary>
/// 單一風險發現
/// </summary>
public sealed record RiskFinding
{
    /// <summary>
    /// 風險情境類型
    /// </summary>
    public required RiskScenario Scenario { get; init; }

    /// <summary>
    /// 風險等級
    /// </summary>
    public required RiskLevel RiskLevel { get; init; }

    /// <summary>
    /// 風險描述
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// 造成風險的檔案
    /// </summary>
    public required string AffectedFile { get; init; }

    /// <summary>
    /// 相關 diff 片段
    /// </summary>
    public required string DiffSnippet { get; init; }

    /// <summary>
    /// 可能受影響的專案清單
    /// </summary>
    public required IReadOnlyList<string> PotentiallyAffectedProjects { get; init; }

    /// <summary>
    /// 建議動作
    /// </summary>
    public required string RecommendedAction { get; init; }

    /// <summary>
    /// 變更者（PR 作者）
    /// </summary>
    public required string ChangedBy { get; init; }
}
