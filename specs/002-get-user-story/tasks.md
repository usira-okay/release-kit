# Tasks: Azure Work Item User Story Resolution

**Input**: Design documents from `/specs/002-get-user-story/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: TDD approach - tests MUST be written FIRST and FAIL before implementation (Constitution requirement)

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

Based on plan.md, using Clean Architecture structure:
- **Common**: `src/ReleaseKit.Common/Constants/`
- **Domain**: `src/ReleaseKit.Domain/Abstractions/`, `src/ReleaseKit.Domain/Entities/`
- **Application**: `src/ReleaseKit.Application/Common/`, `src/ReleaseKit.Application/Tasks/`
- **Infrastructure**: `src/ReleaseKit.Infrastructure/AzureDevOps/Models/`
- **Console**: `src/ReleaseKit.Console/Parsers/`, `src/ReleaseKit.Console/Extensions/`
- **Tests**: `tests/ReleaseKit.Application.Tests/Tasks/`, `tests/ReleaseKit.Application.Tests/Common/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: 建立基礎常數與 enum，為所有 User Story 提供共用型別

- [ ] T001 [P] 建立 UserStoryResolutionStatus enum 在 src/ReleaseKit.Application/Common/UserStoryResolutionStatus.cs
- [ ] T002 [P] 建立 WorkItemTypeConstants 常數類別在 src/ReleaseKit.Common/Constants/WorkItemTypeConstants.cs
- [ ] T003 [P] 在 RedisKeys.cs 新增 AzureDevOpsUserStoryWorkItems 常數於 src/ReleaseKit.Common/Constants/RedisKeys.cs
- [ ] T004 [P] 在 TaskType.cs 新增 GetUserStory enum 值於 src/ReleaseKit.Application/Tasks/TaskType.cs

**Checkpoint**: 基礎型別與常數已建立，可開始建立資料模型

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: 建立核心資料模型與基礎設施擴充，MUST 完成後才能開始 User Story 實作

**⚠️ CRITICAL**: 所有 User Story 工作都依賴此階段完成

- [ ] T005 [P] 建立 UserStoryWorkItemOutput DTO 在 src/ReleaseKit.Application/Common/UserStoryWorkItemOutput.cs
- [ ] T006 [P] 建立 UserStoryFetchResult DTO 在 src/ReleaseKit.Application/Common/UserStoryFetchResult.cs
- [ ] T007 [P] 擴充 AzureDevOpsWorkItemResponse 新增 Relations 欄位於 src/ReleaseKit.Infrastructure/AzureDevOps/Models/AzureDevOpsWorkItemResponse.cs
- [ ] T008 [P] 建立 AzureDevOpsRelationResponse model 在 src/ReleaseKit.Infrastructure/AzureDevOps/Models/AzureDevOpsRelationResponse.cs
- [ ] T009 在 CommandLineParser.cs 新增 get-user-story 命令對應於 src/ReleaseKit.Console/Parsers/CommandLineParser.cs

**Checkpoint**: 資料模型與基礎設施已就緒，User Story 實作可以開始

---

## Phase 3: User Story 1 - 解析並轉換 Work Item 至 User Story 層級 (Priority: P1) 🎯 MVP

**Goal**: 實作核心遞迴查詢功能，將 Redis 中所有 Work Item 轉換為 User Story 層級並儲存

**Independent Test**: 執行 `dotnet run -- get-user-story` 命令，驗證 Redis 中新增 `AzureDevOps:WorkItems:UserStories` Key，包含轉換後的資料，且原始資料被正確保留

### Tests for User Story 1 (TDD - MUST write FIRST) ⚠️

> **CRITICAL: 所有測試 MUST 先撰寫並確認 FAIL，然後才能開始實作**

