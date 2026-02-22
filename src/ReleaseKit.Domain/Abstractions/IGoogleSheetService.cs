namespace ReleaseKit.Domain.Abstractions;

/// <summary>
/// Google Sheets 服務介面
/// </summary>
public interface IGoogleSheetService
{
    /// <summary>
    /// 讀取指定範圍的資料
    /// </summary>
    /// <param name="spreadsheetId">試算表 ID</param>
    /// <param name="sheetName">工作表名稱</param>
    /// <param name="range">儲存格範圍（例如 "A:Z"）</param>
    /// <returns>二維資料列表，每個內部列表代表一列的儲存格值</returns>
    Task<IList<IList<object>>?> GetSheetDataAsync(string spreadsheetId, string sheetName, string range);

    /// <summary>
    /// 在指定位置插入空白列
    /// </summary>
    /// <param name="spreadsheetId">試算表 ID</param>
    /// <param name="sheetId">工作表 ID（整數）</param>
    /// <param name="rowIndex">插入位置（0-based row index）</param>
    Task InsertRowAsync(string spreadsheetId, int sheetId, int rowIndex);

    /// <summary>
    /// 批次更新多個儲存格值
    /// </summary>
    /// <param name="spreadsheetId">試算表 ID</param>
    /// <param name="sheetName">工作表名稱</param>
    /// <param name="updates">儲存格更新列表，Key 為 A1 表示法的儲存格位址，Value 為儲存格內容</param>
    Task UpdateCellsAsync(string spreadsheetId, string sheetName, IReadOnlyDictionary<string, string> updates);
}
