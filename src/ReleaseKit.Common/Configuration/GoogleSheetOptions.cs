namespace ReleaseKit.Common.Configuration;

/// <summary>
/// Google Sheets 配置選項
/// </summary>
public class GoogleSheetOptions
{
    /// <summary>
    /// Google 試算表 ID
    /// </summary>
    public string SpreadsheetId { get; init; } = string.Empty;

    /// <summary>
    /// 工作表名稱
    /// </summary>
    public string SheetName { get; init; } = string.Empty;

    /// <summary>
    /// 工作表整數 ID（用於 InsertRow 操作，可在 Google Sheets URL 中找到 gid= 後的數值，預設為 0）
    /// </summary>
    public int SheetId { get; init; } = 0;

    /// <summary>
    /// 服務帳戶憑證檔案路徑
    /// </summary>
    public string ServiceAccountCredentialPath { get; init; } = string.Empty;

    /// <summary>
    /// 欄位映射配置
    /// </summary>
    public ColumnMappingOptions ColumnMapping { get; init; } = new();
}
