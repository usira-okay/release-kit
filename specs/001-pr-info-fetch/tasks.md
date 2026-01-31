# Tasks: GitLab / Bitbucket PR 資訊擷取

**Input**: Design documents from `/specs/001-pr-info-fetch/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ISourceControlRepository.md

**Tests**: Tests are included as per TDD approach specified in the Constitution.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and Result Pattern foundation

- [X] T001 Create Result<T> class in src/ReleaseKit.Domain/Common/Result.cs
- [X] T002 [P] Create Error sealed record in src/ReleaseKit.Domain/Common/Error.cs
- [X] T003 [P] Create SourceControlPlatform enum in src/ReleaseKit.Domain/ValueObjects/SourceControlPlatform.cs
- [X] T004 [P] Add TargetBranch property to FetchModeOptions in src/ReleaseKit.Infrastructure/Configuration/FetchModeOptions.cs

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T005 Create MergeRequest entity in src/ReleaseKit.Domain/Entities/MergeRequest.cs
- [X] T006 Create ISourceControlRepository interface in src/ReleaseKit.Domain/Abstractions/ISourceControlRepository.cs
- [X] T007 [P] Create GitLabMergeRequestResponse model in src/ReleaseKit.Infrastructure/SourceControl/GitLab/Models/GitLabMergeRequestResponse.cs
- [X] T008 [P] Create GitLabAuthorResponse model in src/ReleaseKit.Infrastructure/SourceControl/GitLab/Models/GitLabAuthorResponse.cs
- [X] T009 [P] Create GitLabCompareResponse model in src/ReleaseKit.Infrastructure/SourceControl/GitLab/Models/GitLabCompareResponse.cs
- [X] T010 [P] Create GitLabCommitResponse model in src/ReleaseKit.Infrastructure/SourceControl/GitLab/Models/GitLabCommitResponse.cs
- [X] T011 [P] Create BitbucketPullRequestResponse model in src/ReleaseKit.Infrastructure/SourceControl/Bitbucket/Models/BitbucketPullRequestResponse.cs
- [X] T012 [P] Create BitbucketSummaryResponse model in src/ReleaseKit.Infrastructure/SourceControl/Bitbucket/Models/BitbucketSummaryResponse.cs
- [X] T013 [P] Create BitbucketBranchRefResponse model in src/ReleaseKit.Infrastructure/SourceControl/Bitbucket/Models/BitbucketBranchRefResponse.cs
- [X] T014 [P] Create BitbucketBranchResponse model in src/ReleaseKit.Infrastructure/SourceControl/Bitbucket/Models/BitbucketBranchResponse.cs
- [X] T015 [P] Create BitbucketAuthorResponse model in src/ReleaseKit.Infrastructure/SourceControl/Bitbucket/Models/BitbucketAuthorResponse.cs
- [X] T016 [P] Create BitbucketLinksResponse model in src/ReleaseKit.Infrastructure/SourceControl/Bitbucket/Models/BitbucketLinksResponse.cs
- [X] T017 [P] Create BitbucketLinkResponse model in src/ReleaseKit.Infrastructure/SourceControl/Bitbucket/Models/BitbucketLinkResponse.cs
- [X] T018 [P] Create BitbucketPageResponse<T> model in src/ReleaseKit.Infrastructure/SourceControl/Bitbucket/Models/BitbucketPageResponse.cs
- [X] T019 Create GitLabMergeRequestMapper in src/ReleaseKit.Infrastructure/SourceControl/GitLab/GitLabMergeRequestMapper.cs
- [X] T020 [P] Create BitbucketPullRequestMapper in src/ReleaseKit.Infrastructure/SourceControl/Bitbucket/BitbucketPullRequestMapper.cs
- [X] T021 Register HttpClient factories in src/ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - 依時間區間擷取已合併的 PR (Priority: P1) 🎯 MVP

**Goal**: 實作 DateTimeRange 模式，使用者可透過指定時間區間與目標分支，取得該期間內所有已合併的 PR 清單

**Independent Test**: 透過 Mock HttpClient 驗證 API 呼叫與時間過濾邏輯正確

### Tests for User Story 1 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T022 [P] [US1] Unit test for Result<T> in tests/ReleaseKit.Domain.Tests/Common/ResultTests.cs
- [X] T023 [P] [US1] Unit test for Error in tests/ReleaseKit.Domain.Tests/Common/ErrorTests.cs
- [X] T024 [P] [US1] Unit test for MergeRequest entity in tests/ReleaseKit.Domain.Tests/Entities/MergeRequestTests.cs
- [X] T025 [P] [US1] Unit test for GitLabMergeRequestMapper in tests/ReleaseKit.Infrastructure.Tests/SourceControl/GitLab/GitLabMergeRequestMapperTests.cs
- [X] T026 [P] [US1] Unit test for GitLabRepository.GetMergeRequestsByDateRangeAsync in tests/ReleaseKit.Infrastructure.Tests/SourceControl/GitLab/GitLabRepositoryTests.cs

### Implementation for User Story 1

- [X] T027 [US1] Implement GitLabRepository constructor with IHttpClientFactory in src/ReleaseKit.Infrastructure/SourceControl/GitLab/GitLabRepository.cs
- [X] T028 [US1] Implement GetMergeRequestsByDateRangeAsync in GitLabRepository with pagination handling in src/ReleaseKit.Infrastructure/SourceControl/GitLab/GitLabRepository.cs
- [X] T029 [US1] Implement secondary filtering by merged_at in GitLabRepository.GetMergeRequestsByDateRangeAsync in src/ReleaseKit.Infrastructure/SourceControl/GitLab/GitLabRepository.cs
- [X] T030 [US1] Register GitLabRepository as keyed service in src/ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs
- [X] T031 [US1] Implement FetchGitLabPullRequestsTask DateTimeRange mode in src/ReleaseKit.Application/Tasks/FetchGitLabPullRequestsTask.cs

**Checkpoint**: User Story 1 (GitLab DateTimeRange) should be fully functional and testable independently

---

## Phase 4: User Story 2 - 支援 GitLab 與 Bitbucket 雙平台 (Priority: P1)

**Goal**: 新增 Bitbucket 平台支援，確保輸出格式與 GitLab 一致 (closed_on 映射到 MergedAt)

**Independent Test**: 透過 Mock HttpClient 驗證 Bitbucket API 呼叫與欄位映射正確

### Tests for User Story 2 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T032 [P] [US2] Unit test for BitbucketPullRequestMapper in tests/ReleaseKit.Infrastructure.Tests/SourceControl/Bitbucket/BitbucketPullRequestMapperTests.cs
- [X] T033 [P] [US2] Unit test for BitbucketRepository.GetMergeRequestsByDateRangeAsync in tests/ReleaseKit.Infrastructure.Tests/SourceControl/Bitbucket/BitbucketRepositoryTests.cs

### Implementation for User Story 2

- [X] T034 [US2] Implement BitbucketRepository constructor with IHttpClientFactory in src/ReleaseKit.Infrastructure/SourceControl/Bitbucket/BitbucketRepository.cs
- [X] T035 [US2] Implement GetMergeRequestsByDateRangeAsync in BitbucketRepository with cursor-based pagination in src/ReleaseKit.Infrastructure/SourceControl/Bitbucket/BitbucketRepository.cs
- [X] T036 [US2] Implement secondary filtering by closed_on in BitbucketRepository.GetMergeRequestsByDateRangeAsync in src/ReleaseKit.Infrastructure/SourceControl/Bitbucket/BitbucketRepository.cs
- [X] T037 [US2] Register BitbucketRepository as keyed service in src/ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs
- [X] T038 [US2] Implement FetchBitbucketPullRequestsTask DateTimeRange mode in src/ReleaseKit.Application/Tasks/FetchBitbucketPullRequestsTask.cs

**Checkpoint**: User Story 2 (Bitbucket DateTimeRange) should be fully functional with consistent output format

---

## Phase 5: User Story 3 - 依分支差異擷取相關 PR (Priority: P2)

**Goal**: 實作 BranchDiff 模式，比較兩分支差異並查詢對應的 PR

**Independent Test**: 透過 Mock HttpClient 驗證 Compare API 與 Commit-to-MR 查詢正確

### Tests for User Story 3 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T039 [P] [US3] Unit test for GitLabRepository.GetBranchesAsync in tests/ReleaseKit.Infrastructure.Tests/SourceControl/GitLab/GitLabRepositoryTests.cs
- [ ] T040 [P] [US3] Unit test for GitLabRepository.GetMergeRequestsByBranchDiffAsync in tests/ReleaseKit.Infrastructure.Tests/SourceControl/GitLab/GitLabRepositoryTests.cs
- [ ] T041 [P] [US3] Unit test for GitLabRepository.GetMergeRequestsByCommitAsync in tests/ReleaseKit.Infrastructure.Tests/SourceControl/GitLab/GitLabRepositoryTests.cs
- [ ] T042 [P] [US3] Unit test for BitbucketRepository.GetBranchesAsync in tests/ReleaseKit.Infrastructure.Tests/SourceControl/Bitbucket/BitbucketRepositoryTests.cs
- [ ] T043 [P] [US3] Unit test for BitbucketRepository.GetMergeRequestsByBranchDiffAsync in tests/ReleaseKit.Infrastructure.Tests/SourceControl/Bitbucket/BitbucketRepositoryTests.cs
- [ ] T044 [P] [US3] Unit test for BitbucketRepository.GetMergeRequestsByCommitAsync in tests/ReleaseKit.Infrastructure.Tests/SourceControl/Bitbucket/BitbucketRepositoryTests.cs

### Implementation for User Story 3

- [ ] T045 [US3] Implement GetBranchesAsync in GitLabRepository in src/ReleaseKit.Infrastructure/SourceControl/GitLab/GitLabRepository.cs
- [ ] T046 [US3] Implement GetMergeRequestsByCommitAsync in GitLabRepository in src/ReleaseKit.Infrastructure/SourceControl/GitLab/GitLabRepository.cs
- [ ] T047 [US3] Implement GetMergeRequestsByBranchDiffAsync in GitLabRepository in src/ReleaseKit.Infrastructure/SourceControl/GitLab/GitLabRepository.cs
- [ ] T048 [P] [US3] Implement GetBranchesAsync in BitbucketRepository in src/ReleaseKit.Infrastructure/SourceControl/Bitbucket/BitbucketRepository.cs
- [ ] T049 [P] [US3] Implement GetMergeRequestsByCommitAsync in BitbucketRepository in src/ReleaseKit.Infrastructure/SourceControl/Bitbucket/BitbucketRepository.cs
- [ ] T050 [US3] Implement GetMergeRequestsByBranchDiffAsync in BitbucketRepository in src/ReleaseKit.Infrastructure/SourceControl/Bitbucket/BitbucketRepository.cs
- [ ] T051 [US3] Implement FetchGitLabPullRequestsTask BranchDiff mode in src/ReleaseKit.Application/Tasks/FetchGitLabPullRequestsTask.cs
- [ ] T052 [US3] Implement FetchBitbucketPullRequestsTask BranchDiff mode in src/ReleaseKit.Application/Tasks/FetchBitbucketPullRequestsTask.cs

**Checkpoint**: User Story 3 (BranchDiff mode) should be fully functional for both platforms

---

## Phase 6: User Story 4 - 多專案批次擷取 (Priority: P2)

**Goal**: 支援多專案批次執行，實作階層式設定覆蓋 (專案層級優先於根層級)

**Independent Test**: 透過設定多個專案驗證設定繼承與覆蓋邏輯

### Tests for User Story 4 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T053 [P] [US4] Unit test for configuration inheritance logic in tests/ReleaseKit.Application.Tests/Tasks/ConfigurationInheritanceTests.cs
- [ ] T054 [P] [US4] Integration test for multi-project execution in tests/ReleaseKit.Application.Tests/Tasks/MultiProjectExecutionTests.cs

### Implementation for User Story 4

- [ ] T055 [US4] Implement configuration inheritance helper in src/ReleaseKit.Application/Common/ConfigurationHelper.cs
- [ ] T056 [US4] Implement multi-project iteration logic in FetchGitLabPullRequestsTask in src/ReleaseKit.Application/Tasks/FetchGitLabPullRequestsTask.cs
- [ ] T057 [US4] Implement multi-project iteration logic in FetchBitbucketPullRequestsTask in src/ReleaseKit.Application/Tasks/FetchBitbucketPullRequestsTask.cs
- [ ] T058 [US4] Create FetchResult output model in src/ReleaseKit.Application/Common/FetchResult.cs
- [ ] T059 [US4] Create ProjectResult output model in src/ReleaseKit.Application/Common/ProjectResult.cs
- [ ] T060 [US4] Create MergeRequestOutput output model in src/ReleaseKit.Application/Common/MergeRequestOutput.cs

**Checkpoint**: User Story 4 (multi-project batch) should be fully functional with correct config inheritance

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] T061 [P] Add XML documentation comments to ISourceControlRepository in src/ReleaseKit.Domain/Abstractions/ISourceControlRepository.cs
- [ ] T062 [P] Add XML documentation comments to Result<T> and Error in src/ReleaseKit.Domain/Common/
- [ ] T063 [P] Add XML documentation comments to MergeRequest entity in src/ReleaseKit.Domain/Entities/MergeRequest.cs
- [ ] T064 [P] Add logging for API calls in GitLabRepository in src/ReleaseKit.Infrastructure/SourceControl/GitLab/GitLabRepository.cs
- [ ] T065 [P] Add logging for API calls in BitbucketRepository in src/ReleaseKit.Infrastructure/SourceControl/Bitbucket/BitbucketRepository.cs
- [ ] T066 Code review and refactoring for DRY principle across repositories
- [ ] T067 Run quickstart.md validation scenarios

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 completion - BLOCKS all user stories
- **User Story 1 (Phase 3)**: Depends on Phase 2 completion
- **User Story 2 (Phase 4)**: Depends on Phase 2 completion (can run in parallel with US1)
- **User Story 3 (Phase 5)**: Depends on US1 + US2 completion (uses existing repository implementations)
- **User Story 4 (Phase 6)**: Depends on US1 + US2 completion (uses existing repository implementations)
- **Polish (Phase 7)**: Depends on all user stories being complete

### User Story Dependencies

```
Phase 1 (Setup)
    ↓
