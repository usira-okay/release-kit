# Tasks: Filter Pull Requests by User

**Input**: Design documents from `/specs/001-filter-pr-by-user/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md

**Tests**: 包含測試任務（Constitution 第 I 條：TDD 為強制性開發流程）

**Organization**: 按 User Story 分組，每個 Story 可獨立實作與驗證。

## Format: `[ID] [P?] [Story] Description`

- **[P]**: 可平行執行（不同檔案，無依賴關係）
- **[Story]**: 所屬 User Story（US1、US2、US3）
- 包含確切檔案路徑

---

## Phase 1: Setup（共用基礎設施）

**Purpose**: 新增常數與列舉值，為後續實作提供基礎

- [ ] T001 [P] 在 `src/ReleaseKit.Common/Constants/RedisKeys.cs` 新增 `GitLabPullRequestsByUser` 和 `BitbucketPullRequestsByUser` 常數
- [ ] T002 [P] 在 `src/ReleaseKit.Application/Tasks/TaskType.cs` 新增 `FilterGitLabPullRequestsByUser` 和 `FilterBitbucketPullRequestsByUser` 列舉值

✅ 可建置 / ⚠️ 待補測試

---

## Phase 2: User Story 1 - 過濾 GitLab PR 資料中的指定使用者 (Priority: P1) 🎯 MVP

**Goal**: 從 Redis 讀取 GitLab PR 資料，依 UserMapping 的 GitLabUserId 過濾，將結果寫入 `GitLab:PullRequests:ByUser`

**Independent Test**: 在 Redis 中預先存入 GitLab PR 資料並執行 `filter-gitlab-pr-by-user`，驗證過濾結果僅包含指定使用者的 PR

### Tests for User Story 1 ⚠️

> **NOTE: 先撰寫測試並確認失敗，再進行實作（Red-Green-Refactor）**

- [ ] T003 [US1] 在 `tests/ReleaseKit.Application.Tests/Tasks/FilterPullRequestsByUserTaskTests.cs` 撰寫 GitLab 過濾測試：Redis 中有 PR 資料且使用者清單有匹配項，驗證過濾後僅保留匹配使用者的 PR
- [ ] T004 [US1] 在同一測試檔案撰寫 GitLab 多專案過濾測試：驗證多個 ProjectResult 各自獨立過濾
- [ ] T005 [US1] 在同一測試檔案撰寫 GitLab 過濾後寫入 Redis 測試：驗證結果寫入 `GitLab:PullRequests:ByUser` 且格式為 FetchResult

### Implementation for User Story 1

- [ ] T006 [US1] 建立 `src/ReleaseKit.Application/Tasks/BaseFilterPullRequestsByUserTask.cs` 抽象基底類別，封裝讀取 Redis → 反序列化 FetchResult → 過濾 PR → 序列化 → 寫入 Redis 與 stdout 的共用邏輯。子類別需提供：來源 Redis Key、目標 Redis Key、平台名稱、使用者 ID 清單
- [ ] T007 [US1] 建立 `src/ReleaseKit.Application/Tasks/FilterGitLabPullRequestsByUserTask.cs`，繼承基底類別，注入 `IOptions<UserMappingOptions>` 並從 `Mappings` 中提取非空的 `GitLabUserId` 清單，設定來源 Key 為 `RedisKeys.GitLabPullRequests`，目標 Key 為 `RedisKeys.GitLabPullRequestsByUser`

**Checkpoint**: 測試應由 Red 轉為 Green。執行建置與測試驗證。

✅ 可建置 / ✅ 測試通過

---

## Phase 3: User Story 2 - 過濾 Bitbucket PR 資料中的指定使用者 (Priority: P1)

**Goal**: 從 Redis 讀取 Bitbucket PR 資料，依 UserMapping 的 BitbucketUserId 過濾，將結果寫入 `Bitbucket:PullRequests:ByUser`

**Independent Test**: 在 Redis 中預先存入 Bitbucket PR 資料並執行 `filter-bitbucket-pr-by-user`，驗證過濾結果僅包含指定使用者的 PR

### Tests for User Story 2 ⚠️

> **NOTE: 先撰寫測試並確認失敗，再進行實作（Red-Green-Refactor）**

- [ ] T008 [US2] 在 `tests/ReleaseKit.Application.Tests/Tasks/FilterPullRequestsByUserTaskTests.cs` 撰寫 Bitbucket 過濾測試：Redis 中有 PR 資料且使用者清單有匹配項，驗證過濾後僅保留匹配使用者的 PR
- [ ] T009 [US2] 在同一測試檔案撰寫 Bitbucket 過濾後寫入 Redis 測試：驗證結果寫入 `Bitbucket:PullRequests:ByUser` 且格式為 FetchResult

### Implementation for User Story 2

- [ ] T010 [US2] 建立 `src/ReleaseKit.Application/Tasks/FilterBitbucketPullRequestsByUserTask.cs`，繼承基底類別，注入 `IOptions<UserMappingOptions>` 並從 `Mappings` 中提取非空的 `BitbucketUserId` 清單，設定來源 Key 為 `RedisKeys.BitbucketPullRequests`，目標 Key 為 `RedisKeys.BitbucketPullRequestsByUser`

**Checkpoint**: 測試應由 Red 轉為 Green。執行建置與測試驗證。

✅ 可建置 / ✅ 測試通過

---

## Phase 4: User Story 3 - 處理無資料或異常情境 (Priority: P2)

**Goal**: 當 Redis 中無 PR 資料或使用者清單為空時，系統應記錄警告並正常結束

**Independent Test**: 清空 Redis 中的 PR 資料或移除使用者清單，驗證系統產生警告日誌且不寫入新 Redis Key

### Tests for User Story 3 ⚠️

> **NOTE: 先撰寫測試並確認失敗，再進行實作（Red-Green-Refactor）**

- [ ] T011 [P] [US3] 在 `tests/ReleaseKit.Application.Tests/Tasks/FilterPullRequestsByUserTaskTests.cs` 撰寫無 PR 資料測試：Redis 中不存在 PR 資料時，驗證記錄警告日誌且不寫入新 Redis Key
- [ ] T012 [P] [US3] 在同一測試檔案撰寫空使用者清單測試：UserMapping.Mappings 為空時，驗證記錄警告日誌且不寫入新 Redis Key
- [ ] T013 [P] [US3] 在同一測試檔案撰寫含 Error 的 ProjectResult 測試：驗證含 Error 的 ProjectResult 保留原樣不進行 PR 過濾
- [ ] T014 [P] [US3] 在同一測試檔案撰寫空 UserId 過濾測試：UserMapping 中某 UserId 為空字串時，驗證該項目不參與過濾比對

### Implementation for User Story 3

- [ ] T015 [US3] 更新 `src/ReleaseKit.Application/Tasks/BaseFilterPullRequestsByUserTask.cs`，加入邊界條件處理：Redis 無資料時記錄警告並提前返回、使用者清單為空時記錄警告並提前返回、含 Error 的 ProjectResult 保留原樣

**Checkpoint**: 測試應由 Red 轉為 Green。執行建置與測試驗證。

✅ 可建置 / ✅ 測試通過

---

## Phase 5: Polish & 整合註冊

**Purpose**: 完成 CLI 指令註冊、DI 註冊、建置與測試最終驗證

- [ ] T016 [P] 在 `src/ReleaseKit.Console/Parsers/CommandLineParser.cs` 新增 `filter-gitlab-pr-by-user` 和 `filter-bitbucket-pr-by-user` 指令對應
- [ ] T017 [P] 在 `src/ReleaseKit.Application/Tasks/TaskFactory.cs` 新增 `FilterGitLabPullRequestsByUser` 和 `FilterBitbucketPullRequestsByUser` 的 case
- [ ] T018 在 `src/ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs` 的 `AddApplicationServices` 方法中新增 `FilterGitLabPullRequestsByUserTask` 和 `FilterBitbucketPullRequestsByUserTask` 的 Transient 註冊
- [ ] T019 在 `tests/ReleaseKit.Application.Tests/Tasks/TaskFactoryTests.cs` 補充 TaskFactory 對新 TaskType 的測試
- [ ] T020 執行完整建置驗證 `dotnet build src/release-kit.sln` 並確認無錯誤
- [ ] T021 執行完整測試驗證 `dotnet test` 並確認所有測試通過

✅ 可建置 / ✅ 測試通過

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: 無依賴，可立即開始
- **US1 (Phase 2)**: 依賴 Phase 1 完成（需要 RedisKeys 常數和 TaskType 列舉值）
- **US2 (Phase 3)**: 依賴 Phase 2 完成（需要 BaseFilterPullRequestsByUserTask 基底類別）
- **US3 (Phase 4)**: 依賴 Phase 2 完成（需要基底類別已建立，在此之上新增邊界條件）
- **Polish (Phase 5)**: 依賴 Phase 2、3、4 全部完成

### User Story Dependencies

- **US1 (P1)**: Phase 1 完成後可開始。建立基底類別 + GitLab 過濾任務
- **US2 (P1)**: Phase 2 完成後可開始。複用基底類別，僅建立 Bitbucket 過濾任務
- **US3 (P2)**: Phase 2 完成後可開始。在基底類別新增邊界條件處理

### Within Each User Story

1. 測試先撰寫並確認失敗（Red）
2. 實作功能使測試通過（Green）
3. 重構優化（Refactor）

### Parallel Opportunities

- T001、T002 可平行執行（不同檔案）
- T011、T012、T013、T014 可平行撰寫（同檔案但獨立測試案例）
- T016、T017 可平行執行（不同檔案）
- US2 與 US3 可在 US1 完成後平行進行

---

## Parallel Example: Phase 1 Setup

```bash
# 平行執行 Setup 任務：
Task: "T001 新增 RedisKeys 常數 in src/ReleaseKit.Common/Constants/RedisKeys.cs"
Task: "T002 新增 TaskType 列舉值 in src/ReleaseKit.Application/Tasks/TaskType.cs"
```

## Parallel Example: Phase 5 Polish

```bash
# 平行執行整合註冊任務：
Task: "T016 新增 CLI 指令對應 in CommandLineParser.cs"
Task: "T017 新增 TaskFactory case in TaskFactory.cs"
```

---

## Implementation Strategy

### MVP First（User Story 1 Only）

1. 完成 Phase 1: Setup（T001-T002）
2. 完成 Phase 2: US1 GitLab 過濾（T003-T007）
3. **STOP and VALIDATE**: 驗證 GitLab PR 過濾功能獨立可用
4. 可先行部署/展示 MVP

### Incremental Delivery

1. Setup → 基礎就緒
2. US1 GitLab 過濾 → 測試驗證 → MVP
3. US2 Bitbucket 過濾 → 測試驗證 → 雙平台支援
4. US3 異常處理 → 測試驗證 → 穩定性提升
5. Polish → 整合註冊 → 完整可用

---

## Notes

- [P] 任務 = 不同檔案，無依賴關係
- [Story] 標籤對應 spec.md 中的 User Story
- 每個 User Story 可獨立完成與驗證
- 遵循 TDD：先撰寫失敗測試 → 實作 → 重構
- 每個 Phase Checkpoint 後執行建置與測試驗證
- 使用 `JsonExtensions.ToJson()` 和 `ToTypedObject<T>()` 處理 JSON 序列化
- 使用 `UserMappingOptions`（已完成 DI 註冊於 `ServiceCollectionExtensions.AddConfigurationOptions`）
