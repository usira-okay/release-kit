# Data Model: Get User Story

**Date**: 2026-02-14
**Branch**: `001-get-user-story`

## Entity 變更

### MergeRequest（修改既有）

| 欄位 | 類型 | 狀態 | 說明 |
|------|------|------|------|
| PullRequestId | `int` (required) | **新增** | PR/MR 識別碼。GitLab: iid；Bitbucket: id |
| Title | `string` (required) | 既有 | PR/MR 標題 |
| Description | `string?` | 既有 | PR/MR 描述 |
| SourceBranch | `string` (required) | 既有 | 來源分支 |
| TargetBranch | `string` (required) | 既有 | 目標分支 |
| CreatedAt | `DateTimeOffset` (required) | 既有 | 建立時間 |
| MergedAt | `DateTimeOffset?` (required) | 既有 | 合併時間 |
| State | `string` (required) | 既有 | 狀態 |
| AuthorUserId | `string` (required) | 既有 | 作者 ID |
| AuthorName | `string` (required) | 既有 | 作者名稱 |
| PRUrl | `string` (required) | 既有 | PR 網址 |
| Platform | `SourceControlPlatform` (required) | 既有 | 平台類型 |
| ProjectPath | `string` (required) | 既有 | 專案路徑 |

### WorkItem（修改既有）

| 欄位 | 類型 | 狀態 | 說明 |
|------|------|------|------|
| WorkItemId | `int` (required) | 既有 | Work Item ID |
| Title | `string` (required) | 既有 | 標題 |
| Type | `string` (required) | 既有 | 類型 (Task/Bug/User Story/Feature/Epic) |
| State | `string` (required) | 既有 | 狀態 |
| Url | `string` (required) | 既有 | 網頁連結 |
| OriginalTeamName | `string` (required) | 既有 | 區域路徑 |
| ParentWorkItemId | `int?` | **新增** | 父層 Work Item ID（從 relations 解析） |

## DTO 變更

### MergeRequestOutput（修改既有）

| 欄位 | 類型 | 狀態 | 說明 |
|------|------|------|------|
| PullRequestId | `int` | **新增** | PR/MR 識別碼 |
| _（其餘欄位）_ | | 既有 | 不變 |

### WorkItemOutput（修改既有）

| 欄位 | 類型 | 狀態 | 說明 |
|------|------|------|------|
| SourcePullRequestId | `int?` | **新增** | 來源 PR ID |
| SourceProjectName | `string?` | **新增** | 來源 PR 所屬專案名稱 |
| SourcePRUrl | `string?` | **新增** | 來源 PR 網址 |
| _（其餘欄位）_ | | 既有 | 不變 |

### UserStoryOutput（新增）

| 欄位 | 類型 | 說明 |
|------|------|------|
| WorkItemId | `int` (required) | 解析後的 Work Item ID（User Story/Feature/Epic） |
| OriginalWorkItemId | `int` (required) | 原始 Work Item ID |
| Title | `string?` | 標題 |
| Type | `string?` | 類型 |
| State | `string?` | 狀態 |
| Url | `string?` | 網頁連結 |
| OriginalTeamName | `string?` | 區域路徑 |
| IsSuccess | `bool` (required) | 是否成功解析 |
| ErrorMessage | `string?` | 錯誤訊息 |

### UserStoryFetchResult（新增）

| 欄位 | 類型 | 說明 |
|------|------|------|
| UserStories | `List<UserStoryOutput>` (required) | 解析結果清單 |
| TotalWorkItemsProcessed | `int` (required) | 處理的 Work Item 總數 |
| AlreadyUserStoryCount | `int` (required) | 已是高層級類型的數量 |
| ResolvedCount | `int` (required) | 成功向上解析的數量 |
| KeptOriginalCount | `int` (required) | 保留原始資料的數量 |

## Infrastructure Model 變更

### AzureDevOpsRelationResponse（新增）

| 欄位 | JSON 屬性 | 類型 | 說明 |
|------|-----------|------|------|
| Rel | `rel` | `string` | 關聯類型（如 `System.LinkTypes.Hierarchy-Reverse`） |
| Url | `url` | `string` | 關聯目標的 API URL |

### AzureDevOpsWorkItemResponse（修改既有）

| 欄位 | JSON 屬性 | 類型 | 狀態 | 說明 |
|------|-----------|------|------|------|
| Relations | `relations` | `List<AzureDevOpsRelationResponse>?` | **新增** | Work Item 關聯清單 |

## Redis 資料結構

### 既有 Key: `AzureDevOps:WorkItems`

儲存 `WorkItemFetchResult` JSON，每筆 `WorkItemOutput` 新增 PR 來源欄位。

```json
{
  "workItems": [
    {
      "workItemId": 12345,
      "title": "修復登入錯誤",
      "type": "Bug",
      "state": "Active",
      "url": "https://dev.azure.com/...",
      "originalTeamName": "MyTeam",
      "isSuccess": true,
      "errorMessage": null,
      "sourcePullRequestId": 101,
      "sourceProjectName": "group/api",
      "sourcePRUrl": "https://gitlab.example.com/pr/101"
    }
  ],
  "totalPRsAnalyzed": 10,
  "totalWorkItemsFound": 5,
  "successCount": 4,
  "failureCount": 1
}
```

### 新增 Key: `AzureDevOps:UserStories`

儲存 `UserStoryFetchResult` JSON。

```json
{
  "userStories": [
    {
      "workItemId": 200,
      "originalWorkItemId": 100,
      "title": "使用者登入功能",
      "type": "User Story",
      "state": "Active",
      "url": "https://dev.azure.com/...",
      "originalTeamName": "MyTeam",
      "isSuccess": true,
      "errorMessage": null
    }
  ],
  "totalWorkItemsProcessed": 5,
  "alreadyUserStoryCount": 2,
  "resolvedCount": 2,
  "keptOriginalCount": 1
}
```

## 關係圖

```
PR (GitLab/Bitbucket)
  │ PullRequestId (新增)
  │
  ▼
WorkItemOutput (修改)
  │ SourcePullRequestId, SourceProjectName, SourcePRUrl (新增)
  │
  ▼ [get-user-story 指令讀取]
GetUserStoryTask
  │ 遞迴查詢 parent via IAzureDevOpsRepository
  │
  ▼
UserStoryOutput (新增)
  │ WorkItemId = 解析後的 US/Feature/Epic
  │ OriginalWorkItemId = 原始 Task/Bug
  │
  ▼ [寫入 Redis]
AzureDevOps:UserStories (新增 key)
```
