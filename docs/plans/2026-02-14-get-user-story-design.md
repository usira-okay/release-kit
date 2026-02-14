# Get User Story 功能實作計畫

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 為 release-kit 新增 PR ID 追蹤、Work Item 的 PR 來源關聯，以及 `get-user-story` 指令將 Task/Bug 遞迴解析至 parent User Story。

**Architecture:** 分離式任務（方案 A）— 三個獨立功能分別實作。PR ID 新增到既有 Domain entity 與 DTO；Work Item 抓取邏輯重構為保留 PR 關聯的一對一記錄；新增 `GetUserStoryTask` 讀取 Redis work items 後透過 Azure DevOps API 遞迴查詢 parent 直到找到 User Story/Feature/Epic。

**Tech Stack:** .NET 9, xUnit, Moq, StackExchange.Redis, System.Text.Json

---

## Task 1: 新增 PullRequestId 到 MergeRequest entity 與 Mappers

**Files:**
- Modify: `src/ReleaseKit.Domain/Entities/MergeRequest.cs:14`
- Modify: `src/ReleaseKit.Infrastructure/SourceControl/GitLab/GitLabMergeRequestMapper.cs:19`
- Modify: `src/ReleaseKit.Infrastructure/SourceControl/Bitbucket/BitbucketPullRequestMapper.cs:19`
- Modify: `tests/ReleaseKit.Infrastructure.Tests/SourceControl/GitLab/GitLabMergeRequestMapperTests.cs:40`
- Modify: `tests/ReleaseKit.Infrastructure.Tests/SourceControl/Bitbucket/BitbucketPullRequestMapperTests.cs:60`

**Step 1: 寫失敗測試 — GitLab mapper 應映射 PullRequestId**

在 `GitLabMergeRequestMapperTests.ToDomain_WithValidResponse_ShouldMapCorrectly` 最後加入：

```csharp
Assert.Equal(42, domain.PullRequestId);
```

在 `BitbucketPullRequestMapperTests.ToDomain_WithValidResponse_ShouldMapCorrectly` 的 Arrange 區塊加入 `Id = 42`，Assert 區塊加入：

```csharp
Assert.Equal(42, result.PullRequestId);
```

**Step 2: 執行測試確認失敗**

```bash
dotnet test src/release-kit.sln --filter "FullyQualifiedName~MapperTests" --no-restore
```

Expected: 編譯失敗 — `MergeRequest` 沒有 `PullRequestId` 屬性

**Step 3: 實作最小變更使測試通過**

`MergeRequest.cs` — 在 `Title` 之前加入：

```csharp
/// <summary>
/// PR/MR 識別碼
/// </summary>
/// <remarks>
/// GitLab: 對應 iid（專案內唯一編號）。
/// Bitbucket: 對應 id（Repository 內唯一編號）。
/// </remarks>
public required int PullRequestId { get; init; }
```

`GitLabMergeRequestMapper.cs` — 在 `return new MergeRequest` 中加入：

```csharp
PullRequestId = response.Iid,
```

`BitbucketPullRequestMapper.cs` — 在 `return new MergeRequest` 中加入：

```csharp
PullRequestId = response.Id,
```

修復其他 mapper 測試中缺少 `PullRequestId` 的編譯錯誤 — 在所有構建 `GitLabMergeRequestResponse` 的測試 Arrange 中確認 `Iid` 已設值（已有的測試已設定），在所有 `BitbucketPullRequestResponse` 測試 Arrange 中確認 `Id` 已設值（預設為 0，可接受）。

**Step 4: 執行測試確認通過**

```bash
dotnet test src/release-kit.sln --filter "FullyQualifiedName~MapperTests" --no-restore
```

Expected: 全部通過

**Step 5: Commit**

```bash
git add -A && git commit -m "feat: 新增 PullRequestId 到 MergeRequest entity 與 GitLab/Bitbucket mappers"
```

---

## Task 2: 新增 PullRequestId 到 MergeRequestOutput 與 BaseFetchPullRequestsTask

**Files:**
- Modify: `src/ReleaseKit.Application/Common/MergeRequestOutput.cs:10`
- Modify: `src/ReleaseKit.Application/Tasks/BaseFetchPullRequestsTask.cs:125`
- Modify: `tests/ReleaseKit.Application.Tests/Tasks/FetchAzureDevOpsWorkItemsTaskTests.cs:330`

**Step 1: 加入 PullRequestId 到 MergeRequestOutput**

`MergeRequestOutput.cs` — 在 `Title` 之前加入：

```csharp
/// <summary>
/// PR/MR 識別碼
/// </summary>
public int PullRequestId { get; init; }
```

**Step 2: 更新 BaseFetchPullRequestsTask 輸出映射**

`BaseFetchPullRequestsTask.cs` line 125 — 在 `new MergeRequestOutput` 中加入：

```csharp
PullRequestId = mr.PullRequestId,
```

**Step 3: 更新測試 Helper 設定 PullRequestId**

`FetchAzureDevOpsWorkItemsTaskTests.cs` `CreateMergeRequest` helper — 加入：

```csharp
PullRequestId = 1,
```

**Step 4: 執行測試確認通過**

```bash
dotnet test src/release-kit.sln --no-restore
```

Expected: 全部通過

**Step 5: Commit**

```bash
git add -A && git commit -m "feat: 新增 PullRequestId 到 MergeRequestOutput 與 BaseFetchPullRequestsTask"
```

---

## Task 3: 新增 PR 來源欄位到 WorkItemOutput 並重構 FetchAzureDevOpsWorkItemsTask

**Files:**
- Modify: `src/ReleaseKit.Application/Common/WorkItemOutput.cs`
- Modify: `src/ReleaseKit.Application/Tasks/FetchAzureDevOpsWorkItemsTask.cs`
- Modify: `tests/ReleaseKit.Application.Tests/Tasks/FetchAzureDevOpsWorkItemsTaskTests.cs`

**Step 1: 寫失敗測試 — Work Item 輸出應包含 PR 來源資訊**

在 `FetchAzureDevOpsWorkItemsTaskTests.cs` 新增測試：

```csharp
[Fact]
public async Task ExecuteAsync_WithSingleVSTSId_ShouldIncludePRSourceInfo()
{
    // Arrange
    var fetchResult = new FetchResult
    {
        Results = new List<ProjectResult>
        {
            new ProjectResult
            {
                ProjectPath = "group/api",
                Platform = SourceControlPlatform.GitLab,
                PullRequests = new List<MergeRequestOutput>
                {
                    new MergeRequestOutput
                    {
                        PullRequestId = 101,
                        Title = "VSTS12345 修復登入錯誤",
                        SourceBranch = "feature/test",
                        TargetBranch = "main",
                        State = "merged",
                        AuthorUserId = "12345",
                        AuthorName = "Test User",
                        PRUrl = "https://gitlab.example.com/pr/101",
                        CreatedAt = DateTimeOffset.UtcNow
                    }
                }
            }
        }
    };
    SetupRedis(gitLabData: fetchResult);

    var workItem = CreateWorkItem(12345, "修復登入錯誤", "Bug", "Active");
    _azureDevOpsRepositoryMock
        .Setup(x => x.GetWorkItemAsync(12345))
        .ReturnsAsync(Result<WorkItem>.Success(workItem));

    var task = CreateTask();

    // Act
    await task.ExecuteAsync();

    // Assert
    VerifyRedisWrite(result =>
    {
        Assert.Single(result.WorkItems);
        var item = result.WorkItems[0];
        Assert.Equal(101, item.SourcePullRequestId);
        Assert.Equal("group/api", item.SourceProjectName);
        Assert.Equal("https://gitlab.example.com/pr/101", item.SourcePRUrl);
    });
}
```

