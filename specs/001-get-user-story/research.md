# Research: 取得 User Story 層級資訊

**Feature Branch**: `001-get-user-story`
**Date**: 2026-02-17

## R-001: Azure DevOps API 取得 Parent 關係的方式

**Decision**: 利用現有的 `$expand=all` API 呼叫，從回傳的 `relations` 陣列中解析 Parent Work Item ID。

**Rationale**:
- 現有的 `AzureDevOpsRepository.GetWorkItemAsync()` 已使用 `$expand=all` 參數，API 回應中已包含 `relations` 陣列
- 目前的 `AzureDevOpsWorkItemResponse` 模型未定義 `relations` 欄位，導致該資料被忽略
- 只需擴充回應模型與 Mapper，即可取得 Parent 資訊，無需額外 API 呼叫
- Azure DevOps API 中 Parent 關係的 `rel` 值為 `System.LinkTypes.Hierarchy-Reverse`，URL 格式為 `https://dev.azure.com/{org}/{project}/_apis/wit/workitems/{parentId}`

**Alternatives Considered**:
1. **新增獨立的 API 呼叫 (`/relations` endpoint)**: 需額外 HTTP 請求，增加延遲，且 `$expand=all` 已包含此資訊
2. **使用 WIQL 查詢 Parent**: 過度複雜，不符合 KISS 原則

---

## R-002: Work Item Type 階層分類

**Decision**: 定義「User Story 以上」包含 `User Story`、`Feature`、`Epic` 三種類型，使用常數集合管理。

**Rationale**:
- Azure DevOps 預設 Agile 流程範本的 Work Item 階層為：Epic → Feature → User Story → Task / Bug
- User Story 為需求追蹤的基本單位，Feature 與 Epic 為更高層級的需求分類
- 以常數集合管理，便於未來擴充或調整
- 未知類型視為「User Story 以下」，觸發遞迴查找以確保安全

**Alternatives Considered**:
1. **僅以 User Story 為目標**: 過於限制，可能遺失已在 Feature/Epic 層級的項目
2. **動態偵測階層**: 過度工程化，Azure DevOps 的 Work Item 階層在同一專案中通常是固定的

---

## R-003: 新 Redis Key 命名

**Decision**: 使用 `AzureDevOps:WorkItems:UserStories` 作為新的 Redis Key。

**Rationale**:
- 遵循現有命名慣例（`Namespace:Resource:SubResource`）
- 與來源資料 `AzureDevOps:WorkItems` 在同一命名空間下，語意清晰
- 表達此 Key 儲存的是經 User Story 解析後的 Work Item 資料

**Alternatives Considered**:
1. **`AzureDevOps:UserStories`**: 過於簡化，未表達與原始 Work Items 的關聯
2. **`AzureDevOps:WorkItems:Resolved`**: 語意不夠明確

---

## R-004: 遞迴策略與防護機制

**Decision**: 使用迭代式遞迴（while loop）搭配已訪問 ID 集合偵測循環參照，最大深度限制為 10 層。

**Rationale**:
- Azure DevOps 實務上 Work Item 階層深度很少超過 5 層（Epic → Feature → User Story → Task → Sub-task）
- 10 層限制提供足夠的安全邊界
- 使用 `HashSet<int>` 追蹤已訪問的 Work Item ID，有效偵測循環參照
- 迭代式比遞迴式更好控制深度限制且避免 Stack Overflow

**Alternatives Considered**:
1. **無限制遞迴**: 風險過高，可能因資料異常導致無窮迴圈
2. **僅限制 3 層**: 過於保守，可能在特殊流程中無法找到 User Story

---

## R-005: 新任務實作模式

**Decision**: 建立 `GetUserStoryTask` 實作 `ITask` 介面，遵循現有 Task 模式（讀取 Redis → 處理 → 寫入 Redis）。

**Rationale**:
- 與現有 `FetchAzureDevOpsWorkItemsTask`、`BaseFilterPullRequestsByUserTask` 模式一致
- Task 直接實作 `ITask`（不使用 Base Class），因為邏輯較為獨立
- 依賴 `IRedisService`（讀取/寫入）和 `IAzureDevOpsRepository`（查詢 Parent）

**Alternatives Considered**:
1. **抽象為 Base Class**: 無其他類似任務需要共用邏輯，不符合 YAGNI
2. **使用 MediatR Handler**: 現有專案雖參照 CQRS 但實際使用 Task Pattern，保持一致性

---

## R-006: 可重用元件清單

以下現有元件將直接重用：

| 元件 | 位置 | 用途 |
|------|------|------|
| `IRedisService` | Domain/Abstractions | Redis 讀寫操作 |
| `IAzureDevOpsRepository` | Domain/Abstractions | 取得 Work Item 詳細資訊（含 Parent） |
| `RedisKeys` | Common/Constants | Redis Key 常數管理 |
| `JsonExtensions` | Common/Extensions | JSON 序列化/反序列化 |
| `Result<T>` | Domain/Common | 錯誤處理模式 |
| `WorkItemOutput` | Application/Common | 原始 Work Item 輸出模型（直接內嵌至新模型） |
| `WorkItemFetchResult` | Application/Common | 作為讀取來源的資料結構 |
| `TaskFactory` | Application/Tasks | 任務建立工廠（擴充新任務） |
| `CommandLineParser` | Console/Parsers | CLI 參數解析（擴充新指令） |
| `AzureDevOpsWorkItemMapper` | Infrastructure/AzureDevOps/Mappers | Work Item 映射（擴充 Parent ID 解析） |
