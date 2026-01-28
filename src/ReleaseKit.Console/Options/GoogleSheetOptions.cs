namespace ReleaseKit.Console.Options;

/// <summary>
/// Google Sheet 設定選項
/// </summary>
public class GoogleSheetOptions
{
    /// <summary>
    /// Spreadsheet ID
    /// </summary>
    public string SpreadsheetId { get; set; } = string.Empty;

    /// <summary>
    /// Sheet 名稱
    /// </summary>
    public string SheetName { get; set; } = string.Empty;

    /// <summary>
    /// Service Account 憑證檔案路徑
    /// </summary>
    public string ServiceAccountCredentialPath { get; set; } = string.Empty;

    /// <summary>
    /// 欄位對應設定
    /// </summary>
    public ColumnMappingOptions ColumnMapping { get; set; } = new();
}
