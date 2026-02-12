# Feature Specification: Filter Pull Requests by User

**Feature Branch**: `001-filter-pr-by-user`
**Created**: 2026-02-12
**Status**: Draft
**Input**: User description: "建立兩個新的 CLI 指令，用於過濾 GitLab 和 Bitbucket 平台中 PR 資訊的 Author"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - 過濾 GitLab PR 資料中的指定使用者 (Priority: P1)

身為 Release Manager，我希望能從已擷取的 GitLab PR 資料中，僅篩選出屬於團隊成員的 PR，以便後續產出精準的 Release Notes，排除非團隊成員的無關 PR。

**Why this priority**: 這是核心功能的第一個平台實現。GitLab 過濾指令是獨立可交付的最小可用單元，過濾後的資料可直接供下游流程使用。

**Independent Test**: 可透過在 Redis 中預先存入 GitLab PR 資料並執行 `filter-gitlab-pr-by-user` 指令，驗證過濾後的結果僅包含設定檔中指定使用者的 PR。

**Acceptance Scenarios**:

1. **Given** Redis 中存有 GitLab PR 資料且設定檔中有使用者清單，**When** 執行 `filter-gitlab-pr-by-user` 指令，**Then** 系統僅保留 AuthorUserId 存在於使用者清單中的 PR，並將結果寫入新的 Redis Key。
2. **Given** Redis 中存有包含多個專案的 GitLab PR 資料，**When** 執行過濾指令，**Then** 每個專案的 PR 清單各自獨立過濾，且輸出格式與原始資料結構一致。
3. **Given** 設定檔中某使用者的 GitLab UserId 為空字串，**When** 執行過濾指令，**Then** 該使用者不參與比對，不影響其他使用者的過濾結果。

---

### User Story 2 - 過濾 Bitbucket PR 資料中的指定使用者 (Priority: P1)

身為 Release Manager，我希望能從已擷取的 Bitbucket PR 資料中，僅篩選出屬於團隊成員的 PR，以便與 GitLab 過濾結果一起整合至 Release Notes 流程。

**Why this priority**: 與 GitLab 過濾指令同等重要，兩個平台的過濾功能共同構成完整的使用者過濾能力。

**Independent Test**: 可透過在 Redis 中預先存入 Bitbucket PR 資料並執行 `filter-bitbucket-pr-by-user` 指令，驗證過濾後的結果僅包含設定檔中指定使用者的 PR。

**Acceptance Scenarios**:

1. **Given** Redis 中存有 Bitbucket PR 資料且設定檔中有使用者清單，**When** 執行 `filter-bitbucket-pr-by-user` 指令，**Then** 系統僅保留 AuthorUserId 存在於使用者清單中的 PR，並將結果寫入新的 Redis Key。
2. **Given** Redis 中存有包含多個專案的 Bitbucket PR 資料，**When** 執行過濾指令，**Then** 每個專案的 PR 清單各自獨立過濾，且輸出格式與原始資料結構一致。
3. **Given** 設定檔中某使用者的 Bitbucket UserId 為空字串，**When** 執行過濾指令，**Then** 該使用者不參與比對，不影響其他使用者的過濾結果。

---

### User Story 3 - 處理無資料或異常情境 (Priority: P2)

身為 Release Manager，當 Redis 中不存在 PR 資料或設定檔中沒有使用者清單時，我希望系統能給出明確的提示，避免無聲失敗。

**Why this priority**: 錯誤處理是確保系統穩定運行的必要條件，但屬於核心功能的補充。

**Independent Test**: 可透過清空 Redis 中的 PR 資料或移除設定檔中的使用者清單，驗證系統是否產生明確的日誌訊息。

**Acceptance Scenarios**:

