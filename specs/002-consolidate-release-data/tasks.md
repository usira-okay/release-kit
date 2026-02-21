# Tasks: 整合 Release 資料

**Input**: Design documents from `/specs/002-consolidate-release-data/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, quickstart.md ✅

**Tests**: 依據專案憲法（TDD 為強制性開發流程），所有任務皆包含對應測試。

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Source**: `src/ReleaseKit.{Layer}/`
- **Tests**: `tests/ReleaseKit.{Layer}.Tests/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: 新增 Redis Key 常數與 DTO 資料模型，作為所有 User Story 的共用基礎

- [X] T001 新增 `ConsolidatedReleaseData` 常數至 `src/ReleaseKit.Common/Constants/RedisKeys.cs`
- [X] T002 [P] 新增 `ConsolidatedAuthorInfo` record 至 `src/ReleaseKit.Application/Common/ConsolidatedAuthorInfo.cs`
- [X] T003 [P] 新增 `ConsolidatedPrInfo` record 至 `src/ReleaseKit.Application/Common/ConsolidatedPrInfo.cs`
- [X] T004 [P] 新增 `ConsolidatedOriginalData` record 至 `src/ReleaseKit.Application/Common/ConsolidatedOriginalData.cs`
- [X] T005 [P] 新增 `ConsolidatedReleaseEntry` record 至 `src/ReleaseKit.Application/Common/ConsolidatedReleaseEntry.cs`
- [X] T006 [P] 新增 `ConsolidatedProjectGroup` record 至 `src/ReleaseKit.Application/Common/ConsolidatedProjectGroup.cs`
- [X] T007 [P] 新增 `ConsolidatedReleaseResult` record 至 `src/ReleaseKit.Application/Common/ConsolidatedReleaseResult.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: 註冊 Task 類型、工廠對映、CLI 指令與 DI，使 `consolidate-release-data` 指令可被辨識與執行

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T008 新增 `ConsolidateReleaseData` 列舉值至 `src/ReleaseKit.Application/Tasks/TaskType.cs`
- [X] T009 新增 `ConsolidateReleaseData` case 至 `src/ReleaseKit.Application/Tasks/TaskFactory.cs` 的 switch expression
- [X] T010 新增 `consolidate-release-data` 指令對映至 `src/ReleaseKit.Console/Parsers/CommandLineParser.cs` 的 `_taskMappings`
- [X] T011 註冊 `ConsolidateReleaseDataTask` 至 `src/ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs` 的 `AddApplicationServices` 方法
- [X] T012 新增 `TaskFactory` 測試：驗證 `ConsolidateReleaseData` 正確建立 Task 實例，於 `tests/ReleaseKit.Application.Tests/Tasks/TaskFactoryTests.cs`
- [X] T013 新增 `CommandLineParser` 測試：驗證 `consolidate-release-data` 正確解析為 `TaskType.ConsolidateReleaseData`，於 `tests/ReleaseKit.Console.Tests/Parsers/CommandLineParserTests.cs`

**Checkpoint**: Foundation ready — `consolidate-release-data` 指令可被解析並路由至 Task 實例

---

## Phase 3: User Story 1 — 整合 PR 與 Work Item 資料 (Priority: P1) 🎯 MVP

**Goal**: 從 Redis 讀取 PR（Bitbucket/GitLab ByUser）與 Work Item（UserStories）資料，以 PrId 配對、依 ProjectPath 分組、依 TeamDisplayName 與 WorkItemId 排序後存入新 Redis Key

**Independent Test**: 在測試中 Mock Redis 寫入 PR 與 Work Item 資料，執行 Task 後驗證輸出結構與排序正確性

### Tests for User Story 1 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T014 [P] [US1] 測試：讀取 Bitbucket + GitLab PR 資料並以 PrId 配對 Work Item，驗證整合記錄數量與欄位正確，於 `tests/ReleaseKit.Application.Tests/Tasks/ConsolidateReleaseDataTaskTests.cs`
- [X] T015 [P] [US1] 測試：驗證整合結果依 ProjectPath 最後一段分組（如 `group/subgroup/project` → `project`），於 `tests/ReleaseKit.Application.Tests/Tasks/ConsolidateReleaseDataTaskTests.cs`
- [X] T016 [P] [US1] 測試：驗證同一專案內記錄依 TeamDisplayName 升冪、再依 WorkItemId 升冪排序，於 `tests/ReleaseKit.Application.Tests/Tasks/ConsolidateReleaseDataTaskTests.cs`
- [X] T017 [P] [US1] 測試：驗證 TeamMapping 正確將 OriginalTeamName 轉換為 DisplayName，於 `tests/ReleaseKit.Application.Tests/Tasks/ConsolidateReleaseDataTaskTests.cs`
- [X] T018 [P] [US1] 測試：驗證同一 Work Item 有多個 PR 時，Authors 與 PullRequests 清單包含所有相關 PR 資訊（去重 AuthorName），於 `tests/ReleaseKit.Application.Tests/Tasks/ConsolidateReleaseDataTaskTests.cs`
- [X] T019 [P] [US1] 測試：驗證 PrId 為 null 的 Work Item 仍出現在結果中，PR 資訊與作者資訊為空陣列，ProjectName 為 "unknown"，於 `tests/ReleaseKit.Application.Tests/Tasks/ConsolidateReleaseDataTaskTests.cs`
- [X] T020 [P] [US1] 測試：驗證整合結果以 JSON 序列化後正確寫入 Redis Key `ConsolidatedReleaseData`，於 `tests/ReleaseKit.Application.Tests/Tasks/ConsolidateReleaseDataTaskTests.cs`

### Implementation for User Story 1

- [X] T021 [US1] 實作 `ConsolidateReleaseDataTask`（實作 `ITask`）於 `src/ReleaseKit.Application/Tasks/ConsolidateReleaseDataTask.cs`，包含：
  - 建構子注入 `IRedisService`、`IOptions<AzureDevOpsOptions>`、`ILogger<ConsolidateReleaseDataTask>`
  - `ExecuteAsync` 方法實作完整整合流程：
    1. 從 Redis 讀取 Bitbucket/GitLab ByUser PR 資料（`FetchResult`）
    2. 建立 `Dictionary<string, List<(MergeRequestOutput PR, string ProjectName)>>` 以 PrId 為 Key
    3. 從 Redis 讀取 UserStories Work Item 資料（`UserStoryFetchResult`）
    4. 建立 TeamMapping `Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)`
    5. 遍歷 Work Items，依 PrId 配對 PR，合併相同 WorkItemId 的多筆記錄
    6. 收集 Authors（依 AuthorName 去重）與 PR URLs
    7. 依 ProjectName 分組（split('/') 取最後一段，無 PR 時為 "unknown"）
    8. 每組內依 TeamDisplayName 升冪 → WorkItemId 升冪排序
    9. 序列化為 `ConsolidatedReleaseResult` 並寫入 Redis

**Checkpoint**: User Story 1 完成 — PR 與 Work Item 資料可正確整合、分組、排序並存入 Redis

---

## Phase 4: User Story 2 — 缺少 PR 資料時的錯誤處理 (Priority: P1)

**Goal**: 當 Redis 中 Bitbucket 與 GitLab 的 ByUser PR 資料均不存在或為空時，拋出明確的 `InvalidOperationException`

**Independent Test**: Mock Redis 返回 null/空資料，驗證 Task 拋出正確例外訊息

### Tests for User Story 2 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T022 [P] [US2] 測試：當 Bitbucket 與 GitLab ByUser PR 資料 Key 均不存在時，拋出 `InvalidOperationException` 且錯誤訊息指出缺少的 Key，於 `tests/ReleaseKit.Application.Tests/Tasks/ConsolidateReleaseDataTaskTests.cs`
- [X] T023 [P] [US2] 測試：當 Bitbucket 與 GitLab ByUser PR 資料均為空集合（`Results` 為空 List）時，拋出 `InvalidOperationException`，於 `tests/ReleaseKit.Application.Tests/Tasks/ConsolidateReleaseDataTaskTests.cs`

### Implementation for User Story 2

- [X] T024 [US2] 在 `ConsolidateReleaseDataTask.ExecuteAsync` 中新增 PR 資料驗證邏輯於 `src/ReleaseKit.Application/Tasks/ConsolidateReleaseDataTask.cs`：
  - 讀取 Bitbucket 與 GitLab PR 資料後，檢查是否兩者皆為 null 或所有 Results 均為空
  - 若是，拋出 `InvalidOperationException`，訊息明確指出缺少的 Redis Key

**Checkpoint**: User Story 2 完成 — PR 資料缺失時系統正確拋出錯誤

---

## Phase 5: User Story 3 — 缺少 Work Item 資料時的錯誤處理 (Priority: P1)

**Goal**: 當 Redis 中 UserStories Work Item 資料不存在或為空時，拋出明確的 `InvalidOperationException`

**Independent Test**: Mock Redis 返回 null/空 Work Item 資料，驗證 Task 拋出正確例外訊息

### Tests for User Story 3 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T025 [P] [US3] 測試：當 UserStories Work Item 資料 Key 不存在時，拋出 `InvalidOperationException` 且錯誤訊息指出缺少的 Key，於 `tests/ReleaseKit.Application.Tests/Tasks/ConsolidateReleaseDataTaskTests.cs`
- [X] T026 [P] [US3] 測試：當 UserStories Work Item 資料為空集合（`WorkItems` 為空 List）時，拋出 `InvalidOperationException`，於 `tests/ReleaseKit.Application.Tests/Tasks/ConsolidateReleaseDataTaskTests.cs`

### Implementation for User Story 3

- [X] T027 [US3] 在 `ConsolidateReleaseDataTask.ExecuteAsync` 中新增 Work Item 資料驗證邏輯於 `src/ReleaseKit.Application/Tasks/ConsolidateReleaseDataTask.cs`：
  - 讀取 UserStories 資料後，檢查是否為 null 或 WorkItems 為空
  - 若是，拋出 `InvalidOperationException`，訊息明確指出缺少的 Redis Key

**Checkpoint**: User Story 3 完成 — Work Item 資料缺失時系統正確拋出錯誤

---

## Phase 6: User Story 4 — 團隊名稱對映忽略大小寫 (Priority: P2)

**Goal**: TeamMapping 查詢使用 `StringComparer.OrdinalIgnoreCase`，找不到對映時使用原始 OriginalTeamName

**Independent Test**: 使用大小寫不同的 OriginalTeamName 測試對映結果

### Tests for User Story 4 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T028 [P] [US4] 測試：TeamMapping 忽略大小寫 — OriginalTeamName 為 "moneylogistic"（全小寫）仍正確對映為 "金流團隊"，於 `tests/ReleaseKit.Application.Tests/Tasks/ConsolidateReleaseDataTaskTests.cs`
- [X] T029 [P] [US4] 測試：TeamMapping 找不到對映時 — TeamDisplayName 使用原始 OriginalTeamName，於 `tests/ReleaseKit.Application.Tests/Tasks/ConsolidateReleaseDataTaskTests.cs`

### Implementation for User Story 4

- [X] T030 [US4] 確認 `ConsolidateReleaseDataTask` 中 TeamMapping Dictionary 使用 `StringComparer.OrdinalIgnoreCase` 並處理 fallback 邏輯於 `src/ReleaseKit.Application/Tasks/ConsolidateReleaseDataTask.cs`

**Checkpoint**: User Story 4 完成 — 團隊名稱對映容錯性完備

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: 最終驗證與文件整理

- [X] T031 執行 `dotnet build src/release-kit.sln` 確認建置成功
- [X] T032 執行 `dotnet test tests/ReleaseKit.Application.Tests` 確認所有測試通過
- [X] T033 執行 `dotnet test` 確認全專案所有測試通過

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 (T001 for RedisKeys, T002-T007 for DTOs)
- **User Story 1 (Phase 3)**: Depends on Phase 2 completion — 核心整合邏輯
- **User Story 2 (Phase 4)**: Depends on Phase 3 (T021) — 在已有的 Task 中新增驗證
- **User Story 3 (Phase 5)**: Depends on Phase 3 (T021) — 在已有的 Task 中新增驗證
- **User Story 4 (Phase 6)**: Depends on Phase 3 (T021) — 確認大小寫忽略邏輯
- **Polish (Phase 7)**: Depends on all user stories complete

### User Story Dependencies

- **User Story 1 (P1)**: 核心功能，所有其他 User Story 依賴此 Task 實作
- **User Story 2 (P1)**: 可在 US1 實作同時加入 PR 驗證邏輯
- **User Story 3 (P1)**: 可在 US1 實作同時加入 Work Item 驗證邏輯
- **User Story 4 (P2)**: 可在 US1 實作同時內建大小寫忽略（US1 已包含 TeamMapping 邏輯）

> **實務建議**: US2、US3、US4 的邏輯實際上會在實作 US1 的 `ConsolidateReleaseDataTask` 時一併內建。獨立的 Phase 4/5/6 主要用於確保各場景的測試覆蓋完整性。

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- DTO models before Task implementation
- Core implementation before validation logic
- Story complete before moving to next priority

### Parallel Opportunities

- T002-T007（DTO records）可完全並行建立
- T008-T011（註冊相關）可並行修改（不同檔案）
- T012-T013（Foundational 測試）可並行
- T014-T020（US1 測試）可完全並行撰寫
- T022-T023（US2 測試）可並行
- T025-T026（US3 測試）可並行
- T028-T029（US4 測試）可並行

---

## Parallel Example: Phase 1 Setup

```bash
# 所有 DTO records 可同時建立（不同檔案）:
Task: "T002 ConsolidatedAuthorInfo in Application/Common/ConsolidatedAuthorInfo.cs"
Task: "T003 ConsolidatedPrInfo in Application/Common/ConsolidatedPrInfo.cs"
Task: "T004 ConsolidatedOriginalData in Application/Common/ConsolidatedOriginalData.cs"
Task: "T005 ConsolidatedReleaseEntry in Application/Common/ConsolidatedReleaseEntry.cs"
Task: "T006 ConsolidatedProjectGroup in Application/Common/ConsolidatedProjectGroup.cs"
Task: "T007 ConsolidatedReleaseResult in Application/Common/ConsolidatedReleaseResult.cs"
```

## Parallel Example: Phase 2 Foundational

```bash
# 註冊相關修改可同時進行（不同檔案）:
Task: "T008 TaskType.cs"
Task: "T009 TaskFactory.cs"
Task: "T010 CommandLineParser.cs"
Task: "T011 ServiceCollectionExtensions.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup（DTO 與 RedisKey）
2. Complete Phase 2: Foundational（TaskType、Factory、Parser、DI 註冊）
3. Complete Phase 3: User Story 1（核心整合邏輯 + 測試）
4. **STOP and VALIDATE**: 建置成功 + 測試通過
5. 此時已可執行 `consolidate-release-data` 指令

### Incremental Delivery

1. Setup + Foundational → 指令可被辨識
2. User Story 1 → 核心整合功能完成 → **MVP!**
3. User Story 2 → PR 資料缺失錯誤處理
4. User Story 3 → Work Item 資料缺失錯誤處理
5. User Story 4 → 團隊名稱大小寫容錯
6. Polish → 全域驗證

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- 遵循 TDD：先寫失敗測試 → 實作 → 重構
- 使用 `IRedisService` 讀寫 Redis，使用 `JsonExtensions` 序列化
- 團隊對映使用現有 `TeamMappingOptions`（已在 `AzureDevOpsOptions` 中）
- 所有公開成員必須包含繁體中文 XML Summary 註解
- 每個新類別獨立為一個檔案
