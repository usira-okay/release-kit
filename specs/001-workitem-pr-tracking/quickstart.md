# Quickstart: PR 與 Work Item 關聯追蹤改善

**Feature**: 001-workitem-pr-tracking
**Created**: 2026-02-19

## 功能概述

本功能改善 PR 與 Azure DevOps Work Item 之間的關聯追蹤：
1. 移除 Work Item ID 去重複邏輯，保留每個 PR 對應的完整 Work Item 清單
2. 在 Work Item 物件中記錄來源 PR 網址（PrUrl）
3. 在 User Story 層級物件中記錄 PrUrl，但不寫入 OriginalWorkItem

## 修改的關鍵位置

### 涉及的檔案（全部在 Application 層）

| 檔案 | 修改類型 |
|------|---------|
| `WorkItemOutput.cs` | 新增 `PrUrl` 欄位 |
| `UserStoryWorkItemOutput.cs` | 新增 `PrUrl` 欄位 |
| `WorkItemFetchResult.cs` | 更新 XML 註解 |
| `FetchAzureDevOpsWorkItemsTask.cs` | 移除去重複，填入 PrUrl |
| `GetUserStoryTask.cs` | 傳遞 PrUrl，清除 OriginalWorkItem.PrUrl |

## 核心變更說明

### 變更 1：移除去重複

`FetchAzureDevOpsWorkItemsTask.ExtractWorkItemIdsFromPRs` 從返回 `HashSet<int>` 改為返回 `List<(string prUrl, int workItemId)>`，保留每個 PR 對應的關係。

### 變更 2：WorkItemOutput 記錄 PrUrl

`FetchAzureDevOpsWorkItemsTask.FetchWorkItemsAsync` 接收 PR-WorkItem 對應清單，在建立 `WorkItemOutput` 時填入 `PrUrl`。

### 變更 3：UserStoryWorkItemOutput 記錄 PrUrl，OriginalWorkItem 不記錄

`GetUserStoryTask.ProcessWorkItemAsync` 在建立 `UserStoryWorkItemOutput` 時：
- `PrUrl = workItem.PrUrl`（從輸入的 WorkItemOutput 複製）
- `OriginalWorkItem = workItem with { PrUrl = null }`（用 C# record with-expression 清除）

## 建置驗證

```bash
dotnet build
dotnet test
```

兩個指令均需通過，無任何錯誤或測試失敗。
