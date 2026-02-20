# Feature Specification: 整合 Release 資料

**Feature Branch**: `002-consolidate-release-data`  
**Created**: 2026-02-20  
**Status**: Draft  
**Input**: 新增 console arg 用來整理 PR 以及 work item 資料，整理後存在 Redis 中

## User Scenarios & Testing

### User Story 1 - 整合 PR 與 Work Item 資料 (Priority: P1)

身為開發團隊的 PM 或 Release Manager，我需要將分散在不同平台（GitLab、Bitbucket）的 PR 資料與 Azure DevOps 的 Work Item 資料整合成統一格式，以便後續產出 Release Notes。透過執行新的 console 指令，系統會自動從 Redis 讀取已擷取的 PR 與 Work Item 資料，依據 PR ID 進行配對，並依專案路徑分組、依團隊顯示名稱與 Work Item ID 排序後，寫回 Redis 供後續使用。

**Why this priority**: 這是整個功能的核心需求，沒有這個整合步驟，後續 Release Notes 的產出就無法進行。

**Independent Test**: 可透過先在 Redis 寫入測試用 PR 與 Work Item 資料，執行指令後檢查 Redis 中的輸出資料是否正確整合。

**Acceptance Scenarios**:

1. **Given** Redis 中已有 Bitbucket 與 GitLab 的 ByUser PR 資料以及 Azure DevOps 的 UserStories Work Item 資料，**When** 執行整合指令，**Then** 系統產出整合後的 JSON 並存入新的 Redis Key。
2. **Given** 整合後的資料，**When** 檢視 JSON 結構，**Then** 資料以專案路徑（取路徑最後一段）分組，每筆記錄包含 PR 標題、Work Item ID、團隊顯示名稱、作者資訊、PR 資訊及原始資料。
3. **Given** Work Item 的 OriginalTeamName 為 "MoneyLogistic"，且 TeamMapping 設定中 DisplayName 為 "金流團隊"，**When** 執行整合，**Then** 團隊顯示名稱應為 "金流團隊"。
4. **Given** 整合後的資料，**When** 檢視同一專案路徑下的記錄，**Then** 記錄依團隊顯示名稱升冪排序，同團隊再依 Work Item ID 升冪排序。

---

### User Story 2 - 缺少 PR 資料時的錯誤處理 (Priority: P1)

身為系統操作者，當 Redis 中不存在 PR 資料時，我需要得到明確的錯誤訊息以便排查問題。

**Why this priority**: 錯誤處理與主功能同等重要，確保系統在資料缺失時不會靜默失敗。

**Independent Test**: 可透過清空 Redis 中的 PR Key 後執行指令，驗證錯誤行為。

**Acceptance Scenarios**:

1. **Given** Redis 中 `ReleaseKit:Bitbucket:PullRequests:ByUser` 與 `ReleaseKit:GitLab:PullRequests:ByUser` 均不存在或為空，**When** 執行整合指令，**Then** 系統拋出錯誤並結束程式，錯誤訊息明確指出 PR 資料不存在。

---

### User Story 3 - 缺少 Work Item 資料時的錯誤處理 (Priority: P1)

身為系統操作者，當 Redis 中不存在 Work Item 資料時，我需要得到明確的錯誤訊息以便排查問題。

**Why this priority**: 與 User Story 2 同理，確保所有必要資料來源都有適當的驗證。

**Independent Test**: 可透過清空 Redis 中的 Work Item Key 後執行指令，驗證錯誤行為。

**Acceptance Scenarios**:

1. **Given** Redis 中 `ReleaseKit:AzureDevOps:WorkItems:UserStories` 不存在或為空，**When** 執行整合指令，**Then** 系統拋出錯誤並結束程式，錯誤訊息明確指出 Work Item 資料不存在。

---

### User Story 4 - 團隊名稱對映（忽略大小寫）(Priority: P2)

身為系統操作者，我希望團隊名稱的對映能忽略大小寫，以避免因資料來源的大小寫不一致導致對映失敗。

**Why this priority**: 此為資料品質保障功能，確保團隊對映的容錯性。

**Independent Test**: 可建立大小寫不同的 OriginalTeamName 測試資料，驗證對映結果。

**Acceptance Scenarios**:

1. **Given** Work Item 的 OriginalTeamName 為 "moneylogistic"（全小寫），且 TeamMapping 中 OriginalTeamName 為 "MoneyLogistic"，**When** 執行整合，**Then** 團隊顯示名稱正確對映為 "金流團隊"。
2. **Given** Work Item 的 OriginalTeamName 在 TeamMapping 中找不到對應，**When** 執行整合，**Then** 團隊顯示名稱使用原始的 OriginalTeamName。

### Edge Cases

