# Feature Specification: PR 與 Work Item 關聯追蹤改善

**Feature Branch**: `001-workitem-pr-tracking`
**Created**: 2026-02-19
**Status**: Draft
**Input**: User description: "PR Work Item ID 保留重複並追蹤 PR 關聯"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - 保留 PR 對應的重複 Work Item ID（Priority: P1）

當系統從 PR 解析 Work Item ID 清單時，若同一個 Work Item ID 在 PR 中被參照多次，應保留所有參照紀錄，不得去重複。這能確保系統忠實反映每個 PR 與 Work Item 之間的多重關係，避免資料丟失。

**Why this priority**: 這是後續追蹤 PR 與 Work Item 關聯的基礎。若在此步驟去重複，後續的關聯紀錄將不完整，影響稽核與分析準確性。

**Independent Test**: 建立一個包含重複 Work Item ID 的 PR，驗證系統解析後保留所有重複 ID，不進行去重複即可驗證此功能。

**Acceptance Scenarios**:

1. **Given** 一個 PR 中包含相同的 Work Item ID 出現兩次，**When** 系統解析該 PR 的 Work Item ID 清單，**Then** 清單中包含兩個相同的 Work Item ID，未被去重複。
2. **Given** 多個 PR 各自包含相同的 Work Item ID，**When** 系統處理所有 PR，**Then** 每個 PR 的 Work Item ID 清單均完整保留，互不干擾。

---

### User Story 2 - 將 PR ID 記錄至 Work Item 物件並儲存（Priority: P2）

當系統從 Azure DevOps 取得 Work Item 資料後，應將觸發該次查詢的 PR ID 記錄於 Work Item 物件內，並將包含 PR ID 的 Work Item 物件完整儲存至資料快取。此功能讓使用者或下游系統可從 Work Item 資料中直接得知對應的 PR 來源。

**Why this priority**: 在保留重複 ID 的基礎上，必須確保 PR 與 Work Item 的關聯不因資料轉換或快取而中斷，是資料完整性的核心需求。

**Independent Test**: 觸發特定 PR 的 Work Item 抓取流程，查詢快取中的 Work Item 物件，驗證其包含正確的 PR ID 欄位即可獨立驗證。

**Acceptance Scenarios**:

1. **Given** 系統從 PR #123 解析出 Work Item ID 456，**When** 系統查詢並取得 Work Item 456 的資料，**Then** Work Item 456 的物件中包含 PR ID 123，且此物件被完整儲存至快取。
2. **Given** 同一 Work Item ID 出現在多個 PR 中，**When** 系統分別處理各 PR，**Then** 每個 PR 對應的 Work Item 物件中均各自記錄其來源 PR ID。
3. **Given** Work Item 物件已記錄 PR ID，**When** 系統後續查詢快取，**Then** 可從快取中取得包含 PR ID 的完整 Work Item 物件。

---

### User Story 3 - User Story 層級僅在 User Story 物件記錄 PR ID（Priority: P3）

當系統在 User Story 層級抓取資料時，應僅將 PR ID 記錄至 User Story 物件，而不應同時記錄至 `originalWorkItem` 物件。此規則確保資料模型的層級職責清晰，避免 PR 關聯資訊被冗餘記錄於原始工作項目層。

**Why this priority**: 此為資料模型精煉需求，確保層級間的資料職責分離，但不影響核心追蹤功能的可用性。

**Independent Test**: 觸發 User Story 層級的資料抓取，驗證 User Story 物件包含 PR ID，且 `originalWorkItem` 物件不含 PR ID 欄位。

**Acceptance Scenarios**:

1. **Given** 系統在 User Story 層級抓取工作項目，**When** 系統處理並記錄 PR ID，**Then** PR ID 僅存在於 User Story 物件中，`originalWorkItem` 物件中不含 PR ID。
2. **Given** User Story 物件已記錄 PR ID，**When** 系統查詢快取，**Then** 可從 User Story 物件中取得 PR ID，`originalWorkItem` 物件保持原始狀態不變。

---

### Edge Cases

- 若一個 PR 未包含任何 Work Item ID，系統應正常跳過，不產生錯誤。
- 若快取儲存失敗，Work Item 物件（含 PR ID）應在記錄錯誤後繼續嘗試後續項目，不中斷整體流程。
- 若同一 Work Item ID 對應多個 PR，每筆 PR-WorkItem 對應關係應個別處理，確保各自的 PR ID 被正確記錄。
- 若 User Story 層級的 `originalWorkItem` 已存在 PR ID 欄位（歷史資料），本次修改後應不再寫入，但不應主動清除既有資料。

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: 系統從 PR 解析 Work Item ID 時，MUST 保留所有 ID，包含重複出現的相同 ID，不得進行去重複處理。
- **FR-002**: 系統取得 Work Item 資料後，MUST 將觸發本次查詢的 PR ID 寫入該 Work Item 物件的對應欄位。
- **FR-003**: 系統 MUST 將包含 PR ID 的 Work Item 物件完整儲存至快取，確保快取中的資料包含 PR 來源資訊。
- **FR-004**: 在 User Story 層級的處理流程中，系統 MUST 僅將 PR ID 記錄至 User Story 物件，MUST NOT 將 PR ID 記錄至 `originalWorkItem` 物件。
- **FR-005**: 系統 MUST 確保非 User Story 層級的 Work Item 物件仍依 FR-002 與 FR-003 的規則記錄並儲存 PR ID。

### Key Entities

- **PR（Pull Request）**: 觸發 Work Item 查詢的來源，具有唯一 PR ID；一個 PR 可包含一至多個 Work Item ID，且允許重複參照。
- **Work Item**: 從 Azure DevOps 取得的工作項目物件，新增 PR ID 欄位以記錄來源 PR；儲存至快取時須含此欄位。
- **User Story**: Work Item 的特定層級類型，僅在此物件層級記錄 PR ID，不向下傳遞至 `originalWorkItem`。
- **originalWorkItem**: Work Item 的原始資料物件，在 User Story 層級處理時不寫入 PR ID。
- **快取（Cache）**: 用於儲存 Work Item 物件的快取系統，必須能儲存並查詢包含 PR ID 的完整物件。

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 當 PR 中出現重複的 Work Item ID 時，100% 的重複 ID 均被保留於處理清單中，不遺漏。
- **SC-002**: 所有經由 PR 觸發查詢的 Work Item 物件，儲存至快取後，100% 包含正確的來源 PR ID 欄位。
- **SC-003**: User Story 層級的處理結果中，PR ID 僅存在於 User Story 物件，`originalWorkItem` 物件的 PR ID 欄位不存在或為空，正確率達 100%。
- **SC-004**: 整體資料處理流程在引入上述變更後，現有功能的回歸測試通過率維持 100%，不產生新的錯誤。

## Assumptions

- 系統已有從 PR 解析 Work Item ID 的現有機制，本次修改僅移除去重複邏輯，不重新設計解析流程。
- Work Item 物件已有可擴展的資料結構，可新增 PR ID 欄位而不破壞現有欄位。
- 快取系統已在現有架構中運作，本次修改僅調整寫入內容，不變更快取操作方式。
- `originalWorkItem` 與 User Story 物件在現有程式碼中已明確區分，可獨立控制各自的欄位寫入邏輯。
- 「User Story 層級」的判斷邏輯已在系統中存在，無需本次新增識別機制。
