# Feature Specification: Azure DevOps Work Item 資訊擷取

**Feature Branch**: `003-fetch-azure-workitems`
**Created**: 2026-02-13
**Status**: Draft
**Input**: User description: "新增 console arg `fetch-azure-workitems`，從 Redis 讀取已過濾的 GitLab 與 Bitbucket PR 資訊，解析 PR title 中的 `VSTS{number}` 關鍵字，呼叫 Azure DevOps API 取得 Work Item 詳細資訊。"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - 從 PR 標題解析 VSTS Work Item ID (Priority: P1)

身為 Release Notes 產出人員，我希望系統能自動從已過濾的 GitLab 與 Bitbucket PR 標題中解析出 `VSTS{number}` 格式的 Work Item ID，以便後續取得 Work Item 詳細資訊。

**Why this priority**: 這是整個功能的核心前置步驟。若無法正確解析 VSTS ID，後續所有流程都無法進行。

**Independent Test**: 可透過提供包含各種 VSTS ID 格式的 PR 標題資料來獨立驗證解析邏輯，無需實際呼叫外部 API。

**Acceptance Scenarios**:

1. **Given** Redis 中存在已過濾的 GitLab PR 資料（key: `GitLab:PullRequests:ByUser`），且 PR 標題包含 `VSTS12345`, **When** 執行 `fetch-azure-workitems` 指令, **Then** 系統成功解析出 Work Item ID `12345`
2. **Given** 一個 PR 標題同時包含 `VSTS111` 與 `VSTS222`, **When** 系統解析該標題, **Then** 兩個 Work Item ID（111、222）皆被解析出來
3. **Given** 多個 PR 標題包含相同的 `VSTS99999`, **When** 系統合併解析結果, **Then** Work Item ID `99999` 只出現一次（已去重複）
4. **Given** PR 標題不包含任何 VSTS 關鍵字, **When** 系統解析該標題, **Then** 該 PR 不產生任何 Work Item ID

---

### User Story 2 - 呼叫 Azure DevOps API 取得 Work Item 詳細資訊 (Priority: P1)

身為 Release Notes 產出人員，我希望系統能根據解析出的 VSTS ID，逐一向 Azure DevOps API 查詢 Work Item 的完整資訊（標題、類型、狀態、所屬團隊區域路徑、連結），以便產出完整的 Release Notes。

**Why this priority**: 取得 Work Item 詳細資訊是本功能的核心價值，直接影響後續 Release Notes 的完整性。

**Independent Test**: 可透過模擬 Azure DevOps API 回應來獨立驗證資料擷取與轉換邏輯。

**Acceptance Scenarios**:

1. **Given** 已解析出 Work Item ID `12345` 且 Azure DevOps API 可正常存取, **When** 系統查詢該 Work Item, **Then** 回傳包含標題、類型（Bug/Task/User Story）、狀態（New/Active/Resolved/Closed）、連結 URL、以及區域路徑（AreaPath）的完整資訊
2. **Given** 已解析出 Work Item ID `99999` 但該 ID 不存在, **When** 系統查詢該 Work Item, **Then** 系統記錄查詢失敗、標示該 Work Item 為失敗狀態，並繼續處理其他 Work Item
3. **Given** Azure DevOps API 驗證失敗（PAT 無效）, **When** 系統嘗試查詢 Work Item, **Then** 系統記錄驗證錯誤訊息

---

### User Story 3 - 將結果儲存至 Redis 並輸出摘要 (Priority: P2)

身為 Release Notes 產出人員，我希望系統將所有 Work Item 查詢結果儲存至 Redis，並在 Console 輸出統計摘要，以便後續步驟使用資料並即時了解處理狀況。

**Why this priority**: 資料持久化與摘要輸出提升使用者體驗，但主要價值建立在前兩個故事的基礎上。

**Independent Test**: 可透過預先準備好的 Work Item 查詢結果來驗證 Redis 寫入與 Console 輸出格式。

**Acceptance Scenarios**:

1. **Given** 系統成功取得 3 個 Work Item 資訊且 1 個失敗, **When** 處理完成, **Then** 系統將 4 筆結果（含成功與失敗）寫入 Redis（key: `AzureDevOps:WorkItems`），且不設定過期時間
2. **Given** 系統完成所有 Work Item 查詢, **When** 結果寫入 Redis 後, **Then** Console 輸出統計摘要，包含：分析的 PR 總數、找到的 Work Item ID 總數、成功擷取數、失敗數

---

### User Story 4 - 處理部分 Redis 資料不存在的情境 (Priority: P2)

身為 Release Notes 產出人員，我希望當 GitLab 或 Bitbucket 的 PR 資料只有一方存在時，系統仍能正常處理已存在的資料。

**Why this priority**: 確保系統在不完整資料環境下仍具備韌性，但屬於容錯機制而非核心功能。

**Independent Test**: 可透過只設定一個 Redis key 來驗證系統的容錯行為。

**Acceptance Scenarios**:

1. **Given** Redis 中只存在 `GitLab:PullRequests:ByUser`，而 `Bitbucket:PullRequests:ByUser` 不存在, **When** 執行 `fetch-azure-workitems`, **Then** 系統對缺失的 key 記錄警告訊息，並正常處理 GitLab 的 PR 資料
2. **Given** Redis 中兩個 PR key 都不存在, **When** 執行 `fetch-azure-workitems`, **Then** 系統記錄警告訊息並正常結束，不進行任何 API 呼叫

---

