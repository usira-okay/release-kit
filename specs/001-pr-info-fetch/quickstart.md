# Quickstart: GitLab / Bitbucket PR 資訊擷取

**Feature**: 001-pr-info-fetch  
**Date**: 2026-01-31

---

## 概述

本功能實作從 GitLab 與 Bitbucket 平台擷取已合併 PR/MR 資訊的能力，支援兩種擷取模式：

1. **DateTimeRange 模式**: 依時間區間擷取
2. **BranchDiff 模式**: 依分支差異擷取

---

## 快速開始

### 1. 設定 appsettings.json

```json
{
  "FetchMode": {
    "FetchMode": "DateTimeRange",
    "TargetBranch": "main",
    "StartDateTime": "2024-03-01T00:00:00Z",
    "EndDateTime": "2024-03-31T23:59:59Z"
  },
  "GitLab": {
    "ApiUrl": "https://gitlab.example.com/api/v4",
    "AccessToken": "${GITLAB_ACCESS_TOKEN}",
    "Projects": [
      {
        "ProjectPath": "mygroup/backend-api",
        "TargetBranch": "main"
      }
    ]
  },
  "Bitbucket": {
    "ApiUrl": "https://api.bitbucket.org",
    "Email": "your-email@example.com",
    "AccessToken": "${BITBUCKET_ACCESS_TOKEN}",
    "Projects": [
      {
        "ProjectPath": "myworkspace/frontend-app",
        "TargetBranch": "main"
      }
    ]
  }
}
```

### 2. 設定環境變數

```bash
# Linux/macOS
export GITLAB_ACCESS_TOKEN="your-gitlab-token"
export BITBUCKET_ACCESS_TOKEN="your-bitbucket-token"

# Windows PowerShell
$env:GITLAB_ACCESS_TOKEN = "your-gitlab-token"
$env:BITBUCKET_ACCESS_TOKEN = "your-bitbucket-token"
```

### 3. 執行擷取

```bash
dotnet run --project src/ReleaseKit.Console
```

---

## 擷取模式

### DateTimeRange 模式

擷取指定時間區間內合併到目標分支的所有 PR。

```json
{
  "FetchMode": {
    "FetchMode": "DateTimeRange",
    "TargetBranch": "main",
    "StartDateTime": "2024-03-01T00:00:00Z",
    "EndDateTime": "2024-03-31T23:59:59Z"
  }
}
```

### BranchDiff 模式

擷取兩個分支之間差異對應的 PR。

```json
{
  "FetchMode": {
    "FetchMode": "BranchDiff",
    "SourceBranch": "release/20240301",
    "TargetBranch": "main"
  }
}
```

---

## 設定優先順序

專案層級設定會覆蓋根層級設定：

```json
{
  "FetchMode": {
    "TargetBranch": "main"  // 根層級預設
  },
  "GitLab": {
    "Projects": [
      {
        "ProjectPath": "group/project-a",
        "TargetBranch": "develop"  // 專案層級覆蓋
      },
      {
        "ProjectPath": "group/project-b"
        // 使用根層級的 "main"
      }
    ]
  }
}
```

---

## 輸出格式

擷取結果為 JSON 格式：

```json
{
  "Results": [
    {
      "ProjectPath": "mygroup/backend-api",
      "Platform": "GitLab",
      "PullRequests": [
        {
          "Title": "feat: 新增使用者驗證功能",
          "Description": "實作 JWT 驗證機制...",
          "SourceBranch": "feature/user-auth",
          "TargetBranch": "main",
          "CreatedAt": "2024-03-10T09:30:00+00:00",
          "MergedAt": "2024-03-12T14:22:00+00:00",
          "State": "merged",
          "AuthorUserId": "12345",
          "AuthorName": "john.doe",
          "PRUrl": "https://gitlab.example.com/mygroup/backend-api/-/merge_requests/42"
        }
      ]
    }
  ]
}
```

---

## 程式碼使用範例

### 使用 ISourceControlRepository

```csharp
public class MyService
{
    private readonly ISourceControlRepository _gitLabRepository;
    private readonly ISourceControlRepository _bitbucketRepository;

    public MyService(
        [FromKeyedServices("GitLab")] ISourceControlRepository gitLabRepository,
        [FromKeyedServices("Bitbucket")] ISourceControlRepository bitbucketRepository)
    {
        _gitLabRepository = gitLabRepository;
        _bitbucketRepository = bitbucketRepository;
    }

    public async Task<Result<IReadOnlyList<MergeRequest>>> FetchGitLabPRsAsync()
    {
        return await _gitLabRepository.GetMergeRequestsByDateRangeAsync(
            projectPath: "mygroup/backend-api",
            targetBranch: "main",
            startDateTime: DateTimeOffset.UtcNow.AddDays(-30),
            endDateTime: DateTimeOffset.UtcNow);
    }
}
```

### 處理 Result 回傳值

```csharp
var result = await repository.GetMergeRequestsByDateRangeAsync(...);

if (result.IsSuccess)
{
    foreach (var mr in result.Value!)
    {
        Console.WriteLine($"[{mr.MergedAt:yyyy-MM-dd}] {mr.Title}");
    }
}
else
{
    // 處理錯誤
    logger.LogError("擷取失敗: {ErrorCode} - {ErrorMessage}", 
        result.Error!.Code, 
        result.Error.Message);
}
```

---

## 常見問題

### Q: GitLab 回傳的 PR 數量比預期少？

A: GitLab API 使用 `updated_after` 篩選，而非 `merged_at`。系統會在程式端進行二次過濾，確保只回傳在指定時間範圍內合併的 PR。

### Q: Bitbucket 的 `closed_on` 欄位沒有值？

A: 需要在 API 呼叫時加上 `fields=*.*` 參數才能取得 `closed_on` 欄位。系統已自動處理此問題。

### Q: 如何處理 API Rate Limit？

A: 系統會回傳 `Error.SourceControl.RateLimitExceeded` 錯誤。建議：
1. 減少專案數量分批執行
2. 縮小時間區間
3. 等待一段時間後重試

### Q: BranchDiff 模式如何判斷比較對象？

A: 
- 若 SourceBranch 是最新的 release 分支 → 與 TargetBranch (通常是 main) 比較
- 若 SourceBranch 不是最新 → 與下一版 release 分支比較

---

## 下一步

- 執行 `/speckit.tasks` 產生任務清單
- 依照 TDD 流程實作功能
- 執行測試驗證功能正確性