- [ ] T010 [P] [US1] 建立 GetUserStoryTaskTests 測試類別在 tests/ReleaseKit.Application.Tests/Tasks/GetUserStoryTaskTests.cs
- [ ] T011 [P] [US1] 撰寫測試：從 Redis 讀取 Work Item 資料（空資料情境）於 GetUserStoryTaskTests.cs
- [ ] T012 [P] [US1] 撰寫測試：原始 Work Item 已是 User Story 層級（AlreadyUserStoryOrAbove）於 GetUserStoryTaskTests.cs
- [ ] T013 [P] [US1] 撰寫測試：透過 1 層 Parent 找到 User Story（FoundViaRecursion）於 GetUserStoryTaskTests.cs
- [ ] T014 [P] [US1] 撰寫測試：透過 2 層 Parent 找到 User Story（遞迴）於 GetUserStoryTaskTests.cs
- [ ] T015 [P] [US1] 撰寫測試：Work Item 無 Parent（NotFound）於 GetUserStoryTaskTests.cs
- [ ] T016 [P] [US1] 撰寫測試：將結果寫入 Redis 新 Key 於 GetUserStoryTaskTests.cs

**驗證**: 執行 `dotnet test tests/ReleaseKit.Application.Tests/Tasks/GetUserStoryTaskTests.cs`，確認所有測試 FAIL

### Implementation for User Story 1

- [ ] T017 [US1] 建立 GetUserStoryTask 類別骨架在 src/ReleaseKit.Application/Tasks/GetUserStoryTask.cs（繼承 ITask 介面）
- [ ] T018 [US1] 實作 GetUserStoryTask 建構子（注入 IAzureDevOpsRepository, IRedisService, ILogger, IConfiguration）於 GetUserStoryTask.cs
- [ ] T019 [US1] 實作從 Redis 讀取原始 Work Item 的方法於 GetUserStoryTask.cs
- [ ] T020 [US1] 實作判斷 Work Item 是否為 User Story 層級的邏輯（使用 WorkItemTypeConstants）於 GetUserStoryTask.cs
- [ ] T021 [US1] 實作從 AzureDevOpsWorkItemResponse 解析 Parent ID 的私有方法於 GetUserStoryTask.cs
- [ ] T022 [US1] 實作遞迴查詢 Parent Work Item 的核心方法於 GetUserStoryTask.cs（不含循環偵測與深度限制）
- [ ] T023 [US1] 實作將 WorkItem entity 轉換為 UserStoryWorkItemOutput 的 mapper 方法於 GetUserStoryTask.cs
- [ ] T024 [US1] 實作將結果彙整為 UserStoryFetchResult 的方法於 GetUserStoryTask.cs
- [ ] T025 [US1] 實作將 UserStoryFetchResult 序列化並寫入 Redis 的方法於 GetUserStoryTask.cs
- [ ] T026 [US1] 實作 ExecuteAsync 方法組合所有流程於 GetUserStoryTask.cs
- [ ] T027 [US1] 在 TaskFactory.cs 新增 GetUserStoryTask 建立邏輯於 src/ReleaseKit.Application/Tasks/TaskFactory.cs
- [ ] T028 [US1] 在 ServiceCollectionExtensions.cs 註冊 GetUserStoryTask 於 src/ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs

**驗證**: 執行所有 US1 測試，確認 T011-T016 測試全部 PASS

**Checkpoint**: User Story 1 核心功能完成，可執行命令並成功轉換 Work Item

---

## Phase 4: User Story 2 - 處理無法取得資訊的 Work Item (Priority: P2)

**Goal**: 強化錯誤處理，確保 API 失敗時能夠保留失敗記錄並繼續處理其他 Work Item

**Independent Test**: 模擬 Azure DevOps API 回傳 404/401 錯誤，驗證系統正確記錄失敗狀態（isSuccess: false, errorMessage 有值）並繼續處理其他資料

### Tests for User Story 2 (TDD - MUST write FIRST) ⚠️

- [ ] T029 [P] [US2] 撰寫測試：原始 Work Item API 呼叫失敗（OriginalFetchFailed）於 GetUserStoryTaskTests.cs
- [ ] T030 [P] [US2] 撰寫測試：Parent Work Item API 呼叫失敗（NotFound with error）於 GetUserStoryTaskTests.cs
- [ ] T031 [P] [US2] 撰寫測試：部分 Work Item 失敗但其他繼續處理（批次韌性）於 GetUserStoryTaskTests.cs
- [ ] T032 [P] [US2] 撰寫測試：Parent Work Item 部分欄位為 null 時正常處理於 GetUserStoryTaskTests.cs

