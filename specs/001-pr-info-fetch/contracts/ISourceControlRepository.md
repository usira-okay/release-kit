# API Contracts: ISourceControlRepository

**Feature**: 001-pr-info-fetch  
**Date**: 2026-01-31

---

## Interface Definition

```csharp
namespace ReleaseKit.Domain.Abstractions;

/// <summary>
/// 原始碼控制平台 Repository 介面
/// 提供從 GitLab/Bitbucket 取得 PR/MR 資訊的抽象
/// </summary>
public interface ISourceControlRepository
{
    /// <summary>
    /// 依時間區間取得已合併的 Merge Request 清單
    /// </summary>
    /// <param name="projectPath">專案路徑 (如 "group/project" 或 "workspace/repo")</param>
    /// <param name="targetBranch">目標分支名稱</param>
    /// <param name="startDateTime">開始時間 (UTC)</param>
    /// <param name="endDateTime">結束時間 (UTC)</param>
    /// <param name="cancellationToken">取消權杖</param>
    /// <returns>成功時回傳 MergeRequest 清單，失敗時回傳 Error</returns>
    Task<Result<IReadOnlyList<MergeRequest>>> GetMergeRequestsByDateRangeAsync(
        string projectPath,
        string targetBranch,
        DateTimeOffset startDateTime,
        DateTimeOffset endDateTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 依分支差異取得相關的 Merge Request 清單
    /// </summary>
    /// <param name="projectPath">專案路徑</param>
    /// <param name="sourceBranch">來源分支 (舊版本)</param>
    /// <param name="targetBranch">目標分支 (新版本)</param>
    /// <param name="cancellationToken">取消權杖</param>
    /// <returns>成功時回傳 MergeRequest 清單，失敗時回傳 Error</returns>
    Task<Result<IReadOnlyList<MergeRequest>>> GetMergeRequestsByBranchDiffAsync(
        string projectPath,
        string sourceBranch,
        string targetBranch,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得符合 pattern 的分支清單
    /// </summary>
    /// <param name="projectPath">專案路徑</param>
    /// <param name="pattern">分支名稱 pattern (如 "release/")</param>
    /// <param name="cancellationToken">取消權杖</param>
    /// <returns>成功時回傳分支名稱清單，失敗時回傳 Error</returns>
    Task<Result<IReadOnlyList<string>>> GetBranchesAsync(
        string projectPath,
        string? pattern = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得 commit 關聯的 Merge Request 清單
    /// </summary>
    /// <param name="projectPath">專案路徑</param>
    /// <param name="commitSha">Commit SHA</param>
    /// <param name="cancellationToken">取消權杖</param>
    /// <returns>成功時回傳 MergeRequest 清單，失敗時回傳 Error</returns>
    Task<Result<IReadOnlyList<MergeRequest>>> GetMergeRequestsByCommitAsync(
        string projectPath,
        string commitSha,
        CancellationToken cancellationToken = default);
}
```

---

## Method Specifications

### GetMergeRequestsByDateRangeAsync

#### Input

| 參數 | 型別 | 必填 | 說明 |
|------|------|------|------|
| `projectPath` | `string` | ✅ | 專案路徑 |
| `targetBranch` | `string` | ✅ | 目標分支名稱 |
| `startDateTime` | `DateTimeOffset` | ✅ | 開始時間 (UTC) |
| `endDateTime` | `DateTimeOffset` | ✅ | 結束時間 (UTC) |
| `cancellationToken` | `CancellationToken` | ❌ | 取消權杖 |

#### Output

- **成功**: `Result<IReadOnlyList<MergeRequest>>.Success(list)`
- **失敗**: `Result<IReadOnlyList<MergeRequest>>.Failure(error)`

#### Possible Errors

| Error Code | 情境 |
|------------|------|
| `SourceControl.BranchNotFound` | 目標分支不存在 |
| `SourceControl.Unauthorized` | API Token 無效 |
| `SourceControl.ApiError` | API 呼叫失敗 |
| `SourceControl.RateLimitExceeded` | 超過 API 限制 |

#### Example Usage

```csharp
var result = await repository.GetMergeRequestsByDateRangeAsync(
    projectPath: "mygroup/backend-api",
    targetBranch: "main",
    startDateTime: new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero),
    endDateTime: new DateTimeOffset(2024, 3, 31, 23, 59, 59, TimeSpan.Zero));

if (result.IsSuccess)
{
    foreach (var mr in result.Value!)
    {
        Console.WriteLine($"{mr.Title} - merged at {mr.MergedAt}");
    }
}
else
{
    Console.WriteLine($"Error: {result.Error!.Message}");
}
```

---

### GetMergeRequestsByBranchDiffAsync

#### Input

| 參數 | 型別 | 必填 | 說明 |
|------|------|------|------|
| `projectPath` | `string` | ✅ | 專案路徑 |
| `sourceBranch` | `string` | ✅ | 來源分支 (舊版本) |
| `targetBranch` | `string` | ✅ | 目標分支 (新版本) |
| `cancellationToken` | `CancellationToken` | ❌ | 取消權杖 |

#### Output

- **成功**: `Result<IReadOnlyList<MergeRequest>>.Success(list)`
- **失敗**: `Result<IReadOnlyList<MergeRequest>>.Failure(error)`

#### Possible Errors

