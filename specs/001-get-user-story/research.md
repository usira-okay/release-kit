# Research: Get User Story

**Date**: 2026-02-14
**Branch**: `001-get-user-story`

## 研究項目

### R-001: Azure DevOps API Parent Relation 格式

**決定**: 使用 `$expand=all` 取得 Work Item 的 `relations` 欄位，從中篩選 `rel` 為 `System.LinkTypes.Hierarchy-Reverse` 的關聯，解析 `url` 末段取得 parent Work Item ID。

**理由**:
- 現有 `IAzureDevOpsRepository.GetWorkItemAsync` 已使用 `$expand=all` 參數，API 回應中已包含 `relations` 欄位
- `System.LinkTypes.Hierarchy-Reverse` 是 Azure DevOps 標準的 parent 關聯類型
- URL 格式為 `https://dev.azure.com/{org}/_apis/wit/workItems/{parentId}`，取末段即為 parent ID

**替代方案評估**:
- 使用 `fields["System.Parent"]`：此欄位僅在 Azure DevOps Services 2020+ 可用，且需要額外 API 版本確認。relations 方式更通用。
- 使用 WIQL 查詢：過度複雜，需要額外的 API 端點，不符合 KISS 原則。

---

### R-002: 遞迴深度限制

**決定**: 設定最大遍歷深度為 10 層。

**理由**:
- Azure DevOps 的 Work Item 階層結構通常為 Epic → Feature → User Story → Task/Bug，最多 4 層
- 10 層提供充足的安全邊際，同時避免異常資料造成無限迴圈
- 超過深度時保留原始 Work Item 資料（非錯誤處理，而是回退策略）

**替代方案評估**:
- 無深度限制：風險過高，可能因循環參照導致無限迴圈
- 深度限制 5：過於保守，若有自訂的多層 Work Item 類型可能不夠

---

### R-003: Work Item 一對一 vs 去重複策略

**決定**: 採用一對一記錄，每個 (Work Item ID, PR) 配對產生一筆獨立的輸出記錄。重複的 Work Item ID 僅向 API 查詢一次，使用 Dictionary 快取。

**理由**:
- 使用者明確要求「不需要用 Dictionary，直接用 list 一筆一筆 work item 紀錄就好，先不用考慮重複」
- 保留 PR 來源資訊的完整性，每筆記錄都可追溯到具體的 PR
- API 查詢使用快取避免重複呼叫，兼顧效能

**替代方案評估**:
- Dictionary 去重複（使用者否決）：丟失 PR 來源關聯，無法追溯 Work Item 來自哪個 PR
- 不快取 API 結果：浪費 API 呼叫次數，違反效能與快取優先原則

---

### R-004: 高層級類型判定

**決定**: User Story、Feature、Epic 三種類型視為「高層級類型」，使用 `HashSet<string>` 以 `OrdinalIgnoreCase` 比較器進行判定。

**理由**:
- 使用者確認「User Story 以上」包含 User Story、Feature、Epic
- 使用 HashSet 進行 O(1) 查找，效能最佳
- 不區分大小寫以確保與 Azure DevOps API 回傳值相容

**替代方案評估**:
- 使用 Enum：過度設計，且 Azure DevOps 的 Work Item Type 為自訂字串，不適合用 Enum
- 使用 contains 字串比較：效能較差，且不夠精確

---

### R-005: UserStory 解析結果儲存策略

**決定**: 使用新的 Redis key `AzureDevOps:UserStories` 儲存解析結果，不覆寫原始 Work Item 資料。

**理由**:
- 使用者要求「請建立一個新的 Key 來儲存這些處理過的資訊」
- 保留原始資料允許重新執行解析而不需重新抓取 Work Item
- 資料結構 `UserStoryFetchResult` 包含統計資訊（已是 US 數量、成功解析數量、保留原始數量）

**替代方案評估**:
- 覆寫原始 Redis key（使用者否決）：破壞原始資料，無法重新執行
- 使用 Redis Hash 結構：過度複雜，JSON 字串足以滿足需求

## NEEDS CLARIFICATION 狀態

> 所有疑問已在 brainstorming 階段由使用者確認，無剩餘 NEEDS CLARIFICATION 項目。
