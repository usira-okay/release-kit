namespace ReleaseKit.Domain.ValueObjects;

/// <summary>
/// 風險類別
/// </summary>
public enum RiskCategory
{
    /// <summary>API 契約變更</summary>
    ApiContract,

    /// <summary>DB Schema 變更</summary>
    DatabaseSchema,

    /// <summary>DB 資料異動</summary>
    DatabaseData,

    /// <summary>事件/訊息格式變更</summary>
    EventFormat,

    /// <summary>設定檔變更</summary>
    Configuration
}