同時新增 duplicate 場景測試：

```csharp
[Fact]
public async Task ExecuteAsync_WithDuplicateVSTSIdsAcrossPRs_ShouldProduceMultipleOutputsButFetchOnce()
{
    // Arrange
    var fetchResult = new FetchResult
    {
        Results = new List<ProjectResult>
        {
            new ProjectResult
            {
                ProjectPath = "group/project1",
                Platform = SourceControlPlatform.GitLab,
                PullRequests = new List<MergeRequestOutput>
                {
                    new MergeRequestOutput
                    {
                        PullRequestId = 1,
                        Title = "VSTS123 Fix issue",
                        SourceBranch = "feature/test",
                        TargetBranch = "main",
                        State = "merged",
                        AuthorUserId = "12345",
                        AuthorName = "Test User",
                        PRUrl = "https://example.com/pr/1",
                        CreatedAt = DateTimeOffset.UtcNow
                    },
                    new MergeRequestOutput
                    {
                        PullRequestId = 2,
                        Title = "VSTS123 另一個 PR 提到相同 WorkItem",
                        SourceBranch = "feature/test2",
                        TargetBranch = "main",
                        State = "merged",
                        AuthorUserId = "12345",
                        AuthorName = "Test User",
                        PRUrl = "https://example.com/pr/2",
                        CreatedAt = DateTimeOffset.UtcNow
                    }
                }
            }
        }
    };
    SetupRedis(gitLabData: fetchResult);

    var workItem = CreateWorkItem(123, "Fix issue", "Bug", "Active");
    _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(123))
        .ReturnsAsync(Result<WorkItem>.Success(workItem));

    var task = CreateTask();

    // Act
    await task.ExecuteAsync();

    // Assert
    _azureDevOpsRepositoryMock.Verify(x => x.GetWorkItemAsync(123), Times.Once);
    VerifyRedisWrite(result =>
    {
        Assert.Equal(2, result.WorkItems.Count);
        Assert.Equal(2, result.TotalPRsAnalyzed);
        Assert.Equal(1, result.TotalWorkItemsFound);

        Assert.Equal("https://example.com/pr/1", result.WorkItems[0].SourcePRUrl);
        Assert.Equal("https://example.com/pr/2", result.WorkItems[1].SourcePRUrl);
    });
}
```

**Step 2: 執行測試確認失敗**

```bash
dotnet test src/release-kit.sln --filter "FullyQualifiedName~FetchAzureDevOpsWorkItemsTaskTests" --no-restore
```

Expected: 編譯失敗 — `WorkItemOutput` 沒有 `SourcePullRequestId` 等屬性

**Step 3: 實作 WorkItemOutput 新欄位**

`WorkItemOutput.cs` — 加入三個欄位：

```csharp
/// <summary>
/// 來源 PR 識別碼（可為 null，表示無關聯 PR）
/// </summary>
public int? SourcePullRequestId { get; init; }

/// <summary>
/// 來源 PR 所屬專案名稱
/// </summary>
public string? SourceProjectName { get; init; }

/// <summary>
/// 來源 PR 網址
/// </summary>
public string? SourcePRUrl { get; init; }
```

**Step 4: 重構 FetchAzureDevOpsWorkItemsTask**

以下為完整重構後的 `FetchAzureDevOpsWorkItemsTask.cs`：

```csharp
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ReleaseKit.Application.Common;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Common;
using ReleaseKit.Domain.Entities;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 拉取 Azure DevOps Work Item 資訊任務
/// </summary>
public class FetchAzureDevOpsWorkItemsTask : ITask
{
    private readonly ILogger<FetchAzureDevOpsWorkItemsTask> _logger;
    private readonly IRedisService _redisService;
    private readonly IAzureDevOpsRepository _azureDevOpsRepository;

    /// <summary>
    /// Work Item 與 PR 來源配對
    /// </summary>
    private sealed record WorkItemPullRequestPair(
        int WorkItemId,
        int PullRequestId,
        string ProjectName,
        string PRUrl);

    /// <summary>
    /// 建構子
    /// </summary>
    public FetchAzureDevOpsWorkItemsTask(
        ILogger<FetchAzureDevOpsWorkItemsTask> logger,
        IRedisService redisService,
        IAzureDevOpsRepository azureDevOpsRepository)
    {
        _logger = logger;
        _redisService = redisService;
        _azureDevOpsRepository = azureDevOpsRepository;
    }

    /// <summary>
    /// 執行拉取 Azure DevOps Work Item 資訊任務
    /// </summary>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始拉取 Azure DevOps Work Item 資訊");

        await _redisService.DeleteAsync(RedisKeys.AzureDevOpsWorkItems);

        var projectResults = await LoadProjectResultsFromRedisAsync();
        var allPRCount = projectResults.SelectMany(p => p.PullRequests).Count();

        if (allPRCount == 0)
        {
            _logger.LogWarning("無可用的 PR 資料，任務結束");
            return;
        }

        var pairs = ParseVSTSIdsWithPRInfo(projectResults);

        if (pairs.Count == 0)
        {
            _logger.LogInformation("未從 PR 標題中解析到任何 VSTS ID，任務結束");
            return;
        }

        var uniqueWorkItemCount = pairs.Select(p => p.WorkItemId).Distinct().Count();
        _logger.LogInformation(
            "從 {PRCount} 個 PR 中解析出 {PairCount} 筆 Work Item 配對（{UniqueCount} 個不重複）",
            allPRCount, pairs.Count, uniqueWorkItemCount);

        var workItemOutputs = await FetchWorkItemsAsync(pairs);

        var successCount = workItemOutputs.Count(w => w.IsSuccess);
        var failureCount = workItemOutputs.Count(w => !w.IsSuccess);

        var result = new WorkItemFetchResult
        {
            WorkItems = workItemOutputs,
            TotalPRsAnalyzed = allPRCount,
            TotalWorkItemsFound = uniqueWorkItemCount,
            SuccessCount = successCount,
            FailureCount = failureCount
        };

        await _redisService.SetAsync(RedisKeys.AzureDevOpsWorkItems, result.ToJson(), null);

        _logger.LogInformation(
            "完成 Work Item 查詢：共 {Total} 筆輸出（{UniqueCount} 個不重複），成功 {Success} 筆，失敗 {Failure} 筆",
            workItemOutputs.Count, uniqueWorkItemCount, successCount, failureCount);
    }

    /// <summary>
    /// 從 Redis 載入各平台的 ProjectResult 資料
    /// </summary>
    private async Task<List<ProjectResult>> LoadProjectResultsFromRedisAsync()
    {
        var projectResults = new List<ProjectResult>();

        var redisKeys = new[]
        {
            (Key: RedisKeys.GitLabPullRequestsByUser, Platform: "GitLab"),
            (Key: RedisKeys.BitbucketPullRequestsByUser, Platform: "Bitbucket")
        };

        foreach (var (key, platform) in redisKeys)
        {
            _logger.LogInformation("讀取 Redis Key: {RedisKey}", key);
            var json = await _redisService.GetAsync(key);
            if (json is not null)
            {
                var result = json.ToTypedObject<FetchResult>();
                if (result is not null)
                {
                    projectResults.AddRange(result.Results);
                }
            }
            else
            {
                _logger.LogWarning("Redis Key {RedisKey} 不存在或為空", key);
            }
        }

        return projectResults;
    }

    /// <summary>
    /// 從 PR 標題中解析 VSTS ID 並保留 PR 來源資訊
    /// </summary>
    private List<WorkItemPullRequestPair> ParseVSTSIdsWithPRInfo(List<ProjectResult> projectResults)
    {
        var pairs = new List<WorkItemPullRequestPair>();
        var regex = new Regex(@"VSTS(\d+)", RegexOptions.None);

        foreach (var project in projectResults)
        {
            foreach (var pr in project.PullRequests)
            {
                foreach (Match match in regex.Matches(pr.Title))
                {
                    if (int.TryParse(match.Groups[1].Value, out var id))
                    {
                        pairs.Add(new WorkItemPullRequestPair(
                            id, pr.PullRequestId, project.ProjectPath, pr.PRUrl));
                    }
                }
            }
        }

        return pairs;
    }

    /// <summary>
    /// 逐一查詢 Work Item 並組裝含 PR 來源資訊的輸出
    /// </summary>
    private async Task<List<WorkItemOutput>> FetchWorkItemsAsync(List<WorkItemPullRequestPair> pairs)
    {
        var outputs = new List<WorkItemOutput>();
        var cache = new Dictionary<int, Result<WorkItem>>();
        var uniqueIds = pairs.Select(p => p.WorkItemId).Distinct().ToList();

        _logger.LogInformation("開始查詢 {WorkItemCount} 個不重複的 Work Item", uniqueIds.Count);
        var processedCount = 0;

        foreach (var workItemId in uniqueIds)
        {
            processedCount++;
            _logger.LogInformation("查詢 Work Item {CurrentCount}/{TotalCount}：{WorkItemId}",
                processedCount, uniqueIds.Count, workItemId);
            cache[workItemId] = await _azureDevOpsRepository.GetWorkItemAsync(workItemId);
        }

        foreach (var pair in pairs)
        {
            var result = cache[pair.WorkItemId];

            if (result.IsSuccess)
            {
                outputs.Add(new WorkItemOutput
                {
                    WorkItemId = result.Value.WorkItemId,
                    Title = result.Value.Title,
                    Type = result.Value.Type,
                    State = result.Value.State,
                    Url = result.Value.Url,
                    OriginalTeamName = result.Value.OriginalTeamName,
                    IsSuccess = true,
                    ErrorMessage = null,
                    SourcePullRequestId = pair.PullRequestId,
                    SourceProjectName = pair.ProjectName,
                    SourcePRUrl = pair.PRUrl
                });
            }
            else
            {
                _logger.LogWarning("查詢 Work Item {WorkItemId} 失敗：{ErrorMessage}",
                    pair.WorkItemId, result.Error.Message);
                outputs.Add(new WorkItemOutput
                {
                    WorkItemId = pair.WorkItemId,
                    Title = null,
                    Type = null,
                    State = null,
                    Url = null,
                    OriginalTeamName = null,
                    IsSuccess = false,
                    ErrorMessage = result.Error.Message,
                    SourcePullRequestId = pair.PullRequestId,
                    SourceProjectName = pair.ProjectName,
                    SourcePRUrl = pair.PRUrl
                });
            }
        }

        return outputs;
    }
}
```

