# Feature Specification: Get User Story

**Feature Branch**: `001-get-user-story`
**Created**: 2026-02-14
**Status**: Draft
**Input**: User description: "基於現有的設計文件，為 get-user-story 功能建立 spec。功能包含三部分：1) PR 資料結構新增 PR ID 欄位 2) Work Item 抓取邏輯保留 PR 關聯資訊（一對一，不去重） 3) 新增 get-user-story console arg，將 Task/Bug 遞迴往上找 parent 直到 User Story/Feature/Epic，找不到就保留原始資料。結果存到新的 Redis key AzureDevOps:UserStories。"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - 追蹤 PR 識別碼 (Priority: P1)

身為發行管理人員，我需要在系統抓取的 PR 資料中包含 PR 的唯一識別碼（PR ID），以便後續流程能精確定位每一筆 PR 的來源。

**Why this priority**: PR ID 是後續所有功能的基礎欄位。沒有 PR ID，Work Item 就無法正確關聯其來源 PR。此欄位為最核心的資料結構變更，影響所有下游流程。

**Independent Test**: 可透過執行現有的 `fetch-gitlab-pr` 或 `fetch-bitbucket-pr` 指令，驗證存入 Redis 的 PR 資料結構中是否包含 PR ID 欄位。

**Acceptance Scenarios**:

1. **Given** 系統從 GitLab 抓取到一筆 Merge Request，**When** 將資料存入 Redis，**Then** 該筆資料應包含 GitLab 專案內唯一編號（iid）作為 PR ID
2. **Given** 系統從 Bitbucket 抓取到一筆 Pull Request，**When** 將資料存入 Redis，**Then** 該筆資料應包含 Bitbucket Repository 內唯一編號（id）作為 PR ID

---

### User Story 2 - Work Item 保留 PR 來源關聯 (Priority: P1)

身為發行管理人員，我需要在抓取 Azure DevOps Work Item 時，保留每筆 Work Item 對應的 PR 來源資訊（PR ID、專案名稱、PR 網址），以便追蹤 Work Item 是從哪一個 PR 被提及的。

**Why this priority**: 與 PR ID 同等重要。Work Item 與 PR 的關聯是後續 User Story 解析的輸入來源，必須優先確保資料完整性。

**Independent Test**: 可透過執行 `fetch-azure-workitems` 指令，驗證 Redis 中每筆 Work Item 是否都包含來源 PR 的識別碼、專案名稱與網址。

**Acceptance Scenarios**:

1. **Given** 一筆 PR 標題中包含一個 VSTS ID，**When** 系統抓取該 Work Item，**Then** 輸出應包含該 PR 的 ID、專案名稱與 PR 網址
2. **Given** 同一個 VSTS ID 出現在兩筆不同的 PR 中，**When** 系統抓取該 Work Item，**Then** 應產生兩筆獨立的輸出記錄，各自保留各自的 PR 來源資訊，且系統僅向 Azure DevOps API 查詢該 Work Item 一次
3. **Given** 一筆 PR 標題中包含一個 VSTS ID 但 Azure DevOps API 查詢失敗，**When** 系統處理該筆資料，**Then** 應產生一筆標記為失敗的輸出記錄，並保留 PR 來源資訊

---

### User Story 3 - 解析 Work Item 至 User Story 層級 (Priority: P2)

身為發行管理人員，我需要一個指令能將所有已抓取的 Work Item 解析至 User Story（含）以上層級（User Story / Feature / Epic），以便產出以 User Story 為單位的發行報告。

**Why this priority**: 此為本次功能的核心價值。需要依賴 Story 1 與 Story 2 完成後的資料作為輸入，因此列為 P2。

**Independent Test**: 可透過執行 `get-user-story` 指令，驗證 Redis 中新的 key `AzureDevOps:UserStories` 是否正確存放解析後的結果。

**Acceptance Scenarios**:

1. **Given** Redis 中有一筆類型為 User Story 的 Work Item，**When** 執行 `get-user-story`，**Then** 該筆資料應直接保留，不需向 Azure DevOps API 查詢 parent
2. **Given** Redis 中有一筆類型為 Feature 的 Work Item，**When** 執行 `get-user-story`，**Then** 該筆資料應直接保留
3. **Given** Redis 中有一筆類型為 Epic 的 Work Item，**When** 執行 `get-user-story`，**Then** 該筆資料應直接保留
4. **Given** Redis 中有一筆類型為 Task 的 Work Item 且其 parent 為 User Story，**When** 執行 `get-user-story`，**Then** 應解析至該 parent User Story 並記錄原始 Work Item ID
5. **Given** Redis 中有一筆類型為 Bug 的 Work Item 且其 parent 為 Task、祖父為 User Story，**When** 執行 `get-user-story`，**Then** 應遞迴向上查詢直到找到 User Story
6. **Given** Redis 中有一筆類型為 Task 的 Work Item 且其整條 parent 鏈上都沒有 User Story / Feature / Epic，**When** 執行 `get-user-story`，**Then** 應保留原始 Work Item 資料
7. **Given** Redis 中有一筆原始抓取就失敗的 Work Item，**When** 執行 `get-user-story`，**Then** 應保留該筆失敗記錄
8. **Given** 執行 `get-user-story` 完畢，**When** 查看 Redis，**Then** 結果應存放在 `AzureDevOps:UserStories` key 中

