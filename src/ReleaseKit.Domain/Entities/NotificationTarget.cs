namespace ReleaseKit.Domain.Entities;

/// <summary>
/// 通知對象
/// </summary>
public sealed record NotificationTarget
{
    /// <summary>
    /// 人員名稱
    /// </summary>
    public required string PersonName { get; init; }

    /// <summary>
    /// 需注意的風險項描述
    /// </summary>
    public required string RiskDescription { get; init; }

    /// <summary>
    /// 相關專案
    /// </summary>
    public required string RelatedProject { get; init; }
}