1. **Given** Redis 中不存在對應平台的 PR 資料，**When** 執行過濾指令，**Then** 系統記錄警告日誌並正常結束，不寫入新的 Redis Key。
2. **Given** 設定檔中的使用者清單為空，**When** 執行過濾指令，**Then** 系統記錄警告日誌並正常結束，不寫入新的 Redis Key。
3. **Given** 過濾後所有專案均無匹配的 PR，**When** 過濾完成，**Then** 系統仍將空結果寫入 Redis Key（FetchResult 中各 ProjectResult 的 PullRequests 為空清單）。

---

### Edge Cases

- 當 Redis 中的 PR 資料 JSON 格式損壞或無法反序列化時，系統應讓例外自然向上傳遞至全域處理器。
- 當同一個 AuthorUserId 在使用者清單中出現多次時，該使用者的 PR 仍只會出現一次（不重複）。
- 當某個 ProjectResult 包含 Error 訊息（代表先前擷取失敗）時，過濾時應保留該 ProjectResult 原樣（含 Error 訊息），不進行 PR 過濾。

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: 系統必須提供 `filter-gitlab-pr-by-user` CLI 指令，從 Redis 讀取 GitLab PR 資料並依使用者清單過濾。
- **FR-002**: 系統必須提供 `filter-bitbucket-pr-by-user` CLI 指令，從 Redis 讀取 Bitbucket PR 資料並依使用者清單過濾。
- **FR-003**: 過濾邏輯必須比對 PR 的 AuthorUserId 與設定檔中對應平台的使用者 ID（GitLab 使用 GitLabUserId，Bitbucket 使用 BitbucketUserId）。
- **FR-004**: 過濾後的結果必須維持與原始資料相同的 FetchResult 結構格式。
- **FR-005**: 過濾後的 GitLab 結果必須寫入 `GitLab:PullRequests:ByUser` Redis Key。
- **FR-006**: 過濾後的 Bitbucket 結果必須寫入 `Bitbucket:PullRequests:ByUser` Redis Key。
- **FR-007**: 使用者清單中 UserId 為空字串的項目必須被忽略，不參與過濾比對。
- **FR-008**: 當 Redis 中無 PR 資料或使用者清單為空時，系統必須記錄警告日誌並正常結束。
- **FR-009**: 過濾後的結果必須同時輸出至主控台（stdout）與 Redis。
- **FR-010**: 當 ProjectResult 包含 Error 訊息時，過濾時必須保留該 ProjectResult 原樣不進行 PR 過濾。

### Key Entities

- **UserMapping**: 跨平台使用者對應，包含 GitLabUserId、BitbucketUserId、DisplayName 三個屬性。
- **FetchResult**: PR 擷取結果的頂層容器，包含多個 ProjectResult。
- **ProjectResult**: 單一專案的 PR 擷取結果，包含專案路徑、平台、PR 清單與可能的錯誤訊息。
- **MergeRequestOutput**: 單一 PR 的輸出模型，包含 AuthorUserId 作為過濾比對欄位。

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 執行 `filter-gitlab-pr-by-user` 指令後，Redis 中 `GitLab:PullRequests:ByUser` 的資料僅包含設定檔中指定使用者的 PR。
- **SC-002**: 執行 `filter-bitbucket-pr-by-user` 指令後，Redis 中 `Bitbucket:PullRequests:ByUser` 的資料僅包含設定檔中指定使用者的 PR。
- **SC-003**: 過濾後的 JSON 結構可被下游 `update-googlesheet` 指令正確讀取與處理，無需任何格式調整。
- **SC-004**: 所有單元測試覆蓋核心過濾邏輯，包含正常過濾、空資料、空使用者清單、含錯誤的 ProjectResult 等情境。

## Assumptions

- 使用者清單已在 `appsettings.json` 中的 `UserMapping.Mappings` 區段正確設定。
- `filter-gitlab-pr-by-user` 與 `filter-bitbucket-pr-by-user` 指令的執行前提是已先執行過對應平台的 `fetch-*-pr` 指令，Redis 中已有 PR 資料。
- AuthorUserId 的比對為精確匹配（exact match），不進行模糊比對。
- 過濾指令不會修改原始 Redis Key 中的資料，僅讀取並寫入新的 Key。
