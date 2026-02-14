# Tasks: Get User Story

**Input**: Design documents from `/specs/001-get-user-story/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Included per constitution requirement (TDD 非協商原則). All tests follow Red-Green-Refactor cycle.

**Organization**: Tasks are grouped by user story. US1 and US2 (both P1) can run in parallel. US3 (P2) depends on US2 completion.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Source**: `src/ReleaseKit.{Layer}/`
- **Tests**: `tests/ReleaseKit.{Layer}.Tests/`
- **Solution**: `src/release-kit.sln`

---

## Phase 1: Setup

**Purpose**: Verify existing project baseline before any changes

- [x] T001 Verify current solution builds and all tests pass with `dotnet build src/release-kit.sln` and `dotnet test src/release-kit.sln`

---

## Phase 2: User Story 1 - 追蹤 PR 識別碼 (Priority: P1) 🎯 MVP

**Goal**: PR 資料結構新增 PullRequestId 欄位，GitLab 映射 iid、Bitbucket 映射 id

**Independent Test**: 執行 `fetch-gitlab-pr` 或 `fetch-bitbucket-pr`，驗證 Redis 中 PR 資料包含 PullRequestId 欄位

### Structural Changes for US1

- [x] T002 [P] [US1] Add `PullRequestId` (int) property with XML Summary to MergeRequest entity in src/ReleaseKit.Domain/Entities/MergeRequest.cs
- [x] T003 [P] [US1] Add `PullRequestId` (int) property to MergeRequestOutput DTO in src/ReleaseKit.Application/Common/MergeRequestOutput.cs

### Tests for US1 (Red Phase) 🔴

> **Write tests FIRST. They MUST fail before implementation.**

- [x] T004 [P] [US1] Write PullRequestId mapping test (verify GitLab iid maps to MergeRequest.PullRequestId) in tests/ReleaseKit.Infrastructure.Tests/SourceControl/GitLab/GitLabMergeRequestMapperTests.cs
- [x] T005 [P] [US1] Write PullRequestId mapping test (verify Bitbucket id maps to MergeRequest.PullRequestId) in tests/ReleaseKit.Infrastructure.Tests/SourceControl/Bitbucket/BitbucketPullRequestMapperTests.cs

### Implementation for US1 (Green Phase) 🟢

- [x] T006 [P] [US1] Implement PullRequestId mapping from `Iid` field in GitLabMergeRequestMapper in src/ReleaseKit.Infrastructure/SourceControl/GitLab/GitLabMergeRequestMapper.cs
- [x] T007 [P] [US1] Implement PullRequestId mapping from `Id` field in BitbucketPullRequestMapper in src/ReleaseKit.Infrastructure/SourceControl/Bitbucket/BitbucketPullRequestMapper.cs
- [x] T008 [P] [US1] Add PullRequestId to output mapping in BaseFetchPullRequestsTask in src/ReleaseKit.Application/Tasks/BaseFetchPullRequestsTask.cs

**Checkpoint**: US1 完成。PR 資料結構包含 PullRequestId，GitLab 映射 iid、Bitbucket 映射 id。✅ 可建置 ✅ 測試通過

---

## Phase 3: User Story 2 - Work Item 保留 PR 來源關聯 (Priority: P1)

**Goal**: Work Item 抓取邏輯重構為一對一記錄，每筆保留來源 PR 資訊（PR ID、專案名稱、PR URL），並解析 parent Work Item ID

**Independent Test**: 執行 `fetch-azure-workitems`，驗證 Redis 中每筆 WorkItem 包含 SourcePullRequestId、SourceProjectName、SourcePRUrl，且同一 Work Item 出現在多筆 PR 中時產生多筆記錄

### Structural Changes for US2

- [ ] T009 [P] [US2] Add `ParentWorkItemId` (int?) property with XML Summary to WorkItem entity in src/ReleaseKit.Domain/Entities/WorkItem.cs
- [ ] T010 [P] [US2] Create AzureDevOpsRelationResponse model with `Rel` (string, JsonPropertyName "rel") and `Url` (string, JsonPropertyName "url") properties in src/ReleaseKit.Infrastructure/AzureDevOps/Models/AzureDevOpsRelationResponse.cs
- [ ] T011 [US2] Add `Relations` (List&lt;AzureDevOpsRelationResponse&gt;?, JsonPropertyName "relations") to AzureDevOpsWorkItemResponse in src/ReleaseKit.Infrastructure/AzureDevOps/Models/AzureDevOpsWorkItemResponse.cs
- [ ] T012 [P] [US2] Add `SourcePullRequestId` (int?), `SourceProjectName` (string?), `SourcePRUrl` (string?) properties to WorkItemOutput in src/ReleaseKit.Application/Common/WorkItemOutput.cs

### Tests for US2 (Red Phase) 🔴

> **Write tests FIRST. They MUST fail before implementation.**

- [ ] T013 [P] [US2] Write ExtractParentWorkItemId tests in tests/ReleaseKit.Infrastructure.Tests/AzureDevOps/Mappers/AzureDevOpsWorkItemMapperTests.cs:
  - 有 `System.LinkTypes.Hierarchy-Reverse` relation → 從 URL 末段解析 parent ID
  - 無 relations → 回傳 null
  - 多個 relations → 僅取 Hierarchy-Reverse 類型
  - URL 格式異常 → 回傳 null
- [ ] T014 [P] [US2] Write PR source preservation tests in tests/ReleaseKit.Application.Tests/Tasks/FetchAzureDevOpsWorkItemsTaskTests.cs:
  - 單一 PR 對應單一 Work Item → 輸出包含 PR 來源欄位
  - 同一 Work Item ID 出現在兩筆 PR → 產生兩筆獨立記錄，API 僅查詢一次
  - API 查詢失敗 → 產生失敗記錄並保留 PR 來源資訊

### Implementation for US2 (Green Phase) 🟢

- [ ] T015 [US2] Implement `ExtractParentWorkItemId` static method in AzureDevOpsWorkItemMapper: filter relations by `System.LinkTypes.Hierarchy-Reverse`, parse URL last segment as int in src/ReleaseKit.Infrastructure/AzureDevOps/Mappers/AzureDevOpsWorkItemMapper.cs
- [ ] T016 [US2] Refactor FetchAzureDevOpsWorkItemsTask to produce one-to-one WorkItem-PR records: iterate PR list → extract VSTS IDs → build (WorkItemId, PR) pairs → deduplicate API calls with Dictionary&lt;int, WorkItem&gt; cache → output WorkItemOutput with source PR fields in src/ReleaseKit.Application/Tasks/FetchAzureDevOpsWorkItemsTask.cs

**Checkpoint**: US2 完成。WorkItem 抓取保留 PR 來源資訊，一對一記錄，API 去重快取。✅ 可建置 ✅ 測試通過

---

## Phase 4: User Story 3 - 解析 Work Item 至 User Story 層級 (Priority: P2)

**Goal**: 新增 `get-user-story` 指令，讀取 Redis 中 WorkItem 資料，遞迴向上查詢 parent 直到 User Story/Feature/Epic，結果存至新 Redis key `AzureDevOps:UserStories`

**Independent Test**: 執行 `get-user-story`，驗證 Redis `AzureDevOps:UserStories` key 存放正確解析結果

**Dependencies**: Phase 3 (US2) 必須完成（需要 WorkItemOutput 的 PR 來源欄位、ParentWorkItemId 基礎設施、AzureDevOpsRelationResponse 模型）

### Structural Changes for US3

- [ ] T017 [P] [US3] Create UserStoryOutput DTO (WorkItemId int, OriginalWorkItemId int, Title string?, Type string?, State string?, Url string?, OriginalTeamName string?, IsSuccess bool, ErrorMessage string?) with XML Summary in src/ReleaseKit.Application/Common/UserStoryOutput.cs
- [ ] T018 [P] [US3] Create UserStoryFetchResult DTO (UserStories List&lt;UserStoryOutput&gt;, TotalWorkItemsProcessed int, AlreadyUserStoryCount int, ResolvedCount int, KeptOriginalCount int) with XML Summary in src/ReleaseKit.Application/Common/UserStoryFetchResult.cs
- [ ] T019 [P] [US3] Add `AzureDevOpsUserStories = "AzureDevOps:UserStories"` constant to RedisKeys in src/ReleaseKit.Common/Constants/RedisKeys.cs

### Tests for US3 (Red Phase) 🔴

> **Write tests FIRST. They MUST fail before implementation.**

- [ ] T020 [US3] Write comprehensive GetUserStoryTask tests covering all acceptance scenarios and edge cases in tests/ReleaseKit.Application.Tests/Tasks/GetUserStoryTaskTests.cs:
  - 已是 User Story 的 WorkItem → 直接保留，不查詢 API
  - 已是 Feature 的 WorkItem → 直接保留
  - 已是 Epic 的 WorkItem → 直接保留
  - Task 的 parent 為 User Story → 查詢一次 API，解析至 parent，記錄 OriginalWorkItemId
  - Bug 的祖父為 User Story（二層遞迴）→ 查詢兩次 API，解析至祖父
  - 整條 parent 鏈無高層級類型 → 保留原始 WorkItem 資料
  - 原始抓取失敗（IsSuccess=false）→ 保留失敗記錄
  - 結果正確寫入 Redis key `AzureDevOps:UserStories`
  - 遞迴深度超過 10 層 → 保留原始資料
  - 同一 WorkItem 出現在多筆 PR → 各自獨立解析
  - 遞迴查詢中 API 失敗 → 保留原始 WorkItem 資料
  - 重複 Work Item ID → Dictionary 快取，API 僅查詢一次
  - 統計數字驗證：TotalWorkItemsProcessed == AlreadyUserStoryCount + ResolvedCount + KeptOriginalCount
- [ ] T021 [P] [US3] Write GetUserStory case test (TaskType.GetUserStory → returns GetUserStoryTask instance) in tests/ReleaseKit.Application.Tests/Tasks/TaskFactoryTests.cs
- [ ] T022 [P] [US3] Write get-user-story mapping test ("get-user-story" → TaskType.GetUserStory) in tests/ReleaseKit.Console.Tests/Parsers/CommandLineParserTests.cs

### Implementation for US3 (Green Phase) 🟢

- [ ] T023 [P] [US3] Implement GetUserStoryTask (ITask) in src/ReleaseKit.Application/Tasks/GetUserStoryTask.cs:
  - 注入 IRedisService, IAzureDevOpsRepository, ILogger
  - 從 Redis 讀取 WorkItemFetchResult (key: AzureDevOps:WorkItems)
  - 定義高層級類型 HashSet&lt;string&gt;(OrdinalIgnoreCase): "User Story", "Feature", "Epic"
  - 對每筆 WorkItemOutput: 若 IsSuccess=false → 保留失敗記錄; 若為高層級類型 → 直接保留; 否則遞迴查詢 parent
  - 遞迴邏輯: 呼叫 GetWorkItemAsync → 檢查 ParentWorkItemId → 再查 parent → 直到高層級或無 parent 或深度 > 10
  - 使用 Dictionary&lt;int, WorkItem&gt; 快取已查詢的 WorkItem
  - 使用 Result Pattern 處理 API 回傳
  - 組建 UserStoryFetchResult 寫入 Redis (key: AzureDevOps:UserStories)
  - 所有公開成員加入 XML Summary 繁體中文註解
- [ ] T024 [P] [US3] Add `GetUserStory` value to TaskType enum in src/ReleaseKit.Application/Tasks/TaskType.cs
- [ ] T025 [US3] Add `TaskType.GetUserStory` case to TaskFactory, resolve GetUserStoryTask from DI container in src/ReleaseKit.Application/Tasks/TaskFactory.cs
- [ ] T026 [P] [US3] Add "get-user-story" → TaskType.GetUserStory mapping to CommandLineParser in src/ReleaseKit.Console/Parsers/CommandLineParser.cs
- [ ] T027 [US3] Register GetUserStoryTask as transient in DI container in src/ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs

**Checkpoint**: US3 完成。get-user-story 指令可正確解析 WorkItem 至 User Story 層級，結果存入 Redis。✅ 可建置 ✅ 測試通過

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: 最終驗證與整體品質確認

- [ ] T028 Verify solution builds successfully with `dotnet build src/release-kit.sln`
- [ ] T029 Verify all unit tests pass with `dotnet test src/release-kit.sln`
- [ ] T030 Run quickstart.md validation scenarios end-to-end

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - verify baseline
- **US1 (Phase 2)**: Depends on Phase 1 - can start immediately after baseline
- **US2 (Phase 3)**: Depends on Phase 1 - can start immediately after baseline (**parallel with US1**)
- **US3 (Phase 4)**: Depends on Phase 3 (US2) completion - needs ParentWorkItemId infrastructure and WorkItemOutput source fields
- **Polish (Phase 5)**: Depends on all user stories being complete

### User Story Dependencies

- **US1 (P1)**: Independent - no dependencies on other stories
- **US2 (P1)**: Independent - no dependencies on other stories
- **US3 (P2)**: Depends on US2 structural changes (ParentWorkItemId, AzureDevOpsRelationResponse, WorkItemOutput source fields)

```
Phase 1 (Setup)
    ├──→ Phase 2 (US1) ─────────────────┐
    └──→ Phase 3 (US2) → Phase 4 (US3) ─┤
                                         └──→ Phase 5 (Polish)
