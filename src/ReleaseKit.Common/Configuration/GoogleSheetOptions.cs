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
    /// 服務帳戶憑證檔案路徑
    /// </summary>
    public string ServiceAccountCredentialPath { get; init; } = string.Empty;

    /// <summary>
    /// 欄位映射配置
    /// </summary>
    public ColumnMappingOptions ColumnMapping { get; init; } = new();

    /// <summary>
    /// 團隊排序規則清單（依 Sort 數字由小到大排序）
    /// </summary>
    public List<TeamSortRule> TeamSortRules { get; init; } = [];
}
