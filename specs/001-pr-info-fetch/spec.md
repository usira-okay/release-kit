# Feature Specification: GitLab / Bitbucket PR 資訊擷取

**Feature Branch**: `001-pr-info-fetch`  
**Created**: 2026-01-31  
**Status**: Draft  
**Input**: User description: "從 GitLab 與 Bitbucket 平台擷取 PR/MR 資訊，支援時間區間篩選與分支差異比對模式，並產出統一格式的輸出"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - 依時間區間擷取已合併的 PR (Priority: P1)

使用者需要查詢特定時間範圍內已合併到目標分支的所有 PR/MR，以便產生該期間的變更報告。

**Why this priority**: 這是最常見的使用情境，用於產生週報、月報或特定版本的變更清單。

**Independent Test**: 可透過指定時間區間與目標分支，驗證是否能正確取得該期間內所有已合併的 PR 列表。

**Acceptance Scenarios**:

1. **Given** 使用者設定 `FetchMode=DateTimeRange`、`TargetBranch=main`、`StartDateTime=2024-03-01`、`EndDateTime=2024-03-31`，**When** 執行擷取，**Then** 系統應回傳該時間範圍內所有合併到 `main` 的 PR 清單
2. **Given** 使用者設定的時間區間內沒有任何已合併的 PR，**When** 執行擷取，**Then** 系統應回傳空的 PR 清單，不拋出錯誤
3. **Given** 平台僅支援 `updated_after` 篩選（如 GitLab），**When** 執行擷取，**Then** 系統應在取得資料後，以 `merged_at` 二次過濾出正確的時間範圍

---

### User Story 2 - 支援 GitLab 與 Bitbucket 雙平台 (Priority: P1)

系統需要同時支援 GitLab 與 Bitbucket 兩個平台，使用相同的輸出格式。

**Why this priority**: 公司可能同時使用多個 Git 平台，需要統一的資料格式進行後續處理。

**Independent Test**: 可分別對 GitLab 和 Bitbucket 專案執行擷取，驗證輸出格式一致。

**Acceptance Scenarios**:

1. **Given** 設定檔包含 GitLab 與 Bitbucket 專案，**When** 執行擷取，**Then** 兩種平台的輸出欄位應一致（Title, Description, SourceBranch, TargetBranch, MergedAt, Author 等）
2. **Given** Bitbucket 使用 `closed_on` 欄位而非 `merged_at`，**When** 輸出資料，**Then** 應統一映射到 `MergedAt` 欄位
3. **Given** GitLab 使用 Merge Request 術語，Bitbucket 使用 Pull Request 術語，**When** 系統處理資料，**Then** 應統一使用 MergeRequest 作為內部實體名稱

---

### User Story 3 - 依分支差異擷取相關 PR (Priority: P2)

使用者需要查詢特定版本分支與目標分支之間的差異，取得這些差異對應的 PR 資訊，以便了解特定版本尚未包含的變更。

**Why this priority**: 這對於版本規劃與追蹤非常重要，能幫助 RD/PM 了解哪些功能已包含在特定版本中。

**Independent Test**: 可透過指定來源分支與目標分支，驗證是否能正確取得分支差異對應的 PR 列表。

**Acceptance Scenarios**:

1. **Given** 使用者設定 `FetchMode=BranchDiff`、`SourceBranch=release/20240301`、`TargetBranch=main`，且該 SourceBranch 是最新的 release 分支，**When** 執行擷取，**Then** 系統應取得 `main` 有但 `release/20240301` 沒有的 commits，並查詢對應的 PR
2. **Given** 使用者指定的 SourceBranch 不是最新的 release 分支，**When** 執行擷取，**Then** 系統應自動找到下一版 release 分支進行比較
3. **Given** 比較結果有 commits，**When** 系統查詢 commit 對應的 PR，**Then** 每個 commit 應正確映射到其關聯的 PR 資訊

---

### User Story 4 - 多專案批次擷取 (Priority: P2)

使用者需要一次擷取多個專案的 PR 資訊，設定檔中可定義多個專案，並支援階層式設定覆蓋。

**Why this priority**: 企業環境中常有多個 repository，需要統一管理與擷取。

**Independent Test**: 可透過設定多個專案，驗證是否每個專案都能正確套用其專屬設定或繼承根設定。

**Acceptance Scenarios**:

1. **Given** 設定檔包含 3 個專案，其中 1 個有專屬的 `TargetBranch` 設定，**When** 執行擷取，**Then** 該專案使用專屬設定，其餘 2 個使用根層級設定
2. **Given** 專案層級與根層級都有相同設定項，**When** 執行擷取，**Then** 專案層級設定優先於根層級設定
3. **Given** 設定檔包含混合平台（GitLab 與 Bitbucket）的多個專案，**When** 執行擷取，**Then** 系統應正確識別各專案的平台類型並使用對應的擷取邏輯

