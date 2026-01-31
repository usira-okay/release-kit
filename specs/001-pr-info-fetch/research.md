# Research: GitLab / Bitbucket PR 資訊擷取

**Feature**: 001-pr-info-fetch  
**Date**: 2026-01-31

---

## 1. Result Pattern 實作策略

### Decision
採用輕量級 `Result<T>` 泛型類別實作，不使用第三方套件。

### Rationale
- Constitution 規定禁止 try-catch，必須使用 Result Pattern
- .NET 9.0 原生無內建 Result 類型
- 第三方套件 (如 FluentResults, OneOf) 會增加依賴複雜度
- 自行實作可完全控制 API 設計，符合 KISS 原則

### Alternatives Considered
| 選項 | 優點 | 缺點 | 結論 |
|------|------|------|------|
| FluentResults 套件 | 功能完整、支援錯誤鏈 | 額外依賴、學習曲線 | 排除 |
| OneOf 套件 | 類型安全、函數式風格 | 語法較複雜 | 排除 |
| 自行實作 Result<T> | 簡單、無依賴、易於維護 | 需自行維護 | **採用** |

### Implementation Sketch
```csharp
public class Result<T>
{
    public T? Value { get; }
    public Error? Error { get; }
    public bool IsSuccess => Error is null;
    public bool IsFailure => !IsSuccess;
    
    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(Error error) => new(default, error);
}
```

---

## 2. HttpClient 管理策略

### Decision
使用 `IHttpClientFactory` 搭配 Named Clients 管理 GitLab 與 Bitbucket 的 HttpClient。

### Rationale
- 避免 Socket Exhaustion 問題
- 支援 DI 注入，便於測試時 Mock
- 可為不同平台設定不同的 Base URL 與 Default Headers
- 符合 .NET 最佳實踐

### Alternatives Considered
| 選項 | 優點 | 缺點 | 結論 |
|------|------|------|------|
| 直接 new HttpClient() | 簡單 | Socket Exhaustion、難測試 | 排除 |
| Singleton HttpClient | 避免 Socket 問題 | DNS 變更問題 | 排除 |
| IHttpClientFactory | 最佳實踐、DI 友善 | 需設定 | **採用** |
| Refit 套件 | 聲明式 API | 額外依賴、學習曲線 | 排除 |

### Implementation Sketch
```csharp
// DI Registration
services.AddHttpClient("GitLab", client =>
{
    client.BaseAddress = new Uri(gitLabOptions.ApiUrl);
    client.DefaultRequestHeaders.Add("PRIVATE-TOKEN", gitLabOptions.AccessToken);
});

services.AddHttpClient("Bitbucket", client =>
{
    client.BaseAddress = new Uri(bitbucketOptions.ApiUrl);
    client.DefaultRequestHeaders.Authorization = 
        new AuthenticationHeaderValue("Bearer", bitbucketOptions.AccessToken);
});
```

---

## 3. API 分頁處理策略

### Decision
實作 Platform-specific 分頁邏輯，GitLab 使用頁碼式，Bitbucket 使用 cursor-based (next link)。

### Rationale
- 兩平台分頁機制不同，無法統一抽象
- GitLab 使用 `page` + `per_page` 參數
- Bitbucket 使用 response 中的 `next` URL
- 需處理 1000+ 筆資料的情境

### Implementation Sketch
```csharp
// GitLab - Page-based
async IAsyncEnumerable<MergeRequest> FetchAllMergeRequestsAsync(...)
{
    var page = 1;
    while (true)
    {
        var response = await client.GetAsync($"...&page={page}&per_page=100");
        var items = await ParseResponseAsync(response);
        if (items.Count == 0) yield break;
        foreach (var item in items) yield return item;
        page++;
    }
}

// Bitbucket - Cursor-based
async IAsyncEnumerable<PullRequest> FetchAllPullRequestsAsync(...)
{
    var url = initialUrl;
    while (!string.IsNullOrEmpty(url))
    {
        var response = await client.GetAsync(url);
        var page = await ParseResponseAsync(response);
        foreach (var item in page.Values) yield return item;
        url = page.Next;
    }
}
```

---

## 4. 時間篩選策略 (二次過濾)

### Decision
API 層使用 `updated_after/updated_before` 粗篩，程式端使用 `merged_at/closed_on` 精確過濾。

### Rationale
- GitLab API 不支援直接以 `merged_at` 篩選
- Bitbucket API 不支援直接以 `closed_on` 篩選
- 使用 `updated` 時間粗篩可減少 API 呼叫次數
- 程式端精確過濾確保結果正確性

### Implementation Sketch
```csharp
// API 層粗篩
var apiResults = await repository.GetMergeRequestsAsync(
    targetBranch: "main",
    updatedAfter: startDateTime,
    updatedBefore: endDateTime);

// 程式端精確過濾
var filtered = apiResults
    .Where(mr => mr.MergedAt >= startDateTime && mr.MergedAt <= endDateTime)
    .ToList();
```