### Edge Cases

- 當 PR 標題包含類似但非正確格式的字串（如 `VSTSabc`、`vsts123`、`VSTS`）時，系統如何處理？
  - **假設**: 僅匹配大寫 `VSTS` 後接一或多位數字的格式（`VSTS\d+`），其餘忽略
- 當所有解析出的 Work Item ID 在 Azure DevOps 都不存在時，結果如何？
  - 系統仍將所有失敗記錄（含 WorkItemId + 錯誤原因）寫入 Redis 並輸出統計
- 當同一 VSTS ID 出現在 GitLab 與 Bitbucket 的不同 PR 中時？
  - 去重複後只查詢一次
- Azure DevOps API 發生網路超時或暫時性錯誤時？
  - 記錄為失敗，不重試，繼續處理下一個 Work Item

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: 系統必須支援 `fetch-azure-workitems` Console 指令，觸發 Azure DevOps Work Item 資訊擷取流程
- **FR-002**: 系統必須從 Redis 讀取 `GitLab:PullRequests:ByUser` 與 `Bitbucket:PullRequests:ByUser` 兩個 key 的 PR 資料
- **FR-003**: 系統必須使用正規表達式 `VSTS(\d+)` 解析每個 PR 標題中所有符合的 Work Item ID
- **FR-004**: 系統必須對解析出的 Work Item ID 進行去重複處理
- **FR-005**: 系統必須逐一循序呼叫 Azure DevOps API 取得每個 Work Item 的詳細資訊（標題、類型、狀態、連結 URL、區域路徑）
- **FR-006**: 系統必須使用 Basic Auth 認證方式（空使用者名稱 + Personal Access Token）存取 Azure DevOps API
- **FR-007**: 當單一 Work Item 查詢失敗時，系統必須記錄錯誤並繼續處理剩餘的 Work Item
- **FR-008**: 系統必須將完整的查詢結果（含成功與失敗記錄）寫入 Redis key `AzureDevOps:WorkItems`，且不設定過期時間
- **FR-009**: 系統必須在 Console 輸出統計摘要（分析 PR 數、Work Item 總數、成功數、失敗數）
- **FR-010**: 當兩個 Redis PR key 都不存在時，系統必須記錄警告並正常結束
- **FR-011**: 當僅一個 Redis PR key 存在時，系統必須記錄警告並繼續處理存在的資料
- **FR-012**: 系統必須將 Azure DevOps 回傳的 AreaPath 直接作為 OriginalTeamName 儲存（本階段不進行 TeamMapping 轉換）
- **FR-013**: 每筆 Work Item 輸出結果必須包含成功/失敗標示，失敗時附帶錯誤原因說明

### Key Entities

- **WorkItem**: 代表 Azure DevOps 上的工作項目，包含 Work Item ID、標題、類型（Bug/Task/User Story）、狀態（New/Active/Resolved/Closed）、連結 URL、所屬區域路徑（OriginalTeamName）
- **WorkItemOutput**: 代表單一 Work Item 查詢結果的輸出格式，除了 WorkItem 欄位外，額外包含成功/失敗標示與錯誤訊息
- **WorkItemFetchResult**: 代表整體查詢結果的彙整，包含所有 WorkItemOutput 清單以及統計數據（分析 PR 數、Work Item 總數、成功數、失敗數）

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 使用者執行 `fetch-azure-workitems` 指令後，系統能正確解析所有 PR 標題中的 VSTS ID，解析正確率達 100%
- **SC-002**: 系統能從 Azure DevOps 成功取得存在且有權限的 Work Item 詳細資訊，成功率不因系統邏輯而降低（僅受外部 API 可用性影響）
- **SC-003**: 單一 Work Item 查詢失敗不影響其餘 Work Item 的處理，系統持續處理完成率達 100%
- **SC-004**: 查詢結果完整寫入 Redis 後，後續步驟可直接讀取使用，資料完整性達 100%
- **SC-005**: Console 統計摘要能正確反映實際處理結果，使用者可立即了解處理狀況

## Assumptions

- Redis 中已過濾的 PR 資料格式與既有 `filter-gitlab-pr-by-user` / `filter-bitbucket-pr-by-user` 指令的輸出格式一致
- Azure DevOps PAT 具備讀取 Work Item 的權限
- VSTS ID 格式固定為大寫 `VSTS` 後接數字（case-sensitive）
- 本階段不處理 TeamMapping 對應邏輯，AreaPath 原始值直接儲存
- 不需要 PR 與 Work Item 的反向關聯（不保留 SourcePRUrls）
- Azure DevOps API 使用 REST API v7.0
- Work Item 查詢不需要併發策略，逐一循序呼叫即可滿足需求

## Decisions

| # | 決策項目 | 結論 | 理由 |
|---|---------|------|------|
| 1 | TeamMapping / AreaPath 比對 | 本階段不處理，後續步驟再處理 | 降低複雜度，分階段實作 |
| 2 | 一個 PR Title 包含多個 VSTS ID | 解析所有 VSTS ID | 確保不遺漏任何關聯的 Work Item |
| 3 | Work Item 查詢併發策略 | 不需要併發，逐一循序呼叫 | 簡化實作，目前資料量不需要併發 |
| 4 | 輸出保留 PR 與 Work Item 關聯 | 不需要 SourcePRUrls 欄位 | 本階段需求不包含反向追溯 |
| 5 | Redis TTL | 不設定過期時間 | 資料需要由後續步驟使用，不應自動失效 |