---

### Edge Cases

- 當指定的分支不存在時，系統應回傳明確的錯誤訊息，指出哪個分支找不到
- 當平台 API 回傳分頁結果時，系統應自動處理分頁以取得完整資料
- 當網路連線中斷或 API 回傳錯誤時，系統應提供重試機制或明確的錯誤訊息
- 當 PR 已被刪除或無法存取時，系統應略過該筆資料並記錄警告
- 當時間區間跨越多個時區時，系統應統一使用 UTC 時間處理
- 當單一 commit 關聯多個 PR 時，系統應回傳所有相關的 PR 資訊

## Requirements *(mandatory)*

### Functional Requirements

#### 核心擷取功能

- **FR-001**: 系統 MUST 支援從 GitLab 平台擷取已合併的 Merge Request 資訊
- **FR-002**: 系統 MUST 支援從 Bitbucket 平台擷取已合併的 Pull Request 資訊
- **FR-003**: 系統 MUST 支援「時間區間模式」（DateTimeRange），根據合併時間篩選 PR
- **FR-004**: 系統 MUST 支援「分支差異模式」（BranchDiff），比較兩個分支的 commit 差異並查詢對應的 PR

#### 資料映射與輸出

- **FR-005**: 系統 MUST 將不同平台的欄位名稱統一映射到標準欄位（如 Bitbucket 的 `closed_on` 映射到 `MergedAt`）
- **FR-006**: 系統 MUST 輸出以下標準欄位：Title, Description, SourceBranch, TargetBranch, MergedAt, Author, PlatformType, ProjectId, MergeRequestId
- **FR-007**: 系統 MUST 以 UTC 時間格式儲存與輸出所有時間戳記

#### 設定與配置

- **FR-008**: 系統 MUST 支援設定檔定義多個專案，每個專案可指定不同的平台類型
- **FR-009**: 系統 MUST 支援階層式設定覆蓋，專案層級設定優先於根層級設定
- **FR-010**: 必要的設定項（如平台 URL、專案 ID）若未設定，系統 MUST 於啟動時拋出明確錯誤訊息

#### 分支差異比對

- **FR-011**: 系統 MUST 能取得兩個分支之間的 commit 差異清單
- **FR-012**: 系統 MUST 能根據 commit SHA 查詢對應的 PR 資訊
- **FR-013**: 當 SourceBranch 不是最新 release 分支時，系統 SHOULD 提供選項自動找到下一版 release 分支進行比較

#### 錯誤處理

- **FR-014**: 系統 MUST 在分支不存在時回傳明確錯誤訊息
- **FR-015**: 系統 MUST 自動處理 API 分頁以取得完整資料
- **FR-016**: 系統 SHOULD 在網路錯誤時提供重試機制

### Key Entities

- **MergeRequest**: 代表一個已合併的 PR/MR，包含標題、描述、來源分支、目標分支、合併時間、作者等資訊
- **Project**: 代表一個 Git 專案/Repository，包含專案 ID、平台類型、專屬設定等資訊
- **FetchQuery**: 代表一次擷取請求的參數，包含擷取模式（DateTimeRange 或 BranchDiff）、時間區間或分支資訊
- **SourceControlPlatform**: 代表支援的平台類型（GitLab、Bitbucket）

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 使用者可在 30 秒內完成單一專案的 PR 資訊擷取（100 筆 PR 以內）
- **SC-002**: 系統可正確處理包含 1000 筆以上 PR 的大型專案，不遺漏資料
- **SC-003**: GitLab 與 Bitbucket 的輸出格式 100% 一致，所有標準欄位皆有對應值
- **SC-004**: 時間區間篩選的準確率達 100%（僅回傳在指定時間範圍內合併的 PR）
- **SC-005**: 分支差異比對能正確識別 95% 以上的 commit 對應 PR 關係（少數 direct commit 無對應 PR 屬正常情況）
- **SC-006**: 多專案批次擷取時，設定繼承與覆蓋邏輯正確率達 100%

## Assumptions

- 使用者已具備各平台的 API 存取權限（Token 或 Credentials）
- 各平台的 API 版本穩定，不會頻繁變更欄位名稱或結構
- 網路環境穩定，偶發的網路錯誤可透過重試機制解決
- Release 分支命名遵循統一的命名規則（如 `release/YYYYMMDD`），以便系統自動識別下一版
- PR/MR 的標題與描述採用可識別 Work Item ID 的格式（如 `VSTS123456`）