---

### Edge Cases

- 當 parent 查詢的遍歷深度超過上限時，系統應保留原始 Work Item 資料，避免無限迴圈
- 當同一個 Task/Bug 出現在多筆 PR 中，應各自獨立解析至 User Story（不合併）
- 當 Azure DevOps API 在遞迴查詢 parent 過程中失敗時，應保留原始 Work Item 資料
- 當 Work Item 的類型為非預期值（非 Task/Bug/User Story/Feature/Epic）時，應嘗試向上查詢 parent

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: 系統 MUST 在 PR 資料結構中包含 PR 唯一識別碼欄位
- **FR-002**: 系統 MUST 在 GitLab 平台使用 Merge Request 的專案內唯一編號（iid）作為 PR ID
- **FR-003**: 系統 MUST 在 Bitbucket 平台使用 Pull Request 的 Repository 內唯一編號（id）作為 PR ID
- **FR-004**: 系統 MUST 在抓取 Work Item 時，為每筆 Work Item 保留其來源 PR 的識別碼、專案名稱與 PR 網址
- **FR-005**: 系統 MUST 當同一 Work Item ID 出現在多筆 PR 中時，產生多筆獨立輸出記錄（一對一），不進行去重複
- **FR-006**: 系統 MUST 對重複的 Work Item ID 僅向 Azure DevOps API 查詢一次，使用快取避免重複請求
- **FR-007**: 系統 MUST 提供 `get-user-story` 指令，從 Redis 讀取已抓取的 Work Item 資料進行 User Story 解析
- **FR-008**: 系統 MUST 將 User Story、Feature、Epic 視為「高層級類型」，遇到這些類型時直接保留不再向上查詢
- **FR-009**: 系統 MUST 對非高層級類型的 Work Item（如 Task、Bug），透過 Azure DevOps API 遞迴向上查詢 parent 直到找到高層級類型
- **FR-010**: 系統 MUST 在遞迴查詢過程中遇到以下情況時，保留原始 Work Item 資料：無 parent 關聯、API 查詢失敗、超過最大遍歷深度
- **FR-011**: 系統 MUST 在原始 Work Item 抓取就已失敗的情況下，直接保留該筆失敗記錄
- **FR-012**: 系統 MUST 將解析結果存放至獨立的 Redis key `AzureDevOps:UserStories`
- **FR-013**: 系統 MUST 在解析結果中保留原始 Work Item 的識別碼，以便追溯

### Key Entities *(include if feature involves data)*

- **MergeRequest（合併請求）**: 代表一筆 PR/MR 資料。關鍵屬性包含 PR ID、標題、來源分支、目標分支、作者、狀態、建立時間、合併時間、平台類型、專案路徑
- **WorkItem（工作項目）**: 代表一筆 Azure DevOps 工作項目。關鍵屬性包含 Work Item ID、標題、類型、狀態、網址、區域路徑、父層 Work Item ID
- **WorkItemOutput（工作項目輸出）**: 代表一筆含 PR 來源資訊的工作項目輸出。除了基本 WorkItem 欄位外，額外包含來源 PR ID、來源專案名稱、來源 PR 網址
- **UserStoryOutput（使用者故事輸出）**: 代表一筆解析至 User Story 層級的結果。包含解析後的 Work Item 資訊與原始 Work Item ID

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 所有透過系統抓取的 PR 資料，100% 包含 PR 唯一識別碼欄位
- **SC-002**: 所有 Work Item 輸出記錄，100% 包含來源 PR 的識別碼、專案名稱與網址
- **SC-003**: 當同一 Work Item 被多筆 PR 提及時，每一筆 PR 都有對應的獨立輸出記錄
- **SC-004**: 執行 `get-user-story` 指令後，所有非失敗的解析結果其類型應為 User Story、Feature、Epic 其中之一，或者為保留原始資料的情況
- **SC-005**: 執行 `get-user-story` 指令後，結果正確存放於 `AzureDevOps:UserStories` key 中，且每筆記錄都包含原始 Work Item ID
- **SC-006**: 對於相同的 Work Item ID，系統僅向 Azure DevOps API 發送一次查詢請求
