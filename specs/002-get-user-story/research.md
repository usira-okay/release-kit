# Research: Azure Work Item User Story Resolution

**Date**: 2026-02-18  
**Feature**: Azure Work Item User Story Resolution

## Research Questions

### Q1: 如何從 Azure DevOps API 取得 Work Item 的 Parent 關聯？

**Decision**: 使用現有的 `GetWorkItemAsync()` API，並從回應的 `relations` 欄位解析 Parent 關聯

**Rationale**:
- Azure DevOps REST API `_apis/wit/workitems/{id}?$expand=all` 回傳包含 `relations` 陣列
- `relations` 中 `rel` 為 `System.LinkTypes.Hierarchy-Reverse` 的項目即為 Parent
- `url` 欄位包含 Parent Work Item 的 API 端點，可從中解析出 Parent ID
- 需修改 `AzureDevOpsWorkItemResponse` 模型以支援 `relations` 欄位

**Alternatives Considered**:
1. **使用專門的 Relations API**
   - Rejected: 需要額外的 API 呼叫，增加複雜度與延遲
   - 現有的 `$expand=all` 已包含 relations 資訊
2. **使用 Work Item Batch API 一次取得多個 Parent**
   - Rejected: 過度優化，不符合 KISS 原則
   - 當前需求處理規模（數十至數百筆）不需要 Batch 優化

### Q2: 如何判斷 Work Item 類型是否為 User Story 層級或以上？

**Decision**: 建立 `WorkItemTypeConstants` 類別，定義 User Story 層級類型清單，使用大小寫不敏感比對

**Rationale**:
- 符合 Constitution VII（避免硬編碼）
- User Story 層級類型為固定集合：`User Story`、`Feature`、`Epic`
- 使用 `StringComparer.OrdinalIgnoreCase` 進行比對，支援不同大小寫變體
- 集中管理於 `ReleaseKit.Common/Constants/` 目錄

**Alternatives Considered**:
1. **使用 Enum 表示 Work Item Type**
   - Rejected: Azure DevOps 的 Work Item Type 可自訂，無法窮舉所有類型
   - 使用字串比對更具彈性
2. **使用正規表示式比對**
   - Rejected: 過度複雜，簡單的字串集合即可滿足需求

### Q3: 如何偵測循環參照（如 A → B → A）？

**Decision**: 使用 `HashSet<int>` 追蹤已訪問的 Work Item ID，在每次遞迴前檢查是否已存在

**Rationale**:
- `HashSet<int>` 提供 O(1) 的查詢效能
- 空間複雜度為 O(深度)，在最大深度 10 的限制下可接受
- 簡單且直觀，符合 KISS 原則

**Alternatives Considered**:
1. **在每次遞迴時檢查整個遞迴路徑**
   - Rejected: 時間複雜度為 O(n²)，效能較差
2. **使用圖論演算法（如 DFS）**
   - Rejected: 過度複雜，簡單的 HashSet 即可解決問題

### Q4: 遞迴深度限制應設為多少？

**Decision**: 預設 10 層，可透過 `appsettings.json` 設定 `GetUserStory:MaxDepth` 調整

**Rationale**:
- 分析實際 Azure DevOps 專案結構，正常情況下 Parent 鏈很少超過 5 層
  - 典型結構：Bug → Task → User Story (2-3 層)
  - 極端情況：Bug → Subtask → Task → User Story → Feature → Epic (最多 5 層)
- 10 層提供足夠的安全邊界，避免異常資料導致的效能問題
- 可設定化允許特殊情境調整（符合 Constitution XIII）

**Alternatives Considered**:
1. **固定深度 5 層**
   - Rejected: 可能不足以覆蓋所有合法情境
2. **無限制深度，僅依賴循環偵測**
   - Rejected: 若資料異常（如單向長鏈）可能導致效能問題

### Q5: 新的 Redis Key 命名規則？

**Decision**: `AzureDevOps:WorkItems:UserStories`

**Rationale**:
- 遵循現有 Redis Key 命名慣例（參考 `RedisKeys.AzureDevOpsWorkItems`）
- 使用階層式命名：`{Service}:{ResourceType}:{Qualifier}`
- 語意清楚：表示這是經過 User Story 解析處理的 Work Items
- 符合 Constitution VII（定義於 `RedisKeys` 常數類別）

**Alternatives Considered**:
1. **使用原 Key + 後綴（如 `AzureDevOps:WorkItems:Resolved`）**
   - Rejected: `Resolved` 語意不明確，無法清楚表達這是 User Story 層級的資料
2. **使用獨立命名空間（如 `UserStory:WorkItems`）**
   - Rejected: 與現有命名慣例不一致，破壞一致性

### Q6: `UserStoryWorkItemOutput` 資料結構如何設計？

