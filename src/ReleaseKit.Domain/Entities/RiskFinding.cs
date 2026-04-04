using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Entities;

/// <summary>
/// 單一風險發現，描述特定的風險類別與影響
/// </summary>
public sealed record RiskFinding
{
    /// <summary>風險類別</summary>
    public required RiskCategory Category { get; init; }

    /// <summary>風險描述</summary>
    public required string Description { get; init; }

    /// <summary>受影響的元件名稱</summary>
    public required string AffectedComponent { get; init; }
}