```

### Within Each User Story

1. Structural changes FIRST (add properties, create DTOs)
2. Tests MUST be written and FAIL before implementation (TDD Red 🔴)
3. Implementation makes tests pass (TDD Green 🟢)
4. Verify checkpoint before moving to next story

### Parallel Opportunities

**Phase 2 + Phase 3 can run in parallel** (US1 和 US2 互不依賴):

Phase 2 (US1):
- T002 ∥ T003 (structural, different files)
- T004 ∥ T005 (tests, different files)
- T006 ∥ T007 ∥ T008 (implementation, different files)

Phase 3 (US2):
- T009 ∥ T010 ∥ T012 (structural, different files)
- T013 ∥ T014 (tests, different files)

Phase 4 (US3):
- T017 ∥ T018 ∥ T019 (structural, different files)
- T021 ∥ T022 (tests, different files; T020 is comprehensive, best executed alone)
- T023 ∥ T024 (implementation, different files)
- T025 ∥ T026 ∥ T027 (wiring, different files, after T023 + T024 complete)

---

## Parallel Example: US1 + US2

```bash
# US1 和 US2 可同時啟動（不同 subagent 或開發者）:

# Agent A: US1
Task: "T002 Add PullRequestId to MergeRequest entity"
Task: "T003 Add PullRequestId to MergeRequestOutput"
# Wait for structural → then tests
Task: "T004 Write GitLab mapper test" ∥ "T005 Write Bitbucket mapper test"
# Wait for tests → then implementation
Task: "T006 GitLab mapper" ∥ "T007 Bitbucket mapper" ∥ "T008 BaseFetchPullRequestsTask"

