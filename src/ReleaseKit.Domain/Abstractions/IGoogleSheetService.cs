namespace ReleaseKit.Domain.Abstractions;

/// <summary>
/// Google Sheets 服務介面，定義與 Google Sheets API 互動的所有操作
/// </summary>
public interface IGoogleSheetService
{
    /// <summary>
    /// 讀取指定範圍的 Google Sheet 資料
    /// </summary>
    /// <param name="spreadsheetId">試算表 ID</param>
    /// <param name="sheetName">工作表名稱</param>
    /// <param name="range">讀取範圍（如 "A:Z"）</param>
    /// <returns>二維陣列資料，若讀取失敗回傳 null</returns>
    Task<IList<IList<object>>?> GetSheetDataAsync(string spreadsheetId, string sheetName, string range);

    /// <summary>
    /// 在指定位置插入空白列
    /// </summary>
    /// <param name="spreadsheetId">試算表 ID</param>
    /// <param name="sheetId">工作表 ID（數字）</param>
    /// <param name="rowIndex">插入位置（0-based）</param>
    Task InsertRowAsync(string spreadsheetId, int sheetId, int rowIndex);

    /// <summary>
    /// 由工作表名稱取得工作表 ID
    /// </summary>
    /// <param name="spreadsheetId">試算表 ID</param>
    /// <param name="sheetName">工作表名稱</param>
    /// <returns>工作表 ID（數字）</returns>
    /// <exception cref="InvalidOperationException">找不到指定名稱的工作表時拋出</exception>
    Task<int> GetSheetIdByNameAsync(string spreadsheetId, string sheetName);

    /// <summary>
    /// 批次更新多個儲存格值（支援文字與 HYPERLINK 公式）
    /// </summary>
    /// <param name="spreadsheetId">試算表 ID</param>
    /// <param name="sheetName">工作表名稱</param>
    /// <param name="updates">儲存格更新清單，Key 為儲存格位址（如 "A1"），Value 為儲存格值</param>
    Task UpdateCellsAsync(string spreadsheetId, string sheetName, IDictionary<string, object> updates);
}