**驗證**: 執行 `dotnet test`，確認 T029-T032 測試 FAIL

### Implementation for User Story 2

- [ ] T033 [US2] 修改遞迴查詢方法處理 API Result.Failure 情境於 GetUserStoryTask.cs
- [ ] T034 [US2] 實作原始 Work Item 無法取得時建立 OriginalFetchFailed 結果於 GetUserStoryTask.cs
- [ ] T035 [US2] 實作 Parent API 失敗時建立 NotFound 結果（保留原始 Work Item）於 GetUserStoryTask.cs
- [ ] T036 [US2] 實作錯誤訊息對應邏輯（從 Error object 提取訊息）於 GetUserStoryTask.cs
- [ ] T037 [US2] 新增批次處理的錯誤隔離邏輯（單筆失敗不中斷整體）於 GetUserStoryTask.cs

**驗證**: 執行所有 US2 測試，確認 T029-T032 測試全部 PASS

**Checkpoint**: User Story 2 完成，系統具備完整錯誤處理能力，US1 與 US2 可獨立運作

---

## Phase 5: User Story 3 - 避免無限遞迴與循環參照 (Priority: P3)

**Goal**: 實作循環偵測與遞迴深度限制，確保系統在異常資料情況下安全停止

**Independent Test**: 建立測試情境模擬循環參照（A → B → A）與超深遞迴（>10 層），驗證系統正確偵測並停止，設定 resolutionStatus 為 NotFound

### Tests for User Story 3 (TDD - MUST write FIRST) ⚠️

- [ ] T038 [P] [US3] 撰寫測試：偵測循環參照（A → B → A）於 GetUserStoryTaskTests.cs
- [ ] T039 [P] [US3] 撰寫測試：達到最大遞迴深度時停止（預設 10 層）於 GetUserStoryTaskTests.cs
- [ ] T040 [P] [US3] 撰寫測試：從 appsettings.json 讀取自訂最大深度於 GetUserStoryTaskTests.cs
- [ ] T041 [P] [US3] 撰寫測試：循環參照的錯誤訊息包含 "偵測到循環參照" 於 GetUserStoryTaskTests.cs
- [ ] T042 [P] [US3] 撰寫測試：超深度的錯誤訊息包含 "超過最大遞迴深度" 於 GetUserStoryTaskTests.cs

**驗證**: 執行 `dotnet test`，確認 T038-T042 測試 FAIL

### Implementation for User Story 3

- [ ] T043 [US3] 在 GetUserStoryTask 建構子新增從 IConfiguration 讀取 GetUserStory:MaxDepth 設定（預設 10）於 GetUserStoryTask.cs
- [ ] T044 [US3] 修改遞迴方法簽章加入 visited HashSet<int> 與 depth int 參數於 GetUserStoryTask.cs
- [ ] T045 [US3] 實作遞迴方法開頭檢查 depth 是否超過最大值於 GetUserStoryTask.cs
- [ ] T046 [US3] 實作遞迴方法開頭檢查 Work Item ID 是否已在 visited 集合中於 GetUserStoryTask.cs
- [ ] T047 [US3] 實作偵測到循環參照時回傳 NotFound 結果（errorMessage: "偵測到循環參照"）於 GetUserStoryTask.cs
- [ ] T048 [US3] 實作達到最大深度時回傳 NotFound 結果（errorMessage: "超過最大遞迴深度"）於 GetUserStoryTask.cs
- [ ] T049 [US3] 修改遞迴呼叫將當前 Work Item ID 加入 visited 並傳遞至下層於 GetUserStoryTask.cs

**驗證**: 執行所有 US3 測試，確認 T038-T042 測試全部 PASS

**Checkpoint**: User Story 3 完成，系統具備完整的安全防護機制，所有 User Stories 可獨立運作

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: 改善跨 User Story 的品質與使用者體驗

