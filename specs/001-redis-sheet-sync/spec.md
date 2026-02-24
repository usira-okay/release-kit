# Feature Specification: Redis → Google Sheet 批次同步

**Feature Branch**: `001-redis-sheet-sync`
**Created**: 2026-02-24
**Status**: Draft
**Input**: 將 Redis 中 `ReleaseData:Consolidated` 的資料批次新增或更新到 Google Sheet

## User Scenarios & Testing *(mandatory)*

### User Story 1 - 批次同步整合資料至 Google Sheet (Priority: P1)

身為 Release 管理人員，我希望執行程式時能自動將 Redis 中已整合的 Release 資料同步到 Google Sheet，讓我不需要手動逐筆輸入或更新資料，以便快速產出 Release Notes。

**Why this priority**: 這是本功能的核心價值——自動化 Redis 到 Google Sheet 的資料同步流程，消除手動作業的時間成本與人為錯誤。

**Independent Test**: 可透過準備 Redis 中的整合資料與空白或部分填入的 Google Sheet，執行同步後驗證 Sheet 中的資料是否正確新增與更新。

**Acceptance Scenarios**:

1. **Given** Redis 中有整合資料且 Google Sheet 可連線，**When** 執行同步，**Then** 新的資料列被新增到 Sheet 中正確的專案區段位置，既有資料的作者與 PR 網址欄位被更新
2. **Given** Redis 中有多個專案的整合資料，**When** 執行同步，**Then** 每個專案的資料列被新增到對應專案區段，且各區段內依排序規則重新排列
3. **Given** Redis 中有新資料需要新增，**When** 執行同步，**Then** 系統先插入所有需要的空白列，再依序填入新增資料與更新既有資料

---

### User Story 2 - 優雅處理無資料情境 (Priority: P2)

身為 Release 管理人員，我希望當 Redis 中沒有整合資料時，程式能安靜地結束而不拋出錯誤，讓我知道「沒有資料需要同步」而不是看到錯誤訊息。

**Why this priority**: 確保無資料時的使用體驗流暢，避免不必要的錯誤訊息造成混亂。這是基礎的錯誤處理改善。

**Independent Test**: 可透過清空 Redis 中的整合資料後執行程式，驗證程式是否正常結束且無錯誤訊息。

**Acceptance Scenarios**:

1. **Given** Redis 中 `ReleaseData:Consolidated` 欄位不存在或為空，**When** 執行同步，**Then** 程式正常結束，不拋出任何例外
2. **Given** Redis 中沒有先行資料（前置步驟未執行），**When** 執行同步，**Then** 程式正常結束，不拋出任何例外

---

### User Story 3 - Google Sheet 連線驗證 (Priority: P3)

身為 Release 管理人員，我希望在開始同步前系統能先驗證 Google Sheet 的可用性，讓我在連線有問題時能提早得知，而不是在同步過程中才失敗。

**Why this priority**: 前置驗證可避免部分同步導致資料不一致的風險，確保同步操作的原子性。

**Independent Test**: 可透過提供無效的 Google Sheet 設定或中斷網路連線來驗證系統的錯誤處理行為。

**Acceptance Scenarios**:

1. **Given** Google Sheet 設定正確且可連線，**When** 執行同步前的驗證步驟，**Then** 驗證通過，程式繼續執行同步流程
2. **Given** Google Sheet 無法連線或設定錯誤，**When** 執行同步前的驗證步驟，**Then** 程式正常結束，不繼續同步
3. **Given** 設定檔中欄位對應超過 Z 欄，**When** 執行同步前的驗證步驟，**Then** 系統拋出錯誤告知欄位設定不合法

---

### Edge Cases

- 當 Redis 整合資料中某筆 Work Item 的標題或 URL 為空時，仍應新增該列但對應欄位留空
- 當同一 Work Item 跨多個專案時，每個專案會產生獨立的資料列（以 UniqueKey = `{workItemId}{projectName}` 區分）
- 當 Google Sheet 中某個專案只有表頭列（RepositoryNameColumn 有值）而無任何資料列時，新增的資料列應插入在該表頭列之後
- 當多筆新增資料屬於同一專案時，所有新增列應在同一批次中插入，避免逐筆插入導致 row index 偏移
- 當 Redis 中的作者清單或 PR 網址清單為空時，對應欄位應留空
- 當 Google Sheet 中已有的資料列其作者與 PR 網址與 Redis 資料相同時，仍應執行更新（覆寫），以確保資料一致性
- 當 RepositoryNameColumn 中只有一個專案（僅一個非空值）時，該專案的資料範圍為該列之後到工作表末尾

## Requirements *(mandatory)*

### Functional Requirements

#### 前置處理

- **FR-001**: 系統 MUST 從 Redis 讀取 `ReleaseData:Consolidated` 欄位的整合資料
- **FR-002**: 系統 MUST 在 Redis 中無整合資料（欄位不存在或值為空）時正常結束程式，不拋出例外
- **FR-003**: 系統 MUST 修正目前「沒有先行資料就拋錯」的行為，改為正常結束程式

