# Research: Redis → Google Sheet 批次同步

**Feature**: 001-redis-sheet-sync
**Date**: 2026-02-24

## R-001: Google Sheets API 整合方式

**Decision**: 使用 `Google.Apis.Sheets.v4` NuGet 套件透過 Service Account 認證存取 Google Sheets

**Rationale**:
- Google.Apis.Sheets.v4 是 Google 官方 .NET SDK，長期維護且文件完整
- Service Account 認證適合無使用者互動的 Console 應用程式
- 專案已有 `ServiceAccountCredentialPath` 設定，符合既有架構
- 使用 `Google.Apis.Auth` 搭配 `ServiceAccountCredential` 進行認證

**Alternatives considered**:
- 直接呼叫 REST API：需自行處理認證與序列化，工作量大且不必要
- gRPC API：.NET SDK 已封裝，無需額外使用 gRPC

## R-002: 批次操作策略

**Decision**: 使用 Google Sheets `batchUpdate` API 進行批次列插入，使用 `values.batchUpdate` 進行批次儲存格更新

**Rationale**:
- `batchUpdate` 可在單次 API 呼叫中插入多列，減少 API 配額消耗
- `values.batchUpdate` 可在單次呼叫中更新多個儲存格範圍
- Google Sheets API 配額為每分鐘 60 次讀取/60 次寫入，批次操作可避免超出配額
- 執行順序：先批次插入所有空白列 → 再批次填入/更新資料

**Alternatives considered**:
- 逐列操作：每筆資料一次 API 呼叫，效率低且易超出配額
- Sheets API v3：已棄用，不建議使用

## R-003: Sheet ID 動態取得

**Decision**: 透過 Spreadsheets.Get API 取得所有工作表資訊，以 SheetName 比對取得 SheetId

**Rationale**:
- 規格要求透過 Sheet Name 動態取得 Sheet ID
- `Spreadsheets.Get` 回傳的 `Spreadsheet.Sheets` 包含每個工作表的 `SheetProperties`（含 SheetId 與 Title）
- 比對 `SheetProperties.Title` 與設定的 `SheetName` 即可取得 `SheetId`

**Alternatives considered**:
- 靜態設定 SheetId：不符合規格要求，且當工作表重建時 ID 會變更

## R-004: 列插入位置計算

**Decision**: 讀取 RepositoryNameColumn 全欄資料，建立專案區段索引表，再計算每筆新增資料的插入位置

**Rationale**:
- 需求區分「首筆之後」與「中間區段」兩種插入情境
- 讀取整欄資料後可一次性建立所有專案的 row index 映射
- 插入位置計算：
  - 找到專案名稱所在的 row（headerRow）
  - 找到下一個專案名稱的 row（nextHeaderRow）
  - 新列插入於 headerRow + 1（首筆之後）或 nextHeaderRow 之前（中間區段）
- 批次插入時需從最後一列往前插入，避免 row index 偏移

**Alternatives considered**:
- 逐筆插入後重新讀取 Sheet：API 呼叫次數倍增，效率極差

## R-005: 排序實作策略

**Decision**: 使用 Google Sheets `SortRange` batchUpdate 請求，在 Sheet 端直接排序

**Rationale**:
- Google Sheets API 的 `SortRangeRequest` 支援多欄排序，可直接指定排序欄位與方向
- 在 Sheet 端排序避免讀取→排序→回寫的額外 API 呼叫
- 排序範圍由專案區段的 headerRow 與 nextHeaderRow 決定

**Alternatives considered**:
- 讀取資料→本地排序→回寫：需額外 2 次 API 呼叫（讀取+寫入），且需處理超連結等格式保留

## R-006: 超連結處理

**Decision**: 使用 `UpdateCellsRequest` 搭配 `ExtendedValue.FormulaValue` 設定 `=HYPERLINK("url", "text")` 公式

**Rationale**:
- FeatureColumn 需要「VSTS{workItemId} - {title}」文字並帶有超連結
- Google Sheets 的 `HYPERLINK` 函數可同時設定連結與顯示文字
- 使用 FormulaValue 而非 StringValue 可保留超連結功能

**Alternatives considered**:
- 使用 TextFormatRun 設定連結：複雜度高且不直觀
- 僅填入文字不加連結：不符合規格要求

## R-007: ConsolidateReleaseDataTask 無資料處理修正

**Decision**: 將 `InvalidOperationException` 替換為 `return`（正常結束），並記錄 Information 等級的日誌

**Rationale**:
- 規格要求無先行資料時不拋錯，直接結束
- 使用 `ILogger.LogInformation` 記錄「無資料可整合」訊息，便於除錯
- 符合 Constitution V.（錯誤處理策略）——不使用 try-catch，讓程式正常流程處理

**Alternatives considered**:
- 拋出自訂例外再由上層攔截：違反 Constitution 禁止 try-catch 的原則
- 使用 Result Pattern 回傳：ITask.ExecuteAsync() 回傳 Task（void），修改介面影響範圍太大
