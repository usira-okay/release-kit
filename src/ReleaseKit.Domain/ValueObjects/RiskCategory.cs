namespace ReleaseKit.Domain.ValueObjects;

/// <summary>
/// 風險類別，對應不同類型的程式碼變更風險
/// </summary>
public enum RiskCategory
{
    /// <summary>跨服務 API 破壞性變更（endpoint 修改、request/response 格式變動）</summary>
    CrossServiceApiBreaking,

    /// <summary>共用函式庫/套件版本變動影響</summary>
    SharedLibraryChange,

    /// <summary>資料庫 Schema 變更（Migration、欄位異動）</summary>
    DatabaseSchemaChange,

    /// <summary>資料庫資料異動（INSERT/UPDATE/DELETE/SaveChanges 邏輯變更）</summary>
    DatabaseDataChange,

    /// <summary>設定檔變更（appsettings、環境變數異動）</summary>
    ConfigurationChange,

    /// <summary>安全性相關變動（認證、授權邏輯修改）</summary>
    SecurityChange,

    /// <summary>效能相關變動（快取策略、查詢修改）</summary>
    PerformanceChange,

    /// <summary>核心商業邏輯修改（金流、訂單、權限）</summary>
    CoreBusinessLogicChange
}