- [ ] T050 [P] 新增 GetUserStoryTask 的 XML 繁體中文註解（summary, remarks）於 GetUserStoryTask.cs
- [ ] T051 [P] 新增 UserStoryResolutionStatus enum 各值的繁體中文註解於 UserStoryResolutionStatus.cs
- [ ] T052 [P] 新增 WorkItemTypeConstants 的繁體中文註解於 WorkItemTypeConstants.cs
- [ ] T053 [P] 新增 UserStoryWorkItemOutput 與 UserStoryFetchResult 的繁體中文註解於對應檔案
- [ ] T054 在 GetUserStoryTask 加入進度日誌（10%, 50%, 100%）於 GetUserStoryTask.cs
- [ ] T055 在 GetUserStoryTask 加入統計資訊日誌（alreadyUserStoryCount, foundViaRecursionCount 等）於 GetUserStoryTask.cs
- [ ] T056 新增 WorkItemTypeConstantsTests 單元測試驗證 IsUserStoryLevel 方法於 tests/ReleaseKit.Application.Tests/Common/WorkItemTypeConstantsTests.cs
- [ ] T057 執行完整建置驗證：`dotnet build src/release-kit.sln`
- [ ] T058 執行完整測試驗證：`dotnet test tests/ReleaseKit.Application.Tests`
- [ ] T059 執行 quickstart.md 驗證流程（手動測試）

**Checkpoint**: 功能完整，程式碼品質符合 Constitution 規範

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: 無依賴 - 可立即開始
- **Foundational (Phase 2)**: 依賴 Setup 完成 - BLOCKS 所有 User Stories
- **User Stories (Phase 3-5)**: 全部依賴 Foundational 完成
  - User Story 1 (P1): 可在 Foundational 後開始 - 無其他 User Story 依賴
  - User Story 2 (P2): 可在 Foundational 後開始 - 擴充 US1 但獨立可測
  - User Story 3 (P3): 可在 Foundational 後開始 - 擴充 US1 但獨立可測
- **Polish (Phase 6)**: 依賴所有 User Stories 完成

### User Story Dependencies

- **User Story 1 (P1)**: 可在 Foundational (Phase 2) 後開始 - 核心功能，無其他 Story 依賴
- **User Story 2 (P2)**: 可在 Foundational (Phase 2) 後開始 - 修改 US1 的 GetUserStoryTask.cs，但邏輯獨立可測
- **User Story 3 (P3)**: 可在 Foundational (Phase 2) 後開始 - 修改 US1 的遞迴方法，但邏輯獨立可測

**注意**: US2 與 US3 都修改 GetUserStoryTask.cs，建議依優先順序序列執行（P1 → P2 → P3），或由同一開發者負責以避免合併衝突

### Within Each User Story

- **TDD 必須遵循**: Tests FIRST → Ensure FAIL → Implementation → Ensure PASS
- Models before services
- Core implementation before error handling
- Story complete before moving to next priority

### Parallel Opportunities

- **Phase 1 (Setup)**: T001, T002, T003, T004 可同時執行（不同檔案）
- **Phase 2 (Foundational)**: T005, T006, T007, T008, T009 可同時執行（不同檔案）
- **Phase 3 (US1 Tests)**: T010-T016 可同時撰寫（同一測試類別，不同測試方法）
- **Phase 4 (US2 Tests)**: T029-T032 可同時撰寫
- **Phase 5 (US3 Tests)**: T038-T042 可同時撰寫
- **Phase 6 (Polish)**: T050-T056 可同時執行（不同檔案）

**團隊策略**: 若 US2 與 US3 由不同開發者負責，建議在 US1 完成後再平行開始，或使用 feature branch 獨立開發後整合

---

## Parallel Example: User Story 1 Tests

```bash
# 同時撰寫 User Story 1 的所有測試（不同測試方法）:
Task T011: "從 Redis 讀取 Work Item 資料（空資料情境）測試"
Task T012: "AlreadyUserStoryOrAbove 情境測試"
Task T013: "FoundViaRecursion (1層) 情境測試"
Task T014: "FoundViaRecursion (2層) 情境測試"
Task T015: "NotFound 情境測試"
Task T016: "Redis 寫入結果測試"

# 確認所有測試 FAIL 後，再開始實作
```

