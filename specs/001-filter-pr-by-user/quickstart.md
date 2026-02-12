# Quickstart: Filter Pull Requests by User

**Feature**: 001-filter-pr-by-user
**Date**: 2026-02-12

## 前置條件

1. Redis 服務已啟動（`localhost:6379`）
2. 已執行 `fetch-gitlab-pr` 或 `fetch-bitbucket-pr` 指令，Redis 中有 PR 資料
3. `appsettings.json` 已設定 `UserMapping.Mappings` 使用者清單

## 設定範例

在 `appsettings.json` 中設定使用者清單：

```json
{
  "UserMapping": {
    "Mappings": [
      {
        "GitLabUserId": "12345",
        "BitbucketUserId": "{abc-def-123}",
        "DisplayName": "John Doe"
      },
      {
        "GitLabUserId": "67890",
        "BitbucketUserId": "{xyz-789}",
        "DisplayName": "Jane Smith"
      }
    ]
  }
}
```

## 使用方式

### 過濾 GitLab PR

```bash
dotnet run --project src/ReleaseKit.Console -- filter-gitlab-pr-by-user
```

- 從 Redis Key `GitLab:PullRequests` 讀取資料
- 依 `UserMapping.Mappings[].GitLabUserId` 過濾
- 結果寫入 Redis Key `GitLab:PullRequests:ByUser`
- 過濾結果同時輸出至 stdout

### 過濾 Bitbucket PR

```bash
dotnet run --project src/ReleaseKit.Console -- filter-bitbucket-pr-by-user
```

- 從 Redis Key `Bitbucket:PullRequests` 讀取資料
- 依 `UserMapping.Mappings[].BitbucketUserId` 過濾
- 結果寫入 Redis Key `Bitbucket:PullRequests:ByUser`
- 過濾結果同時輸出至 stdout

## 典型工作流程

```bash
# 1. 擷取 GitLab PR 資料
dotnet run --project src/ReleaseKit.Console -- fetch-gitlab-pr

# 2. 過濾 GitLab PR（僅保留指定使用者）
dotnet run --project src/ReleaseKit.Console -- filter-gitlab-pr-by-user

# 3. 擷取 Bitbucket PR 資料
dotnet run --project src/ReleaseKit.Console -- fetch-bitbucket-pr

# 4. 過濾 Bitbucket PR（僅保留指定使用者）
dotnet run --project src/ReleaseKit.Console -- filter-bitbucket-pr-by-user

# 5. 更新 Google Sheets（使用過濾後的資料）
dotnet run --project src/ReleaseKit.Console -- update-googlesheet
```

## 驗證

使用 `redis-cli` 確認過濾結果：

```bash
# 檢查過濾後的 GitLab PR
redis-cli GET "ReleaseKit:GitLab:PullRequests:ByUser"

# 檢查過濾後的 Bitbucket PR
redis-cli GET "ReleaseKit:Bitbucket:PullRequests:ByUser"
```
