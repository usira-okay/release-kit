# Contract: IGoogleSheetService

**Feature**: 001-redis-sheet-sync
**Layer**: Domain / Abstractions

## 介面定義

### IGoogleSheetService

Google Sheet 資料存取服務介面，定義於 Domain 層，由 Infrastructure 層實作。

#### 方法

| 方法 | 回傳 | 說明 |
|------|------|------|
| `GetSheetIdByNameAsync(spreadsheetId, sheetName)` | `Task<int?>` | 透過工作表名稱取得 SheetId，找不到回傳 null |
| `GetSheetDataAsync(spreadsheetId, range)` | `Task<IList<IList<object>>?>` | 讀取指定範圍的儲存格資料 |
| `InsertRowsAsync(spreadsheetId, sheetId, rowIndex, count)` | `Task` | 在指定位置批次插入空白列 |
| `UpdateCellsAsync(spreadsheetId, range, values)` | `Task` | 批次更新指定範圍的儲存格值（純文字） |
| `UpdateCellWithHyperlinkAsync(spreadsheetId, sheetId, rowIndex, columnIndex, displayText, url)` | `Task` | 更新單一儲存格並設定超連結 |
| `SortRangeAsync(spreadsheetId, sheetId, startRowIndex, endRowIndex, sortSpecs)` | `Task` | 對指定範圍依指定欄位排序 |
| `BatchUpdateCellsAsync(spreadsheetId, updates)` | `Task` | 批次更新多個儲存格範圍 |

#### 設計考量

- **介面放置於 Domain 層**：遵循 DDD 依賴反轉原則，Application 層可直接使用此介面
- **方法粒度**：每個方法對應一種 Google Sheets API 操作，保持單一職責
- **批次操作**：`InsertRowsAsync` 與 `BatchUpdateCellsAsync` 支援批次操作，減少 API 呼叫次數
- **超連結分離**：`UpdateCellWithHyperlinkAsync` 獨立於一般更新，因超連結需使用不同的 API 端點（FormulaValue vs StringValue）

#### 排序規格

`SortRangeAsync` 的 `sortSpecs` 參數：

| 欄位 | 類型 | 說明 |
|------|------|------|
| ColumnIndex | int | 0-based 欄位索引 |
| SortOrder | Ascending/Descending | 排序方向 |

## 依賴關係

```text
Domain (IGoogleSheetService)
    ↑
Application (UpdateGoogleSheetsTask 使用介面)
    ↑
Infrastructure (GoogleSheetService 實作介面)
    ↑
Console (DI 註冊)
```
