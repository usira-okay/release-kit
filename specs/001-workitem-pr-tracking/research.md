# Research: PR 與 Work Item 關聯追蹤改善

**Feature**: 001-workitem-pr-tracking
**Created**: 2026-02-19
**Phase**: 0 - 研究與決策

## 1. 現有程式碼分析

### 1.1 去重複邏輯位置

- **Decision**: 需修改的去重複邏輯位於 `FetchAzureDevOpsWorkItemsTask.cs` 的 `ExtractWorkItemIdsFromPRs` 方法（行 132-140）
- **Rationale**: 此方法使用 `.ToHashSet()` 將 Work Item ID 去重複，導致不同 PR 指向同一 Work Item 時，PR 與 Work Item 的關聯被丟失
- **Current code**:
  ```csharp
  private HashSet<int> ExtractWorkItemIdsFromPRs(List<MergeRequestOutput> pullRequests)
  {
      var workItemIds = pullRequests
          .Where(pr => pr.WorkItemId.HasValue)
          .Select(pr => pr.WorkItemId!.Value)
          .ToHashSet(); // ← 去重複在此
      return workItemIds;
  }
  ```

### 1.2 PR 識別欄位現況

- **Decision**: 使用 `PRUrl` 作為 PR 的唯一識別值
- **Rationale**: `MergeRequestOutput` 現有欄位中，`PRUrl` 是跨平台（GitLab、Bitbucket）的唯一識別符，系統已用其進行 PR 去重複（Repository 層），且直接可用於識別 PR 來源
- **Alternatives considered**:
  - 使用平台數字 ID（GitLab `Id`/`Iid`，Bitbucket `Id`）：需修改 Infrastructure 層 Mapper 與 `MergeRequestOutput`，範圍過大
  - 使用 `PrUrl` 字串：已在 `MergeRequestOutput` 中存在，直接可用，無需改動 Infrastructure 層

### 1.3 WorkItemOutput 欄位現況

`WorkItemOutput` 目前無任何 PR 識別欄位。需新增 `PrUrl` 欄位（`string?`，可為 null）以記錄觸發此 Work Item 查詢的 PR 來源。

### 1.4 UserStoryWorkItemOutput 欄位現況

`UserStoryWorkItemOutput` 目前無 PR 識別欄位。需新增 `PrUrl` 欄位，並且在建立 `OriginalWorkItem` 時，使用 C# record with-expression 清除 `PrUrl`（`workItem with { PrUrl = null }`），確保 `OriginalWorkItem` 不含 PR 識別資訊。

## 2. 資料流分析

### 2.1 現有流程（問題所在）

```
MergeRequestOutput (有 PRUrl + WorkItemId)
  ↓ ExtractWorkItemIdsFromPRs - 使用 ToHashSet() 丟失 PRUrl 關聯
List<int> workItemIds (已去重複，PR 關聯中斷)
  ↓ FetchWorkItemsAsync
WorkItemOutput (無 PrUrl)
  ↓ Redis: AzureDevOpsWorkItems
GetUserStoryTask.ProcessWorkItemAsync
  ↓ UserStoryWorkItemOutput (無 PrUrl)
```

### 2.2 改善後流程

```
MergeRequestOutput (有 PRUrl + WorkItemId)
  ↓ ExtractWorkItemIdsFromPRs - 保留 PRUrl 關聯，不去重複
List<(string prUrl, int workItemId)> pairs (含 PR 對應)
  ↓ FetchWorkItemsAsync (含 prUrl 參數)
WorkItemOutput (含 PrUrl)
  ↓ Redis: AzureDevOpsWorkItems
GetUserStoryTask.ProcessWorkItemAsync (從 workItem.PrUrl 取得)
  ↓ UserStoryWorkItemOutput (含 PrUrl, OriginalWorkItem.PrUrl = null)
```

## 3. 影響範圍分析

### 3.1 直接修改的檔案

| 檔案 | 層 | 修改內容 |
|------|-----|---------|
| `WorkItemOutput.cs` | Application/Common | 新增 `PrUrl` 欄位（`string?`） |
| `UserStoryWorkItemOutput.cs` | Application/Common | 新增 `PrUrl` 欄位（`string?`） |
| `FetchAzureDevOpsWorkItemsTask.cs` | Application/Tasks | 移除 `.ToHashSet()`，改為保留 PR 關聯的清單；在 WorkItemOutput 中填入 `PrUrl` |
| `GetUserStoryTask.cs` | Application/Tasks | 在 `UserStoryWorkItemOutput` 中設定 `PrUrl`；在 `OriginalWorkItem` 中清除 `PrUrl` |
| `WorkItemFetchResult.cs` | Application/Common | 更新 `TotalWorkItemsFound` 的 XML 註解（移除「不重複」描述） |

### 3.2 不需修改的檔案

- Infrastructure 層（AzureDevOpsRepository、RedisService）：無需改動
- Domain 層（WorkItem、MergeRequest）：無需改動
- 所有 Mapper 檔案：無需改動

### 3.3 記錄在 WorkItemOutput 的值

當同一個 Work Item 被多個 PR 關聯時，會產生多筆 `WorkItemOutput`，每筆各自攜帶不同的 `PrUrl`。例：

```
PR-A (prUrl=".../prA") → WorkItem #456 → WorkItemOutput { WorkItemId=456, PrUrl=".../prA" }
PR-B (prUrl=".../prB") → WorkItem #456 → WorkItemOutput { WorkItemId=456, PrUrl=".../prB" }
```

這是預期行為，符合「保留重複」的需求。

## 4. 測試策略

### 4.1 單元測試需涵蓋

- `ExtractWorkItemIdsFromPRs` 保留重複 Work Item ID（含 PR 對應）
- `FetchWorkItemsAsync` 對每個 (prUrl, workItemId) 對建立 WorkItemOutput，且 PrUrl 正確填入
- `ProcessWorkItemAsync` 在 UserStoryWorkItemOutput 中正確填入 PrUrl
- `ProcessWorkItemAsync` 在 OriginalWorkItem 中 PrUrl 為 null

### 4.2 現有測試需更新

- 任何斷言 `ExtractWorkItemIdsFromPRs` 結果去重複的測試：需更新
- 任何建立 `WorkItemOutput` 不帶 `PrUrl` 的測試：需補充 `PrUrl` 初始化

## 5. 決策摘要

| 決策項目 | 決策 | 理由 |
|---------|------|------|
| PR 識別值類型 | 使用 `PRUrl` (string) | 跨平台唯一識別，已存在於 MergeRequestOutput，無需改動 Infrastructure |
| 欄位命名 | `PrUrl` | 清楚表達欄位代表的是 PR 的 URL 識別符 |
| 去重複移除方式 | 返回 `List<(string, int)>` | 保留 PR 與 WorkItem 的對應關係，不破壞現有結構 |
| OriginalWorkItem 的 PrUrl | `null` (with-expression 清除) | 符合需求：只在 UserStory 層級記錄 PrUrl，不寫入 OriginalWorkItem |