**Step 5: 更新現有測試以符合新結構**

需要更新的測試：

1. `ExecuteAsync_WithDuplicateVSTSIdsAcrossPRs_ShouldDeduplicateAndFetchOnce` — 刪除此測試（已被新的 `ExecuteAsync_WithDuplicateVSTSIdsAcrossPRs_ShouldProduceMultipleOutputsButFetchOnce` 取代）

2. 所有使用 `CreateFetchResult` helper 的測試 — helper 需更新以設定 `PullRequestId`：

```csharp
private FetchResult CreateFetchResult(string title, string sourceBranch, string targetBranch, SourceControlPlatform platform = SourceControlPlatform.GitLab)
{
    return new FetchResult
    {
        Results = new List<ProjectResult>
        {
            new ProjectResult
            {
                ProjectPath = "group/project1",
                Platform = platform,
                PullRequests = new List<MergeRequestOutput>
                {
                    CreateMergeRequest(title, sourceBranch, targetBranch)
                }
            }
        }
    };
}

private MergeRequestOutput CreateMergeRequest(string title, string sourceBranch = "feature/test", string targetBranch = "main")
{
    return new MergeRequestOutput
    {
        PullRequestId = 1,
        Title = title,
        SourceBranch = sourceBranch,
        TargetBranch = targetBranch,
        State = "merged",
        AuthorUserId = "12345",
        AuthorName = "Test User",
        PRUrl = "https://example.com/pr/1",
        CreatedAt = DateTimeOffset.UtcNow
    };
}
```

**Step 6: 執行全部測試確認通過**

```bash
dotnet test src/release-kit.sln --no-restore
```

Expected: 全部通過

**Step 7: Commit**

```bash
git add -A && git commit -m "feat: 新增 PR 來源欄位到 WorkItemOutput 並重構 FetchAzureDevOpsWorkItemsTask"
```

---

## Task 4: 新增 Relations 模型與 ParentWorkItemId 到 WorkItem

**Files:**
- Create: `src/ReleaseKit.Infrastructure/AzureDevOps/Models/AzureDevOpsRelationResponse.cs`
- Modify: `src/ReleaseKit.Infrastructure/AzureDevOps/Models/AzureDevOpsWorkItemResponse.cs`
- Modify: `src/ReleaseKit.Domain/Entities/WorkItem.cs`
- Modify: `src/ReleaseKit.Infrastructure/AzureDevOps/Mappers/AzureDevOpsWorkItemMapper.cs`
- Modify: `tests/ReleaseKit.Infrastructure.Tests/AzureDevOps/Mappers/AzureDevOpsWorkItemMapperTests.cs` (若存在) 或建立新測試

**Step 1: 寫失敗測試 — Mapper 應解析 parent relation 並設定 ParentWorkItemId**

建立或更新 mapper 測試，先確認測試目錄：

```bash
find tests/ -path "*AzureDevOps*Mapper*" -name "*.cs"
```

若不存在，建立 `tests/ReleaseKit.Infrastructure.Tests/AzureDevOps/Mappers/AzureDevOpsWorkItemMapperTests.cs`：

