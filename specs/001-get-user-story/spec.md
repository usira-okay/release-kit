# Feature Specification: 取得 User Story 層級資訊

**Feature Branch**: `001-get-user-story`
**Created**: 2026-02-17
**Status**: Draft
**Input**: User description: "新增一個 console arg，從 Redis 取得 Azure Work Item 資訊，遞迴找 Parent 至 User Story 層級並將結果存回 Redis"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - 透過指令取得 User Story 層級資訊 (Priority: P1)

身為發佈管理人員，我希望執行一個指令後，系統能自動將 Redis 中所有 Work Item 對應到其所屬的 User Story（或更高層級），並將結果存入新的 Redis Key，以便後續流程能以 User Story 為單位進行追蹤與彙整。

**Why this priority**: 這是此功能的核心價值，沒有這個能力就無法實現 Work Item 到 User Story 的對應關係。

**Independent Test**: 可透過在 Redis 中準備包含不同 Type（User Story、Task、Bug）的 Work Item 資料，執行指令後驗證新 Redis Key 中的內容是否正確對應到 User Story 層級。

**Acceptance Scenarios**:

1. **Given** Redis 中已有包含多個 Work Item 的資料（其中包含 User Story、Task、Bug 類型），**When** 使用者執行 `get-user-story` 指令，**Then** 系統讀取所有 Work Item 並依據類型進行處理，將結果寫入新的 Redis Key。

2. **Given** Redis 中有一個 Type 為 Task 的 Work Item，且其 Parent 為 User Story，**When** 系統處理該 Work Item，**Then** 結果中包含原始 Work Item 資訊、對應的 User Story 資訊，以及標註「透過遞迴找到 User Story」的結果狀態。

3. **Given** Redis 中有一個 Type 為 User Story 的 Work Item，**When** 系統處理該 Work Item，**Then** 結果中包含原始 Work Item 資訊，並標註「原始 Type 即為 User Story 以上」的結果狀態，無需進行遞迴查找。

---

### User Story 2 - 處理無法取得資訊的 Work Item (Priority: P2)

身為發佈管理人員，我希望即使某些 Work Item 無法取得詳細資訊（例如原始取得就失敗），系統仍然保留這些項目在結果中，以便我能知道哪些項目需要手動處理。

**Why this priority**: 確保資料完整性，避免遺失任何 Work Item 的追蹤，但屬於核心功能的補充。

**Independent Test**: 可在 Redis 中準備包含取得失敗（IsSuccess = false）的 Work Item 資料，驗證這些項目是否出現在結果中並標註正確狀態。

**Acceptance Scenarios**:

1. **Given** Redis 中有一個原始就無法取得資訊的 Work Item（IsSuccess 為 false），**When** 系統處理該 Work Item，**Then** 結果中保留該 Work Item，並標註「原始的 Work Item 就無法取得資訊」的結果狀態。

2. **Given** Redis 中有一個 Type 為 Task 的 Work Item，但遞迴查找 Parent 過程中某層級的 Work Item 無法取得，**When** 系統處理完成，**Then** 結果中標註「無法找到 User Story 以上的類型」的結果狀態。

---

### User Story 3 - 處理深層巢狀的 Work Item 階層 (Priority: P3)

身為發佈管理人員，我希望系統能正確處理多層巢狀的 Work Item（例如 Sub-Task → Task → User Story），透過逐層遞迴找到最近的 User Story 或更高層級。

**Why this priority**: 在實際使用中，Work Item 可能存在多層巢狀關係，需確保遞迴邏輯正確處理。

**Independent Test**: 可準備多層巢狀的 Work Item（如 Bug → Task → User Story），驗證系統是否正確向上遞迴並找到 User Story。

**Acceptance Scenarios**:

1. **Given** 一個 Bug 類型的 Work Item，其 Parent 為 Task，Task 的 Parent 為 User Story，**When** 系統遞迴查找，**Then** 最終找到 User Story，結果標註「透過遞迴找到 User Story」。

2. **Given** 一個 Task 類型的 Work Item，其 Parent 為另一個 Task，而該 Task 沒有 Parent，**When** 系統遞迴查找，**Then** 無法找到 User Story，結果標註「無法找到 User Story 以上的類型」。

---

### Edge Cases

