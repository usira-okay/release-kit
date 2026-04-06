namespace ReleaseKit.Domain.ValueObjects;

/// <summary>
/// 風險等級
/// </summary>
public enum RiskLevel
{
    /// <summary>高風險：需立即處理</summary>
    High,

    /// <summary>中風險：建議關注</summary>
    Medium,

    /// <summary>低風險：知悉即可</summary>
    Low
}