**Decision**: 擴充 `WorkItemOutput`，新增三個欄位：
- `resolutionStatus: UserStoryResolutionStatus` - 解析狀態（enum）
- `originalWorkItem: WorkItemOutput?` - 原始 Work Item（若有轉換）
- `originalWorkItemId: int?` - 原始 Work Item ID（方便查詢）

**Rationale**:
- 保留所有原始 `WorkItemOutput` 欄位（workItemId, title, type, state, url, originalTeamName, isSuccess, errorMessage）
- `resolutionStatus` 使用 enum 提供型別安全與自我說明能力
- `originalWorkItem` 完整保留原始資料，避免資訊遺失
- `originalWorkItemId` 為冗餘欄位，但提升查詢便利性（可直接檢索原始 ID 而不需反序列化 originalWorkItem）
- 符合使用者提供的 JSON 結構需求

**Alternatives Considered**:
1. **使用繼承（UserStoryWorkItemOutput : WorkItemOutput）**
   - Rejected: C# record 不適合繼承，且會增加序列化複雜度
2. **將 originalWorkItem 設為字串（JSON）而非型別化物件**
   - Rejected: 失去型別安全，且不符合現有程式碼風格（使用強型別物件）

### Q7: 如何處理 API 呼叫失敗的 Work Item？

**Decision**: 設定 `isSuccess = false`，記錄錯誤於 `errorMessage`，保留所有可取得的欄位

**Rationale**:
- 符合現有 `WorkItemOutput` 設計模式
- 使用者可識別哪些 Work Item 處理失敗
- 不中斷整個批次處理流程，符合 SC-003（成功率 100% 不中斷）
- 錯誤訊息提供足夠診斷資訊（包含 HTTP 狀態碼、API 錯誤訊息）

**Alternatives Considered**:
1. **直接跳過失敗的 Work Item**
   - Rejected: 使用者無法得知哪些資料缺失
2. **在失敗時拋出例外中斷處理**
   - Rejected: 違反 Result Pattern 原則，且降低系統韌性

### Q8: JSON 序列化應使用哪個工具？

**Decision**: 使用現有的 `JsonExtensions.ToJson()` 與 `ToTypedObject<T>()`

**Rationale**:
- 符合 Constitution IX（優先使用 JsonExtensions）
- 專案已存在 `JsonExtensions`，提供統一的序列化行為
- 自動處理 camelCase 命名轉換
- 支援 enum 序列化為字串（`JsonStringEnumConverter`）

**Alternatives Considered**:
1. **直接使用 System.Text.Json**
   - Rejected: Constitution 要求優先使用 JsonExtensions
2. **使用 Newtonsoft.Json**
   - Rejected: Constitution 明確禁止（除非有相容性需求）

## Azure DevOps API Contract

### Parent Relation Structure

```json
{
  "id": 12345,
  "fields": { ... },
  "relations": [
    {
      "rel": "System.LinkTypes.Hierarchy-Reverse",
      "url": "https://dev.azure.com/{organization}/{project}/_apis/wit/workitems/67890",
      "attributes": {
        "isLocked": false
      }
    }
  ]
}
```

- `relations` 陣列包含所有關聯
- `rel` 為 `System.LinkTypes.Hierarchy-Reverse` 表示這是 Parent 關聯
- `url` 包含 Parent Work Item 的完整 API 端點
- 從 URL 解析出 Parent ID（正規表示式：`workitems/(\d+)$`）

### 範例：解析 Parent ID

```csharp
// Input URL
"https://dev.azure.com/org/proj/_apis/wit/workitems/67890"

// Regex Pattern
@"workitems/(\d+)$"

// Extracted Parent ID
67890
```

## Implementation Notes

1. **修改 AzureDevOpsWorkItemResponse**:
   - 新增 `Relations` 屬性（List<AzureDevOpsRelationResponse>）
   - 新增 `AzureDevOpsRelationResponse` record（包含 Rel, Url, Attributes）

2. **新增 IAzureDevOpsRepository 方法**:
   - 不需新增，直接重複使用現有的 `GetWorkItemAsync(int workItemId)`
   - Parent ID 從 relations 解析後，再次呼叫 GetWorkItemAsync 取得 Parent 資訊

3. **錯誤處理**:
   - Relations 為 null 或空陣列：視為無 Parent，停止遞迴
   - Parent ID 解析失敗：記錄錯誤，設定 resolutionStatus 為 NotFound
   - Parent API 呼叫失敗：記錄錯誤，設定 resolutionStatus 為 NotFound

## Performance Considerations

- **API 呼叫次數**: 最壞情況每個 Work Item 需 10 次 API 呼叫（最大深度）
- **批次處理 100 筆 Work Item**: 假設平均深度 2，預計 200 次 API 呼叫
- **Azure DevOps API 速率限制**: 預設 200 requests/min，足以支援當前規模
- **未來優化**（YAGNI，暫不實作）:
  - 使用 Batch API 減少 API 呼叫
  - 實作 Work Item 快取（避免重複查詢相同 Parent）