- 當同一個 Work Item 有多個不同 PR 關聯時，該 Work Item 的整合記錄中應包含所有相關的 PR 資訊與作者資訊。
- 當 Work Item 的 PrId 為 null 時，該筆 Work Item 不會被配對到任何 PR，但仍應出現在整合結果中。
- 當 ProjectPath 包含多層目錄時（如 `group/subgroup/project`），split('/') 後取最後一段作為專案名稱。
- 當多個不同平台的 PR（GitLab 與 Bitbucket）對應到同一個 Work Item 時，PR 資訊應合併呈現。

## Requirements

### Functional Requirements

- **FR-001**: 系統 MUST 提供新的 console 指令用於執行 PR 與 Work Item 資料整合。
- **FR-002**: 系統 MUST 從 Redis Key `ReleaseKit:Bitbucket:PullRequests:ByUser` 與 `ReleaseKit:GitLab:PullRequests:ByUser` 讀取 PR 資料。
- **FR-003**: 當 PR 資料不存在或為空時，系統 MUST 拋出錯誤並結束程式。
- **FR-004**: 系統 MUST 從 Redis Key `ReleaseKit:AzureDevOps:WorkItems:UserStories` 讀取 Work Item 資料。
- **FR-005**: 當 Work Item 資料不存在或為空時，系統 MUST 拋出錯誤並結束程式。
- **FR-006**: 系統 MUST 從 PR 資料中的 ProjectPath 欄位，取 split('/') 後的最後一段作為專案名稱。
- **FR-007**: 系統 MUST 使用 appsettings.json 中的 TeamMapping 設定，將 Work Item 的 OriginalTeamName 對映為 DisplayName。
- **FR-008**: 團隊名稱對映 MUST 忽略大小寫。
- **FR-009**: 當 OriginalTeamName 無法在 TeamMapping 中找到對應時，系統 MUST 使用原始的 OriginalTeamName 作為團隊顯示名稱。
- **FR-010**: 系統 MUST 以 PR ID 為基礎，將 PR 資料與 Work Item 資料進行配對。
- **FR-011**: 整合後的資料 MUST 以專案名稱分組。
- **FR-012**: 同一專案下的記錄 MUST 依團隊顯示名稱升冪排序，同團隊再依 Work Item ID 升冪排序。
- **FR-013**: 每筆整合記錄 MUST 包含：PR 標題、Work Item ID、團隊顯示名稱、作者資訊（含作者名稱）、PR 資訊（含 URL）、原始 Work Item 資料與原始 PR 資料。
- **FR-014**: 整合後的 JSON 結果 MUST 存入新的 Redis Key。
- **FR-015**: 當同一 Work Item 有多個 PR 關聯時，整合記錄中的 PR 資訊與作者資訊 MUST 包含所有相關 PR 的資料。

### Key Entities

- **整合記錄 (Consolidated Entry)**: 代表一筆 Work Item 與其相關 PR 的整合資料，包含 PR 標題、Work Item ID、團隊顯示名稱、作者清單、PR 連結清單及原始資料。
- **專案分組 (Project Group)**: 以專案名稱（ProjectPath 最後一段）為鍵，將整合記錄分組後的集合。
- **團隊對映 (Team Mapping)**: appsettings.json 中定義的原始團隊名稱與顯示名稱的對照關係。

## Success Criteria

### Measurable Outcomes

- **SC-001**: 使用者執行整合指令後，所有已配對的 PR 與 Work Item 資料皆正確出現在 Redis 輸出中，無遺漏。
- **SC-002**: 整合後的資料結構完全符合指定的 JSON 格式，可直接供後續流程使用。
- **SC-003**: 團隊名稱對映正確率達 100%（大小寫不同的相同團隊名稱皆能正確對映）。
- **SC-004**: 資料排序正確（依團隊顯示名稱 → Work Item ID），可直接用於 Release Notes 輸出。
- **SC-005**: 當必要資料缺失時，錯誤訊息足夠清楚，操作者可在 30 秒內理解問題並知道如何修復。

## Assumptions

- PR 資料已經過 `filter-*-pr-by-user` 指令處理並存入 Redis（即 ByUser 版本的資料已就緒）。
- Work Item 資料已經過 `get-user-story` 指令處理並存入 Redis（即 UserStories 版本的資料已就緒）。
- PR 與 Work Item 之間的配對以 `UserStoryWorkItemOutput.PrId` 與 `MergeRequestOutput.PrId` 進行關聯。
- ProjectPath 來自 PR 資料中的專案完整路徑，代表各 PR 所屬的專案。
- 一個 Work Item 可能對應多個 PR（多對一關係），整合時需將多個 PR 的資訊合併。
- TeamMapping 未匹配到時保留原始 OriginalTeamName，不視為錯誤。