```csharp
using ReleaseKit.Infrastructure.AzureDevOps.Mappers;
using ReleaseKit.Infrastructure.AzureDevOps.Models;

namespace ReleaseKit.Infrastructure.Tests.AzureDevOps.Mappers;

/// <summary>
/// AzureDevOpsWorkItemMapper 單元測試
/// </summary>
public class AzureDevOpsWorkItemMapperTests
{
    [Fact]
    public void ToDomain_WithParentRelation_ShouldSetParentWorkItemId()
    {
        // Arrange
        var response = new AzureDevOpsWorkItemResponse
        {
            Id = 100,
            Fields = new Dictionary<string, object?>
            {
                { "System.Title", "子任務" },
                { "System.WorkItemType", "Task" },
                { "System.State", "Active" },
                { "System.AreaPath", "MyProject\\MyTeam" }
            },
            Links = new AzureDevOpsLinksResponse
            {
                Html = new AzureDevOpsLinkResponse
                {
                    Href = "https://dev.azure.com/org/project/_workitems/edit/100"
                }
            },
            Relations = new List<AzureDevOpsRelationResponse>
            {
                new AzureDevOpsRelationResponse
                {
                    Rel = "System.LinkTypes.Hierarchy-Reverse",
                    Url = "https://dev.azure.com/org/_apis/wit/workItems/200"
                }
            }
        };

        // Act
        var workItem = AzureDevOpsWorkItemMapper.ToDomain(response);

        // Assert
        Assert.Equal(200, workItem.ParentWorkItemId);
    }

    [Fact]
    public void ToDomain_WithNoRelations_ShouldSetParentWorkItemIdToNull()
    {
        // Arrange
        var response = new AzureDevOpsWorkItemResponse
        {
            Id = 100,
            Fields = new Dictionary<string, object?>
            {
                { "System.Title", "獨立任務" },
                { "System.WorkItemType", "User Story" },
                { "System.State", "New" },
                { "System.AreaPath", "MyProject" }
            },
            Links = new AzureDevOpsLinksResponse
            {
                Html = new AzureDevOpsLinkResponse { Href = "https://example.com" }
            },
            Relations = null
        };

        // Act
        var workItem = AzureDevOpsWorkItemMapper.ToDomain(response);

        // Assert
        Assert.Null(workItem.ParentWorkItemId);
    }

    [Fact]
    public void ToDomain_WithNonParentRelations_ShouldSetParentWorkItemIdToNull()
    {
        // Arrange
        var response = new AzureDevOpsWorkItemResponse
        {
            Id = 100,
            Fields = new Dictionary<string, object?>
            {
                { "System.Title", "任務" },
                { "System.WorkItemType", "Task" },
                { "System.State", "Active" },
                { "System.AreaPath", "MyProject" }
            },
            Links = new AzureDevOpsLinksResponse
            {
                Html = new AzureDevOpsLinkResponse { Href = "https://example.com" }
            },
            Relations = new List<AzureDevOpsRelationResponse>
            {
                new AzureDevOpsRelationResponse
                {
                    Rel = "System.LinkTypes.Hierarchy-Forward",
                    Url = "https://dev.azure.com/org/_apis/wit/workItems/300"
                }
            }
        };

        // Act
        var workItem = AzureDevOpsWorkItemMapper.ToDomain(response);

        // Assert
        Assert.Null(workItem.ParentWorkItemId);
    }
}
```

**Step 2: 執行測試確認失敗**

```bash
dotnet test src/release-kit.sln --filter "FullyQualifiedName~AzureDevOpsWorkItemMapperTests" --no-restore
```

Expected: 編譯失敗

**Step 3: 實作**

建立 `src/ReleaseKit.Infrastructure/AzureDevOps/Models/AzureDevOpsRelationResponse.cs`：

```csharp
using System.Text.Json.Serialization;

namespace ReleaseKit.Infrastructure.AzureDevOps.Models;

/// <summary>
/// Azure DevOps Work Item 關聯回應模型
/// </summary>
public sealed record AzureDevOpsRelationResponse
{
    /// <summary>
    /// 關聯類型（如 System.LinkTypes.Hierarchy-Reverse 表示 parent）
    /// </summary>
    [JsonPropertyName("rel")]
    public string Rel { get; init; } = string.Empty;

    /// <summary>
    /// 關聯目標的 API URL
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;
}
```

`AzureDevOpsWorkItemResponse.cs` — 加入：

```csharp
/// <summary>
/// Work Item 關聯清單
/// </summary>
[JsonPropertyName("relations")]
public List<AzureDevOpsRelationResponse>? Relations { get; init; }
```

`WorkItem.cs` — 加入：

```csharp
/// <summary>
/// 父層 Work Item 識別碼
/// </summary>
/// <remarks>
/// 從 Azure DevOps API 回應的 relations 中解析。
/// 若無父層關聯或 relations 為空，則為 null。
/// </remarks>
public int? ParentWorkItemId { get; init; }
```

`AzureDevOpsWorkItemMapper.cs` — 更新：

```csharp
public static WorkItem ToDomain(AzureDevOpsWorkItemResponse response)
{
    return new WorkItem
    {
        WorkItemId = response.Id,
        Title = GetFieldValue(response.Fields, "System.Title"),
        Type = GetFieldValue(response.Fields, "System.WorkItemType"),
        State = GetFieldValue(response.Fields, "System.State"),
        Url = response.Links?.Html?.Href ?? string.Empty,
        OriginalTeamName = GetFieldValue(response.Fields, "System.AreaPath"),
        ParentWorkItemId = ExtractParentWorkItemId(response.Relations)
    };
}

/// <summary>
/// 從關聯清單中提取父層 Work Item ID
/// </summary>
private static int? ExtractParentWorkItemId(List<AzureDevOpsRelationResponse>? relations)
{
    if (relations is null) return null;

    var parentRelation = relations.FirstOrDefault(r =>
        r.Rel == "System.LinkTypes.Hierarchy-Reverse");

    if (parentRelation is null) return null;

    var segments = parentRelation.Url.Split('/');
    if (segments.Length > 0 && int.TryParse(segments[^1], out var parentId))
    {
        return parentId;
    }

    return null;
}
```

同時修復所有建構 `WorkItem` 的地方（如 `FetchAzureDevOpsWorkItemsTaskTests.CreateWorkItem`）以加入 `ParentWorkItemId`（預設 `null` 不需顯式設定，因為非 `required`）。

**Step 4: 執行測試確認通過**

```bash
dotnet test src/release-kit.sln --no-restore
```

Expected: 全部通過

**Step 5: Commit**

```bash
git add -A && git commit -m "feat: 新增 Relations 模型與 ParentWorkItemId 到 WorkItem entity"
```

---

## Task 5: 新增 UserStory DTOs、Redis Key 與 Console 佈線

**Files:**
- Create: `src/ReleaseKit.Application/Common/UserStoryOutput.cs`
- Create: `src/ReleaseKit.Application/Common/UserStoryFetchResult.cs`
- Modify: `src/ReleaseKit.Common/Constants/RedisKeys.cs`
- Modify: `src/ReleaseKit.Application/Tasks/TaskType.cs`
- Modify: `src/ReleaseKit.Console/Parsers/CommandLineParser.cs`
- Modify: `src/ReleaseKit.Application/Tasks/TaskFactory.cs`
- Modify: `src/ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs`
- Modify: `tests/ReleaseKit.Console.Tests/Parsers/CommandLineParserTests.cs`
- Modify: `tests/ReleaseKit.Application.Tests/Tasks/TaskFactoryTests.cs`

**Step 1: 寫失敗測試 — CommandLineParser 與 TaskFactory**

`CommandLineParserTests.cs` — 在 `Parse_WithValidTaskName_ShouldReturnSuccessWithCorrectTaskType` Theory 加入：

```csharp
[InlineData("get-user-story", TaskType.GetUserStory)]
```

在 `Parse_WithValidTaskName_ShouldBeCaseInsensitive` Theory 加入：

```csharp
[InlineData("GET-USER-STORY", TaskType.GetUserStory)]
```

在 `Parse_WithInvalidTaskName_ShouldShowValidTasks` 加入：

```csharp
Assert.Contains("get-user-story", result.ErrorMessage);
```

`TaskFactoryTests.cs` — 新增測試：

