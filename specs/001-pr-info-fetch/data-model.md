# Data Model: GitLab / Bitbucket PR 資訊擷取

**Feature**: 001-pr-info-fetch  
**Date**: 2026-01-31

---

## Entity Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                         Domain Layer                                │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌─────────────────────┐       ┌──────────────────────────────────┐ │
│  │  MergeRequest       │       │  SourceControlPlatform           │ │
│  │  (Entity)           │       │  (Value Object)                  │ │
│  ├─────────────────────┤       ├──────────────────────────────────┤ │
│  │ - Title             │       │ - GitLab                         │ │
│  │ - Description       │       │ - Bitbucket                      │ │
│  │ - SourceBranch      │       └──────────────────────────────────┘ │
│  │ - TargetBranch      │                                           │
│  │ - CreatedAt         │       ┌──────────────────────────────────┐ │
│  │ - MergedAt          │       │  Result<T>                       │ │
│  │ - State             │       │  (Common)                        │ │
│  │ - AuthorUserId      │       ├──────────────────────────────────┤ │
│  │ - AuthorName        │       │ - Value: T?                      │ │
│  │ - PRUrl             │       │ - Error: Error?                  │ │
│  │ - Platform          │◄──────│ - IsSuccess: bool                │ │
│  │ - ProjectPath       │       │ - IsFailure: bool                │ │
│  └─────────────────────┘       └──────────────────────────────────┘ │
│                                                                     │
│  ┌─────────────────────────────────────────────────────────────────┐│
│  │  Error (Common)                                                 ││
│  ├─────────────────────────────────────────────────────────────────┤│
│  │ - Code: string                                                  ││
│  │ - Message: string                                               ││
│  │ + static Error.SourceControl.BranchNotFound(branch)             ││
│  │ + static Error.SourceControl.ApiError(message)                  ││
│  │ + static Error.SourceControl.Unauthorized                       ││
│  │ + static Error.SourceControl.RateLimitExceeded                  ││
│  └─────────────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────────┘
```

---

## Entities

### MergeRequest

代表一個已合併的 PR/MR，統一 GitLab MR 與 Bitbucket PR 的資料結構。

| 欄位 | 型別 | 說明 | 驗證規則 |
|------|------|------|----------|
| `Title` | `string` | PR/MR 標題 | 必填，不可為空白 |
| `Description` | `string?` | PR/MR 描述 | 可為 null |
| `SourceBranch` | `string` | 來源分支名稱 | 必填 |
| `TargetBranch` | `string` | 目標分支名稱 | 必填 |
| `CreatedAt` | `DateTimeOffset` | 建立時間 (UTC) | 必填 |
| `MergedAt` | `DateTimeOffset` | 合併時間 (UTC) | 必填 |
| `State` | `string` | 狀態 (merged) | 必填 |
| `AuthorUserId` | `string` | 作者 ID | 必填 |
| `AuthorName` | `string` | 作者名稱 | 必填 |
| `PRUrl` | `string` | PR/MR 網址 | 必填，需為有效 URL |
| `Platform` | `SourceControlPlatform` | 來源平台 | 必填 |
| `ProjectPath` | `string` | 專案路徑 | 必填 |

**欄位映射**:

| 輸出欄位 | GitLab API 欄位 | Bitbucket API 欄位 |
|----------|-----------------|-------------------|
| `Title` | `title` | `title` |
| `Description` | `description` | `summary.raw` |
| `SourceBranch` | `source_branch` | `source.branch.name` |
| `TargetBranch` | `target_branch` | `destination.branch.name` |
| `CreatedAt` | `created_at` | `created_on` |
| `MergedAt` | `merged_at` | `closed_on` |
| `State` | `state` | `state` |
| `AuthorUserId` | `author.id` (轉字串) | `author.uuid` |
| `AuthorName` | `author.username` | `author.display_name` |
| `PRUrl` | `web_url` | `links.html.href` |

---

## Value Objects

### SourceControlPlatform

代表支援的原始碼控制平台類型。

```csharp
public enum SourceControlPlatform
{
    GitLab,
    Bitbucket
}
```

**行為**:
- 不可變 (Immutable)
- 用於區分 PR/MR 來源平台
- 影響 API 呼叫邏輯與欄位映射

---

## Common Types

### Result\<T\>

用於包裝操作結果，取代 try-catch 例外處理。

| 屬性 | 型別 | 說明 |
|------|------|------|
| `Value` | `T?` | 成功時的回傳值 |
| `Error` | `Error?` | 失敗時的錯誤資訊 |
| `IsSuccess` | `bool` | 是否成功 |
| `IsFailure` | `bool` | 是否失敗 |

**Factory Methods**:
- `Result<T>.Success(T value)` - 建立成功結果
- `Result<T>.Failure(Error error)` - 建立失敗結果

---

### Error

代表操作錯誤，包含錯誤碼與訊息。

| 屬性 | 型別 | 說明 |
|------|------|------|
| `Code` | `string` | 錯誤碼 (如 `SourceControl.BranchNotFound`) |
| `Message` | `string` | 人類可讀的錯誤訊息 |

**預定義錯誤**:

| 錯誤碼 | 說明 |
|--------|------|
| `SourceControl.BranchNotFound` | 指定的分支不存在 |
| `SourceControl.ApiError` | API 呼叫失敗 |
| `SourceControl.Unauthorized` | API 驗證失敗 |
| `SourceControl.RateLimitExceeded` | 已達 API 請求限制 |
| `SourceControl.NetworkError` | 網路連線錯誤 |
| `SourceControl.InvalidResponse` | API 回應格式無效 |

---

## Infrastructure Models

### GitLab API Response Models

#### GitLabMergeRequestResponse

```csharp
public sealed record GitLabMergeRequestResponse
{
    [JsonPropertyName("id")]
    public int Id { get; init; }
    
