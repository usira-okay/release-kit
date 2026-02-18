# Data Model: PR 與 Work Item 關聯追蹤改善

**Feature**: 001-workitem-pr-tracking
**Created**: 2026-02-19
**Phase**: 1 - 資料模型設計

## 1. 現有模型與變更

### 1.1 WorkItemOutput（修改）

**位置**: `src/ReleaseKit.Application/Common/WorkItemOutput.cs`

**變更**: 新增 `PrUrl` 欄位

```csharp
// 新增欄位（加在 ErrorMessage 前）
/// <summary>
/// 觸發此 Work Item 查詢的 PR 網址
/// </summary>
/// <remarks>
/// 用於追蹤此 Work Item 由哪個 PR 觸發查詢。
/// 若非由 PR 觸發，此值為 null。
/// </remarks>
public string? PrUrl { get; init; }
```

**完整欄位清單（修改後）**:
| 欄位 | 類型 | 說明 |
|------|------|------|
| `WorkItemId` | `int` (required) | Work Item 識別碼 |
| `Title` | `string?` | 標題（失敗時為 null） |
| `Type` | `string?` | 類型（Bug/Task/User Story 等） |
| `State` | `string?` | 狀態（New/Active 等） |
| `Url` | `string?` | Work Item 網頁連結 |
| `OriginalTeamName` | `string?` | 原始團隊名稱 |
| **`PrUrl`** | `string?` | **[新增] 觸發查詢的 PR 網址** |
| `IsSuccess` | `bool` (required) | 是否成功取得資訊 |
| `ErrorMessage` | `string?` | 失敗原因 |

---

### 1.2 UserStoryWorkItemOutput（修改）

**位置**: `src/ReleaseKit.Application/Common/UserStoryWorkItemOutput.cs`

**變更**: 新增 `PrUrl` 欄位

```csharp
// 新增欄位（加在 OriginalWorkItem 前）
/// <summary>
/// 觸發此 Work Item 查詢的 PR 網址
/// </summary>
/// <remarks>
/// 記錄源頭 PR，不寫入 OriginalWorkItem，
/// 保持 OriginalWorkItem 的原始性。
/// </remarks>
public string? PrUrl { get; init; }
```

**完整欄位清單（修改後）**:
| 欄位 | 類型 | 說明 |
|------|------|------|
| `WorkItemId` | `int` (required) | 轉換後的 Work Item ID |
| `Title` | `string?` | 轉換後標題 |
| `Type` | `string?` | 轉換後類型 |
| `State` | `string?` | 轉換後狀態 |
| `Url` | `string?` | 轉換後 URL |
| `OriginalTeamName` | `string?` | 原始團隊名稱 |
| `IsSuccess` | `bool` (required) | 是否成功 |
| `ErrorMessage` | `string?` | 失敗原因 |
| `ResolutionStatus` | `UserStoryResolutionStatus` (required) | 解析狀態 |
| **`PrUrl`** | `string?` | **[新增] 觸發查詢的 PR 網址** |
| `OriginalWorkItem` | `WorkItemOutput?` | 原始 Work Item（**PrUrl 欄位在此為 null**） |

---

### 1.3 WorkItemFetchResult（小幅更新）

**位置**: `src/ReleaseKit.Application/Common/WorkItemFetchResult.cs`

**變更**: 更新 `TotalWorkItemsFound` XML 註解，移除「不重複」描述

```csharp
// 舊註解
/// <summary>解析出的不重複 Work Item ID 總數</summary>
/// <remarks>從所有 PR 標題中解析出並去重複後的 VSTS ID 數量。</remarks>

// 新註解
/// <summary>解析出的 Work Item ID 總數</summary>
/// <remarks>
/// 從所有 PR 解析出的 VSTS Work Item ID 總數，包含同一 ID 被多個 PR 參照的情況。
/// 此數量等同於最終 WorkItems 清單的長度。
/// </remarks>
```

---

## 2. 業務邏輯模型變更

### 2.1 FetchAzureDevOpsWorkItemsTask 的資料結構

**ExtractWorkItemIdsFromPRs 返回型別變更**:
- 舊: `HashSet<int>`
- 新: `List<(string prUrl, int workItemId)>`（允許重複 workItemId，各自有不同 prUrl）

**FetchWorkItemsAsync 參數變更**:
- 舊: `HashSet<int> workItemIds`
- 新: `IReadOnlyList<(string prUrl, int workItemId)> workItemPairs`

---

## 3. 資料流範例

### 3.1 去重複前後對比

**輸入 PRs**:
```
PR-A (prUrl="https://gitlab.com/proj/mrs/1") → WorkItemId=456
PR-B (prUrl="https://gitlab.com/proj/mrs/2") → WorkItemId=456  ← 同一 Work Item，不同 PR
PR-C (prUrl="https://gitlab.com/proj/mrs/3") → WorkItemId=789
```

**舊輸出（去重複後，WorkItems 清單）**:
```json
[
  { "workItemId": 456, "title": "...", "prUrl": null },
  { "workItemId": 789, "title": "...", "prUrl": null }
]
```

**新輸出（保留重複，WorkItems 清單）**:
```json
[
  { "workItemId": 456, "title": "...", "prUrl": "https://gitlab.com/proj/mrs/1" },
  { "workItemId": 456, "title": "...", "prUrl": "https://gitlab.com/proj/mrs/2" },
  { "workItemId": 789, "title": "...", "prUrl": "https://gitlab.com/proj/mrs/3" }
]
```

### 3.2 UserStoryWorkItemOutput PrUrl 分配範例

**輸入（WorkItemOutput）**:
```json
{ "workItemId": 456, "type": "Bug", "prUrl": "https://gitlab.com/proj/mrs/1" }
```

**輸出（UserStoryWorkItemOutput，WorkItem #456 的 Bug → 轉換為 User Story #100）**:
```json
{
  "workItemId": 100,
  "type": "User Story",
  "prUrl": "https://gitlab.com/proj/mrs/1",
  "resolutionStatus": "FoundViaRecursion",
  "originalWorkItem": {
    "workItemId": 456,
    "type": "Bug",
    "prUrl": null
  }
}
```

**關鍵點**:
- `UserStoryWorkItemOutput.PrUrl` = 從 `WorkItemOutput.PrUrl` 複製
- `UserStoryWorkItemOutput.OriginalWorkItem.PrUrl` = **null**（不記錄至 OriginalWorkItem）

---

## 4. 測試案例模型

### 4.1 WorkItemOutput 測試建立範例

```csharp
// 舊（需更新）
new WorkItemOutput { WorkItemId = 456, IsSuccess = true, ... }

// 新（含 PrUrl）
new WorkItemOutput { WorkItemId = 456, IsSuccess = true, PrUrl = "https://...", ... }
```

### 4.2 UserStoryWorkItemOutput 驗證重點

- `PrUrl` 應等於輸入 `WorkItemOutput.PrUrl`
- `OriginalWorkItem?.PrUrl` 應為 `null`
