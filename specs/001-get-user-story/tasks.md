# Tasks: 取得 User Story 層級資訊

**Input**: Design documents from `/specs/001-get-user-story/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: 包含測試任務（Constitution 原則 I 強制 TDD）

**Organization**: 任務依 User Story 分組，每個 Story 可獨立實作與測試。

## Format: `[ID] [P?] [Story] Description`

- **[P]**: 可平行執行（不同檔案，無相依性）
- **[Story]**: 所屬 User Story（US1, US2, US3）
- 包含確切檔案路徑

---

## Phase 1: Setup（共用常數與列舉）

**Purpose**: 建立所有 User Story 共用的常數、列舉與基礎模型

- [ ] T001 [P] 新增 `AzureDevOpsUserStories` 常數至 `src/ReleaseKit.Common/Constants/RedisKeys.cs`，值為 `"AzureDevOps:WorkItems:UserStories"`
- [ ] T002 [P] 建立 `WorkItemTypeConstants` 靜態類別於 `src/ReleaseKit.Common/Constants/WorkItemTypeConstants.cs`，定義 `UserStoryOrAboveTypes` 集合（HashSet：User Story、Feature、Epic）與 `MaxRecursionDepth = 10` 常數
- [ ] T003 [P] 建立 `UserStoryResolutionStatus` 列舉於 `src/ReleaseKit.Application/Common/UserStoryResolutionStatus.cs`，包含四個值：AlreadyUserStoryOrAbove(0)、FoundViaRecursion(1)、NotFound(2)、OriginalFetchFailed(3)

✅ 可建置 / ⚠️ 待補測試

---

## Phase 2: Foundational（基礎設施與領域模型擴充）

**Purpose**: 擴充基礎設施層 API 回應模型與 Mapper，使系統能解析 Parent Work Item ID

**⚠️ CRITICAL**: 所有 User Story 的遞迴查找均依賴此階段的 Parent ID 解析能力

### Tests for Foundational

> **NOTE: 先撰寫測試，確認測試失敗後再實作**

- [ ] T004 [P] 撰寫 `AzureDevOpsWorkItemMapper` Parent ID 解析測試於 `tests/ReleaseKit.Infrastructure.Tests/AzureDevOps/Mappers/AzureDevOpsWorkItemMapperTests.cs`：測試含 Parent 關聯的回應可正確解析 ParentWorkItemId、無 Parent 關聯時 ParentWorkItemId 為 null、多個關聯中正確識別 Parent
- [ ] T005 [P] 撰寫 `WorkItemTypeConstants` 測試於 `tests/ReleaseKit.Common.Tests/Constants/WorkItemTypeConstantsTests.cs`：驗證 UserStoryOrAboveTypes 包含 User Story、Feature、Epic，且不包含 Task、Bug

### Implementation for Foundational

- [ ] T006 [P] 建立 `AzureDevOpsRelationResponse` 記錄於 `src/ReleaseKit.Infrastructure/AzureDevOps/Models/AzureDevOpsRelationResponse.cs`，包含 Rel（string）、Url（string）、Attributes（Dictionary）欄位，使用 JsonPropertyName 標註
- [ ] T007 [P] 擴充 `AzureDevOpsWorkItemResponse` 於 `src/ReleaseKit.Infrastructure/AzureDevOps/Models/AzureDevOpsWorkItemResponse.cs`，新增 `Relations` 欄位（List\<AzureDevOpsRelationResponse\>?）
- [ ] T008 擴充 `WorkItem` 領域實體於 `src/ReleaseKit.Domain/Entities/WorkItem.cs`，新增 `ParentWorkItemId`（int?）欄位
- [ ] T009 擴充 `AzureDevOpsWorkItemMapper.ToDomain()` 於 `src/ReleaseKit.Infrastructure/AzureDevOps/Mappers/AzureDevOpsWorkItemMapper.cs`，從 Relations 中找到 `System.LinkTypes.Hierarchy-Reverse` 類型的關聯，解析 URL 末尾數字為 ParentWorkItemId
- [ ] T010 執行建置與測試驗證：確認 T004、T005 測試通過

✅ 可建置 / ✅ 測試通過

**Checkpoint**: 基礎設施層可正確解析 Parent Work Item ID，所有 User Story 的前置條件已就緒

---

## Phase 3: User Story 1 - 透過指令取得 User Story 層級資訊 (Priority: P1) 🎯 MVP

**Goal**: 使用者執行 `get-user-story` 指令後，系統讀取 Redis 中的 Work Item 資料，判斷類型並遞迴找 Parent 至 User Story 層級，結果存入新 Redis Key

**Independent Test**: 準備含 User Story、Task、Bug 類型的 Work Item 資料，驗證 AlreadyUserStoryOrAbove 與 FoundViaRecursion 兩種狀態正確標註

### Tests for User Story 1

> **NOTE: 先撰寫測試，確認測試失敗後再實作**

- [ ] T011 [P] [US1] 撰寫 `GetUserStoryTask` 核心測試於 `tests/ReleaseKit.Application.Tests/Tasks/GetUserStoryTaskTests.cs`：(1) Redis 中有 User Story 類型的 Work Item → ResolutionStatus 為 AlreadyUserStoryOrAbove 且 UserStory 包含自身資訊；(2) Redis 中有 Task 類型的 Work Item 且其 Parent 為 User Story → ResolutionStatus 為 FoundViaRecursion 且 UserStory 包含 Parent 資訊；(3) 結果正確寫入 Redis Key `AzureDevOps:WorkItems:UserStories`
- [ ] T012 [P] [US1] 撰寫 `CommandLineParser` 新指令測試於 `tests/ReleaseKit.Console.Tests/Parsers/CommandLineParserTests.cs`：測試 `get-user-story` 指令解析為 `TaskType.GetUserStory`、大小寫不敏感

### Implementation for User Story 1

- [ ] T013 [P] [US1] 建立 `UserStoryInfo` 記錄於 `src/ReleaseKit.Application/Common/UserStoryInfo.cs`，包含 WorkItemId（int）、Title（string）、Type（string）、State（string）、Url（string）
- [ ] T014 [P] [US1] 建立 `UserStoryResolutionOutput` 記錄於 `src/ReleaseKit.Application/Common/UserStoryResolutionOutput.cs`，包含原始 WorkItemOutput 所有欄位 + ResolutionStatus + UserStory?
- [ ] T015 [P] [US1] 建立 `UserStoryResolutionResult` 記錄於 `src/ReleaseKit.Application/Common/UserStoryResolutionResult.cs`，包含 Items 清單與統計欄位（TotalCount、AlreadyUserStoryCount、FoundViaRecursionCount、NotFoundCount、OriginalFetchFailedCount）
- [ ] T016 [US1] 建立 `GetUserStoryTask` 於 `src/ReleaseKit.Application/Tasks/GetUserStoryTask.cs`，實作 `ITask` 介面：注入 ILogger、IRedisService、IAzureDevOpsRepository；ExecuteAsync 流程：(1) 從 Redis 讀取 WorkItemFetchResult、(2) 遍歷每個 WorkItemOutput、(3) 判斷 Type 是否為 UserStoryOrAbove、(4) 若不是則透過 API 遞迴查找 Parent、(5) 組裝 UserStoryResolutionResult、(6) 寫入新 Redis Key、(7) 輸出 JSON 至 stdout
- [ ] T017 [US1] 新增 `GetUserStory` 至 `TaskType` 列舉於 `src/ReleaseKit.Application/Tasks/TaskType.cs`
- [ ] T018 [US1] 新增 `get-user-story` 指令對應至 `src/ReleaseKit.Console/Parsers/CommandLineParser.cs` 的 `_taskMappings` 字典
- [ ] T019 [US1] 新增 `TaskType.GetUserStory` case 至 `src/ReleaseKit.Application/Tasks/TaskFactory.cs` 的 `CreateTask` switch
- [ ] T020 [US1] 註冊 `GetUserStoryTask` 至 DI 容器於 `src/ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs` 的 `AddApplicationServices` 方法
- [ ] T021 [US1] 執行建置與測試驗證：確認 T011、T012 測試通過

✅ 可建置 / ✅ 測試通過

**Checkpoint**: User Story 1 完成，可執行 `get-user-story` 指令處理 AlreadyUserStoryOrAbove 與 FoundViaRecursion 兩種基本情境

---

## Phase 4: User Story 2 - 處理無法取得資訊的 Work Item (Priority: P2)

**Goal**: 原始取得失敗的 Work Item 與遞迴查找失敗的 Work Item 都保留在結果中，並標註正確狀態

**Independent Test**: 準備含 IsSuccess=false 的 Work Item 及遞迴中斷的情境，驗證 OriginalFetchFailed 與 NotFound 狀態

### Tests for User Story 2

> **NOTE: 先撰寫測試，確認測試失敗後再實作**

- [ ] T022 [P] [US2] 撰寫測試於 `tests/ReleaseKit.Application.Tests/Tasks/GetUserStoryTaskTests.cs`：(1) IsSuccess=false 的 Work Item → ResolutionStatus 為 OriginalFetchFailed、UserStory 為 null；(2) Task 類型但 Parent 無法取得（API 回傳失敗）→ ResolutionStatus 為 NotFound；(3) Task 類型但無 Parent（ParentWorkItemId 為 null）→ ResolutionStatus 為 NotFound
- [ ] T023 [P] [US2] 撰寫測試於 `tests/ReleaseKit.Application.Tests/Tasks/GetUserStoryTaskTests.cs`：驗證空 Redis 資料（無 Work Item）→ 正常完成並寫入空結果

### Implementation for User Story 2

- [ ] T024 [US2] 擴充 `GetUserStoryTask.ExecuteAsync()` 於 `src/ReleaseKit.Application/Tasks/GetUserStoryTask.cs`：在遍歷 WorkItemOutput 時，先判斷 IsSuccess 為 false 的項目直接標記為 OriginalFetchFailed；遞迴查找失敗時（API 錯誤、無 Parent）標記為 NotFound
- [ ] T025 [US2] 執行建置與測試驗證：確認 T022、T023 測試通過

✅ 可建置 / ✅ 測試通過

**Checkpoint**: User Story 1 + 2 完成，系統可正確處理所有四種解析狀態

---

## Phase 5: User Story 3 - 處理深層巢狀的 Work Item 階層 (Priority: P3)

**Goal**: 正確處理多層巢狀（如 Bug → Task → User Story）與循環參照偵測

**Independent Test**: 準備多層巢狀 Work Item 與循環參照情境，驗證遞迴邏輯正確

### Tests for User Story 3

> **NOTE: 先撰寫測試，確認測試失敗後再實作**

- [ ] T026 [P] [US3] 撰寫測試於 `tests/ReleaseKit.Application.Tests/Tasks/GetUserStoryTaskTests.cs`：(1) Bug → Task → User Story 三層遞迴 → ResolutionStatus 為 FoundViaRecursion 且 UserStory 為最終的 User Story；(2) Task → Task → 無 Parent 兩層遞迴失敗 → ResolutionStatus 為 NotFound
- [ ] T027 [P] [US3] 撰寫測試於 `tests/ReleaseKit.Application.Tests/Tasks/GetUserStoryTaskTests.cs`：(1) 循環參照（Work Item A → B → A）→ ResolutionStatus 為 NotFound，不會無窮迴圈；(2) 超過最大遞迴深度（10 層）→ ResolutionStatus 為 NotFound

### Implementation for User Story 3

- [ ] T028 [US3] 擴充 `GetUserStoryTask` 遞迴邏輯於 `src/ReleaseKit.Application/Tasks/GetUserStoryTask.cs`：使用 HashSet\<int\> 追蹤已訪問 ID 偵測循環參照；使用計數器限制最大遞迴深度（WorkItemTypeConstants.MaxRecursionDepth）
- [ ] T029 [US3] 執行建置與測試驗證：確認 T026、T027 測試通過

✅ 可建置 / ✅ 測試通過

**Checkpoint**: 所有 User Story 完成，系統可正確處理所有情境（基本解析、失敗處理、深層巢狀、循環偵測）

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: 最終驗證與跨 User Story 的品質確認

- [ ] T030 執行完整建置驗證：`dotnet build src/release-kit.sln`
- [ ] T031 執行全部單元測試：`dotnet test` 確認所有測試通過
- [ ] T032 驗證 quickstart.md 流程：確認 `get-user-story` 指令可正確執行

✅ 可建置 / ✅ 測試通過

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: 無相依性，可立即開始
- **Foundational (Phase 2)**: 依賴 Phase 1 完成（使用 WorkItemTypeConstants）
- **User Stories (Phase 3+)**: 全部依賴 Phase 2 完成（需要 Parent ID 解析能力）
  - US1、US2、US3 理論上可平行，但建議依優先序實作
- **Polish (Phase 6)**: 依賴所有 User Story 完成

### User Story Dependencies

- **User Story 1 (P1)**: Phase 2 完成後可開始，不依賴其他 Story
- **User Story 2 (P2)**: Phase 2 完成後可開始，邏輯上擴充 US1 的 GetUserStoryTask（建議 US1 完成後實作）
- **User Story 3 (P3)**: Phase 2 完成後可開始，邏輯上擴充 US1 的遞迴邏輯（建議 US1 完成後實作）

### Within Each User Story

- 測試 MUST 先撰寫且確認失敗（TDD Red phase）
- DTO/Model 先於 Service/Task
- 核心邏輯先於 CLI 整合
- 完成後執行建置與測試驗證

### Parallel Opportunities

**Phase 1（全部可平行）**:
- T001、T002、T003 → 不同檔案，無相依性

**Phase 2（測試與部分實作可平行）**:
- T004、T005 → 不同測試檔案
- T006、T007 → 不同模型檔案

**Phase 3 - US1（測試與 DTO 可平行）**:
- T011、T012 → 不同測試檔案
- T013、T014、T015 → 不同 DTO 檔案

---

## Parallel Example: Phase 1

```bash
# 三個獨立檔案，可同時執行：
Task: "新增 AzureDevOpsUserStories 常數至 RedisKeys.cs"
Task: "建立 WorkItemTypeConstants.cs"
Task: "建立 UserStoryResolutionStatus.cs"
```

## Parallel Example: User Story 1

```bash
# 先平行撰寫測試：
Task: "撰寫 GetUserStoryTask 核心測試"
Task: "撰寫 CommandLineParser 新指令測試"

