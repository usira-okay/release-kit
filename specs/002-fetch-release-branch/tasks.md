# Tasks: 取得各 Repository 最新 Release Branch 名稱

**Input**: Design documents from `/specs/002-fetch-release-branch/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md

**Tests**: 依 Constitution 第 I 條 TDD 規範，所有功能實作 MUST 遵循 Red-Green-Refactor 循環，測試為必要項目。

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions
- 每個任務完成後標註建置與測試狀態

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: 新增所有 User Story 共用的常數、列舉值與 DTO

- [x] T001 [P] 在 `src/ReleaseKit.Common/Constants/RedisKeys.cs` 新增 `GitLabReleaseBranches = "GitLab:ReleaseBranches"` 與 `BitbucketReleaseBranches = "Bitbucket:ReleaseBranches"` 常數
  - 建置: ✅ 可建置 | 測試: ✅ 通過（既有測試不受影響）

- [x] T002 [P] 在 `src/ReleaseKit.Application/Tasks/TaskType.cs` 新增 `FetchGitLabReleaseBranches` 與 `FetchBitbucketReleaseBranches` 列舉值，加入 XML summary 註解（繁體中文）
  - 建置: ✅ 可建置 | 測試: ✅ 通過（既有測試不受影響）

- [x] T003 [P] 建立 `src/ReleaseKit.Application/Common/ReleaseBranchResult.cs`，定義 `sealed record ReleaseBranchResult`，包含 `Dictionary<string, List<string>> Branches` 屬性，加入 XML summary 註解（繁體中文）
  - 建置: ✅ 可建置 | 測試: ✅ 通過

**Checkpoint**: 共用基礎元件就緒

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: 實作基底任務類別，為 US1 與 US2 的共用邏輯

**⚠️ CRITICAL**: US1、US2 的具體任務必須等此階段完成後才能實作

- [x] T004 建立 `src/ReleaseKit.Application/Tasks/BaseFetchReleaseBranchTask.cs`，實作抽象泛型基底類別 `BaseFetchReleaseBranchTask<TOptions, TProjectOptions> : ITask where TProjectOptions : IProjectOptions`
  - 依賴注入: `ISourceControlRepository`、`ILogger`、`IRedisService`、`TOptions`
  - 抽象屬性: `PlatformName`、`RedisKey`
  - 抽象方法: `GetProjects()` 回傳 `IEnumerable<TProjectOptions>`
  - `ExecuteAsync()` 邏輯:
    1. 記錄開始日誌
    2. 檢查並清除 Redis 舊資料（`ExistsAsync` → `DeleteAsync`）
    3. 遍歷 `GetProjects()`，對每個專案呼叫 `repository.GetBranchesAsync(projectPath, "release/")`
    4. 成功且有分支 → `OrderByDescending` 取最新 → 加入 Dictionary 對應 key 的 List
    5. 失敗或空清單 → 加入 `"NotFound"` key 的 List
    6. 使用 `JsonExtensions.ToJson()` 序列化 Dictionary 並輸出到 Console
    7. 使用 `RedisService.SetAsync(RedisKey, json)` 存入 Redis
    8. 記錄完成日誌（含專案數量、成功/失敗統計）
  - 所有公開類別與方法加入 XML summary 註解（繁體中文）
  - 建置: ✅ 可建置 | 測試: ✅ 通過（抽象類別，無法直接測試）

**Checkpoint**: 基底邏輯就緒，可開始實作具體任務

---

## Phase 3: User Story 1 - 取得 GitLab 各專案最新 Release Branch (Priority: P1) 🎯 MVP

**Goal**: 使用者可執行 `fetch-gitlab-release-branch` 指令，查詢所有 GitLab 專案的最新 release branch，結果依分支名稱分組，輸出 JSON 至 Console 並存入 Redis

**Independent Test**: 執行 `fetch-gitlab-release-branch` 指令，驗證 Console 輸出正確 JSON 格式，Redis 中 `GitLab:ReleaseBranches` key 有正確資料

### Tests for User Story 1 (TDD Red Phase)

> **NOTE: 先撰寫測試，確認測試失敗，再進行實作**

- [x] T005 [US1] 建立 `tests/ReleaseKit.Application.Tests/Tasks/FetchGitLabReleaseBranchTaskTests.cs`，撰寫以下測試案例（此時測試應全部失敗）:
  1. `FetchGitLabReleaseBranchTask_ExecuteAsync_WithEmptyProjects_ShouldCompleteSuccessfully` — 空專案清單應正常完成
  2. `FetchGitLabReleaseBranchTask_ExecuteAsync_WithProjectsHavingReleaseBranches_ShouldGroupByBranchName` — 有 release branch 的專案應依分支名稱分組
  3. `FetchGitLabReleaseBranchTask_ExecuteAsync_WithProjectsWithoutReleaseBranches_ShouldAddToNotFound` — 無 release branch 的專案應歸入 NotFound
  4. `FetchGitLabReleaseBranchTask_ExecuteAsync_WithMultipleReleaseBranches_ShouldPickLatest` — 多個 release branch 應取字母排序最大的
  5. `FetchGitLabReleaseBranchTask_ExecuteAsync_WithGetBranchesFailure_ShouldAddToNotFound` — GetBranchesAsync 失敗時應歸入 NotFound
  - Mock 設定: `ISourceControlRepository.GetBranchesAsync` 回傳 `Result<IReadOnlyList<string>>`、`IRedisService` mock、`ILogger` mock、`GitLabOptions` with Projects
  - 測試模式參考: `tests/ReleaseKit.Application.Tests/Tasks/TasksTests.cs` 中的 `FetchGitLabPullRequestsTask` 測試
  - 建置: ✅ 可建置 | 測試: ❌ 失敗（Red Phase — 預期行為）

### Implementation for User Story 1 (TDD Green Phase)

- [x] T006 [US1] 建立 `src/ReleaseKit.Application/Tasks/FetchGitLabReleaseBranchTask.cs`，繼承 `BaseFetchReleaseBranchTask<GitLabOptions, GitLabProjectOptions>`
  - 建構子接收: `IServiceProvider`、`ILogger<FetchGitLabReleaseBranchTask>`、`IRedisService`、`IOptions<GitLabOptions>`
  - 透過 `serviceProvider.GetRequiredKeyedService<ISourceControlRepository>("GitLab")` 取得 Repository
  - `PlatformName` = `"GitLab"`
  - `RedisKey` = `RedisKeys.GitLabReleaseBranches`
  - `GetProjects()` = `PlatformOptions.Projects`
  - 所有公開類別與方法加入 XML summary 註解（繁體中文）
  - 建置: ✅ 可建置 | 測試: ✅ 通過（T005 測試應全部通過）

- [x] T007 [US1] 建立 `tests/ReleaseKit.Application.Tests/Tasks/ReleaseBranchRedisIntegrationTests.cs`，撰寫 GitLab Redis 整合測試（TDD Red → Green）:
  1. `FetchGitLabReleaseBranchTask_ShouldClearOldRedisData_WhenDataExists` — 有舊資料時應先清除
  2. `FetchGitLabReleaseBranchTask_ShouldNotDeleteRedisData_WhenNoDataExists` — 無舊資料時不應呼叫 Delete
  3. `FetchGitLabReleaseBranchTask_ShouldSaveDataToRedis_AfterFetch` — 擷取後應存入 Redis
  4. `FetchGitLabReleaseBranchTask_ShouldUseCorrectRedisKey` — 應使用 `GitLab:ReleaseBranches` key，不應使用 Bitbucket key
  - 測試模式參考: `tests/ReleaseKit.Application.Tests/Tasks/RedisIntegrationTests.cs`
  - 建置: ✅ 可建置 | 測試: ✅ 通過

- [x] T008 [US1] 在 `tests/ReleaseKit.Application.Tests/Tasks/TaskFactoryTests.cs` 新增測試（TDD Red → Green）:
  1. `CreateTask_WithFetchGitLabReleaseBranches_ShouldReturnCorrectTaskType` — 驗證 TaskFactory 能建立 FetchGitLabReleaseBranchTask
  - 更新 `TaskFactoryTests` 建構子，註冊 `FetchGitLabReleaseBranchTask`
  - 同時在 `src/ReleaseKit.Application/Tasks/TaskFactory.cs` 新增 `TaskType.FetchGitLabReleaseBranches` case
  - 建置: ✅ 可建置 | 測試: ✅ 通過

- [x] T009 [US1] 在 `tests/ReleaseKit.Console.Tests/Parsers/CommandLineParserTests.cs` 新增測試（TDD Red → Green）:
  1. 在 `Parse_WithValidTaskName_ShouldReturnSuccessWithCorrectTaskType` Theory 新增 `[InlineData("fetch-gitlab-release-branch", TaskType.FetchGitLabReleaseBranches)]`
  2. 在 `Parse_WithValidTaskName_ShouldBeCaseInsensitive` Theory 新增 `[InlineData("FETCH-GITLAB-RELEASE-BRANCH", TaskType.FetchGitLabReleaseBranches)]`
  3. 更新 `Parse_WithInvalidTaskName_ShouldShowValidTasks` 驗證包含 `fetch-gitlab-release-branch`
  - 同時在 `src/ReleaseKit.Console/Parsers/CommandLineParser.cs` 新增 `{ "fetch-gitlab-release-branch", TaskType.FetchGitLabReleaseBranches }` 對應
  - 建置: ✅ 可建置 | 測試: ✅ 通過

- [x] T010 [US1] 在 `src/ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs` 的 `AddApplicationServices` 方法中註冊 `services.AddTransient<FetchGitLabReleaseBranchTask>()`
  - 建置: ✅ 可建置 | 測試: ✅ 通過

**Checkpoint**: User Story 1 完成。可獨立執行 `fetch-gitlab-release-branch` 指令並驗證功能

---

## Phase 4: User Story 2 - 取得 Bitbucket 各專案最新 Release Branch (Priority: P1)

**Goal**: 使用者可執行 `fetch-bitbucket-release-branch` 指令，查詢所有 Bitbucket 專案的最新 release branch，功能與 GitLab 對稱

**Independent Test**: 執行 `fetch-bitbucket-release-branch` 指令，驗證 Console 輸出正確 JSON 格式，Redis 中 `Bitbucket:ReleaseBranches` key 有正確資料

### Tests for User Story 2 (TDD Red Phase)

> **NOTE: 先撰寫測試，確認測試失敗，再進行實作**

- [x] T011 [US2] 建立 `tests/ReleaseKit.Application.Tests/Tasks/FetchBitbucketReleaseBranchTaskTests.cs`，撰寫以下測試案例（此時測試應全部失敗）:
  1. `FetchBitbucketReleaseBranchTask_ExecuteAsync_WithEmptyProjects_ShouldCompleteSuccessfully` — 空專案清單應正常完成
  2. `FetchBitbucketReleaseBranchTask_ExecuteAsync_WithProjectsHavingReleaseBranches_ShouldGroupByBranchName` — 有 release branch 的專案應依分支名稱分組
  3. `FetchBitbucketReleaseBranchTask_ExecuteAsync_WithProjectsWithoutReleaseBranches_ShouldAddToNotFound` — 無 release branch 的專案應歸入 NotFound
  4. `FetchBitbucketReleaseBranchTask_ExecuteAsync_WithMultipleReleaseBranches_ShouldPickLatest` — 多個 release branch 應取字母排序最大的
  5. `FetchBitbucketReleaseBranchTask_ExecuteAsync_WithGetBranchesFailure_ShouldAddToNotFound` — GetBranchesAsync 失敗時應歸入 NotFound
  - Mock 設定: `ISourceControlRepository.GetBranchesAsync`、`IRedisService`、`ILogger`、`BitbucketOptions` with Projects
  - 測試模式參考: `tests/ReleaseKit.Application.Tests/Tasks/TasksTests.cs` 中的 `FetchBitbucketPullRequestsTask` 測試
  - 建置: ✅ 可建置 | 測試: ❌ 失敗（Red Phase — 預期行為）

### Implementation for User Story 2 (TDD Green Phase)

- [x] T012 [US2] 建立 `src/ReleaseKit.Application/Tasks/FetchBitbucketReleaseBranchTask.cs`，繼承 `BaseFetchReleaseBranchTask<BitbucketOptions, BitbucketProjectOptions>`
  - 建構子接收: `IServiceProvider`、`ILogger<FetchBitbucketReleaseBranchTask>`、`IRedisService`、`IOptions<BitbucketOptions>`
  - 透過 `serviceProvider.GetRequiredKeyedService<ISourceControlRepository>("Bitbucket")` 取得 Repository
  - `PlatformName` = `"Bitbucket"`
  - `RedisKey` = `RedisKeys.BitbucketReleaseBranches`
  - `GetProjects()` = `PlatformOptions.Projects`
  - 所有公開類別與方法加入 XML summary 註解（繁體中文）
  - 建置: ✅ 可建置 | 測試: ✅ 通過（T011 測試應全部通過）

- [x] T013 [US2] 在 `tests/ReleaseKit.Application.Tests/Tasks/ReleaseBranchRedisIntegrationTests.cs` 新增 Bitbucket Redis 整合測試（TDD Red → Green）:
  1. `FetchBitbucketReleaseBranchTask_ShouldClearOldRedisData_WhenDataExists`
  2. `FetchBitbucketReleaseBranchTask_ShouldNotDeleteRedisData_WhenNoDataExists`
  3. `FetchBitbucketReleaseBranchTask_ShouldSaveDataToRedis_AfterFetch`
  4. `FetchBitbucketReleaseBranchTask_ShouldUseCorrectRedisKey` — 應使用 `Bitbucket:ReleaseBranches` key，不應使用 GitLab key
  - 建置: ✅ 可建置 | 測試: ✅ 通過

- [x] T014 [US2] 在 `tests/ReleaseKit.Application.Tests/Tasks/TaskFactoryTests.cs` 新增測試（TDD Red → Green）:
  1. `CreateTask_WithFetchBitbucketReleaseBranches_ShouldReturnCorrectTaskType`
  - 更新 `TaskFactoryTests` 建構子，註冊 `FetchBitbucketReleaseBranchTask`
  - 同時在 `src/ReleaseKit.Application/Tasks/TaskFactory.cs` 新增 `TaskType.FetchBitbucketReleaseBranches` case
  - 建置: ✅ 可建置 | 測試: ✅ 通過

- [x] T015 [US2] 在 `tests/ReleaseKit.Console.Tests/Parsers/CommandLineParserTests.cs` 新增測試（TDD Red → Green）:
  1. 在 `Parse_WithValidTaskName_ShouldReturnSuccessWithCorrectTaskType` Theory 新增 `[InlineData("fetch-bitbucket-release-branch", TaskType.FetchBitbucketReleaseBranches)]`
  2. 在 `Parse_WithValidTaskName_ShouldBeCaseInsensitive` Theory 新增 `[InlineData("FETCH-BITBUCKET-RELEASE-BRANCH", TaskType.FetchBitbucketReleaseBranches)]`
  3. 更新 `Parse_WithInvalidTaskName_ShouldShowValidTasks` 驗證包含 `fetch-bitbucket-release-branch`
  - 同時在 `src/ReleaseKit.Console/Parsers/CommandLineParser.cs` 新增 `{ "fetch-bitbucket-release-branch", TaskType.FetchBitbucketReleaseBranches }` 對應
  - 建置: ✅ 可建置 | 測試: ✅ 通過

- [x] T016 [US2] 在 `src/ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs` 的 `AddApplicationServices` 方法中註冊 `services.AddTransient<FetchBitbucketReleaseBranchTask>()`
  - 建置: ✅ 可建置 | 測試: ✅ 通過

**Checkpoint**: User Story 2 完成。可獨立執行 `fetch-bitbucket-release-branch` 指令並驗證功能

---

## Phase 5: User Story 3 - 查詢結果依 Release Branch 名稱分組 (Priority: P2)

**Goal**: 驗證查詢結果的分組邏輯正確性，包含多專案同分支、不同分支、NotFound 等邊界情境

**Independent Test**: 驗證輸出 JSON 結構為 `{ "release/YYYYMMDD": ["ProjectPath1", ...], "NotFound": [...] }` 格式，且分組邏輯正確

### Tests for User Story 3 (分組邏輯邊界案例驗證)

- [x] T017 [P] [US3] 在 `tests/ReleaseKit.Application.Tests/Tasks/FetchGitLabReleaseBranchTaskTests.cs` 新增分組邊界案例測試（TDD Red → Green）:
  1. `FetchGitLabReleaseBranchTask_ExecuteAsync_WithMultipleProjectsSameBranch_ShouldGroupTogether` — 多個專案有相同最新 release branch 時應歸在同一組
  2. `FetchGitLabReleaseBranchTask_ExecuteAsync_WithMixedResults_ShouldGroupCorrectly` — 混合情境（有分支 + 無分支 + 錯誤）應正確分組
  3. `FetchGitLabReleaseBranchTask_ExecuteAsync_OutputJson_ShouldMatchExpectedFormat` — 驗證序列化後的 JSON 結構與預期格式一致
  - 建置: ✅ 可建置 | 測試: ✅ 通過（分組邏輯已在 Phase 2 BaseFetchReleaseBranchTask 中實作）

- [x] T018 [P] [US3] 在 `tests/ReleaseKit.Application.Tests/Tasks/FetchBitbucketReleaseBranchTaskTests.cs` 新增分組邊界案例測試（TDD Red → Green）:
  1. `FetchBitbucketReleaseBranchTask_ExecuteAsync_WithMultipleProjectsSameBranch_ShouldGroupTogether`
  2. `FetchBitbucketReleaseBranchTask_ExecuteAsync_WithMixedResults_ShouldGroupCorrectly`
  - 建置: ✅ 可建置 | 測試: ✅ 通過

**Checkpoint**: 所有 User Story 完成，分組邏輯經完整驗證

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: 建置驗證、全量測試、品質確認

- [x] T019 執行 `dotnet build` 驗證整體方案可正確建置
  - 建置: ✅ 可建置

- [x] T020 執行 `dotnet test` 驗證所有測試通過（含既有測試與新增測試）
  - 測試: ✅ 通過

- [x] T021 依 `specs/002-fetch-release-branch/quickstart.md` 驗證使用方式描述與實際行為一致

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 completion — BLOCKS all user stories
- **US1 (Phase 3)**: Depends on Phase 2 — can start after Foundational
- **US2 (Phase 4)**: Depends on Phase 2 — can start after Foundational (parallel with US1)
- **US3 (Phase 5)**: Depends on Phase 3 AND Phase 4 completion
- **Polish (Phase 6)**: Depends on all phases completion

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Phase 2 — No dependencies on other stories
- **User Story 2 (P1)**: Can start after Phase 2 — No dependencies on other stories, **can run in parallel with US1**
- **User Story 3 (P2)**: Depends on US1 AND US2 concrete task classes existing（需要具體任務類別才能撰寫邊界測試）

### Within Each User Story

1. Tests (Red Phase) MUST be written and FAIL before implementation
2. Implementation (Green Phase) makes tests pass
3. Integration tasks (TaskFactory, CommandLineParser, DI) follow
4. Story complete before moving to next priority

### Parallel Opportunities

- **Phase 1**: T001, T002, T003 can all run in parallel (different files)
- **Phase 3 + Phase 4**: US1 and US2 can run in parallel after Phase 2
- **Phase 5**: T017 and T018 can run in parallel (different test files)

---

## Parallel Example: Phase 1 (Setup)

```bash
# 三個任務可同時進行（不同檔案，無相依性）:
T001: "在 RedisKeys.cs 新增常數"
T002: "在 TaskType.cs 新增列舉值"
T003: "建立 ReleaseBranchResult.cs"
```

## Parallel Example: US1 + US2 (after Phase 2)

```bash
# 兩個 User Story 可同時進行（對稱結構，不同檔案）:
Developer A: US1 (T005 → T006 → T007 → T008 → T009 → T010)
Developer B: US2 (T011 → T012 → T013 → T014 → T015 → T016)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T003)
2. Complete Phase 2: Foundational (T004)
3. Complete Phase 3: User Story 1 (T005-T010)
4. **STOP and VALIDATE**: 執行 `dotnet test`，驗證 `fetch-gitlab-release-branch` 可正確運作
5. 可先部署 GitLab 版本

### Incremental Delivery

1. Setup + Foundational → 基礎就緒
2. User Story 1 → 獨立測試 → 部署（MVP!）
3. User Story 2 → 獨立測試 → 部署（Bitbucket 支援）
4. User Story 3 → 邊界驗證 → 完整功能
5. Polish → 品質確認

### Parallel Team Strategy

With multiple developers:

1. 團隊共同完成 Setup + Foundational
2. Foundational 完成後:
   - Developer A: User Story 1 (GitLab)
   - Developer B: User Story 2 (Bitbucket)
3. 各 Story 獨立完成後合併

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- TDD 為強制性：每個任務標註 Red/Green 階段
- 每個任務完成後標註建置與測試狀態
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