Phase 2 (Foundational)
    ↓
┌───────────────────────────────────┐
│                                   │
↓                                   ↓
Phase 3 (US1: GitLab DateTimeRange) Phase 4 (US2: Bitbucket DateTimeRange)
│                                   │
└───────────────────────────────────┘
    ↓
┌───────────────────────────────────┐
│                                   │
↓                                   ↓
Phase 5 (US3: BranchDiff)           Phase 6 (US4: Multi-Project)
│                                   │
└───────────────────────────────────┘
    ↓
Phase 7 (Polish)
```

### Within Each User Story

- Tests (TDD) MUST be written and FAIL before implementation
- Implementation follows: Repository → Mapper → Task
- Verify tests pass after implementation

### Parallel Opportunities

Phase 1 (all tasks except T001 can run in parallel after T001):
```bash
# T001 first (Result<T> is foundation)
Task: T001 (Result<T>)
# Then in parallel:
Task: T002 (Error)
Task: T003 (SourceControlPlatform)
Task: T004 (FetchModeOptions)
```

Phase 2 (API response models can run in parallel):
```bash
# After T005-T006:
Task: T007, T008, T009, T010 (GitLab models in parallel)
Task: T011-T018 (Bitbucket models in parallel)
Task: T019, T020 (Mappers in parallel after models)
```

Phase 3 User Story 1 (tests in parallel):
```bash
Task: T022, T023, T024, T025, T026 (all tests in parallel)
# Then implementation sequentially: T027 → T028 → T029 → T030 → T031
```

Phase 4 User Story 2 (tests in parallel):
```bash
Task: T032, T033 (tests in parallel)
# Then implementation sequentially: T034 → T035 → T036 → T037 → T038
```

Phase 5 User Story 3 (tests in parallel, some implementation in parallel):
```bash
Task: T039-T044 (all tests in parallel)
# GitLab: T045 → T046 → T047
# Bitbucket (parallel with GitLab): T048, T049 → T050
# Tasks: T051, T052
```

---

## Implementation Strategy

### MVP First (User Story 1 + User Story 2)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1 (GitLab DateTimeRange)
4. Complete Phase 4: User Story 2 (Bitbucket DateTimeRange)
5. **STOP and VALIDATE**: Test both platforms with DateTimeRange mode
6. Deploy/demo if ready - This is the MVP!

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add User Story 1 → Test GitLab independently → Working MVP for GitLab
3. Add User Story 2 → Test Bitbucket independently → Full MVP (both platforms)
4. Add User Story 3 → Test BranchDiff mode → Enhanced functionality
5. Add User Story 4 → Test multi-project → Complete feature

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: User Story 1 (GitLab DateTimeRange)
   - Developer B: User Story 2 (Bitbucket DateTimeRange)
3. After US1 + US2:
   - Developer A: User Story 3 (BranchDiff)
   - Developer B: User Story 4 (Multi-Project)

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Verify tests fail before implementing (TDD Red-Green-Refactor)
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- All time handling uses DateTimeOffset in UTC as per Constitution
- Use Result Pattern instead of try-catch as per Constitution
