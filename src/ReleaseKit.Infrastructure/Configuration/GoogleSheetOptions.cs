using System.ComponentModel.DataAnnotations;

namespace ReleaseKit.Infrastructure.Configuration;

/// <summary>
/// Google Sheets 配置選項
/// </summary>
public class GoogleSheetOptions
{
    /// <summary>
    /// Google 試算表 ID
    /// </summary>
    [Required(ErrorMessage = "GoogleSheet:SpreadsheetId 不可為空")]
    public string SpreadsheetId { get; init; } = string.Empty;

    /// <summary>
    /// 工作表名稱
    /// </summary>
    [Required(ErrorMessage = "GoogleSheet:SheetName 不可為空")]
    public string SheetName { get; init; } = string.Empty;

    /// <summary>
    /// 服務帳戶憑證檔案路徑
    /// </summary>
    [Required(ErrorMessage = "GoogleSheet:ServiceAccountCredentialPath 不可為空")]
    public string ServiceAccountCredentialPath { get; init; } = string.Empty;

    /// <summary>
    /// 欄位映射配置
    /// </summary>
    [Required(ErrorMessage = "GoogleSheet:ColumnMapping 不可為空")]
    public ColumnMappingOptions ColumnMapping { get; init; } = new();
}