    [JsonPropertyName("iid")]
    public int Iid { get; init; }
    
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string? Description { get; init; }
    
    [JsonPropertyName("source_branch")]
    public string SourceBranch { get; init; } = string.Empty;
    
    [JsonPropertyName("target_branch")]
    public string TargetBranch { get; init; } = string.Empty;
    
    [JsonPropertyName("state")]
    public string State { get; init; } = string.Empty;
    
    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; }
    
    [JsonPropertyName("merged_at")]
    public DateTimeOffset? MergedAt { get; init; }
    
    [JsonPropertyName("web_url")]
    public string WebUrl { get; init; } = string.Empty;
    
    [JsonPropertyName("author")]
    public GitLabAuthorResponse Author { get; init; } = new();
}

public sealed record GitLabAuthorResponse
{
    [JsonPropertyName("id")]
    public int Id { get; init; }
    
    [JsonPropertyName("username")]
    public string Username { get; init; } = string.Empty;
}
```

#### GitLabCompareResponse

```csharp
public sealed record GitLabCompareResponse
{
    [JsonPropertyName("commits")]
    public List<GitLabCommitResponse> Commits { get; init; } = new();
    
    [JsonPropertyName("compare_timeout")]
    public bool CompareTimeout { get; init; }
    
    [JsonPropertyName("compare_same_ref")]
    public bool CompareSameRef { get; init; }
}

public sealed record GitLabCommitResponse
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;
    
    [JsonPropertyName("short_id")]
    public string ShortId { get; init; } = string.Empty;
    
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;
    
    [JsonPropertyName("author_name")]
    public string AuthorName { get; init; } = string.Empty;
    
    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; }
}
```

---

### Bitbucket API Response Models

#### BitbucketPullRequestResponse

```csharp
public sealed record BitbucketPullRequestResponse
{
    [JsonPropertyName("id")]
    public int Id { get; init; }
    
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;
    
    [JsonPropertyName("summary")]
    public BitbucketSummaryResponse? Summary { get; init; }
    
    [JsonPropertyName("source")]
    public BitbucketBranchRefResponse Source { get; init; } = new();
    