```csharp
[Fact]
public void CreateTask_WithGetUserStory_ShouldReturnCorrectTaskType()
{
    var task = _factory.CreateTask(TaskType.GetUserStory);

    Assert.NotNull(task);
    Assert.IsType<GetUserStoryTask>(task);
}
```

並在 constructor 中加入 Logger mock 與 Task 註冊：

```csharp
services.AddSingleton(new Mock<ILogger<GetUserStoryTask>>().Object);
```

```csharp
services.AddTransient<GetUserStoryTask>();
```

**Step 2: 執行測試確認失敗**

```bash
dotnet test src/release-kit.sln --filter "FullyQualifiedName~CommandLineParserTests|FullyQualifiedName~TaskFactoryTests" --no-restore
```

Expected: 編譯失敗 — `TaskType.GetUserStory` 與 `GetUserStoryTask` 不存在

**Step 3: 實作所有佈線**

`RedisKeys.cs` — 加入：

```csharp
/// <summary>
/// Azure DevOps User Story 解析結果
/// </summary>
public const string AzureDevOpsUserStories = "AzureDevOps:UserStories";
```

`UserStoryOutput.cs`：

```csharp
namespace ReleaseKit.Application.Common;

/// <summary>
/// User Story 解析輸出 DTO
/// </summary>
/// <remarks>
/// 表示單一 Work Item 解析至 User Story 層級的結果。
/// 若原始 Work Item 為 Task/Bug，會解析至其 parent User Story。
/// </remarks>
public sealed record UserStoryOutput
{
    /// <summary>
    /// 解析後的 Work Item 識別碼（User Story/Feature/Epic 的 ID）
    /// </summary>
    public required int WorkItemId { get; init; }

    /// <summary>
    /// 原始 Work Item 識別碼
    /// </summary>
    /// <remarks>
    /// 若原始 Work Item 已是 User Story 以上，則與 WorkItemId 相同。
    /// </remarks>
    public required int OriginalWorkItemId { get; init; }

    /// <summary>
    /// 標題（失敗時為 null）
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// 類型（User Story/Feature/Epic 等，失敗時為 null）
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// 狀態（失敗時為 null）
    /// </summary>
    public string? State { get; init; }

    /// <summary>
    /// Work Item 網頁連結（失敗時為 null）
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// 原始區域路徑（失敗時為 null）
    /// </summary>
    public string? OriginalTeamName { get; init; }

    /// <summary>
    /// 是否成功解析
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// 失敗時的錯誤原因
    /// </summary>
    public string? ErrorMessage { get; init; }
}
```

`UserStoryFetchResult.cs`：

```csharp
namespace ReleaseKit.Application.Common;

/// <summary>
/// User Story 解析結果彙整 DTO
/// </summary>
public sealed record UserStoryFetchResult
{
    /// <summary>
    /// 所有 User Story 解析結果清單
    /// </summary>
    public required List<UserStoryOutput> UserStories { get; init; }

    /// <summary>
    /// 處理的 Work Item 總數
    /// </summary>
    public required int TotalWorkItemsProcessed { get; init; }

    /// <summary>
    /// 已是 User Story 以上類型的數量
    /// </summary>
    public required int AlreadyUserStoryCount { get; init; }

    /// <summary>
    /// 成功解析至 User Story 的數量
    /// </summary>
    public required int ResolvedCount { get; init; }

    /// <summary>
    /// 保留原始資料的數量（無法解析或原始失敗）
    /// </summary>
    public required int KeptOriginalCount { get; init; }
}
```

`TaskType.cs` — 加入：

```csharp
/// <summary>
/// 解析 Work Item 至 User Story 層級
/// </summary>
GetUserStory
```

`CommandLineParser.cs` — 在 `_taskMappings` 加入：

```csharp
{ "get-user-story", TaskType.GetUserStory },
```

建立空的 `GetUserStoryTask`（佔位，Task 6 實作）：

`src/ReleaseKit.Application/Tasks/GetUserStoryTask.cs`：

```csharp
using Microsoft.Extensions.Logging;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 解析 Work Item 至 User Story 層級任務
/// </summary>
public class GetUserStoryTask : ITask
{
    private readonly ILogger<GetUserStoryTask> _logger;
    private readonly IRedisService _redisService;
    private readonly IAzureDevOpsRepository _azureDevOpsRepository;

    /// <summary>
    /// 建構子
    /// </summary>
    public GetUserStoryTask(
        ILogger<GetUserStoryTask> logger,
        IRedisService redisService,
        IAzureDevOpsRepository azureDevOpsRepository)
    {
        _logger = logger;
        _redisService = redisService;
        _azureDevOpsRepository = azureDevOpsRepository;
    }

    /// <summary>
    /// 執行解析 Work Item 至 User Story 層級任務
    /// </summary>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始解析 Work Item 至 User Story 層級");
        await Task.CompletedTask;
    }
}
```

`TaskFactory.cs` — 在 switch 加入：

```csharp
TaskType.GetUserStory => _serviceProvider.GetRequiredService<GetUserStoryTask>(),
```

`ServiceCollectionExtensions.cs` — 在任務註冊區塊加入：

```csharp
services.AddTransient<GetUserStoryTask>();
```

**Step 4: 執行測試確認通過**

```bash
dotnet test src/release-kit.sln --no-restore
```

Expected: 全部通過

**Step 5: Commit**

```bash
git add -A && git commit -m "feat: 新增 UserStory DTOs、Redis key 與 get-user-story console 佈線"
```

---

## Task 6: 實作 GetUserStoryTask 基本場景

**Files:**
- Create: `tests/ReleaseKit.Application.Tests/Tasks/GetUserStoryTaskTests.cs`
- Modify: `src/ReleaseKit.Application/Tasks/GetUserStoryTask.cs`

**Step 1: 寫失敗測試 — 基本場景**

建立 `tests/ReleaseKit.Application.Tests/Tasks/GetUserStoryTaskTests.cs`：

