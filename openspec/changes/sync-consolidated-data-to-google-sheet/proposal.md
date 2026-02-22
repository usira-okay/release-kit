## Why

目前 `UpdateGoogleSheetsTask` 尚未實作（僅拋出 `NotImplementedException`），且 `ConsolidateReleaseDataTask` 在缺少前置資料時會拋出例外中斷整個流程。我們需要將 Redis 中已整合的 Release 資料（`ReleaseKit:ReleaseData:Consolidated`）批次同步到 Google Sheet，以便 PM 與團隊能透過 Google Sheet 追蹤 Release Notes。同時需修正前置任務的錯誤處理策略，改為無資料時優雅結束而非拋錯。

## What Changes

- 實作 `UpdateGoogleSheetsTask`：從 Redis 讀取 `ReleaseData:Consolidated` 資料，並批次新增/更新到 Google Sheet
- 新增 Google Sheets API 基礎設施層：建立 `IGoogleSheetService` 介面與實作，支援讀取、新增列、更新儲存格、排序等操作
- 修正 `ConsolidateReleaseDataTask` 錯誤處理：缺少前置資料時改為記錄警告並正常結束，不再拋出例外
- 新增 Google.Apis.Sheets.v4 NuGet 套件依賴
- 支援欄位映射配置驅動的動態欄位對應（透過 `GoogleSheet:ColumnMapping`）
- 新增資料時在正確位置插入列（依 RepositoryNameColumn 分組定位）
- 更新資料時僅更新 Authors 與 PullRequestUrls 欄位
- 每個 Project 區塊內依 TeamColumn、Authors、FeatureColumn、UniqueKey 排序（空白排最後）

## Capabilities

### New Capabilities

- `sync-to-google-sheet`: 將 Redis 整合資料批次同步至 Google Sheet，包含新增列、更新欄位、區塊內排序等完整流程

### Modified Capabilities

（無既有 spec 需要修改）

## Impact

- **程式碼**: `ConsolidateReleaseDataTask`（修正錯誤處理）、`UpdateGoogleSheetsTask`（全面重寫）、新增 `IGoogleSheetService` 與實作
- **依賴**: 新增 `Google.Apis.Sheets.v4` NuGet 套件至 `ReleaseKit.Infrastructure`
- **設定**: 需正確配置 `GoogleSheet` 區段（SpreadsheetId、SheetName、ServiceAccountCredentialPath、ColumnMapping）
- **DI**: 需在 `ServiceCollectionExtensions` 註冊 `IGoogleSheetService`
