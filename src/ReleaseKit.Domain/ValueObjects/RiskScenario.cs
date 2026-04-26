namespace ReleaseKit.Domain.ValueObjects;

/// <summary>
/// 風險分析情境列舉
/// </summary>
public enum RiskScenario
{
    /// <summary>
    /// API 契約破壞
    /// </summary>
    ApiContractBreak,

    /// <summary>
    /// 資料庫 Schema 變更
    /// </summary>
    DatabaseSchemaChange,

    /// <summary>
    /// 訊息佇列格式變更
    /// </summary>
    MessageQueueFormat,

    /// <summary>
    /// 設定檔/環境變數變更
    /// </summary>
    ConfigEnvChange,

    /// <summary>
    /// 資料庫資料語意變更
    /// </summary>
    DataSemanticChange
}
