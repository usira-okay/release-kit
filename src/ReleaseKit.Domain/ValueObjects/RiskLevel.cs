namespace ReleaseKit.Domain.ValueObjects;

/// <summary>
/// 風險等級列舉
/// </summary>
public enum RiskLevel
{
    /// <summary>
    /// 高風險：破壞性變更（刪除欄位、移除 API、改變 MQ schema）
    /// </summary>
    High,

    /// <summary>
    /// 中風險：可能影響（新增必填欄位、修改回傳格式）
    /// </summary>
    Medium,

    /// <summary>
    /// 低風險：輕微影響（新增選填欄位、新增 API）
    /// </summary>
    Low
}
