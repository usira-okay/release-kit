# Data Model: 整合 Release 資料

## 輸入資料模型（Redis 現有）

### FetchResult（PR 資料）

來源 Key: `GitLab:PullRequests:ByUser`, `Bitbucket:PullRequests:ByUser`

```
FetchResult
├── Results: List<ProjectResult>
│   ├── ProjectPath: string           // 專案完整路徑（如 "group/subgroup/project"）
│   ├── Platform: SourceControlPlatform
│   ├── PullRequests: List<MergeRequestOutput>
│   │   ├── Title: string
│   │   ├── Description: string?
│   │   ├── SourceBranch: string
│   │   ├── TargetBranch: string
│   │   ├── CreatedAt: DateTimeOffset
│   │   ├── MergedAt: DateTimeOffset?
│   │   ├── State: string
│   │   ├── AuthorUserId: string
│   │   ├── AuthorName: string
│   │   ├── PrId: string              // ← 配對用 Key
│   │   ├── PRUrl: string
│   │   └── WorkItemId: int?
│   └── Error: string?
```

### UserStoryFetchResult（Work Item 資料）

來源 Key: `AzureDevOps:WorkItems:UserStories`

```
UserStoryFetchResult
├── WorkItems: List<UserStoryWorkItemOutput>
│   ├── WorkItemId: int               // User Story ID
│   ├── Title: string?
│   ├── Type: string?
│   ├── State: string?
│   ├── Url: string?
│   ├── OriginalTeamName: string?     // ← TeamMapping 輸入
│   ├── IsSuccess: bool
│   ├── ErrorMessage: string?
│   ├── ResolutionStatus: UserStoryResolutionStatus
│   ├── PrId: string?                 // ← 配對用 Key
│   └── OriginalWorkItem: WorkItemOutput?
│       ├── WorkItemId: int
│       ├── Title: string?
│       ├── Type: string?
│       ├── State: string?
│       ├── Url: string?
│       ├── OriginalTeamName: string?
│       ├── PrId: string?
│       ├── IsSuccess: bool
│       └── ErrorMessage: string?
```

### TeamMappingOptions（設定檔）

來源: `appsettings.json` → `AzureDevOps:TeamMapping`

```
AzureDevOpsOptions
└── TeamMapping: List<TeamMappingOptions>
    ├── OriginalTeamName: string      // 原始團隊名稱（英文）
    └── DisplayName: string           // 顯示名稱（中文）
```

## 輸出資料模型（新增）

### ConsolidatedReleaseResult

目標 Key: `ConsolidatedReleaseData`

```csharp
public sealed record ConsolidatedReleaseResult
{
    /// <summary>
    /// 依專案分組的整合結果
    /// </summary>
    public required List<ConsolidatedProjectGroup> Projects { get; init; }
}
```

### ConsolidatedProjectGroup

```csharp
public sealed record ConsolidatedProjectGroup
{
    /// <summary>
    /// 專案名稱（ProjectPath split('/') 後取最後一段）
    /// </summary>
    public required string ProjectName { get; init; }

    /// <summary>
    /// 該專案下的整合記錄清單（已排序：依 TeamDisplayName 升冪，再依 WorkItemId 升冪）
    /// </summary>
    public required List<ConsolidatedReleaseEntry> Entries { get; init; }
}
```

### ConsolidatedReleaseEntry

```csharp
public sealed record ConsolidatedReleaseEntry
{
    /// <summary>
    /// PR 標題（取第一筆配對 PR 的標題）
    /// </summary>
    public required string PrTitle { get; init; }

    /// <summary>
    /// Work Item ID
    /// </summary>
    public required int WorkItemId { get; init; }

    /// <summary>
    /// 團隊顯示名稱（經 TeamMapping 轉換後）
    /// </summary>
    public required string TeamDisplayName { get; init; }

    /// <summary>
    /// 作者資訊清單
    /// </summary>
    public required List<ConsolidatedAuthorInfo> Authors { get; init; }

    /// <summary>
    /// PR 資訊清單
    /// </summary>
    public required List<ConsolidatedPrInfo> PullRequests { get; init; }

    /// <summary>
    /// 原始資料
    /// </summary>
    public required ConsolidatedOriginalData OriginalData { get; init; }
}
```

### ConsolidatedAuthorInfo

```csharp
public sealed record ConsolidatedAuthorInfo
{
    /// <summary>
    /// 作者名稱
    /// </summary>
    public required string AuthorName { get; init; }
}
```

### ConsolidatedPrInfo

```csharp
public sealed record ConsolidatedPrInfo
{
    /// <summary>
    /// PR 網址
    /// </summary>
    public required string Url { get; init; }
}
```

### ConsolidatedOriginalData

```csharp
public sealed record ConsolidatedOriginalData
{
    /// <summary>
    /// 原始 Work Item 資料
    /// </summary>
    public required UserStoryWorkItemOutput WorkItem { get; init; }

    /// <summary>
    /// 原始 PR 資料清單
    /// </summary>
    public required List<MergeRequestOutput> PullRequests { get; init; }
}
```

## 資料關聯圖

```
                    配對 (PrId)
UserStoryWorkItemOutput ───────── MergeRequestOutput
        │                               │
        │ OriginalTeamName              │ ProjectPath (from ProjectResult)
        │                               │
        ▼                               ▼
  TeamMapping                    split('/').Last()
  (大小寫忽略)                      專案名稱
        │                               │
        ▼                               ▼
  TeamDisplayName              ConsolidatedProjectGroup
        │                               │
        ▼                               │
  ConsolidatedReleaseEntry ◄────────────┘
        │
        ├── Authors (from PR.AuthorName, 去重)
        ├── PullRequests (from PR.PRUrl)
        └── OriginalData
            ├── WorkItem (UserStoryWorkItemOutput)
            └── PullRequests (List<MergeRequestOutput>)
```

## 配對邏輯

1. 讀取 Bitbucket + GitLab ByUser PR 資料
2. 建立 `Dictionary<string, List<(MergeRequestOutput PR, string ProjectName)>>` 以 PrId 為 Key
3. 讀取 UserStories Work Item 資料
4. 建立 TeamMapping `Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)`
5. 對每個 Work Item：
   - 依 PrId 查詢配對的 PR 清單
   - 取得 TeamDisplayName（mapping 或原始名稱）
   - 從 PR 收集 Authors（去重）和 PR URLs
   - 從 PR 取得 ProjectName（若無 PR 則為 "unknown"）
6. 以 ProjectName 分組
7. 每組內依 TeamDisplayName 升冪 → WorkItemId 升冪 排序

## 排序規則

- 第一層：ProjectName 分組（字典序）
- 第二層：同組內 TeamDisplayName 升冪（string comparison, ordinal）
- 第三層：同團隊內 WorkItemId 升冪（int comparison）
