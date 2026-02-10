# Data Model: 取得各 Repository 最新 Release Branch 名稱

**Feature Branch**: `002-fetch-release-branch`
**Date**: 2026-02-10

## Entities

### ReleaseBranchResult

代表查詢結果的整體輸出，以 Dictionary 形式呈現。

| 屬性 | 型別 | 說明 |
|------|------|------|
| (Dictionary Key) | `string` | Release branch 名稱（如 `release/20260101`）或 `"NotFound"` |
| (Dictionary Value) | `List<string>` | 對應的專案路徑清單 |

**序列化後 JSON 範例**:
```json
{
  "release/20260101": [
    "group/backend-api",
    "group/frontend-app"
  ],
  "release/20260106": [
    "group/payment-service"
  ],
  "NotFound": [
    "group/legacy-tool"
  ]
}
```

**備註**:
- 使用 `Dictionary<string, List<string>>` 直接序列化即可產生目標格式
- key 的排列順序由 `Dictionary` 的插入順序決定（C# `Dictionary` 在未重新 hash 的情況下維持插入順序）
- `"NotFound"` 為保留 key，固定排在最後插入

## 類別關係

```text
ITask
  └── BaseFetchReleaseBranchTask<TOptions, TProjectOptions>
        ├── FetchGitLabReleaseBranchTask     (TOptions=GitLabOptions, TProjectOptions=GitLabProjectOptions)
        └── FetchBitbucketReleaseBranchTask   (TOptions=BitbucketOptions, TProjectOptions=BitbucketProjectOptions)

BaseFetchReleaseBranchTask 依賴:
  ├── ISourceControlRepository  (Keyed DI: "GitLab" 或 "Bitbucket")
  ├── IRedisService
  ├── ILogger<T>
  └── IOptions<TOptions>

輸出:
  └── Dictionary<string, List<string>>  →  JsonExtensions.ToJson()  →  Console + Redis
```

## 常數

### RedisKeys（新增）

| 常數名稱 | 值 | 說明 |
|----------|-----|------|
| `GitLabReleaseBranches` | `"GitLab:ReleaseBranches"` | GitLab Release Branch 資料的 Redis Key |
| `BitbucketReleaseBranches` | `"Bitbucket:ReleaseBranches"` | Bitbucket Release Branch 資料的 Redis Key |

### TaskType（新增）

| 列舉值 | 說明 |
|--------|------|
| `FetchGitLabReleaseBranches` | 取得 GitLab 各專案最新 Release Branch |
| `FetchBitbucketReleaseBranches` | 取得 Bitbucket 各專案最新 Release Branch |

### CommandLineParser Mapping（新增）

| CLI 指令 | TaskType |
|----------|----------|
| `fetch-gitlab-release-branch` | `FetchGitLabReleaseBranches` |
| `fetch-bitbucket-release-branch` | `FetchBitbucketReleaseBranches` |

## 資料流

```text
1. 使用者執行 CLI 指令
   ↓
2. CommandLineParser 解析 → TaskType
   ↓
3. TaskFactory.CreateTask() → BaseFetchReleaseBranchTask
   ↓
4. ExecuteAsync():
   a. 清除 Redis 舊資料
   b. 遍歷 GetProjects()
      ├── repository.GetBranchesAsync(projectPath, "release/")
      ├── 成功且有分支 → 取最新分支 → 加入分組
      └── 失敗或無分支 → 加入 "NotFound" 分組
   c. Dictionary<string, List<string>>.ToJson() → Console.WriteLine
   d. RedisService.SetAsync(RedisKey, json) → Redis
```