# Agent B: US2
Task: "T009 Add ParentWorkItemId" ∥ "T010 Create RelationResponse" ∥ "T012 Add source fields"
Task: "T011 Add Relations to WorkItemResponse"
# Wait for structural → then tests
Task: "T013 Write mapper tests" ∥ "T014 Write task tests"
# Wait for tests → then implementation
Task: "T015 Implement mapper"
Task: "T016 Refactor task"
```

---

## Implementation Strategy

### MVP First (US1 Only)

1. Complete Phase 1: Setup (baseline verification)
2. Complete Phase 2: US1 - PR ID
3. **STOP and VALIDATE**: 測試 PR 資料包含 PullRequestId
4. Deploy/demo if ready

### Incremental Delivery

1. Phase 1 → Baseline verified
2. Phase 2 (US1) → PR ID 功能完成 → **MVP!**
3. Phase 3 (US2) → Work Item PR 關聯完成
4. Phase 4 (US3) → User Story 解析功能完成 → **Full Feature!**
5. Phase 5 → 品質確認

### Parallel Team Strategy

With 2 developers/agents:
1. Both verify baseline (Phase 1)
2. Developer A: US1 (Phase 2) → US3 (Phase 4)
3. Developer B: US2 (Phase 3) → Polish (Phase 5)
4. US3 starts after US2 completes

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks
- [Story] label maps task to specific user story (US1, US2, US3)
- TDD is mandatory per constitution - write tests first, verify they fail (Red), then implement (Green)
- Each user story is independently testable at its checkpoint
- ✅ 可建置 / ✅ 測試通過 status required at each checkpoint
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- JsonPropertyName 允許用於 AzureDevOpsRelationResponse（外部 API 契約，符合憲法例外條件）