#### Google Sheet 連線驗證

- **FR-004**: 系統 MUST 透過 Sheet Name 動態取得 Sheet ID，而非使用靜態設定
- **FR-005**: 系統 MUST 在同步前測試是否能取得 Google Sheet 及 Sheet 中 A–Z 的所有資料
- **FR-006**: 系統 MUST 在無法取得 Google Sheet 資料時正常結束程式
- **FR-007**: 系統 MUST 在設定檔中任何欄位對應超過 Z 欄時拋出錯誤

#### 新增資料

- **FR-008**: 系統 MUST 以 UniqueKey（`{workItemId}{projectName}`）比對 Sheet 中 UniqueKeyColumn 欄位，判斷資料是否已存在
- **FR-009**: 系統 MUST 在 UniqueKey 不存在於 Sheet 時，將該筆資料視為「新增」
- **FR-010**: 系統 MUST 先批次插入所有需要新增的空白列，再填入資料或更新既有資料
- **FR-011**: 新增列的插入位置 MUST 依據 RepositoryNameColumn 判斷——插入到對應專案區段內
- **FR-012**: 當新增資料的專案名稱出現在 RepositoryNameColumn 的第一筆時，新列 MUST 插入在該列之後
- **FR-013**: 當新增資料的專案名稱出現在 RepositoryNameColumn 的中間區段時，新列 MUST 插入在該專案區段的下一列
- **FR-014**: 新增列 MUST 填入以下欄位：
  - FeatureColumn：`VSTS{workItemId} - {title}`，並加上超連結指向 workItemUrl
  - TeamColumn：teamDisplayName
  - AuthorsColumn：所有作者的 authorName，依 authorName 排序後以換行分隔
  - PullRequestUrlsColumn：所有 PR 的 url，依 url 排序後以換行分隔
  - UniqueKeyColumn：`{workItemId}{projectName}`
  - AutoSyncColumn：固定值 `TRUE`

#### 更新資料

- **FR-015**: 系統 MUST 在 UniqueKey 存在於 Sheet 時，將該筆資料視為「更新」
- **FR-016**: 更新時 MUST 僅更新 AuthorsColumn 與 PullRequestUrlsColumn 兩個欄位
- **FR-017**: 更新的 AuthorsColumn MUST 依 authorName 排序後以換行分隔
- **FR-018**: 更新的 PullRequestUrlsColumn MUST 依 url 排序後以換行分隔

#### 排序

- **FR-019**: 當有新增或更新發生時，系統 MUST 對該專案區段內的資料列重新排序
- **FR-020**: 排序範圍 MUST 為同一專案中兩個相鄰 RepositoryNameColumn 之間的資料列（不含 RepositoryNameColumn 本身）
- **FR-021**: 排序規則 MUST 依序為：TeamColumn → AuthorsColumn → FeatureColumn → UniqueKeyColumn
- **FR-022**: 排序時空白值 MUST 排在最後

### Key Entities

- **ConsolidatedReleaseEntry**: 代表一筆已整合的 Release 資料，包含標題、Work Item 資訊、團隊名稱、作者清單、PR 清單
- **ConsolidatedReleaseResult**: 代表按專案名稱分組的整合結果集合
- **GoogleSheet 專案區段**: Sheet 中以 RepositoryNameColumn 為分界的資料區塊，每個區段代表一個專案的 Release 資料
- **UniqueKey**: 由 Work Item ID 與專案名稱組合而成的唯一識別碼，用於判斷資料列是否已存在

### Assumptions

- 每個專案在 RepositoryNameColumn 中只會有一個表頭列（即 RepositoryNameColumn 中每個專案名稱只出現一次）
- Google Sheet 的欄位範圍限制為 A–Z（26 欄），不支援 AA 以後的欄位
- 所有欄位對應設定均在應用程式啟動時載入，同步過程中不會變更
- 排序操作在 Sheet 中直接進行，以 Sheet 當前的資料為準
- Redis 中的整合資料結構與 ConsolidatedReleaseResult 一致
- AutoSyncColumn 的 `TRUE` 為文字字串值

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 執行同步後，Redis 中所有新增的整合資料 100% 正確出現在 Google Sheet 的對應專案區段中
- **SC-002**: 執行同步後，所有既有資料的作者與 PR 網址欄位與 Redis 資料完全一致
- **SC-003**: 當 Redis 無資料時，程式在不拋出任何例外的情況下正常結束
- **SC-004**: 同步完成後，每個專案區段內的資料列均依指定排序規則正確排列
- **SC-005**: 所有新增列的欄位值（Feature、Team、Authors、PR URLs、UniqueKey、AutoSync）均與 Redis 來源資料一致
- **SC-006**: 欄位設定超過 Z 欄時，系統在同步前即拋出明確的錯誤訊息
