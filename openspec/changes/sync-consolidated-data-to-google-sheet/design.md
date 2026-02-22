## Context

目前 `UpdateGoogleSheetsTask` 為未實作的 stub（拋出 `NotImplementedException`），Google Sheets 基礎設施層僅有空目錄（`.gitkeep`），尚無 NuGet 套件依賴。`ConsolidateReleaseDataTask` 在缺少前置 PR/Work Item 資料時會拋出 `InvalidOperationException`，導致整個流程中斷。

需求是將 Redis 中 `ReleaseData:Consolidated` 的整合資料同步至 Google Sheet，支援新增與更新，並在每個 Project 區塊內排序。

**現有基礎：**
- `GoogleSheetOptions` / `ColumnMappingOptions` 設定類別已存在於 `ReleaseKit.Infrastructure.Configuration`
- `ConsolidatedReleaseResult` / `ConsolidatedReleaseEntry` 資料模型已存在
- `ITask` 介面與 `TaskFactory` 任務工廠已建立
- Redis 存取透過 `IRedisService` 的 `HashGetAsync` 操作

## Goals / Non-Goals

**Goals:**
- 實作完整的 Google Sheet 同步流程（讀取、新增列、更新儲存格、排序）
- 修正 `ConsolidateReleaseDataTask` 為無資料時優雅結束
- 建立 `IGoogleSheetService` 抽象介面，確保可測試性
- 支援設定驅動的欄位映射，所有欄位位置透過 `ColumnMapping` 配置
- 欄位範圍限制在 A-Z（單字母欄位），超過 Z 拋錯

**Non-Goals:**
- 不支援多字母欄位（如 AA、AB）
- 不支援從 Google Sheet 反向同步回 Redis
- 不處理 Google Sheet 的權限管理或建立新 Sheet
- 不支援批次刪除 Google Sheet 中的過時資料
- 不實作重試機制（由 Google API 客戶端內建處理）

## Decisions

### 1. Google Sheets API 套件選擇

**決定**: 使用 `Google.Apis.Sheets.v4` 官方 NuGet 套件

**理由**: Google 官方維護，API 穩定且文件完整，支援 Service Account 認證。

**替代方案**: 使用第三方套件如 `GoogleSheetsHelper` — 抽象層太厚，彈性不足，無法精確控制 Insert Row 與 Hyperlink 等操作。

### 2. 介面抽象層設計

**決定**: 在 Domain 層定義 `IGoogleSheetService` 介面，Infrastructure 層實作 `GoogleSheetService`

**理由**: 遵循 DDD 依賴反轉原則，Application 層的 `UpdateGoogleSheetsTask` 透過介面依賴，測試時可 Mock。

**介面方法設計：**
- `GetSheetDataAsync(spreadsheetId, sheetName, range)` — 讀取指定範圍資料
- `InsertRowAsync(spreadsheetId, sheetId, rowIndex)` — 在指定位置插入空白列
- `UpdateCellsAsync(spreadsheetId, sheetName, updates)` — 批次更新儲存格
- `SortRangeAsync(spreadsheetId, sheetId, range, sortSpecs)` — 對指定範圍排序

### 3. 新增列定位策略

**決定**: 

- 第一個 Project（如 repo1）：在該 RepositoryNameColumn 行的**下一行**插入（往後）
- 非第一個 Project（如 repo3）：在該 Project 區間的**第一個空行**或區間末尾前插入（往前），即在下一個 RepositoryNameColumn 前一行插入

**理由**: 確保 RepositoryNameColumn 的分組標記行不被覆蓋，新資料插在正確的 Project 區塊內。

### 4. UniqueKey 作為比對鍵

**決定**: 使用 `$"{workItemId}{dictionaryKey}"` 作為 UniqueKey 寫入 UniqueKeyColumn，並用於判斷新增或更新

**理由**: WorkItemId 加上 Dictionary Key（ProjectName）的組合可唯一識別每筆記錄，避免跨 Project 重複。

### 5. 超連結格式

**決定**: FeatureColumn 使用 Google Sheets 的 `=HYPERLINK("url", "display_text")` 公式

**理由**: Google Sheets API 的 `UpdateCells` 支援設定超連結格式，透過公式方式最為簡單可靠。

### 6. 排序實作方式

**決定**: 在所有新增/更新操作完成後，使用程式邏輯重新排序每個 Project 區塊的資料列（不使用 Google Sheets API 的 SortRange）

**理由**: Google Sheets API 的 `SortRange` 不支援「空白排最後」的自訂排序邏輯。需要在程式端讀取資料、排序後重新寫回。排序依據：TeamColumn → Authors → FeatureColumn → UniqueKey，空白值排最後。

### 7. ConsolidateReleaseDataTask 錯誤處理修正

**決定**: 將缺少 PR/Work Item 資料的 `throw new InvalidOperationException` 改為 `_logger.LogWarning(...)` + `return`

**理由**: 遵循需求「不要拋錯並結束程式」，讓程式在無前置資料時優雅結束而不中斷後續流程。

## Risks / Trade-offs

**[Google API 配額限制]** → 使用批次更新（BatchUpdate）減少 API 呼叫次數，單次操作盡量合併多個儲存格更新

**[並發存取衝突]** → 此為 CLI 工具手動執行，不預期並發情境；若未來需支援並發，可加入樂觀鎖機制

**[大量資料效能]** → 排序邏輯需讀取→排序→寫回完整區塊；若資料量極大可能較慢。目前 Release Notes 資料量預期在合理範圍內（數百行以內）

**[欄位映射錯誤]** → 所有欄位字母在啟動時驗證是否在 A-Z 範圍內，超出則拋 `InvalidOperationException` 中斷程式