# 再平行建立 DTO：
Task: "建立 UserStoryInfo.cs"
Task: "建立 UserStoryResolutionOutput.cs"
Task: "建立 UserStoryResolutionResult.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup（T001-T003）
2. Complete Phase 2: Foundational（T004-T010）
3. Complete Phase 3: User Story 1（T011-T021）
4. **STOP and VALIDATE**: 測試 `get-user-story` 指令處理 AlreadyUserStoryOrAbove 與 FoundViaRecursion
5. 可交付 MVP

### Incremental Delivery

1. Phase 1 + Phase 2 → 基礎就緒
2. + User Story 1 → MVP（基本解析功能）
3. + User Story 2 → 完整錯誤處理
4. + User Story 3 → 穩健的遞迴邏輯（循環偵測、深度限制）
5. + Polish → 最終驗證

---

## Notes

- [P] 標記 = 不同檔案、無相依性，可平行執行
- [Story] 標記 = 對應 spec.md 的 User Story
- Constitution 強制 TDD：每個 Phase 的測試必須先寫並確認失敗
- 每個任務完成後標註建置與測試狀態
- 所有註解使用繁體中文
- 使用 JsonExtensions 進行 JSON 序列化/反序列化
- 錯誤處理使用 Result Pattern，禁止 try-catch
