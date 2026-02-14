# Redis Data Contracts: Get User Story

**Date**: 2026-02-14

## Contract 1: `AzureDevOps:WorkItems`（修改既有）

**類型**: `WorkItemFetchResult` JSON 字串
**變更**: `WorkItemOutput` 新增三個 nullable 欄位

### Schema

```jsonc
{
  "workItems": [
    {
      "workItemId": 12345,           // int, required
      "title": "string | null",
      "type": "string | null",
      "state": "string | null",
      "url": "string | null",
      "originalTeamName": "string | null",
      "isSuccess": true,             // bool, required
      "errorMessage": "string | null",
      // 以下三個為新增欄位
      "sourcePullRequestId": 101,    // int | null (新增)
      "sourceProjectName": "group/api",  // string | null (新增)
      "sourcePRUrl": "https://..."   // string | null (新增)
    }
  ],
  "totalPRsAnalyzed": 10,           // int, required
  "totalWorkItemsFound": 5,         // int, required
  "successCount": 4,                // int, required
  "failureCount": 1                 // int, required
}
```

### 向下相容性

新增欄位皆為 nullable，既有消費者反序列化時會忽略未知欄位（System.Text.Json 預設行為），不會造成破壞性變更。

---

## Contract 2: `AzureDevOps:UserStories`（新增）

**類型**: `UserStoryFetchResult` JSON 字串
**消費者**: 後續 Google Sheets 更新或其他報告產生流程

### Schema

```jsonc
{
  "userStories": [
    {
      "workItemId": 200,             // int, required - 解析後的 US/Feature/Epic ID
      "originalWorkItemId": 100,     // int, required - 原始 Work Item ID
      "title": "string | null",
      "type": "string | null",       // User Story / Feature / Epic / 或原始類型
      "state": "string | null",
      "url": "string | null",
      "originalTeamName": "string | null",
      "isSuccess": true,             // bool, required
      "errorMessage": "string | null"
    }
  ],
  "totalWorkItemsProcessed": 5,     // int, required
  "alreadyUserStoryCount": 2,       // int, required
  "resolvedCount": 2,               // int, required
  "keptOriginalCount": 1            // int, required
}
```

### 不變條件

- `totalWorkItemsProcessed == alreadyUserStoryCount + resolvedCount + keptOriginalCount`
- 當 `isSuccess == false` 時，`workItemId == originalWorkItemId`
- 當 `type` 為 User Story/Feature/Epic 且 `workItemId != originalWorkItemId` 時，表示成功向上解析
