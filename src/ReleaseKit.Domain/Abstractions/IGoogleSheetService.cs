namespace ReleaseKit.Domain.Abstractions;

/// <summary>
/// Google Sheets 服務介面
/// </summary>
public interface IGoogleSheetService
{
    /// <summary>
    /// 讀取指定範圍的試算表資料
    /// </summary>
    /// <param name="spreadsheetId">試算表 ID</param>
    /// <param name="sheetName">工作表名稱</param>
    /// <param name="range">讀取範圍（如 "A:Z"）</param>
    /// <returns>二維清單，每個元素代表一列的儲存格值；若無資料則回傳 null</returns>
    Task<IList<IList<object>>?> GetSheetDataAsync(string spreadsheetId, string sheetName, string range);

    /// <summary>
    /// 取得工作表的數字 ID
    /// </summary>
    /// <param name="spreadsheetId">試算表 ID</param>
    /// <param name="sheetName">工作表名稱</param>
    /// <returns>工作表的數字 ID</returns>
    Task<int> GetSheetIdAsync(string spreadsheetId, string sheetName);

    /// <summary>
    /// 在指定位置插入空白列
    /// </summary>
    /// <param name="spreadsheetId">試算表 ID</param>
    /// <param name="sheetId">工作表 ID（數字）</param>
    /// <param name="rowIndex">插入位置的列索引（0-based）</param>
    Task InsertRowAsync(string spreadsheetId, int sheetId, int rowIndex);

    /// <summary>
    /// 批次更新多個儲存格的值
    /// </summary>
    /// <param name="spreadsheetId">試算表 ID</param>
    /// <param name="sheetName">工作表名稱</param>
    /// <param name="updates">儲存格更新清單，Key 為儲存格位址（如 "B5"），Value 為要寫入的值</param>
    Task UpdateCellsAsync(string spreadsheetId, string sheetName, IReadOnlyDictionary<string, string> updates);
}
