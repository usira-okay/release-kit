# Feature Specification: Azure Work Item User Story Resolution

**Feature Branch**: `002-get-user-story`  
**Created**: 2026-02-18  
**Status**: Draft  
**Input**: User description: "新增一個 console arg，用來處理當前 redis 資料中 azure work item 不為 User Story 或以上的類型時,遞迴找 Parent 的功能，並且將取得的資訊存到 Redis 中"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - 解析並轉換 Work Item 至 User Story 層級 (Priority: P1)

作為 Release Notes 產出者，我需要系統能夠自動將 Redis 中所有低於 User Story 層級的 Work Item（如 Bug、Task）轉換為其對應的 User Story，讓我可以在 Release Notes 中以 User Story 為單位進行統計與報告。

**Why this priority**: 這是核心功能，沒有這個轉換功能，使用者無法取得以 User Story 為單位的 Release Notes 資料，這是整個功能的基礎。

**Independent Test**: 可以透過執行新的 console 命令，驗證 Redis 中新增的 Key 包含所有 Work Item 轉換後的 User Story 資料，且原始資料被正確保留。

**Acceptance Scenarios**:

1. **Given** Redis 中有 5 筆 Bug 類型的 Work Item，每筆都有對應的 Parent User Story，**When** 執行 console 命令進行轉換，**Then** 新的 Redis Key 中應包含 5 筆資料，每筆的 Type 為 "User Story"，並保留原始 Bug 資訊在 `originalWorkItem` 欄位中，`resolutionStatus` 為 "FoundViaRecursion"
2. **Given** Redis 中有 3 筆 Task 類型的 Work Item，其中 2 筆有 Parent User Story，1 筆無法找到 Parent，**When** 執行 console 命令進行轉換，**Then** 新的 Redis Key 中應包含 3 筆資料，2 筆的 Type 為 "User Story"（`resolutionStatus` 為 "FoundViaRecursion"），1 筆保持原始 Task 資料（`resolutionStatus` 為 "NotFound"）
3. **Given** Redis 中有 2 筆已經是 User Story 的 Work Item，**When** 執行 console 命令進行轉換，**Then** 新的 Redis Key 中應包含 2 筆資料，照抄原始資料，`originalWorkItem` 欄位為 null，`resolutionStatus` 為 "AlreadyUserStoryOrAbove"
4. **Given** Redis 中有 1 筆 Bug，其 Parent 是 Task，Task 的 Parent 是 User Story（需遞迴 2 層），**When** 執行 console 命令進行轉換，**Then** 新的 Redis Key 中應包含 1 筆資料，Type 為 "User Story"（最終找到的），`originalWorkItem` 保留原始 Bug 資訊，`resolutionStatus` 為 "FoundViaRecursion"
5. **Given** Redis 中沒有任何 Work Item 資料，**When** 執行 console 命令，**Then** 系統應顯示訊息 "Redis 中無 Work Item 資料" 並正常結束，不產生錯誤

---

### User Story 2 - 處理無法取得資訊的 Work Item (Priority: P2)

作為 Release Notes 產出者，當某些 Work Item 無法從 Azure DevOps 取得完整資訊時（API 失敗、權限不足、Work Item 已刪除等），我需要系統能夠保留這些失敗記錄，讓我知道哪些資料可能不完整。

**Why this priority**: 這是資料完整性的保障，確保使用者能夠追蹤哪些資料可能有問題，避免資料遺失的風險。

**Independent Test**: 可以模擬 Azure DevOps API 回傳錯誤的情境，驗證系統正確記錄失敗狀態並保留原始 Work Item ID。

**Acceptance Scenarios**:

1. **Given** Redis 中有 1 筆 Bug，其對應的 Azure DevOps Work Item 已被刪除或無權限存取，**When** 執行 console 命令進行轉換，**Then** 新的 Redis Key 中應包含 1 筆資料，保留原始 Work Item ID，`isSuccess` 為 false，`errorMessage` 說明無法取得資訊的原因，`resolutionStatus` 為 "OriginalFetchFailed"
2. **Given** Redis 中有 1 筆 Task，其 Parent User Story 的 API 請求逾時，**When** 執行 console 命令進行轉換，**Then** 新的 Redis Key 中應包含 1 筆資料，保留原始 Task 資訊，`isSuccess` 為 false，`errorMessage` 說明 API 逾時，`resolutionStatus` 為 "NotFound"

---

### User Story 3 - 避免無限遞迴與循環參照 (Priority: P3)

作為系統管理者，當 Azure DevOps 中存在異常的 Parent-Child 關係（如循環參照：A → B → A）時，我需要系統能夠偵測並安全停止遞迴，避免系統崩潰或無限循環。

**Why this priority**: 這是系統穩定性的防禦機制，雖然在正常情況下不太會遇到，但必須確保系統不會因為異常資料而崩潰。

**Independent Test**: 可以在測試環境中建立循環參照的 Work Item 關係，驗證系統能夠在達到最大遞迴深度或偵測到循環時安全停止。

**Acceptance Scenarios**:

1. **Given** Azure DevOps 中有循環參照（Bug A 的 Parent 是 Task B，Task B 的 Parent 是 Bug A），**When** 執行 console 命令進行轉換，**Then** 系統應在偵測到循環後停止遞迴，記錄錯誤訊息 "偵測到循環參照"，`resolutionStatus` 為 "NotFound"
2. **Given** 有一個 Work Item 的 Parent 鏈超過 10 層（假設最大遞迴深度為 10），**When** 執行 console 命令進行轉換，**Then** 系統應在達到最大深度時停止遞迴，記錄訊息 "超過最大遞迴深度"，`resolutionStatus` 為 "NotFound"

