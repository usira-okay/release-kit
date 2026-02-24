# Tasks: Redis → Google Sheet 批次同步

**Input**: Design documents from `/specs/001-redis-sheet-sync/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅

**Tests**: 依 Constitution I. TDD 原則，所有功能實作 MUST 遵循 Red-Green-Refactor 循環，因此包含測試任務。

**Organization**: 任務按 User Story 優先級組織（P1 → P2 → P3），每個 Story 可獨立實作與測試。

## Format: `[ID] [P?] [Story] Description`

- **[P]**: 可平行執行（不同檔案、無相依性）
- **[Story]**: 所屬 User Story（US1, US2, US3）
- 包含完整檔案路徑

---

## Phase 1: Setup（共享基礎設施）

**Purpose**: 安裝相依套件、建立核心介面

- [X] T001 新增 Google.Apis.Sheets.v4 與 Google.Apis.Auth NuGet 套件至 `src/ReleaseKit.Infrastructure/ReleaseKit.Infrastructure.csproj`
- [X] T002 建立 `IGoogleSheetService` 介面於 `src/ReleaseKit.Domain/Abstractions/IGoogleSheetService.cs`，包含 `GetSheetIdByNameAsync`、`GetSheetDataAsync`、`InsertRowsAsync`、`UpdateCellsAsync`、`UpdateCellWithHyperlinkAsync`、`SortRangeAsync`、`BatchUpdateCellsAsync` 七個方法，依 `contracts/IGoogleSheetService.md` 定義的簽章實作
- [X] T003 驗證建置通過：執行 `dotnet build src/release-kit.sln` 確認無編譯錯誤

---

## Phase 2: Foundational（阻塞性前置任務）

**Purpose**: 實作 Google Sheets API 基礎服務，所有 User Story 均依賴此階段完成

**⚠️ CRITICAL**: 此階段完成前不可開始任何 User Story 任務

### Tests

- [X] T004 撰寫 `GoogleSheetService` 單元測試於 `tests/ReleaseKit.Infrastructure.Tests/GoogleSheets/GoogleSheetServiceTests.cs`：測試 `GetSheetIdByNameAsync` 回傳正確 SheetId 及找不到時回傳 null、`GetSheetDataAsync` 回傳正確資料、`InsertRowsAsync` 呼叫正確 API 參數、`UpdateCellsAsync` 與 `BatchUpdateCellsAsync` 批次更新行為、`UpdateCellWithHyperlinkAsync` 使用 HYPERLINK 公式、`SortRangeAsync` 排序參數正確

### Implementation

- [X] T005 實作 `GoogleSheetService` 於 `src/ReleaseKit.Infrastructure/GoogleSheets/GoogleSheetService.cs`：使用 `ServiceAccountCredential` 認證、實作 `IGoogleSheetService` 全部七個方法。`GetSheetIdByNameAsync` 透過 `Spreadsheets.Get` 比對 SheetName 取得 SheetId；`InsertRowsAsync` 使用 `InsertDimensionRequest`；`UpdateCellWithHyperlinkAsync` 使用 `FormulaValue` 設定 `=HYPERLINK()`；`SortRangeAsync` 使用 `SortRangeRequest`
- [X] T006 於 `src/ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs` 註冊 `IGoogleSheetService` → `GoogleSheetService` 為 Singleton，並確認 `GoogleSheetOptions` 的 `Configure` 已含 `ColumnMapping` 子區段
- [X] T007 驗證建置與測試通過：執行 `dotnet build src/release-kit.sln && dotnet test src/release-kit.sln`

**Checkpoint**: 基礎設施就緒——可開始 User Story 實作 ✅ 可建置 / ✅ 測試通過

---

## Phase 3: User Story 1 — 批次同步整合資料至 Google Sheet (Priority: P1) 🎯 MVP

**Goal**: 從 Redis 讀取整合資料，批次新增或更新到 Google Sheet 對應專案區段，並對受影響區段重新排序

**Independent Test**: 準備 Redis 整合資料與含有部分專案資料的 Google Sheet，執行同步後驗證新增列位置正確、更新列欄位正確、排序正確

### Tests for User Story 1

> **遵循 TDD：先撰寫測試 → 確認失敗 → 再實作**

- [X] T008 [P] [US1] 撰寫測試：讀取 Redis 整合資料並反序列化為 `ConsolidatedReleaseResult`；驗證呼叫 `IRedisService.HashGetAsync(ReleaseDataHash, Consolidated)` 並使用 `JsonExtensions.ToTypedObject` 反序列化，於 `tests/ReleaseKit.Application.Tests/Tasks/UpdateGoogleSheetsTaskTests.cs`
- [X] T009 [P] [US1] 撰寫測試：從 Sheet RepositoryNameColumn 建立專案區段索引（`SheetProjectSegment` 列表）；驗證 HeaderRowIndex、DataStartRowIndex、DataEndRowIndex 正確計算，包含僅一個專案（資料範圍到 Sheet 末尾）與多個專案（相鄰 header 之間）情境，於 `tests/ReleaseKit.Application.Tests/Tasks/UpdateGoogleSheetsTaskTests.cs`
- [X] T010 [P] [US1] 撰寫測試：以 UniqueKey（`{workItemId}{projectName}`）比對 Sheet UniqueKeyColumn，分類為 Insert 或 Update；驗證新 UniqueKey 判定為 Insert、既有 UniqueKey 判定為 Update，於 `tests/ReleaseKit.Application.Tests/Tasks/UpdateGoogleSheetsTaskTests.cs`
- [X] T011 [P] [US1] 撰寫測試：批次插入空白列的位置計算；驗證首筆專案的新列插入在 headerRow 之後、中間區段專案的新列插入在 nextHeaderRow 之前、僅有表頭無資料的專案插入在 headerRow+1；驗證多筆同專案新增時位置不重疊；驗證從最後一列往前插入以避免 index 偏移，於 `tests/ReleaseKit.Application.Tests/Tasks/UpdateGoogleSheetsTaskTests.cs`
- [X] T012 [P] [US1] 撰寫測試：新增列資料填入格式；驗證 FeatureColumn 為 `VSTS{workItemId} - {title}` 含超連結、TeamColumn 為 teamDisplayName、AuthorsColumn 為依 authorName 排序後換行分隔、PullRequestUrlsColumn 為依 url 排序後換行分隔、UniqueKeyColumn 為 `{workItemId}{projectName}`、AutoSyncColumn 為 `TRUE`；驗證空 Authors/PullRequests 時欄位留空，於 `tests/ReleaseKit.Application.Tests/Tasks/UpdateGoogleSheetsTaskTests.cs`
- [X] T013 [P] [US1] 撰寫測試：更新既有列僅更新 AuthorsColumn 與 PullRequestUrlsColumn；驗證其他欄位不被修改、Authors 依 authorName 排序後換行分隔、PRUrls 依 url 排序後換行分隔，於 `tests/ReleaseKit.Application.Tests/Tasks/UpdateGoogleSheetsTaskTests.cs`
- [X] T014 [P] [US1] 撰寫測試：專案區段排序；驗證排序範圍為 headerRow+1 到 nextHeaderRow-1、排序規則依序為 TeamColumn → AuthorsColumn → FeatureColumn → UniqueKeyColumn、空白值排最後，於 `tests/ReleaseKit.Application.Tests/Tasks/UpdateGoogleSheetsTaskTests.cs`
- [X] T015 [US1] 撰寫測試：完整 ExecuteAsync 端對端流程；驗證執行順序為「讀取 Redis → 讀取 Sheet → 分類 → 批次插入空白列 → 填入新增資料 + 更新既有資料 → 排序」；驗證多專案情境各區段獨立處理，於 `tests/ReleaseKit.Application.Tests/Tasks/UpdateGoogleSheetsTaskTests.cs`

### Implementation for User Story 1

- [X] T016 [US1] 實作 `UpdateGoogleSheetsTask` 建構子注入 `IRedisService`、`IGoogleSheetService`、`IOptions<GoogleSheetOptions>`、`ILogger<UpdateGoogleSheetsTask>` 於 `src/ReleaseKit.Application/Tasks/UpdateGoogleSheetsTask.cs`，並更新 DI 註冊確保新增依賴可正確解析
- [X] T017 [US1] 實作 Redis 整合資料讀取：呼叫 `HashGetAsync(RedisKeys.ReleaseDataHash, RedisKeys.Fields.Consolidated)` 並以 `JsonExtensions.ToTypedObject<ConsolidatedReleaseResult>` 反序列化，於 `src/ReleaseKit.Application/Tasks/UpdateGoogleSheetsTask.cs`
- [X] T018 [US1] 實作 Sheet 資料讀取與專案區段索引建構：讀取 A:Z 範圍資料、解析 RepositoryNameColumn 建立 `SheetProjectSegment` 列表、解析 UniqueKeyColumn 建立既有 UniqueKey 集合，於 `src/ReleaseKit.Application/Tasks/UpdateGoogleSheetsTask.cs`
- [X] T019 [US1] 實作 Insert/Update 分類邏輯：遍歷 `ConsolidatedReleaseResult.Projects`，以 `{workItemId}{projectName}` 為 UniqueKey 比對 Sheet 既有資料，分類為 Insert 或 Update，於 `src/ReleaseKit.Application/Tasks/UpdateGoogleSheetsTask.cs`
- [X] T020 [US1] 實作批次空白列插入：按專案分組計算每筆 Insert 的插入位置，從最後一列往前執行 `InsertRowsAsync` 避免 index 偏移，於 `src/ReleaseKit.Application/Tasks/UpdateGoogleSheetsTask.cs`
- [X] T021 [US1] 實作新增列資料填入：使用 `UpdateCellWithHyperlinkAsync` 填入 FeatureColumn（`=HYPERLINK("workItemUrl","VSTS{id} - {title}")`）、使用 `BatchUpdateCellsAsync` 填入 TeamColumn、AuthorsColumn（排序+換行）、PullRequestUrlsColumn（排序+換行）、UniqueKeyColumn、AutoSyncColumn（`TRUE`），於 `src/ReleaseKit.Application/Tasks/UpdateGoogleSheetsTask.cs`
- [X] T022 [US1] 實作既有列更新：找到 UniqueKey 對應的 row index，僅更新 AuthorsColumn 與 PullRequestUrlsColumn，使用 `BatchUpdateCellsAsync` 批次更新，於 `src/ReleaseKit.Application/Tasks/UpdateGoogleSheetsTask.cs`
- [X] T023 [US1] 實作專案區段排序：對每個有新增或更新的專案，呼叫 `SortRangeAsync` 以 TeamColumn → AuthorsColumn → FeatureColumn → UniqueKeyColumn 排序，排序範圍為 headerRow+1 至 nextHeaderRow-1，於 `src/ReleaseKit.Application/Tasks/UpdateGoogleSheetsTask.cs`
- [X] T024 [US1] 驗證建置與測試通過：執行 `dotnet build src/release-kit.sln && dotnet test src/release-kit.sln`

**Checkpoint**: User Story 1 完成，可獨立執行 Redis → Sheet 同步 ✅ 可建置 / ✅ 測試通過

---

## Phase 4: User Story 2 — 優雅處理無資料情境 (Priority: P2)

**Goal**: 當 Redis 無整合資料或無先行資料時，程式正常結束不拋例外

**Independent Test**: 清空 Redis 整合資料後執行程式，確認程式正常結束無例外

### Tests for User Story 2

- [X] T025 [P] [US2] 撰寫測試：`UpdateGoogleSheetsTask` 當 Redis `ReleaseData:Consolidated` 不存在或為空字串時，`ExecuteAsync` 正常結束不拋例外、記錄 Information 日誌，於 `tests/ReleaseKit.Application.Tests/Tasks/UpdateGoogleSheetsTaskTests.cs`
- [X] T026 [P] [US2] 撰寫測試：`ConsolidateReleaseDataTask` 當 Bitbucket 與 GitLab PR 資料均不存在時，`ExecuteAsync` 正常結束不拋 `InvalidOperationException`、記錄 Information 日誌，於 `tests/ReleaseKit.Application.Tests/Tasks/ConsolidateReleaseDataTaskTests.cs`
- [X] T027 [P] [US2] 撰寫測試：`ConsolidateReleaseDataTask` 當 Azure DevOps Work Item 資料不存在時，`ExecuteAsync` 正常結束不拋 `InvalidOperationException`、記錄 Information 日誌，於 `tests/ReleaseKit.Application.Tests/Tasks/ConsolidateReleaseDataTaskTests.cs`

### Implementation for User Story 2

- [X] T028 [US2] 修正 `ConsolidateReleaseDataTask.ExecuteAsync`：將「Bitbucket 與 GitLab PR 資料均不存在時拋出 `InvalidOperationException`」改為記錄 `LogInformation("沒有 PR 資料可供整合")` 後 `return`，於 `src/ReleaseKit.Application/Tasks/ConsolidateReleaseDataTask.cs`
- [X] T029 [US2] 修正 `ConsolidateReleaseDataTask.ExecuteAsync`：將「Azure DevOps Work Item 資料不存在時拋出 `InvalidOperationException`」改為記錄 `LogInformation("沒有 Work Item 資料可供整合")` 後 `return`，於 `src/ReleaseKit.Application/Tasks/ConsolidateReleaseDataTask.cs`
- [X] T030 [US2] 實作 `UpdateGoogleSheetsTask.ExecuteAsync` 無資料早期結束：Redis 回傳 null 或空字串時記錄 `LogInformation("Redis 中沒有整合資料，結束同步")` 後 `return`，於 `src/ReleaseKit.Application/Tasks/UpdateGoogleSheetsTask.cs`
- [X] T031 [US2] 驗證建置與測試通過：執行 `dotnet build src/release-kit.sln && dotnet test src/release-kit.sln`

**Checkpoint**: User Story 2 完成，無資料情境不再拋例外 ✅ 可建置 / ✅ 測試通過

---

## Phase 5: User Story 3 — Google Sheet 連線驗證 (Priority: P3)

**Goal**: 同步前驗證 Google Sheet 可用性與欄位設定合法性，不合法時提早結束或拋錯

**Independent Test**: 提供無效 Sheet 設定或超過 Z 欄的設定，驗證系統的錯誤處理行為

### Tests for User Story 3

- [X] T032 [P] [US3] 撰寫測試：ColumnMappingOptions 欄位驗證，任何欄位值超過 `Z`（如 `AA`、`AB`）時拋出 `InvalidOperationException`；所有欄位均在 A–Z 範圍內時驗證通過，於 `tests/ReleaseKit.Application.Tests/Tasks/UpdateGoogleSheetsTaskTests.cs`
- [X] T033 [P] [US3] 撰寫測試：`GetSheetIdByNameAsync` 回傳 null（Sheet Name 找不到）時，`ExecuteAsync` 正常結束不繼續同步、記錄日誌，於 `tests/ReleaseKit.Application.Tests/Tasks/UpdateGoogleSheetsTaskTests.cs`
- [X] T034 [P] [US3] 撰寫測試：`GetSheetDataAsync` 回傳 null（無法讀取 Sheet 資料）時，`ExecuteAsync` 正常結束不繼續同步、記錄日誌，於 `tests/ReleaseKit.Application.Tests/Tasks/UpdateGoogleSheetsTaskTests.cs`

### Implementation for User Story 3

- [X] T035 [US3] 實作欄位範圍驗證：在 `UpdateGoogleSheetsTask.ExecuteAsync` 開頭驗證 `ColumnMappingOptions` 所有欄位均為單一字母 A–Z，任何欄位超出範圍時拋出 `InvalidOperationException` 含明確錯誤訊息（如「欄位 RepositoryNameColumn 的值 'AA' 超出 A–Z 範圍」），於 `src/ReleaseKit.Application/Tasks/UpdateGoogleSheetsTask.cs`
- [X] T036 [US3] 實作 Sheet 連線驗證：透過 `GetSheetIdByNameAsync` 動態取得 SheetId，回傳 null 時記錄 `LogWarning` 後 `return`；取得 SheetId 後讀取 A:Z 範圍資料，回傳 null 時記錄 `LogWarning` 後 `return`，於 `src/ReleaseKit.Application/Tasks/UpdateGoogleSheetsTask.cs`
- [X] T037 [US3] 驗證建置與測試通過：執行 `dotnet build src/release-kit.sln && dotnet test src/release-kit.sln`

**Checkpoint**: User Story 3 完成，同步前預先驗證設定與連線 ✅ 可建置 / ✅ 測試通過

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: 最終驗證、文件更新與程式碼品質確認

- [X] T038 [P] 確認所有公開類別與方法均有繁體中文 XML Summary 註解，於 `src/ReleaseKit.Domain/Abstractions/IGoogleSheetService.cs` 與 `src/ReleaseKit.Infrastructure/GoogleSheets/GoogleSheetService.cs` 與 `src/ReleaseKit.Application/Tasks/UpdateGoogleSheetsTask.cs`
- [X] T039 [P] 確認 `appsettings.json` 包含完整 `GoogleSheet` 區段設定（含 ColumnMapping 子區段），對照 `quickstart.md` 範例，於 `src/ReleaseKit.Console/appsettings.json`
- [X] T040 最終驗證：執行 `dotnet build src/release-kit.sln && dotnet test src/release-kit.sln` 確認全部建置成功且所有測試通過

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: 無相依性 — 可立即開始
- **Foundational (Phase 2)**: 相依於 Phase 1 完成 — **阻塞**所有 User Story
- **US1 (Phase 3)**: 相依於 Phase 2 完成
- **US2 (Phase 4)**: 相依於 Phase 2 完成（`ConsolidateReleaseDataTask` 修正不需 Phase 3）
- **US3 (Phase 5)**: 相依於 Phase 2 完成
- **Polish (Phase 6)**: 相依於所有 User Story 完成

### User Story Dependencies

- **US1 (P1)**: Phase 2 完成後可開始 — 不依賴其他 Story
- **US2 (P2)**: Phase 2 完成後可開始 — 不依賴其他 Story（`ConsolidateReleaseDataTask` 修正獨立於 US1）
- **US3 (P3)**: Phase 2 完成後可開始 — 不依賴其他 Story

> 💡 US1、US2、US3 可平行開發，但建議按優先序（P1 → P2 → P3）依序完成，因 US1 的 `ExecuteAsync` 結構會影響 US2 和 US3 的插入位置。

### Within Each User Story

- 測試 MUST 先撰寫並確認失敗，再開始實作
- 每個 Checkpoint 執行建置與測試驗證

### Parallel Opportunities

- Phase 1: T002 可與 T001 平行（不同檔案）
- Phase 2: T004 撰寫測試時可與 Phase 1 收尾平行
- Phase 3: T008–T014 所有測試任務可平行撰寫
- Phase 4: T025–T027 所有測試任務可平行撰寫
- Phase 5: T032–T034 所有測試任務可平行撰寫
- Phase 6: T038 與 T039 可平行

---

## Parallel Example: User Story 1 Tests

```bash
# 可同時啟動所有 US1 測試撰寫任務（不同測試案例、同一檔案但不衝突）：
T008: 測試 Redis 資料讀取與反序列化
T009: 測試專案區段索引建構
T010: 測試 UniqueKey Insert/Update 分類
T011: 測試批次列插入位置計算
T012: 測試新增列資料填入格式
T013: 測試既有列更新範圍
T014: 測試專案區段排序
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. 完成 Phase 1: Setup（T001–T003）
2. 完成 Phase 2: Foundational（T004–T007）
3. 完成 Phase 3: User Story 1（T008–T024）
4. **STOP and VALIDATE**: 獨立測試 US1 — 確認 Redis 資料可正確同步至 Sheet
5. 若可部署則發佈 MVP

### Incremental Delivery

1. Setup + Foundational → 基礎設施就緒
2. + User Story 1 → 核心同步功能（**MVP** 🎯）
3. + User Story 2 → 無資料情境優雅處理
4. + User Story 3 → 同步前驗證（完整功能）
5. + Polish → 文件與品質確認（正式發佈）

---

## Notes

- [P] 任務 = 不同檔案或無相依性，可平行
- [Story] 標籤對應 spec.md 的 User Story 編號
- 每個 User Story 完成後獨立可測試
- 遵循 TDD：先寫測試確認失敗，再實作
- 每個 Checkpoint 後執行 `dotnet build && dotnet test` 驗證