    [JsonPropertyName("destination")]
    public BitbucketBranchRefResponse Destination { get; init; } = new();
    
    [JsonPropertyName("state")]
    public string State { get; init; } = string.Empty;
    
    [JsonPropertyName("created_on")]
    public DateTimeOffset CreatedOn { get; init; }
    
    [JsonPropertyName("closed_on")]
    public DateTimeOffset? ClosedOn { get; init; }
    
    [JsonPropertyName("author")]
    public BitbucketAuthorResponse Author { get; init; } = new();
    
    [JsonPropertyName("links")]
    public BitbucketLinksResponse Links { get; init; } = new();
}

public sealed record BitbucketSummaryResponse
{
    [JsonPropertyName("raw")]
    public string Raw { get; init; } = string.Empty;
}

public sealed record BitbucketBranchRefResponse
{
    [JsonPropertyName("branch")]
    public BitbucketBranchResponse Branch { get; init; } = new();
}

public sealed record BitbucketBranchResponse
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}

public sealed record BitbucketAuthorResponse
{
    [JsonPropertyName("uuid")]
    public string Uuid { get; init; } = string.Empty;
    
    [JsonPropertyName("display_name")]
    public string DisplayName { get; init; } = string.Empty;
}

public sealed record BitbucketLinksResponse
{
    [JsonPropertyName("html")]
    public BitbucketLinkResponse Html { get; init; } = new();
}

public sealed record BitbucketLinkResponse
{
    [JsonPropertyName("href")]
    public string Href { get; init; } = string.Empty;
}
```

#### BitbucketPageResponse\<T\>

```csharp
public sealed record BitbucketPageResponse<T>
{
    [JsonPropertyName("values")]
    public List<T> Values { get; init; } = new();
    
    [JsonPropertyName("next")]
    public string? Next { get; init; }
    
    [JsonPropertyName("page")]
    public int Page { get; init; }
    
    [JsonPropertyName("pagelen")]
    public int PageLen { get; init; }
    
    [JsonPropertyName("size")]
    public int Size { get; init; }
}
```

---

## Output Format

### FetchResult

最終輸出格式，包含所有專案的 PR 資訊。

```csharp
public sealed record FetchResult
{
    public List<ProjectResult> Results { get; init; } = new();
}

public sealed record ProjectResult
{
    public string ProjectPath { get; init; } = string.Empty;
    public string Platform { get; init; } = string.Empty;
    public List<MergeRequestOutput> PullRequests { get; init; } = new();
}

public sealed record MergeRequestOutput
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string SourceBranch { get; init; } = string.Empty;
    public string TargetBranch { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset MergedAt { get; init; }
    public string State { get; init; } = string.Empty;
    public string AuthorUserId { get; init; } = string.Empty;
    public string AuthorName { get; init; } = string.Empty;
    public string PRUrl { get; init; } = string.Empty;
}
```

### JSON Output Example

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

## Entity Relationships

```
FetchResult
    │
    └── Results: List<ProjectResult>
            │
            ├── ProjectPath: string
            ├── Platform: string
            └── PullRequests: List<MergeRequestOutput>
                    │
                    ├── Title
                    ├── Description
                    ├── SourceBranch
                    ├── TargetBranch
                    ├── CreatedAt
                    ├── MergedAt
                    ├── State
                    ├── AuthorUserId
                    ├── AuthorName
                    └── PRUrl
```

---

## Validation Rules Summary

| 實體 | 欄位 | 規則 |
|------|------|------|
| MergeRequest | Title | 必填，不可為空白 |
| MergeRequest | SourceBranch | 必填 |
| MergeRequest | TargetBranch | 必填 |
| MergeRequest | MergedAt | 必填，必須在 CreatedAt 之後 |
| MergeRequest | PRUrl | 必填，需為有效 URL 格式 |
| Error | Code | 必填，格式為 `Category.ErrorName` |
| Error | Message | 必填，繁體中文錯誤訊息 |