---

### Edge Cases

- **Redis 中資料格式錯誤**：如果 Redis 中的 Work Item 資料格式不符合預期（缺少必要欄位、JSON 格式錯誤），系統應記錄錯誤並跳過該筆資料，繼續處理其他資料
- **Azure DevOps API 回傳部分欄位為 null**：當 Parent User Story 的某些欄位（如 Title、State）為 null 時，系統應保留這些 null 值，不應中斷處理流程
- **同一 Work Item 被多筆原始資料參照**：如果多個 Bug 都指向同一個 User Story，系統應正確處理，每筆 Bug 都產生各自的轉換結果，即使 User Story 資訊相同
- **Redis 連線失敗**：當 Redis 無法連線時，系統應顯示明確的錯誤訊息（包含連線資訊）並安全退出，不應拋出未處理的例外
- **Azure DevOps API 速率限制**：當達到 API 速率限制時，系統應記錄警告訊息，並將受影響的 Work Item 標記為 `isSuccess: false`

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: 系統 MUST 提供一個新的 console 命令列參數（如 `--get-user-story`），用於觸發 Work Item 轉換功能
- **FR-002**: 系統 MUST 從 Redis 中讀取現有的 Azure Work Item 資料（使用現有的 Redis Key）
- **FR-003**: 系統 MUST 判斷 Work Item 的 Type 是否為 "User Story"、"Feature" 或 "Epic"（大小寫不敏感），這些視為 User Story 層級或以上的類型
- **FR-004**: 當 Work Item 的 Type 不為 User Story 層級或以上時，系統 MUST 透過 Azure DevOps API 遞迴查詢其 Parent Work Item，直到找到 User Story 層級或以上的類型
- **FR-005**: 系統 MUST 偵測循環參照（如 A → B → A），並在偵測到循環時停止遞迴
- **FR-006**: 系統 MUST 限制最大遞迴深度為 10 層，避免異常資料導致的無限遞迴
- **FR-007**: 系統 MUST 將轉換後的結果寫入新的 Redis Key（Key 名稱格式：`{原始Key}:user-stories`）
- **FR-008**: 轉換後的資料結構 MUST 包含以下欄位：
  - `workItemId`: 轉換後的 Work Item ID（User Story 的 ID）
  - `title`: 轉換後的標題
  - `type`: 轉換後的類型（應為 User Story 層級或以上）
  - `state`: 轉換後的狀態
  - `url`: 轉換後的 URL
  - `originalTeamName`: 原始團隊名稱
  - `isSuccess`: 是否成功取得資訊（boolean）
  - `errorMessage`: 錯誤訊息（若失敗）
  - `resolutionStatus`: 解析狀態（enum）
  - `originalWorkItem`: 原始 Work Item 資訊（若有轉換）
- **FR-009**: `resolutionStatus` 欄位 MUST 使用 enum 表示以下狀態：
  - `AlreadyUserStoryOrAbove`: 原始 Type 就是 User Story 或以上的類型
  - `FoundViaRecursion`: 透過遞迴找到 User Story 或以上的類型
  - `NotFound`: 無法找到 User Story 或以上的類型（遞迴過程中失敗或達到最大深度）
  - `OriginalFetchFailed`: 原始的 Work Item 就無法取得資訊
- **FR-010**: 當原始 Work Item 已經是 User Story 層級或以上時，系統 MUST 照抄原始資料，`originalWorkItem` 欄位為 null，`resolutionStatus` 為 `AlreadyUserStoryOrAbove`
- **FR-011**: 當無法從 Azure DevOps 取得 Work Item 資訊時（API 錯誤、權限不足、Work Item 不存在），系統 MUST 保留原始 Work Item 資訊，設定 `isSuccess` 為 false，並記錄錯誤原因於 `errorMessage`
- **FR-012**: 當 Redis 中沒有任何 Work Item 資料時，系統 MUST 顯示訊息並正常結束，不應拋出錯誤

### Key Entities

- **Work Item (Original)**: 從 Redis 中讀取的原始 Work Item 資料，包含 workItemId、title、type、state、url、originalTeamName 等欄位
- **Work Item (Resolved)**: 轉換後的 Work Item 資料，結構與原始相同，但 Type 應為 User Story 層級或以上，並新增 `resolutionStatus`、`originalWorkItem`、`isSuccess`、`errorMessage` 欄位
- **Resolution Status**: 列舉類型，表示 Work Item 的解析結果（AlreadyUserStoryOrAbove、FoundViaRecursion、NotFound、OriginalFetchFailed）

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 使用者執行新的 console 命令後，系統能在 30 秒內完成 100 筆 Work Item 的轉換（假設每筆平均需 1-2 次 API 呼叫）
- **SC-002**: 當 Redis 中所有 Work Item 都已經是 User Story 層級或以上時，轉換過程不應呼叫 Azure DevOps API（效能優化）
- **SC-003**: 當遇到循環參照或無法取得資訊的 Work Item 時，系統能夠繼續處理其他 Work Item，成功率達 100%（不應中斷整個批次處理）
- **SC-004**: 轉換後的資料完整保留原始資訊，使用者可以從新的 Redis Key 中同時取得 User Story 資訊與原始 Work Item 資訊，資料完整性達 100%
- **SC-005**: 當發生錯誤時（Redis 連線失敗、API 錯誤等），系統顯示的錯誤訊息能夠讓使用者在 5 分鐘內定位並解決問題（訊息包含具體的錯誤原因與受影響的資源）
