namespace ReleaseKit.Domain.Abstractions;

/// <summary>
/// Google Sheet 資料存取服務介面
/// </summary>
public interface IGoogleSheetService
{
    /// <summary>
    /// 透過工作表名稱取得 SheetId
    /// </summary>
    /// <param name="spreadsheetId">試算表 ID</param>
    /// <param name="sheetName">工作表名稱</param>
    /// <returns>SheetId，找不到時回傳 null</returns>
    Task<int?> GetSheetIdByNameAsync(string spreadsheetId, string sheetName);

    /// <summary>
    /// 讀取指定範圍的儲存格資料
    /// </summary>
    /// <param name="spreadsheetId">試算表 ID</param>
    /// <param name="range">讀取範圍（如 "A:Z"）</param>
    /// <returns>儲存格資料，無法讀取時回傳 null</returns>
    Task<IList<IList<object>>?> GetSheetDataAsync(string spreadsheetId, string range);

    /// <summary>
    /// 在指定位置批次插入空白列
    /// </summary>
    /// <param name="spreadsheetId">試算表 ID</param>
    /// <param name="sheetId">工作表 ID</param>
    /// <param name="rowIndex">插入位置（0-based）</param>
    /// <param name="count">插入列數</param>
    Task InsertRowsAsync(string spreadsheetId, int sheetId, int rowIndex, int count);

    /// <summary>
    /// 批次更新指定範圍的儲存格值（純文字）
    /// </summary>
    /// <param name="spreadsheetId">試算表 ID</param>
    /// <param name="range">更新範圍（如 "A1:F1"）</param>
    /// <param name="values">儲存格值</param>
    Task UpdateCellsAsync(string spreadsheetId, string range, IList<IList<object>> values);

    /// <summary>
    /// 更新單一儲存格並設定超連結（使用 HYPERLINK 公式）
    /// </summary>
    /// <param name="spreadsheetId">試算表 ID</param>
    /// <param name="sheetId">工作表 ID</param>
    /// <param name="rowIndex">列索引（0-based）</param>
    /// <param name="columnIndex">欄索引（0-based）</param>
    /// <param name="displayText">顯示文字</param>
    /// <param name="url">超連結網址</param>
    Task UpdateCellWithHyperlinkAsync(string spreadsheetId, int sheetId, int rowIndex, int columnIndex, string displayText, string url);

    /// <summary>
    /// 對指定範圍依指定欄位排序
    /// </summary>
    /// <param name="spreadsheetId">試算表 ID</param>
    /// <param name="sheetId">工作表 ID</param>
    /// <param name="startRowIndex">排序起始列（0-based，含）</param>
    /// <param name="endRowIndex">排序結束列（0-based，不含）</param>
    /// <param name="sortSpecs">排序規格（欄位索引與排序方向）</param>
    Task SortRangeAsync(string spreadsheetId, int sheetId, int startRowIndex, int endRowIndex, IList<(int ColumnIndex, bool Ascending)> sortSpecs);

    /// <summary>
    /// 批次更新多個儲存格範圍
    /// </summary>
    /// <param name="spreadsheetId">試算表 ID</param>
    /// <param name="updates">更新清單（範圍與值的對應）</param>
    Task BatchUpdateCellsAsync(string spreadsheetId, IList<(string Range, IList<IList<object>> Values)> updates);
}
