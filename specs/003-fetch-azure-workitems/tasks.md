# Tasks: Azure DevOps Work Item 資訊擷取

**Input**: Design documents from `/specs/003-fetch-azure-workitems/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: 本專案 Constitution 要求 TDD（不可妥協），所有功能實作必須遵循 Red-Green-Refactor 循環。

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3, US4)
- Include exact file paths in descriptions

---

## Phase 1: Foundational (Shared Types & Abstractions)

**Purpose**: 建立所有 User Story 共用的型別定義、介面、常數。此階段不含任何業務邏輯。

- [ ] T001 [P] Create WorkItem domain entity (sealed record with WorkItemId, Title, Type, State, Url, OriginalTeamName) in `src/ReleaseKit.Domain/Entities/WorkItem.cs`
- [ ] T002 [P] Create IAzureDevOpsRepository interface with GetWorkItemAsync(int workItemId) returning Task<Result<WorkItem>> in `src/ReleaseKit.Domain/Abstractions/IAzureDevOpsRepository.cs`
- [ ] T003 [P] Add AzureDevOps static error class (WorkItemNotFound, ApiError, Unauthorized) to `src/ReleaseKit.Domain/Common/Error.cs`
- [ ] T004 [P] Add AzureDevOpsWorkItems constant ("AzureDevOps:WorkItems") to `src/ReleaseKit.Common/Constants/RedisKeys.cs`
- [ ] T005 [P] Add AzureDevOps constant ("AzureDevOps") to `src/ReleaseKit.Common/Constants/HttpClientNames.cs`
- [ ] T006 [P] Create WorkItemOutput sealed record DTO (WorkItemId, Title?, Type?, State?, Url?, OriginalTeamName?, IsSuccess, ErrorMessage?) in `src/ReleaseKit.Application/Common/WorkItemOutput.cs`
- [ ] T007 [P] Create WorkItemFetchResult sealed record DTO (WorkItems list, TotalPRsAnalyzed, TotalWorkItemsFound, SuccessCount, FailureCount) in `src/ReleaseKit.Application/Common/WorkItemFetchResult.cs`

**Checkpoint**: ✅ 可建置 / ⚠️ 待補測試

---

## Phase 2: User Story 2 - 呼叫 Azure DevOps API 取得 Work Item 詳細資訊 (Priority: P1)

**Goal**: 建立獨立可測試的 Azure DevOps API Client，透過 REST API 取得 Work Item 詳細資訊。

**Independent Test**: 透過模擬 HTTP Response 驗證 Repository 正確處理成功回應、404、401、其他 HTTP 錯誤。

### Infrastructure Models & Mapper

- [ ] T008 [P] [US2] Create AzureDevOpsWorkItemResponse, AzureDevOpsLinksResponse, AzureDevOpsLinkResponse API response models (with [JsonPropertyName] for external API contract) in `src/ReleaseKit.Infrastructure/AzureDevOps/Models/AzureDevOpsWorkItemResponse.cs`
- [ ] T009 [P] [US2] Create AzureDevOpsWorkItemMapper with static ToDomain method (map API fields to WorkItem entity: id→WorkItemId, System.Title→Title, System.WorkItemType→Type, System.State→State, _links.html.href→Url, System.AreaPath→OriginalTeamName) in `src/ReleaseKit.Infrastructure/AzureDevOps/Mappers/AzureDevOpsWorkItemMapper.cs`

### Tests (RED)

- [ ] T010 [US2] Write AzureDevOpsRepository unit tests using Moq HttpMessageHandler: test success response mapping, 401 returns Unauthorized error, 404 returns WorkItemNotFound error, other HTTP errors return ApiError, in `tests/ReleaseKit.Infrastructure.Tests/AzureDevOps/AzureDevOpsRepositoryTests.cs`

### Implementation (GREEN)

- [ ] T011 [US2] Implement AzureDevOpsRepository: inject IHttpClientFactory, call GET _apis/wit/workitems/{id}?$expand=all&api-version=7.0, handle status codes with Result Pattern, use Mapper for response conversion, in `src/ReleaseKit.Infrastructure/AzureDevOps/AzureDevOpsRepository.cs`

### DI Registration

- [ ] T012 [US2] Register Azure DevOps Named HttpClient (BaseAddress from AzureDevOpsOptions.OrganizationUrl, Basic Auth header with PAT) and IAzureDevOpsRepository→AzureDevOpsRepository in `src/ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs`

**Checkpoint**: ✅ 可建置 / ✅ 測試通過 (AzureDevOpsRepositoryTests)

---

## Phase 3: User Story 1 - 從 PR 標題解析 VSTS Work Item ID (Priority: P1) 🎯 MVP

**Goal**: 實作完整的 FetchAzureDevOpsWorkItemsTask，從 Redis 讀取 PR 資料、解析 VSTS ID、呼叫 API、組裝結果。

**Independent Test**: 透過 Mock IRedisService 與 IAzureDevOpsRepository 驗證解析邏輯與完整流程。

### Tests (RED)

- [X] T013 [US1] Write FetchAzureDevOpsWorkItemsTask unit tests for VSTS ID parsing and full flow: (1) single VSTS ID in title, (2) multiple VSTS IDs in one title (VSTS111 and VSTS222), (3) dedup same ID across multiple PRs, (4) no VSTS ID in title returns empty, (5) invalid formats (VSTSabc, vsts123, VSTS without number) are ignored, (6) successful end-to-end: Redis read → parse → API call → Redis write, in `tests/ReleaseKit.Application.Tests/Tasks/FetchAzureDevOpsWorkItemsTaskTests.cs`

### Implementation (GREEN)

- [X] T014 [US1] Implement FetchAzureDevOpsWorkItemsTask.ExecuteAsync: inject IRedisService + IAzureDevOpsRepository + ILogger, read from Redis keys (GitLab:PullRequests:ByUser, Bitbucket:PullRequests:ByUser), deserialize as FetchResult using ToTypedObject, parse all PR titles with Regex VSTS(\d+), deduplicate IDs with HashSet, call GetWorkItemAsync for each ID sequentially, map Result to WorkItemOutput (success/failure), assemble WorkItemFetchResult with statistics, write to Redis AzureDevOps:WorkItems using ToJson (no TTL), output to Console, in `src/ReleaseKit.Application/Tasks/FetchAzureDevOpsWorkItemsTask.cs`

**Checkpoint**: ✅ 可建置 / ✅ 測試通過 (FetchAzureDevOpsWorkItemsTaskTests - parsing + flow)

---

## Phase 4: User Story 3 - 將結果儲存至 Redis 並輸出摘要 (Priority: P2)

**Goal**: 驗證結果輸出格式符合 Redis Output Contract，統計數據正確。

**Independent Test**: 透過 Mock 驗證 Redis 寫入的 JSON 格式與統計數據正確性。

### Tests (RED → should be GREEN from T014 implementation)

- [X] T015 [US3] Write additional FetchAzureDevOpsWorkItemsTask tests for output: (1) Redis write JSON matches contract format (camelCase, all fields), (2) statistics correctly count success/failure, (3) TotalPRsAnalyzed counts all PRs from both platforms, (4) Redis SetAsync called with key AzureDevOps:WorkItems and no TTL (null expiry), (5) mixed success and failure WorkItemOutputs have correct IsSuccess and ErrorMessage, in `tests/ReleaseKit.Application.Tests/Tasks/FetchAzureDevOpsWorkItemsTaskTests.cs`

**Checkpoint**: ✅ 可建置 / ✅ 測試通過

---

## Phase 5: User Story 4 - 處理部分 Redis 資料不存在的情境 (Priority: P2)

**Goal**: 驗證系統在部分或全部 Redis 資料缺失時的容錯行為。

**Independent Test**: 透過 Mock 設定不同的 Redis key 存在情境來驗證。

### Tests (RED → should be GREEN from T014 implementation)

- [X] T016 [US4] Write additional FetchAzureDevOpsWorkItemsTask tests for partial data: (1) only GitLab key exists - processes GitLab PRs, logs warning for missing Bitbucket, (2) only Bitbucket key exists - processes Bitbucket PRs, logs warning for missing GitLab, (3) both keys missing - logs warning and exits gracefully without API calls, (4) one key returns null and other has data - processes available data, in `tests/ReleaseKit.Application.Tests/Tasks/FetchAzureDevOpsWorkItemsTaskTests.cs`

**Checkpoint**: ✅ 可建置 / ✅ 測試通過

---

## Phase 6: Polish & Validation

**Purpose**: 最終建置驗證與全部測試通過確認

- [X] T017 Execute full build verification with `dotnet build src/release-kit.sln` and confirm zero errors
- [X] T018 Execute all unit tests with `dotnet test` and confirm 100% pass rate across all test projects

**Checkpoint**: ✅ 可建置 / ✅ 測試通過 (全部)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Foundational)**: No dependencies - can start immediately
- **Phase 2 (US2 Repository)**: Depends on Phase 1 (needs WorkItem, IAzureDevOpsRepository, Error.AzureDevOps)
- **Phase 3 (US1 Task)**: Depends on Phase 1 + Phase 2 (needs all types + working Repository)
- **Phase 4 (US3 Output)**: Depends on Phase 3 (tests verify task output behavior)
- **Phase 5 (US4 Resilience)**: Depends on Phase 3 (tests verify task resilience behavior)
- **Phase 6 (Polish)**: Depends on all previous phases

### User Story Dependencies

- **US2 (API Repository)**: Can start after Phase 1 - independent component, no story dependencies
- **US1 (VSTS Parsing + Task)**: Depends on US2 (Task calls Repository)
- **US3 (Output)**: Depends on US1 (tests verify task output from US1 implementation)
- **US4 (Resilience)**: Depends on US1 (tests verify task resilience from US1 implementation)

### Within Each Phase

- Phase 1: All tasks [P] - can run in parallel (different files)
- Phase 2: T008+T009 [P] parallel → T010 (tests) → T011 (implementation) → T012 (DI)
- Phase 3: T013 (tests RED) → T014 (implementation GREEN)
- Phase 4: T015 (tests - should be GREEN)
- Phase 5: T016 (tests - should be GREEN)
- Phase 6: T017 → T018 (sequential)

### Parallel Opportunities

```
Phase 1 (all parallel):
  T001 ║ T002 ║ T003 ║ T004 ║ T005 ║ T006 ║ T007

