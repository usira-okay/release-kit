namespace ReleaseKit.Console.Options;

/// <summary>
/// Google Sheet 設定選項
/// </summary>
public class GoogleSheetOptions
{
    /// <summary>
    /// Google Spreadsheet ID
    /// </summary>
    public string SpreadsheetId { get; set; } = string.Empty;
    
    /// <summary>
    /// 工作表名稱 (例如 "Sheet1")
    /// </summary>
    public string SheetName { get; set; } = string.Empty;
    
    /// <summary>
    /// 服務帳號憑證檔案路徑
    /// </summary>
    public string ServiceAccountCredentialPath { get; set; } = string.Empty;
    
    /// <summary>
    /// 欄位對應設定
    /// </summary>
    public ColumnMappingOptions ColumnMapping { get; set; } = new();
    
    /// <summary>
    /// 驗證配置是否正確
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SpreadsheetId))
            throw new InvalidOperationException("GoogleSheet:SpreadsheetId 組態設定不得為空");
            
        if (string.IsNullOrWhiteSpace(SheetName))
            throw new InvalidOperationException("GoogleSheet:SheetName 組態設定不得為空");
            
        if (string.IsNullOrWhiteSpace(ServiceAccountCredentialPath))
            throw new InvalidOperationException("GoogleSheet:ServiceAccountCredentialPath 組態設定不得為空");
        
        ColumnMapping.Validate();
    }
}