| Error Code | 情境 |
|------------|------|
| `SourceControl.BranchNotFound` | 來源或目標分支不存在 |
| `SourceControl.Unauthorized` | API Token 無效 |
| `SourceControl.ApiError` | API 呼叫失敗 |

#### Processing Logic

1. 呼叫 Compare API 取得 sourceBranch 與 targetBranch 的 commit 差異
2. 對每個 commit 呼叫 API 取得關聯的 MR
3. 合併結果並去除重複
4. 回傳 MergeRequest 清單

---

### GetBranchesAsync

#### Input

| 參數 | 型別 | 必填 | 說明 |
|------|------|------|------|
| `projectPath` | `string` | ✅ | 專案路徑 |
| `pattern` | `string?` | ❌ | 分支名稱 pattern (如 "release/") |
| `cancellationToken` | `CancellationToken` | ❌ | 取消權杖 |

#### Output

- **成功**: `Result<IReadOnlyList<string>>.Success(branchNames)`
- **失敗**: `Result<IReadOnlyList<string>>.Failure(error)`

#### Example Usage

```csharp
var result = await repository.GetBranchesAsync(
    projectPath: "mygroup/backend-api",
    pattern: "release/");

if (result.IsSuccess)
{
    // 取得所有 release 分支，如 ["release/20240101", "release/20240201", "release/20240301"]
    var releaseBranches = result.Value!.OrderBy(b => b).ToList();
}
```

---

### GetMergeRequestsByCommitAsync

#### Input

| 參數 | 型別 | 必填 | 說明 |
|------|------|------|------|
| `projectPath` | `string` | ✅ | 專案路徑 |
| `commitSha` | `string` | ✅ | Commit SHA (完整或簡短) |
| `cancellationToken` | `CancellationToken` | ❌ | 取消權杖 |

#### Output

- **成功**: `Result<IReadOnlyList<MergeRequest>>.Success(list)`
- **失敗**: `Result<IReadOnlyList<MergeRequest>>.Failure(error)`

#### Note

- 一個 commit 可能關聯多個 MR (如 cherry-pick 情境)
- 若 commit 是 direct push，則回傳空清單

---

## Implementation Requirements

### GitLab Implementation

```csharp
public class GitLabRepository : ISourceControlRepository
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<GitLabOptions> _options;
    
    // 實作各 API 呼叫
}
```

**API Endpoints Used**:
- `GET /projects/:id/merge_requests` - DateTimeRange 模式
- `GET /projects/:id/repository/compare` - BranchDiff 模式
- `GET /projects/:id/repository/commits/:sha/merge_requests` - Commit 關聯 MR
- `GET /projects/:id/repository/branches` - 取得分支清單

### Bitbucket Implementation

```csharp
public class BitbucketRepository : ISourceControlRepository
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<BitbucketOptions> _options;
    
    // 實作各 API 呼叫
}
```

**API Endpoints Used**:
- `GET /2.0/repositories/{workspace}/{repo_slug}/pullrequests` - DateTimeRange 模式
- `GET /2.0/repositories/{workspace}/{repo_slug}/diffstat/{spec}` - BranchDiff 模式
- `GET /2.0/repositories/{workspace}/{repo_slug}/commit/{commit}/pullrequests` - Commit 關聯 PR
- `GET /2.0/repositories/{workspace}/{repo_slug}/refs/branches` - 取得分支清單

---

## Error Contract

```csharp
namespace ReleaseKit.Domain.Common;

/// <summary>
/// 錯誤類型定義
/// </summary>
public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);
    
    public static class SourceControl
    {
        public static Error BranchNotFound(string branch) => 
            new("SourceControl.BranchNotFound", $"分支 '{branch}' 不存在");
        
        public static Error ProjectNotFound(string projectPath) => 
            new("SourceControl.ProjectNotFound", $"專案 '{projectPath}' 不存在");
        
        public static Error ApiError(string message) => 
            new("SourceControl.ApiError", $"API 呼叫失敗: {message}");
        
        public static Error Unauthorized => 
            new("SourceControl.Unauthorized", "API 驗證失敗，請檢查 Access Token");
        
        public static Error RateLimitExceeded => 
            new("SourceControl.RateLimitExceeded", "已達到 API 請求限制，請稍後再試");
        
        public static Error NetworkError(string message) => 
            new("SourceControl.NetworkError", $"網路連線錯誤: {message}");
        
        public static Error InvalidResponse(string message) => 
            new("SourceControl.InvalidResponse", $"API 回應格式無效: {message}");
    }
}
```

---

## Result Contract

```csharp
namespace ReleaseKit.Domain.Common;

/// <summary>
/// 操作結果包裝類別，用於取代 try-catch 例外處理
/// </summary>
public class Result<T>
{
    private Result(T? value, Error? error)
    {
        Value = value;
        Error = error;
    }
    
    /// <summary>
    /// 成功時的回傳值
    /// </summary>
    public T? Value { get; }
    
    /// <summary>
    /// 失敗時的錯誤資訊
    /// </summary>
    public Error? Error { get; }
    
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess => Error is null;
    
    /// <summary>
    /// 是否失敗
    /// </summary>
    public bool IsFailure => !IsSuccess;
    
    /// <summary>
    /// 建立成功結果
    /// </summary>
    public static Result<T> Success(T value) => new(value, null);
    
    /// <summary>
    /// 建立失敗結果
    /// </summary>
    public static Result<T> Failure(Error error) => new(default, error);
}
```