```csharp
using Microsoft.Extensions.Logging;
using Moq;
using ReleaseKit.Application.Common;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Common;
using ReleaseKit.Domain.Entities;

namespace ReleaseKit.Application.Tests.Tasks;

/// <summary>
/// GetUserStoryTask 單元測試
/// </summary>
public class GetUserStoryTaskTests
{
    private readonly Mock<ILogger<GetUserStoryTask>> _loggerMock;
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly Mock<IAzureDevOpsRepository> _azureDevOpsRepositoryMock;

    public GetUserStoryTaskTests()
    {
        _loggerMock = new Mock<ILogger<GetUserStoryTask>>();
        _redisServiceMock = new Mock<IRedisService>();
        _azureDevOpsRepositoryMock = new Mock<IAzureDevOpsRepository>();
    }

    [Fact]
    public async Task ExecuteAsync_WithNoWorkItems_ShouldExitGracefully()
    {
        // Arrange
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsWorkItems))
            .ReturnsAsync((string?)null);
        _redisServiceMock.Setup(x => x.DeleteAsync(RedisKeys.AzureDevOpsUserStories))
            .ReturnsAsync(true);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _redisServiceMock.Verify(
            x => x.SetAsync(RedisKeys.AzureDevOpsUserStories, It.IsAny<string>(), null),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithUserStoryType_ShouldKeepAsIs()
    {
        // Arrange
        var workItemResult = CreateWorkItemFetchResult(
            CreateWorkItemOutput(100, "使用者故事", "User Story", "Active"));
        SetupRedis(workItemResult);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _azureDevOpsRepositoryMock.Verify(
            x => x.GetWorkItemAsync(It.IsAny<int>()), Times.Never);
        VerifyRedisWrite(result =>
        {
            Assert.Single(result.UserStories);
            var output = result.UserStories[0];
            Assert.Equal(100, output.WorkItemId);
            Assert.Equal(100, output.OriginalWorkItemId);
            Assert.Equal("User Story", output.Type);
            Assert.True(output.IsSuccess);
        });
    }

    [Theory]
    [InlineData("Feature")]
    [InlineData("Epic")]
    public async Task ExecuteAsync_WithHighLevelType_ShouldKeepAsIs(string type)
    {
        // Arrange
        var workItemResult = CreateWorkItemFetchResult(
            CreateWorkItemOutput(100, "高層級項目", type, "Active"));
        SetupRedis(workItemResult);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _azureDevOpsRepositoryMock.Verify(
            x => x.GetWorkItemAsync(It.IsAny<int>()), Times.Never);
        VerifyRedisWrite(result =>
        {
            Assert.Single(result.UserStories);
            Assert.Equal(100, result.UserStories[0].WorkItemId);
            Assert.Equal(100, result.UserStories[0].OriginalWorkItemId);
            Assert.Equal(type, result.UserStories[0].Type);
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithFailedWorkItem_ShouldKeepAsIs()
    {
        // Arrange
        var workItemResult = CreateWorkItemFetchResult(
            new WorkItemOutput
            {
                WorkItemId = 999,
                IsSuccess = false,
                ErrorMessage = "Work Item 不存在"
            });
        SetupRedis(workItemResult);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _azureDevOpsRepositoryMock.Verify(
            x => x.GetWorkItemAsync(It.IsAny<int>()), Times.Never);
        VerifyRedisWrite(result =>
        {
            Assert.Single(result.UserStories);
            Assert.Equal(999, result.UserStories[0].WorkItemId);
            Assert.Equal(999, result.UserStories[0].OriginalWorkItemId);
            Assert.False(result.UserStories[0].IsSuccess);
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithTaskType_ParentIsUserStory_ShouldResolveToParent()
    {
        // Arrange
        var workItemResult = CreateWorkItemFetchResult(
            CreateWorkItemOutput(100, "子任務", "Task", "Active"));
        SetupRedis(workItemResult);

        var taskWorkItem = CreateWorkItem(100, "子任務", "Task", "Active", parentId: 200);
        var userStoryWorkItem = CreateWorkItem(200, "使用者故事", "User Story", "Active");

        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(100))
            .ReturnsAsync(Result<WorkItem>.Success(taskWorkItem));
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(200))
            .ReturnsAsync(Result<WorkItem>.Success(userStoryWorkItem));

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        VerifyRedisWrite(result =>
        {
            Assert.Single(result.UserStories);
            var output = result.UserStories[0];
            Assert.Equal(200, output.WorkItemId);
            Assert.Equal(100, output.OriginalWorkItemId);
            Assert.Equal("User Story", output.Type);
            Assert.Equal("使用者故事", output.Title);
            Assert.True(output.IsSuccess);
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithTaskType_NoParent_ShouldKeepOriginal()
    {
        // Arrange
        var workItemResult = CreateWorkItemFetchResult(
            CreateWorkItemOutput(100, "獨立任務", "Task", "Active"));
        SetupRedis(workItemResult);

        var taskWorkItem = CreateWorkItem(100, "獨立任務", "Task", "Active", parentId: null);
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(100))
            .ReturnsAsync(Result<WorkItem>.Success(taskWorkItem));

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        VerifyRedisWrite(result =>
        {
            Assert.Single(result.UserStories);
            var output = result.UserStories[0];
            Assert.Equal(100, output.WorkItemId);
            Assert.Equal(100, output.OriginalWorkItemId);
            Assert.Equal("Task", output.Type);
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithTaskType_ParentApiFails_ShouldKeepOriginal()
    {
        // Arrange
        var workItemResult = CreateWorkItemFetchResult(
            CreateWorkItemOutput(100, "子任務", "Task", "Active"));
        SetupRedis(workItemResult);

        var taskWorkItem = CreateWorkItem(100, "子任務", "Task", "Active", parentId: 200);
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(100))
            .ReturnsAsync(Result<WorkItem>.Success(taskWorkItem));
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(200))
            .ReturnsAsync(Result<WorkItem>.Failure(Error.AzureDevOps.WorkItemNotFound(200)));

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        VerifyRedisWrite(result =>
        {
            Assert.Single(result.UserStories);
            var output = result.UserStories[0];
            Assert.Equal(100, output.WorkItemId);
            Assert.Equal(100, output.OriginalWorkItemId);
        });
    }

    // Helper methods

    private GetUserStoryTask CreateTask()
    {
        return new GetUserStoryTask(
            _loggerMock.Object,
            _redisServiceMock.Object,
            _azureDevOpsRepositoryMock.Object);
    }

    private WorkItemOutput CreateWorkItemOutput(int id, string title, string type, string state)
    {
        return new WorkItemOutput
        {
            WorkItemId = id,
            Title = title,
            Type = type,
            State = state,
            Url = $"https://dev.azure.com/org/project/_workitems/edit/{id}",
            OriginalTeamName = "MyTeam",
            IsSuccess = true,
            ErrorMessage = null
        };
    }

    private WorkItem CreateWorkItem(int id, string title, string type, string state, int? parentId = null)
    {
        return new WorkItem
        {
            WorkItemId = id,
            Title = title,
            Type = type,
            State = state,
            Url = $"https://dev.azure.com/org/project/_workitems/edit/{id}",
            OriginalTeamName = "MyTeam",
            ParentWorkItemId = parentId
        };
    }

    private WorkItemFetchResult CreateWorkItemFetchResult(params WorkItemOutput[] workItems)
    {
        return new WorkItemFetchResult
        {
            WorkItems = workItems.ToList(),
            TotalPRsAnalyzed = 1,
            TotalWorkItemsFound = workItems.Length,
            SuccessCount = workItems.Count(w => w.IsSuccess),
            FailureCount = workItems.Count(w => !w.IsSuccess)
        };
    }

    private void SetupRedis(WorkItemFetchResult workItemResult)
    {
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsWorkItems))
            .ReturnsAsync(workItemResult.ToJson());
        _redisServiceMock.Setup(x => x.DeleteAsync(RedisKeys.AzureDevOpsUserStories))
            .ReturnsAsync(true);
        _redisServiceMock.Setup(x => x.SetAsync(RedisKeys.AzureDevOpsUserStories, It.IsAny<string>(), null))
            .ReturnsAsync(true);
    }

    private void VerifyRedisWrite(Action<UserStoryFetchResult> assert)
    {
        _redisServiceMock.Verify(x => x.SetAsync(
            RedisKeys.AzureDevOpsUserStories,
            It.IsAny<string>(),
            null), Times.Once);

        _redisServiceMock.Invocations
            .Where(i => i.Method.Name == nameof(IRedisService.SetAsync))
            .Select(i => i.Arguments[1] as string)
            .Where(json => json != null)
            .ToList()
            .ForEach(json =>
            {
                var result = json!.ToTypedObject<UserStoryFetchResult>();
                if (result != null)
                {
                    assert(result);
                }
            });
    }
}
```

**Step 2: 執行測試確認失敗**

```bash
dotnet test src/release-kit.sln --filter "FullyQualifiedName~GetUserStoryTaskTests" --no-restore
```