Phase 2 (partial parallel):
  T008 ║ T009 → T010 → T011 → T012

Phase 3 (sequential TDD):
  T013 → T014

Phase 4+5 (can run in parallel after Phase 3):
  T015 ║ T016

Phase 6 (sequential):
  T017 → T018
```

---

## Parallel Example: Phase 1

```
Launch all foundational type tasks together:
Task T001: "Create WorkItem entity in src/ReleaseKit.Domain/Entities/WorkItem.cs"
Task T002: "Create IAzureDevOpsRepository in src/ReleaseKit.Domain/Abstractions/IAzureDevOpsRepository.cs"
Task T003: "Add AzureDevOps errors to src/ReleaseKit.Domain/Common/Error.cs"
Task T004: "Add RedisKeys constant to src/ReleaseKit.Common/Constants/RedisKeys.cs"
Task T005: "Add HttpClientNames constant to src/ReleaseKit.Common/Constants/HttpClientNames.cs"
Task T006: "Create WorkItemOutput in src/ReleaseKit.Application/Common/WorkItemOutput.cs"
Task T007: "Create WorkItemFetchResult in src/ReleaseKit.Application/Common/WorkItemFetchResult.cs"
```

## Parallel Example: Phase 2

```
Launch API models in parallel:
Task T008: "Create API response models in src/ReleaseKit.Infrastructure/AzureDevOps/Models/"
Task T009: "Create Mapper in src/ReleaseKit.Infrastructure/AzureDevOps/Mappers/"

