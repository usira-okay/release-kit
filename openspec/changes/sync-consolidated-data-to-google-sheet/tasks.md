## 1. 修正 ConsolidateReleaseDataTask 錯誤處理

- [ ] 1.1 修改 `ConsolidateReleaseDataTask.LoadPullRequestsAsync()`：將缺少 PR 資料時的 `throw new InvalidOperationException` 改為 `_logger.LogWarning(...)` 並回傳 `null`
- [ ] 1.2 修改 `ConsolidateReleaseDataTask.LoadUserStoriesAsync()`：將缺少 Work Item 資料時的 `throw new InvalidOperationException` 改為 `_logger.LogWarning(...)` 並回傳 `null`
- [ ] 1.3 修改 `ConsolidateReleaseDataTask.ExecuteAsync()`：在取得 PR/Work Item 資料後檢查是否為 `null`，若是則記錄 Warning 並 `return`
- [ ] 1.4 撰寫 `ConsolidateReleaseDataTask` 無資料優雅結束的單元測試（缺少 PR 資料、缺少 Work Item 資料兩個場景）

## 2. 新增 Google Sheets NuGet 套件與介面定義

- [ ] 2.1 在 `ReleaseKit.Infrastructure.csproj` 新增 `Google.Apis.Sheets.v4` NuGet 套件
- [ ] 2.2 在 `ReleaseKit.Domain/Abstractions/` 新增 `IGoogleSheetService` 介面，定義 `GetSheetDataAsync`、`InsertRowAsync`、`UpdateCellsAsync` 方法
- [ ] 2.3 撰寫 `IGoogleSheetService` 介面的單元測試（驗證 Mock 可正確注入與呼叫）

## 3. 實作 Google Sheets 基礎設施層

- [ ] 3.1 在 `ReleaseKit.Infrastructure/GoogleSheets/` 實作 `GoogleSheetService`，使用 `Google.Apis.Sheets.v4` 套件與 Service Account 認證
- [ ] 3.2 實作 `GetSheetDataAsync`：讀取指定 spreadsheetId、sheetName、range（A:Z）的所有資料
- [ ] 3.3 實作 `InsertRowAsync`：在指定 sheetId 的指定 rowIndex 位置插入空白列
- [ ] 3.4 實作 `UpdateCellsAsync`：批次更新多個儲存格值（支援文字與 HYPERLINK 公式）
- [ ] 3.5 撰寫 `GoogleSheetService` 各方法的單元測試（使用 Mock 驗證 API 呼叫）

## 4. 實作 UpdateGoogleSheetsTask 核心邏輯

- [ ] 4.1 重寫 `UpdateGoogleSheetsTask` 建構式，注入 `IRedisService`、`IGoogleSheetService`、`IOptions<GoogleSheetOptions>`、`ILogger`
- [ ] 4.2 實作從 Redis 讀取 `ReleaseData:Consolidated` 資料，無資料時記錄 Warning 並結束
- [ ] 4.3 實作 ColumnMapping 驗證邏輯：所有欄位設定值必須為 A-Z 單字母，超過 Z 時拋出 `InvalidOperationException`
- [ ] 4.4 實作讀取 Google Sheet A:Z 範圍資料，失敗時記錄 Warning 並結束
- [ ] 4.5 實作解析 RepositoryNameColumn 取得所有非空值的 row index 與對應 ProjectName
- [ ] 4.6 實作解析 UniqueKeyColumn 取得所有既有 UK 值與 row index 的映射
- [ ] 4.7 撰寫步驟 4.2 ~ 4.6 的單元測試

## 5. 實作新增資料邏輯

- [ ] 5.1 實作判斷 FEATURE_DATA 的 UK 是否需要新增（不在 UniqueKeyColumn 既有值中）
- [ ] 5.2 實作新增列定位邏輯：根據 PROJECT_NAME 在 RepositoryNameColumn 找到對應區塊，計算插入位置（第一個 Project 往後插入，其餘 Project 往前插入）
- [ ] 5.3 實作呼叫 `InsertRowAsync` 插入空白列
- [ ] 5.4 實作填入欄位值：FeatureColumn（含 HYPERLINK）、TeamColumn、AuthorsColumn（排序+換行）、PullRequestUrlsColumn（排序+換行）、UniqueKeyColumn、AutoSyncColumn（固定 TRUE）
- [ ] 5.5 撰寫新增資料邏輯的單元測試（第一個 Project 往後插入、中間 Project 往前插入、最後 Project 區塊插入）

## 6. 實作更新資料邏輯

- [ ] 6.1 實作判斷 FEATURE_DATA 的 UK 是否需要更新（存在於 UniqueKeyColumn 既有值中）
- [ ] 6.2 實作僅更新 AuthorsColumn 與 PullRequestUrlsColumn（排序+換行）
- [ ] 6.3 撰寫更新資料邏輯的單元測試（驗證僅更新 Authors 與 PullRequestUrls，其他欄位不變）

## 7. 實作 Project 區塊內排序

- [ ] 7.1 實作讀取 Project 區塊範圍內的資料（RepositoryNameColumn 標記列之間的資料列）
- [ ] 7.2 實作排序邏輯：依 TeamColumn → AuthorsColumn → FeatureColumn → UniqueKeyColumn 升冪排序，空白值排最後
- [ ] 7.3 實作將排序後的資料寫回 Google Sheet
- [ ] 7.4 撰寫排序邏輯的單元測試（驗證多欄位排序與空白排最後）

## 8. DI 註冊與整合

- [ ] 8.1 在 `ServiceCollectionExtensions` 註冊 `IGoogleSheetService` → `GoogleSheetService`
- [ ] 8.2 更新 `UpdateGoogleSheetsTask` 的 DI 註冊（已存在但需確認依賴注入正確）
- [ ] 8.3 執行完整建置驗證（`dotnet build src/release-kit.sln`）
- [ ] 8.4 執行完整單元測試驗證（`dotnet test src/release-kit.sln`）