Expected: 測試失敗（空的 ExecuteAsync 不會寫入 Redis）

**Step 3: 實作 GetUserStoryTask 完整邏輯**

完整 `src/ReleaseKit.Application/Tasks/GetUserStoryTask.cs`：

```csharp
using Microsoft.Extensions.Logging;
using ReleaseKit.Application.Common;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Common;
using ReleaseKit.Domain.Entities;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 解析 Work Item 至 User Story 層級任務
/// </summary>
public class GetUserStoryTask : ITask
{
    private const int MaxTraversalDepth = 10;

    private static readonly HashSet<string> HighLevelTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "User Story",
        "Feature",
        "Epic"
    };

    private readonly ILogger<GetUserStoryTask> _logger;
    private readonly IRedisService _redisService;
    private readonly IAzureDevOpsRepository _azureDevOpsRepository;

    /// <summary>
    /// 建構子
    /// </summary>
    public GetUserStoryTask(
        ILogger<GetUserStoryTask> logger,
        IRedisService redisService,
        IAzureDevOpsRepository azureDevOpsRepository)
    {
        _logger = logger;
        _redisService = redisService;
        _azureDevOpsRepository = azureDevOpsRepository;
    }

    /// <summary>
    /// 執行解析 Work Item 至 User Story 層級任務
    /// </summary>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始解析 Work Item 至 User Story 層級");

        await _redisService.DeleteAsync(RedisKeys.AzureDevOpsUserStories);

        var workItemFetchResult = await LoadWorkItemsFromRedisAsync();

        if (workItemFetchResult is null || workItemFetchResult.WorkItems.Count == 0)
        {
            _logger.LogWarning("無可用的 Work Item 資料，任務結束");
            return;
        }

        _logger.LogInformation("讀取到 {Count} 筆 Work Item，開始解析", workItemFetchResult.WorkItems.Count);

        var outputs = new List<UserStoryOutput>();
        var alreadyUserStoryCount = 0;
        var resolvedCount = 0;
        var keptOriginalCount = 0;

        foreach (var workItem in workItemFetchResult.WorkItems)
        {
            if (!workItem.IsSuccess)
            {
                _logger.LogInformation("Work Item {WorkItemId} 原始取得失敗，保留原始資料", workItem.WorkItemId);
                outputs.Add(CreateFromOriginal(workItem));
                keptOriginalCount++;
                continue;
            }

            if (IsHighLevelType(workItem.Type))
            {
                _logger.LogInformation("Work Item {WorkItemId} 已是 {Type}，直接保留", workItem.WorkItemId, workItem.Type);
                outputs.Add(CreateFromOriginal(workItem));
                alreadyUserStoryCount++;
                continue;
            }

            _logger.LogInformation("Work Item {WorkItemId} 類型為 {Type}，開始向上尋找 User Story", workItem.WorkItemId, workItem.Type);
            var resolved = await ResolveToUserStoryAsync(workItem);
            outputs.Add(resolved);

            if (resolved.WorkItemId != resolved.OriginalWorkItemId)
            {
                resolvedCount++;
            }
            else
            {
                keptOriginalCount++;
            }
        }

        var result = new UserStoryFetchResult
        {
            UserStories = outputs,
            TotalWorkItemsProcessed = workItemFetchResult.WorkItems.Count,
            AlreadyUserStoryCount = alreadyUserStoryCount,
            ResolvedCount = resolvedCount,
            KeptOriginalCount = keptOriginalCount
        };

        await _redisService.SetAsync(RedisKeys.AzureDevOpsUserStories, result.ToJson(), null);

        _logger.LogInformation(
            "完成 User Story 解析：共 {Total} 筆，已是 US 以上 {Already} 筆，成功解析 {Resolved} 筆，保留原始 {Kept} 筆",
            outputs.Count, alreadyUserStoryCount, resolvedCount, keptOriginalCount);
    }

    /// <summary>
    /// 從 Redis 載入 Work Item 資料
    /// </summary>
    private async Task<WorkItemFetchResult?> LoadWorkItemsFromRedisAsync()
    {
        var json = await _redisService.GetAsync(RedisKeys.AzureDevOpsWorkItems);
        if (json is null)
        {
            _logger.LogWarning("Redis Key {RedisKey} 不存在或為空", RedisKeys.AzureDevOpsWorkItems);
            return null;
        }

        return json.ToTypedObject<WorkItemFetchResult>();
    }

    /// <summary>
    /// 遞迴向上尋找 User Story / Feature / Epic
    /// </summary>
    private async Task<UserStoryOutput> ResolveToUserStoryAsync(WorkItemOutput originalWorkItem)
    {
        var currentResult = await _azureDevOpsRepository.GetWorkItemAsync(originalWorkItem.WorkItemId);

        for (var depth = 0; depth < MaxTraversalDepth; depth++)
        {
            if (!currentResult.IsSuccess || !currentResult.Value.ParentWorkItemId.HasValue)
            {
                _logger.LogInformation(
                    "Work Item {WorkItemId} 無法繼續向上查詢，保留原始資料",
                    originalWorkItem.WorkItemId);
                return CreateFromOriginal(originalWorkItem);
            }

            var parentId = currentResult.Value.ParentWorkItemId.Value;
            _logger.LogInformation("查詢 parent Work Item {ParentId}（深度 {Depth}）", parentId, depth + 1);

            var parentResult = await _azureDevOpsRepository.GetWorkItemAsync(parentId);

            if (!parentResult.IsSuccess)
            {
                _logger.LogWarning("查詢 parent Work Item {ParentId} 失敗，保留原始資料", parentId);
                return CreateFromOriginal(originalWorkItem);
            }

            if (IsHighLevelType(parentResult.Value.Type))
            {
                _logger.LogInformation(
                    "找到 {Type} {ParentId}（原始 Work Item {OriginalId}）",
                    parentResult.Value.Type, parentId, originalWorkItem.WorkItemId);
                return CreateFromParent(parentResult.Value, originalWorkItem.WorkItemId);
            }

            currentResult = parentResult;
        }

        _logger.LogWarning(
            "Work Item {WorkItemId} 超過最大遍歷深度 {MaxDepth}，保留原始資料",
            originalWorkItem.WorkItemId, MaxTraversalDepth);
        return CreateFromOriginal(originalWorkItem);
    }

    /// <summary>
    /// 判斷是否為 User Story 以上的類型
    /// </summary>
    private static bool IsHighLevelType(string? type)
    {
        return type is not null && HighLevelTypes.Contains(type);
    }

    /// <summary>
    /// 從原始 Work Item 建立 UserStoryOutput（保留原始資料）
    /// </summary>
    private static UserStoryOutput CreateFromOriginal(WorkItemOutput workItem)
    {
        return new UserStoryOutput
        {
            WorkItemId = workItem.WorkItemId,
            OriginalWorkItemId = workItem.WorkItemId,
            Title = workItem.Title,
            Type = workItem.Type,
            State = workItem.State,
            Url = workItem.Url,
            OriginalTeamName = workItem.OriginalTeamName,
            IsSuccess = workItem.IsSuccess,
            ErrorMessage = workItem.ErrorMessage
        };
    }

    /// <summary>
    /// 從 parent Work Item 建立 UserStoryOutput
    /// </summary>
    private static UserStoryOutput CreateFromParent(WorkItem parent, int originalWorkItemId)
    {
        return new UserStoryOutput
        {
            WorkItemId = parent.WorkItemId,
            OriginalWorkItemId = originalWorkItemId,
            Title = parent.Title,
            Type = parent.Type,
            State = parent.State,
            Url = parent.Url,
            OriginalTeamName = parent.OriginalTeamName,
            IsSuccess = true,
            ErrorMessage = null
        };
    }
}
```