- 當 Redis 中的 Work Item 來源資料為空時（無任何 Work Item），系統應正常完成並寫入空結果至新 Redis Key。
- 當某個 Work Item 的 Parent 指向自己（循環參照），系統應能偵測並中止遞迴，避免無窮迴圈。
- 當遞迴查找 Parent 的過程中遇到已刪除或無權限存取的 Work Item，系統應將該項目標註為「無法找到 User Story 以上的類型」。
- 當所有 Work Item 都已經是 User Story 或更高層級，系統應正常處理而不進行任何遞迴查找。

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: 系統 MUST 提供新的 console arg `get-user-story`，用於觸發 Work Item 到 User Story 層級對應的處理流程。
- **FR-002**: 系統 MUST 從現有的 Redis Key 讀取 Work Item 資料作為輸入來源。
- **FR-003**: 系統 MUST 判斷每個 Work Item 的 Type，若為 User Story、Feature、Epic 等層級，則直接標記為「已是 User Story 以上」。
- **FR-004**: 系統 MUST 對 Type 為 User Story 以下（如 Task、Bug）的 Work Item，透過遞迴查找 Parent，直到找到 User Story 或更高層級的 Work Item。
- **FR-005**: 系統 MUST 保留原始取得失敗（IsSuccess 為 false）的 Work Item，並標記為「原始的 Work Item 就無法取得資訊」。
- **FR-006**: 系統 MUST 在遞迴查找過程中，若無法找到 User Story 以上層級（Parent 鏈結中斷或到達頂層），則標記為「無法找到 User Story 以上的類型」。
- **FR-007**: 系統 MUST 將處理結果寫入新的 Redis Key，與原始 Work Item 資料分開儲存。
- **FR-008**: 系統 MUST 在結果中保留原始 Work Item 的完整資訊（WorkItemId、Title、Type、State、Url、OriginalTeamName）。
- **FR-009**: 系統 MUST 為每個結果項目標註解析結果狀態，共有四種狀態：(1) 原始 Type 即為 User Story 以上、(2) 透過遞迴找到 User Story 以上、(3) 無法找到 User Story 以上、(4) 原始 Work Item 無法取得資訊。
- **FR-010**: 系統 MUST 在遞迴查找時偵測循環參照，避免無窮迴圈。
- **FR-011**: 系統 MUST 在透過遞迴成功找到 User Story 時，一併記錄該 User Story 的資訊（WorkItemId、Title、Type 等）。

### Key Entities

- **WorkItemWithUserStory**: 代表一個經過 User Story 解析處理的 Work Item，包含原始 Work Item 資訊、解析結果狀態，以及找到的 User Story 資訊（若有）。
- **解析結果狀態 (Resolution Status)**: 列舉值，表示 Work Item 與 User Story 的對應結果。四種狀態：原始即為 User Story 以上、透過遞迴找到、無法找到、原始資料取得失敗。
- **UserStoryInfo**: 透過遞迴找到的 User Story 資訊，包含 WorkItemId、Title、Type 等基本屬性。

## Assumptions

- **Work Item Type 階層定義**: 「User Story 以上」包含 User Story、Feature、Epic 三種類型；「User Story 以下」包含 Task、Bug 等類型。若遇到未知類型，視為 User Story 以下進行遞迴查找。
- **Parent 關係來源**: 透過 Azure DevOps API 取得 Work Item 的 Parent 關係（Relations 中的 Parent Link）。
- **遞迴深度限制**: 合理假設遞迴深度不超過 10 層，超過時視為無法找到 User Story。
- **新 Redis Key 命名**: 新的 Redis Key 將採用與現有 Key 一致的命名慣例。
- **Console arg 命名**: 新增的指令名稱為 `get-user-story`，符合現有指令的 kebab-case 命名慣例。

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 使用者執行 `get-user-story` 指令後，100% 的 Work Item（無論成功或失敗）都出現在結果中，不遺失任何項目。
- **SC-002**: 所有 Type 為 User Story 以上的 Work Item，解析結果狀態正確標註為「原始即為 User Story 以上」。
- **SC-003**: 所有 Type 為 Task 或 Bug 且有 Parent 為 User Story 的 Work Item，解析結果狀態正確標註為「透過遞迴找到」，且包含正確的 User Story 資訊。
- **SC-004**: 所有原始取得失敗的 Work Item，解析結果狀態正確標註為「原始無法取得資訊」。
- **SC-005**: 遇到循環參照時，系統不會進入無窮迴圈，能正常完成並回報結果。