## Parallel Example: Foundational Phase

```bash
# 同時建立所有資料模型（不同檔案）:
Task T005: "UserStoryWorkItemOutput.cs"
Task T006: "UserStoryFetchResult.cs"
Task T007: "AzureDevOpsWorkItemResponse.cs (修改)"
Task T008: "AzureDevOpsRelationResponse.cs (新增)"
Task T009: "CommandLineParser.cs (修改)"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. 完成 Phase 1: Setup
2. 完成 Phase 2: Foundational (CRITICAL - blocks all stories)
3. 完成 Phase 3: User Story 1（含 TDD 測試）
4. **STOP and VALIDATE**: 
   - 執行 `dotnet test tests/ReleaseKit.Application.Tests/Tasks/GetUserStoryTaskTests.cs`
   - 執行 `dotnet run -- get-user-story`
   - 檢查 Redis Key `AzureDevOps:WorkItems:UserStories` 是否正確產生
5. MVP 可交付使用

### Incremental Delivery

1. **Setup + Foundational** → 基礎完成
2. **+ User Story 1** → 核心功能可用（MVP）
3. **+ User Story 2** → 錯誤處理完善
4. **+ User Story 3** → 安全防護完整
5. **+ Polish** → 生產就緒

### Parallel Team Strategy

若有多位開發者：

1. 團隊一起完成 Setup + Foundational
2. Foundational 完成後：
   - **Developer A**: User Story 1（核心實作）
   - **Developer B**: 準備 User Story 2 測試（可先寫測試）
   - **Developer C**: 準備 User Story 3 測試（可先寫測試）
3. US1 完成後：
   - **Developer A**: Polish 工作
   - **Developer B**: 實作 US2（基於 US1 的 GetUserStoryTask.cs）
   - **Developer C**: 實作 US3（等 US2 完成或協調分支）

**建議**: 因 US2 與 US3 都修改同一檔案，建議序列執行或由同一開發者負責

---

## Build & Test Validation

### 建置驗證

```bash
cd /home/ari/SourceCode/0-workspace/01-release-kit
dotnet build src/release-kit.sln
```

**預期**: 建置成功，無錯誤

### 測試驗證

```bash
# 單元測試
dotnet test tests/ReleaseKit.Application.Tests/Tasks/GetUserStoryTaskTests.cs

# 完整測試
dotnet test tests/ReleaseKit.Application.Tests

# 特定 User Story 測試
dotnet test --filter "FullyQualifiedName~GetUserStoryTaskTests"
```

**預期**: 所有測試 PASS

### 整合測試（手動）

```bash
# 1. 確認 Redis 有資料
redis-cli GET AzureDevOps:WorkItems

# 2. 執行轉換
cd src/ReleaseKit.Console
dotnet run -- get-user-story

# 3. 檢查結果
redis-cli GET AzureDevOps:WorkItems:UserStories
```

**預期**: 
- 顯示處理進度（10%, 50%, 100%）
- 顯示統計資訊（原本就是 User Story: X 筆，透過遞迴找到: Y 筆...）
- Redis 新 Key 包含轉換後的資料

---

## Notes

- **[P] 標記**: 表示該任務可與同階段其他 [P] 任務平行執行（不同檔案，無依賴）
- **[Story] 標籤**: 追蹤任務屬於哪個 User Story（US1, US2, US3）
- **TDD 強制**: Constitution 要求，所有測試必須先寫並確認 FAIL
- **每個 User Story 獨立可測**: 完成後應能單獨驗證功能
- **Commit 策略**: 建議每完成一個任務或邏輯群組後 commit
- **Stop at checkpoint**: 任何 Checkpoint 都可停下來驗證當前 Story 的獨立功能
- **避免**: 模糊的任務描述、同檔案衝突、破壞獨立性的跨 Story 依賴