Then sequential TDD:
Task T010: "Write Repository tests" → RED
Task T011: "Implement Repository" → GREEN
Task T012: "Register DI"
```

---

## Implementation Strategy

### MVP First (Phase 1 → 2 → 3)

1. Complete Phase 1: Foundational types
2. Complete Phase 2: US2 - Repository (TDD)
3. Complete Phase 3: US1 - Task implementation (TDD)
4. **STOP and VALIDATE**: Run `dotnet build` + `dotnet test` - MVP is functional

### Incremental Delivery

1. Phase 1 + 2 + 3 → MVP: Task reads PRs, parses IDs, calls API, outputs results
2. Phase 4 → Validate output format matches Redis contract
3. Phase 5 → Validate resilience for missing data
4. Phase 6 → Final validation

### Task Summary

| Phase | Story | Tasks | Parallel |
|-------|-------|-------|----------|
| 1 - Foundational | — | 7 | 全部可平行 |
| 2 - US2 Repository | P1 | 5 | T008+T009 可平行 |
| 3 - US1 Task | P1 | 2 | 循序 (TDD) |
| 4 - US3 Output | P2 | 1 | — |
| 5 - US4 Resilience | P2 | 1 | T015 ∥ T016 |
| 6 - Polish | — | 2 | 循序 |
| **Total** | | **18** | |

Per user story:
- US1: 2 tasks (T013, T014)
- US2: 5 tasks (T008-T012)
- US3: 1 task (T015)
- US4: 1 task (T016)

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Constitution 要求 TDD：每個 Phase 的測試必須先撰寫並確認失敗（RED），再實作使其通過（GREEN）
- Phase 4/5 的測試預期在 Phase 3 實作後已能通過；若不通過，回到 T014 補充實作
- All public members MUST have XML Summary comments in Traditional Chinese (zh-TW)
- API Response Model 使用 [JsonPropertyName] 是外部 API 契約例外（Constitution IX）
- 所有 JSON 序列化/反序列化使用 JsonExtensions（ToJson / ToTypedObject）
- Commit after each phase completion
