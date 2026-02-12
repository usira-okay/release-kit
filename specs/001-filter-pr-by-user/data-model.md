# Data Model: Filter Pull Requests by User

**Feature**: 001-filter-pr-by-user
**Date**: 2026-02-12

## 既有實體（重用）

### FetchResult

批次擷取作業的最終輸出結果。

| 欄位 | 型別 | 說明 |
|------|------|------|
| Results | `List<ProjectResult>` | 各專案的擷取結果清單 |

### ProjectResult

單一專案的擷取結果。

| 欄位 | 型別 | 說明 |
|------|------|------|
| ProjectPath | `string` | 專案路徑 |
| Platform | `SourceControlPlatform` | 來源平台 |
| PullRequests | `List<MergeRequestOutput>` | PR 清單 |
| Error | `string?` | 錯誤訊息（成功時為 null） |

### MergeRequestOutput

PR 輸出模型，`AuthorUserId` 為過濾比對欄位。

| 欄位 | 型別 | 說明 |
|------|------|------|
| Title | `string` | PR 標題 |
| Description | `string?` | PR 描述 |
| SourceBranch | `string` | 來源分支 |
| TargetBranch | `string` | 目標分支 |
| CreatedAt | `DateTimeOffset` | 建立時間 |
| MergedAt | `DateTimeOffset?` | 合併時間 |
| State | `string` | 狀態 |
| **AuthorUserId** | `string` | **作者 ID（過濾比對欄位）** |
| AuthorName | `string` | 作者名稱 |
| PRUrl | `string` | PR 網址 |

### UserMapping

使用者對應設定。

| 欄位 | 型別 | 說明 |
|------|------|------|
| GitLabUserId | `string` | GitLab 使用者 ID |
| BitbucketUserId | `string` | Bitbucket 使用者 ID |
| DisplayName | `string` | 顯示名稱 |

### UserMappingOptions

使用者對應設定容器。

| 欄位 | 型別 | 說明 |
|------|------|------|
| Mappings | `List<UserMapping>` | 使用者對應清單 |

## 新增常數

### RedisKeys（擴充）

| 常數名稱 | 值 | 說明 |
|----------|-----|------|
| GitLabPullRequestsByUser | `GitLab:PullRequests:ByUser` | 過濾後的 GitLab PR 資料 |
| BitbucketPullRequestsByUser | `Bitbucket:PullRequests:ByUser` | 過濾後的 Bitbucket PR 資料 |

## 新增列舉值

### TaskType（擴充）

| 列舉值 | 說明 |
|--------|------|
| FilterGitLabPullRequestsByUser | 過濾 GitLab PR 依使用者 |
| FilterBitbucketPullRequestsByUser | 過濾 Bitbucket PR 依使用者 |

## 資料流程

```text
Redis (GitLab:PullRequests)                    Redis (GitLab:PullRequests:ByUser)
         │                                                ▲
         ▼                                                │
    IRedisService.GetAsync()                    IRedisService.SetAsync()
         │                                                ▲
         ▼                                                │
    JsonExtensions.ToTypedObject<FetchResult>()   JsonExtensions.ToJson()
         │                                                ▲
         ▼                                                │
    ┌─────────────────────────────────────────────────────┐
    │  BaseFilterPullRequestsByUserTask.ExecuteAsync()     │
    │                                                      │
    │  foreach ProjectResult:                              │
    │    if Error != null → 保留原樣                        │
    │    else → PullRequests.Where(                        │
    │             pr => userIds.Contains(pr.AuthorUserId)) │
    └─────────────────────────────────────────────────────┘
```