**Step 4: 執行測試確認通過**

```bash
dotnet test src/release-kit.sln --filter "FullyQualifiedName~GetUserStoryTaskTests" --no-restore
```

Expected: 全部通過

**Step 5: Commit**

```bash
git add -A && git commit -m "feat: 實作 GetUserStoryTask 基本場景（User Story/Feature/Epic 直接保留、Task/Bug 向上找 parent）"
```

---

## Task 7: 實作 GetUserStoryTask 遞迴向上查詢場景

**Files:**
- Modify: `tests/ReleaseKit.Application.Tests/Tasks/GetUserStoryTaskTests.cs`

**Step 1: 新增遞迴場景測試**

在 `GetUserStoryTaskTests.cs` 加入：

```csharp
[Fact]
public async Task ExecuteAsync_WithBugType_ParentIsTask_GrandparentIsUserStory_ShouldResolveToGrandparent()
{
    // Arrange
    var workItemResult = CreateWorkItemFetchResult(
        CreateWorkItemOutput(100, "Bug 修正", "Bug", "Active"));
    SetupRedis(workItemResult);

    var bugWorkItem = CreateWorkItem(100, "Bug 修正", "Bug", "Active", parentId: 200);
    var taskWorkItem = CreateWorkItem(200, "父任務", "Task", "Active", parentId: 300);
    var userStoryWorkItem = CreateWorkItem(300, "使用者故事", "User Story", "Active");

    _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(100))
        .ReturnsAsync(Result<WorkItem>.Success(bugWorkItem));
    _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(200))
        .ReturnsAsync(Result<WorkItem>.Success(taskWorkItem));
    _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(300))
        .ReturnsAsync(Result<WorkItem>.Success(userStoryWorkItem));

    var task = CreateTask();

    // Act
    await task.ExecuteAsync();

    // Assert
    VerifyRedisWrite(result =>
    {
        Assert.Single(result.UserStories);
        var output = result.UserStories[0];
        Assert.Equal(300, output.WorkItemId);
        Assert.Equal(100, output.OriginalWorkItemId);
        Assert.Equal("User Story", output.Type);
        Assert.Equal("使用者故事", output.Title);
        Assert.True(output.IsSuccess);
        Assert.Equal(1, result.ResolvedCount);
    });
}

[Fact]
public async Task ExecuteAsync_WithTaskType_ParentIsFeature_ShouldResolveToFeature()
{
    // Arrange
    var workItemResult = CreateWorkItemFetchResult(
        CreateWorkItemOutput(100, "子任務", "Task", "Active"));
    SetupRedis(workItemResult);

    var taskWorkItem = CreateWorkItem(100, "子任務", "Task", "Active", parentId: 200);
    var featureWorkItem = CreateWorkItem(200, "功能", "Feature", "Active");

    _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(100))
        .ReturnsAsync(Result<WorkItem>.Success(taskWorkItem));
    _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(200))
        .ReturnsAsync(Result<WorkItem>.Success(featureWorkItem));

    var task = CreateTask();

    // Act
    await task.ExecuteAsync();

    // Assert
    VerifyRedisWrite(result =>
    {
        Assert.Single(result.UserStories);
        var output = result.UserStories[0];
        Assert.Equal(200, output.WorkItemId);
        Assert.Equal(100, output.OriginalWorkItemId);
        Assert.Equal("Feature", output.Type);
    });
}

[Fact]
public async Task ExecuteAsync_WithMixedTypes_ShouldProcessEachCorrectly()
{
    // Arrange
    var workItemResult = CreateWorkItemFetchResult(
        CreateWorkItemOutput(100, "使用者故事", "User Story", "Active"),
        CreateWorkItemOutput(200, "子任務", "Task", "Active"),
        new WorkItemOutput
        {
            WorkItemId = 300,
            IsSuccess = false,
            ErrorMessage = "Not found"
        });
    SetupRedis(workItemResult);

    var taskWorkItem = CreateWorkItem(200, "子任務", "Task", "Active", parentId: 400);
    var parentUserStory = CreateWorkItem(400, "父故事", "User Story", "Active");

    _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(200))
        .ReturnsAsync(Result<WorkItem>.Success(taskWorkItem));
    _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(400))
        .ReturnsAsync(Result<WorkItem>.Success(parentUserStory));

    var task = CreateTask();

    // Act
    await task.ExecuteAsync();

    // Assert
    VerifyRedisWrite(result =>
    {
        Assert.Equal(3, result.UserStories.Count);
        Assert.Equal(3, result.TotalWorkItemsProcessed);
        Assert.Equal(1, result.AlreadyUserStoryCount);
        Assert.Equal(1, result.ResolvedCount);
        Assert.Equal(1, result.KeptOriginalCount);
    });
}

[Fact]
public async Task ExecuteAsync_WithCurrentWorkItemApiFails_ShouldKeepOriginal()
{
    // Arrange
    var workItemResult = CreateWorkItemFetchResult(
        CreateWorkItemOutput(100, "子任務", "Task", "Active"));
    SetupRedis(workItemResult);

    _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(100))
        .ReturnsAsync(Result<WorkItem>.Failure(Error.AzureDevOps.ApiError("連線逾時")));

    var task = CreateTask();

    // Act
    await task.ExecuteAsync();

    // Assert
    VerifyRedisWrite(result =>
    {
        Assert.Single(result.UserStories);
        Assert.Equal(100, result.UserStories[0].WorkItemId);
        Assert.Equal(100, result.UserStories[0].OriginalWorkItemId);
    });
}
```

**Step 2: 執行測試確認通過**

```bash
dotnet test src/release-kit.sln --filter "FullyQualifiedName~GetUserStoryTaskTests" --no-restore
```

Expected: 全部通過（Task 6 的實作已包含遞迴邏輯）

**Step 3: 執行全部測試確認無回歸**

```bash
dotnet test src/release-kit.sln --no-restore
```

Expected: 全部通過

**Step 4: Commit**

```bash
git add -A && git commit -m "test: 新增 GetUserStoryTask 遞迴向上查詢與混合場景測試"
```

---

## Task 8: 最終驗證

**Step 1: 建置驗證**

```bash
dotnet build src/release-kit.sln --no-restore
```

Expected: 建置成功，無警告

**Step 2: 執行全部測試**

```bash
dotnet test src/release-kit.sln --no-restore
```

Expected: 全部通過

**Step 3: 檢視變更摘要**

```bash
git log --oneline main..HEAD
```

Expected 應包含：
1. `feat: 新增 PullRequestId 到 MergeRequest entity 與 GitLab/Bitbucket mappers`
2. `feat: 新增 PullRequestId 到 MergeRequestOutput 與 BaseFetchPullRequestsTask`
3. `feat: 新增 PR 來源欄位到 WorkItemOutput 並重構 FetchAzureDevOpsWorkItemsTask`
4. `feat: 新增 Relations 模型與 ParentWorkItemId 到 WorkItem entity`
5. `feat: 新增 UserStory DTOs、Redis key 與 get-user-story console 佈線`
6. `feat: 實作 GetUserStoryTask 基本場景`
7. `test: 新增 GetUserStoryTask 遞迴向上查詢與混合場景測試`