---

## 5. Repository 介面設計

### Decision
建立 `ISourceControlRepository` 介面，由 `GitLabRepository` 與 `BitbucketRepository` 實作。

### Rationale
- 符合 Dependency Inversion 原則
- 便於單元測試時 Mock
- 支援未來擴充其他平台 (如 GitHub)
- 策略模式處理不同平台邏輯

### Alternatives Considered
| 選項 | 優點 | 缺點 | 結論 |
|------|------|------|------|
| 直接在 Task 中實作 | 簡單 | 無法測試、違反 SRP | 排除 |
| 抽象基底類別 | 可共用邏輯 | 繼承耦合 | 排除 |
| 介面 + 實作類別 | DI 友善、可測試 | 需更多檔案 | **採用** |

### Interface Design
```csharp
public interface ISourceControlRepository
{
    Task<Result<IReadOnlyList<MergeRequest>>> GetMergeRequestsByDateRangeAsync(
        string projectPath,
        string targetBranch,
        DateTimeOffset startDateTime,
        DateTimeOffset endDateTime,
        CancellationToken cancellationToken = default);
    
    Task<Result<IReadOnlyList<MergeRequest>>> GetMergeRequestsByBranchDiffAsync(
        string projectPath,
        string sourceBranch,
        string targetBranch,
        CancellationToken cancellationToken = default);
    
    Task<Result<IReadOnlyList<string>>> GetBranchesAsync(
        string projectPath,
        string? pattern = null,
        CancellationToken cancellationToken = default);
}
```

---

## 6. 欄位映射策略

### Decision
使用專門的 Mapper 類別處理 API 回應到領域實體的映射。

### Rationale
- API 回應結構與領域實體結構不同
- GitLab 與 Bitbucket 欄位名稱不一致
- 集中管理映射邏輯，便於維護
- 符合 Single Responsibility 原則

### Implementation Sketch
```csharp
public static class GitLabMergeRequestMapper
{
    public static MergeRequest ToDomain(GitLabMergeRequestResponse response) => new(
        Title: response.Title,
        Description: response.Description,
        SourceBranch: response.SourceBranch,
        TargetBranch: response.TargetBranch,
        CreatedAt: response.CreatedAt,
        MergedAt: response.MergedAt ?? DateTimeOffset.MinValue,
        State: response.State,
        AuthorUserId: response.Author.Id.ToString(),
        AuthorName: response.Author.Username,
        PRUrl: response.WebUrl);
}
```

---

## 7. Error 類型設計

### Decision
使用 sealed record 定義具語意的錯誤類型。

### Rationale
- Constitution 規定錯誤類型必須明確定義且具有語意
- record 類型不可變，適合表達錯誤
- sealed 防止繼承，保持簡單

### Implementation Sketch
```csharp
public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);
    
    public static class SourceControl
    {
        public static Error BranchNotFound(string branch) => 
            new("SourceControl.BranchNotFound", $"分支 '{branch}' 不存在");
        
        public static Error ApiError(string message) => 
            new("SourceControl.ApiError", message);
        
        public static Error Unauthorized => 
            new("SourceControl.Unauthorized", "API 驗證失敗，請檢查 Access Token");
        
        public static Error RateLimitExceeded => 
            new("SourceControl.RateLimitExceeded", "已達到 API 請求限制，請稍後再試");
    }
}
```

---

## 8. FetchModeOptions 補充

### Decision
在 `FetchModeOptions` 新增 `TargetBranch` 屬性作為全域預設值。

### Rationale
- 目前 `TargetBranch` 只存在於 ProjectOptions 層級
- 需要全域預設值支援階層式設定覆蓋
- 符合 User Story 3 的需求

### Implementation
```csharp
public class FetchModeOptions
{
    public FetchMode FetchMode { get; init; } = FetchMode.DateTimeRange;
    public string? TargetBranch { get; init; }  // 新增
    public string? SourceBranch { get; init; }
    public DateTimeOffset? StartDateTime { get; init; }
    public DateTimeOffset? EndDateTime { get; init; }
}
```

---

## Summary

所有技術決策已確認，無需進一步澄清。可進入 Phase 1 設計階段。

| 決策項目 | 選擇 | 理由 |
|----------|------|------|
| Result Pattern | 自行實作 Result<T> | 簡單、無依賴 |
| HttpClient | IHttpClientFactory + Named Clients | 最佳實踐 |
| 分頁處理 | Platform-specific 實作 | 兩平台機制不同 |
| 時間篩選 | API 粗篩 + 程式端精確過濾 | API 限制 |
| Repository | 介面 + 實作類別 | DI 友善、可測試 |
| 欄位映射 | 專門 Mapper 類別 | SRP 原則 |
| Error 類型 | Sealed record | 語意明確、不可變 |
