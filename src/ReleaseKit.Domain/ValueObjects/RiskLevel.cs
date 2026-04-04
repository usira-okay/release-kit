namespace ReleaseKit.Domain.ValueObjects;

/// <summary>
/// 風險等級，數值越大風險越高
/// </summary>
public enum RiskLevel
{
    /// <summary>無風險（純文件、測試、格式化等）</summary>
    None = 0,

    /// <summary>低風險（小幅修改，風險可控）</summary>
    Low = 1,

    /// <summary>中風險（可能影響效能或需要其他服務同步更新）</summary>
    Medium = 2,

    /// <summary>高風險（可能造成功能異常或安全漏洞）</summary>
    High = 3,

    /// <summary>嚴重風險（可能導致線上服務中斷或資料遺失）</summary>
    Critical = 4
}
