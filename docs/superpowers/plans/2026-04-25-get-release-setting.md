# Get Release Setting 實作計畫

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 新增 CLI 指令 `get-release-setting`，自動從 Redis 讀取前置 release branch 資料，產生 GitLab + Bitbucket 專案設定 JSON 並寫入 Redis。

**Architecture:** 單一 `GetReleaseSettingTask` 處理全部邏輯。讀取 Redis Hash（GitLab/Bitbucket 的 ReleaseBranches field），依規則產生專案設定，合併為一份 JSON 寫入 Redis String key `ReleaseSetting`。遵循既有 Task 模式，使用 `IRedisService` 與 `INow`。

**Tech Stack:** .NET 10, xUnit, Moq, StackExchange.Redis, System.Text.Json

---

## Task 1: 新增 RedisKeys 常數

**Files:**
- Modify: `src/ReleaseKit.Common/Constants/RedisKeys.cs`

- [ ] **Step 1: 新增 ReleaseSetting 常數**

在 `RedisKeys` 類別中新增 `ReleaseSetting` 常數：

```csharp
/// <summary>
/// Release Setting 設定的 Redis Key
/// </summary>
public const string ReleaseSetting = "ReleaseSetting";
```

新增位置：在 `ReleaseDataHash` 常數之後。

- [ ] **Step 2: 驗證建置**

Run: `dotnet build src/ReleaseKit.Common/ReleaseKit.Common.csproj -v q`
Expected: Build succeeded, 0 Error(s)

- [ ] **Step 3: Commit**

```bash
git add src/ReleaseKit.Common/Constants/RedisKeys.cs
git commit -m "feat: 新增 ReleaseSetting Redis Key 常數"
```

---

## Task 2: 新增 DTO 類別

**Files:**
- Create: `src/ReleaseKit.Application/Tasks/ReleaseSettingOutput.cs`
- Create: `src/ReleaseKit.Application/Tasks/PlatformSettingOutput.cs`
- Create: `src/ReleaseKit.Application/Tasks/ProjectSettingOutput.cs`

- [ ] **Step 1: 建立 ProjectSettingOutput**

建立 `src/ReleaseKit.Application/Tasks/ProjectSettingOutput.cs`：

```csharp
using ReleaseKit.Common.Configuration;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 單一專案的 Release 設定輸出
/// </summary>
public record ProjectSettingOutput
{
    /// <summary>
    /// 專案路徑（如 "group/project" 或 "workspace/repo"）
    /// </summary>
    public string ProjectPath { get; init; } = string.Empty;

    /// <summary>
    /// 目標分支名稱
    /// </summary>
    public string TargetBranch { get; init; } = string.Empty;

    /// <summary>
    /// 拉取模式
    /// </summary>
    public FetchMode FetchMode { get; init; }

    /// <summary>
    /// 來源分支名稱（BranchDiff 模式時使用）
    /// </summary>
    public string? SourceBranch { get; init; }

    /// <summary>
    /// 開始時間（DateTimeRange 模式時使用）
    /// </summary>
    public DateTimeOffset? StartDateTime { get; init; }

    /// <summary>
    /// 結束時間（DateTimeRange 模式時使用）
    /// </summary>
    public DateTimeOffset? EndDateTime { get; init; }
}
```

- [ ] **Step 2: 建立 PlatformSettingOutput**

建立 `src/ReleaseKit.Application/Tasks/PlatformSettingOutput.cs`：

```csharp
namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 平台層級的 Release 設定輸出
/// </summary>
public record PlatformSettingOutput
{
    /// <summary>
    /// 專案設定清單
    /// </summary>
    public List<ProjectSettingOutput> Projects { get; init; } = new();
}
```

- [ ] **Step 3: 建立 ReleaseSettingOutput**

建立 `src/ReleaseKit.Application/Tasks/ReleaseSettingOutput.cs`：

```csharp
namespace ReleaseKit.Application.Tasks;

/// <summary>
/// Release 設定輸出（包含 GitLab 與 Bitbucket 兩個平台）
/// </summary>
public record ReleaseSettingOutput
{
    /// <summary>
    /// GitLab 平台設定
    /// </summary>
    public PlatformSettingOutput GitLab { get; init; } = new();

    /// <summary>
    /// Bitbucket 平台設定
    /// </summary>
    public PlatformSettingOutput Bitbucket { get; init; } = new();
}
```

- [ ] **Step 4: 驗證建置**

Run: `dotnet build src/ReleaseKit.Application/ReleaseKit.Application.csproj -v q`
Expected: Build succeeded, 0 Error(s)

- [ ] **Step 5: Commit**

```bash
git add src/ReleaseKit.Application/Tasks/ProjectSettingOutput.cs \
        src/ReleaseKit.Application/Tasks/PlatformSettingOutput.cs \
        src/ReleaseKit.Application/Tasks/ReleaseSettingOutput.cs
git commit -m "feat: 新增 Release Setting DTO 類別"
```

---

## Task 3: 新增 TaskType 列舉值

**Files:**
- Modify: `src/ReleaseKit.Application/Tasks/TaskType.cs`

- [ ] **Step 1: 新增 GetReleaseSetting 列舉值**

在 `TaskType` enum 的 `EnhanceTitles` 之後新增：

```csharp
/// <summary>
/// 產生 Release Setting 設定
/// </summary>
GetReleaseSetting
```

（別忘了在 `EnhanceTitles` 後面加逗號）

- [ ] **Step 2: 驗證建置**

Run: `dotnet build src/ReleaseKit.Application/ReleaseKit.Application.csproj -v q`
Expected: Build succeeded, 0 Error(s)

- [ ] **Step 3: Commit**

```bash
git add src/ReleaseKit.Application/Tasks/TaskType.cs
git commit -m "feat: 新增 GetReleaseSetting 任務類型"
```

---

## Task 4: 實作 GetReleaseSettingTask（TDD）

**Files:**
- Create: `tests/ReleaseKit.Application.Tests/Tasks/GetReleaseSettingTaskTests.cs`
- Create: `src/ReleaseKit.Application/Tasks/GetReleaseSettingTask.cs`

### 4-1: 測試 — 兩個平台都無前置資料時產生空設定

- [ ] **Step 1: 撰寫失敗測試**

建立 `tests/ReleaseKit.Application.Tests/Tasks/GetReleaseSettingTaskTests.cs`：

```csharp
using Microsoft.Extensions.Logging;
using Moq;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tests.Tasks;

/// <summary>
/// GetReleaseSettingTask 單元測試
/// </summary>
public class GetReleaseSettingTaskTests
{
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly Mock<INow> _nowMock;
    private readonly Mock<ILogger<GetReleaseSettingTask>> _loggerMock;

    public GetReleaseSettingTaskTests()
    {
        _redisServiceMock = new Mock<IRedisService>();
        _nowMock = new Mock<INow>();
        _loggerMock = new Mock<ILogger<GetReleaseSettingTask>>();

        // 預設時間：2025-04-25 UTC
        _nowMock.Setup(x => x.UtcNow).Returns(new DateTimeOffset(2025, 4, 25, 0, 0, 0, TimeSpan.Zero));

        // 預設 Redis 行為
        _redisServiceMock.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _redisServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>())).ReturnsAsync(true);
    }

    private GetReleaseSettingTask CreateTask()
    {
        return new GetReleaseSettingTask(
            _redisServiceMock.Object,
            _nowMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_當兩個平台都無前置資料_應產生空設定並寫入Redis()
    {
        // Arrange
        _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.GitLabHash, RedisKeys.Fields.ReleaseBranches))
            .ReturnsAsync((string?)null);
        _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.BitbucketHash, RedisKeys.Fields.ReleaseBranches))
            .ReturnsAsync((string?)null);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _redisServiceMock.Verify(
            x => x.SetAsync(
                RedisKeys.ReleaseSetting,
                It.Is<string>(json =>
                {
                    var result = json.ToTypedObject<ReleaseSettingOutput>();
                    return result != null
                        && result.GitLab.Projects.Count == 0
                        && result.Bitbucket.Projects.Count == 0;
                }),
                It.IsAny<TimeSpan?>()),
            Times.Once);
    }
}
```

- [ ] **Step 2: 執行測試確認失敗**

Run: `dotnet test tests/ReleaseKit.Application.Tests/ReleaseKit.Application.Tests.csproj --no-restore --filter "GetReleaseSettingTaskTests" -v q`
Expected: FAIL — `GetReleaseSettingTask` 類別不存在

- [ ] **Step 3: 建立最小實作**

建立 `src/ReleaseKit.Application/Tasks/GetReleaseSettingTask.cs`：

```csharp
using Microsoft.Extensions.Logging;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Helpers;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 產生 Release Setting 設定任務
/// </summary>
/// <remarks>
/// 從 Redis 讀取前置指令產生的 release branch 資訊，
/// 依規則產生 GitLab 與 Bitbucket 的專案設定，並寫入 Redis。
/// </remarks>
public class GetReleaseSettingTask : ITask
{
    private readonly IRedisService _redisService;
    private readonly INow _now;
    private readonly ILogger<GetReleaseSettingTask> _logger;

    /// <summary>
    /// 超過此月數的 release branch 視為過期，退回 DateTimeRange 模式
    /// </summary>
    private const int ExpiredMonths = 3;

    /// <summary>
    /// GitLab 平台預設的目標分支
    /// </summary>
    private const string GitLabDefaultTargetBranch = "master";

    /// <summary>
    /// Bitbucket 平台預設的目標分支
    /// </summary>
    private const string BitbucketDefaultTargetBranch = "develop";

    /// <summary>
    /// 前置資料中無 release branch 的專案分組鍵值
    /// </summary>
    private const string NotFoundKey = "NotFound";

    public GetReleaseSettingTask(
        IRedisService redisService,
        INow now,
        ILogger<GetReleaseSettingTask> logger)
    {
        _redisService = redisService ?? throw new ArgumentNullException(nameof(redisService));
        _now = now ?? throw new ArgumentNullException(nameof(now));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 執行產生 Release Setting 設定任務
    /// </summary>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始執行 Release Setting 產生任務");

        // 讀取 GitLab release branch 資料
        var gitLabBranchData = await ReadReleaseBranchDataAsync(
            RedisKeys.GitLabHash, RedisKeys.Fields.ReleaseBranches, "GitLab");

        // 讀取 Bitbucket release branch 資料
        var bitbucketBranchData = await ReadReleaseBranchDataAsync(
            RedisKeys.BitbucketHash, RedisKeys.Fields.ReleaseBranches, "Bitbucket");

        // 產生設定
        var output = new ReleaseSettingOutput
        {
            GitLab = GeneratePlatformSetting(gitLabBranchData, GitLabDefaultTargetBranch, "GitLab"),
            Bitbucket = GeneratePlatformSetting(bitbucketBranchData, BitbucketDefaultTargetBranch, "Bitbucket")
        };

        // 序列化並輸出
        var json = output.ToJson();
        Console.WriteLine(json);

        // 清除舊資料並寫入 Redis
        if (await _redisService.ExistsAsync(RedisKeys.ReleaseSetting))
        {
            await _redisService.DeleteAsync(RedisKeys.ReleaseSetting);
            _logger.LogInformation("已清除 Redis 中的舊 Release Setting 資料");
        }

        await _redisService.SetAsync(RedisKeys.ReleaseSetting, json);
        _logger.LogInformation("Release Setting 已寫入 Redis，Key: {Key}", RedisKeys.ReleaseSetting);

        _logger.LogInformation(
            "Release Setting 產生完成，GitLab 專案數: {GitLabCount}，Bitbucket 專案數: {BitbucketCount}",
            output.GitLab.Projects.Count,
            output.Bitbucket.Projects.Count);
    }

    /// <summary>
    /// 從 Redis 讀取 release branch 資料
    /// </summary>
    private async Task<Dictionary<string, List<string>>?> ReadReleaseBranchDataAsync(
        string hashKey, string field, string platformName)
    {
        var json = await _redisService.HashGetAsync(hashKey, field);
        if (string.IsNullOrWhiteSpace(json))
        {
            _logger.LogInformation("{Platform} 無前置 release branch 資料，將產生空設定", platformName);
            return null;
        }

        var data = json.ToTypedObject<Dictionary<string, List<string>>>();
        _logger.LogInformation("{Platform} 讀取到 {Count} 個 release branch 分組", platformName, data?.Count ?? 0);
        return data;
    }

    /// <summary>
    /// 依 release branch 資料產生平台設定
    /// </summary>
    private PlatformSettingOutput GeneratePlatformSetting(
        Dictionary<string, List<string>>? branchData,
        string defaultTargetBranch,
        string platformName)
    {
        if (branchData == null || branchData.Count == 0)
        {
            return new PlatformSettingOutput { Projects = new List<ProjectSettingOutput>() };
        }

        var projects = new List<ProjectSettingOutput>();
        var cutoffDate = _now.UtcNow.AddMonths(-ExpiredMonths);

        foreach (var (branchName, projectPaths) in branchData)
        {
            var (fetchMode, sourceBranch) = DetermineFetchMode(branchName, cutoffDate);

            foreach (var projectPath in projectPaths)
            {
                var project = new ProjectSettingOutput
                {
                    ProjectPath = projectPath,
                    TargetBranch = defaultTargetBranch,
                    FetchMode = fetchMode,
                    SourceBranch = sourceBranch,
                    StartDateTime = null,
                    EndDateTime = null
                };

                projects.Add(project);

                _logger.LogInformation(
                    "{Platform} 專案 {ProjectPath}: FetchMode={FetchMode}, SourceBranch={SourceBranch}, TargetBranch={TargetBranch}",
                    platformName,
                    projectPath,
                    fetchMode,
                    sourceBranch ?? "(null)",
                    defaultTargetBranch);
            }
        }

        return new PlatformSettingOutput { Projects = projects };
    }

    /// <summary>
    /// 依 release branch 名稱判斷 FetchMode
    /// </summary>
    private static (FetchMode fetchMode, string? sourceBranch) DetermineFetchMode(
        string branchName, DateTimeOffset cutoffDate)
    {
        // 規則 1：NotFound 專案
        if (branchName == NotFoundKey)
        {
            return (FetchMode.DateTimeRange, null);
        }

        // 規則 2：格式不符合 release/yyyyMMdd
        if (!ReleaseBranchHelper.IsReleaseBranch(branchName))
        {
            return (FetchMode.DateTimeRange, null);
        }

        // 規則 3：日期超過 3 個月
        var branchDate = ReleaseBranchHelper.ParseReleaseBranchDate(branchName);
        if (branchDate.HasValue && branchDate.Value < cutoffDate)
        {
            return (FetchMode.DateTimeRange, null);
        }

        // 規則 4：其餘使用 BranchDiff
        return (FetchMode.BranchDiff, branchName);
    }
}
```

- [ ] **Step 4: 執行測試確認通過**

Run: `dotnet test tests/ReleaseKit.Application.Tests/ReleaseKit.Application.Tests.csproj --no-restore --filter "GetReleaseSettingTaskTests" -v q`
Expected: Passed! 1 test passed

- [ ] **Step 5: Commit**

```bash
git add src/ReleaseKit.Application/Tasks/GetReleaseSettingTask.cs \
        tests/ReleaseKit.Application.Tests/Tasks/GetReleaseSettingTaskTests.cs
git commit -m "feat: 實作 GetReleaseSettingTask 基礎功能（空設定場景）"
```

### 4-2: 測試 — BranchDiff 設定產生

- [ ] **Step 6: 新增測試 — 有效 release branch 產生 BranchDiff 設定**

在 `GetReleaseSettingTaskTests.cs` 中新增：

```csharp
[Fact]
public async Task ExecuteAsync_當GitLab有有效ReleaseBranch_應產生BranchDiff設定()
{
    // Arrange
    var branchData = new Dictionary<string, List<string>>
    {
        { "release/20250401", new List<string> { "group/project-a", "group/project-b" } }
    };

    _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.GitLabHash, RedisKeys.Fields.ReleaseBranches))
        .ReturnsAsync(branchData.ToJson());
    _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.BitbucketHash, RedisKeys.Fields.ReleaseBranches))
        .ReturnsAsync((string?)null);

    var task = CreateTask();

    // Act
    await task.ExecuteAsync();

    // Assert
    _redisServiceMock.Verify(
        x => x.SetAsync(
            RedisKeys.ReleaseSetting,
            It.Is<string>(json =>
            {
                var result = json.ToTypedObject<ReleaseSettingOutput>();
                return result != null
                    && result.GitLab.Projects.Count == 2
                    && result.GitLab.Projects[0].ProjectPath == "group/project-a"
                    && result.GitLab.Projects[0].TargetBranch == "master"
                    && result.GitLab.Projects[0].FetchMode == FetchMode.BranchDiff
                    && result.GitLab.Projects[0].SourceBranch == "release/20250401"
                    && result.GitLab.Projects[1].ProjectPath == "group/project-b"
                    && result.GitLab.Projects[1].FetchMode == FetchMode.BranchDiff
                    && result.Bitbucket.Projects.Count == 0;
            }),
            It.IsAny<TimeSpan?>()),
        Times.Once);
}
```

- [ ] **Step 7: 執行測試確認通過**

Run: `dotnet test tests/ReleaseKit.Application.Tests/ReleaseKit.Application.Tests.csproj --no-restore --filter "GetReleaseSettingTaskTests" -v q`
Expected: Passed! 2 tests passed

### 4-3: 測試 — Bitbucket TargetBranch 預設 develop

- [ ] **Step 8: 新增測試 — Bitbucket 預設使用 develop**

```csharp
[Fact]
public async Task ExecuteAsync_當Bitbucket有有效ReleaseBranch_TargetBranch應為develop()
{
    // Arrange
    var branchData = new Dictionary<string, List<string>>
    {
        { "release/20250401", new List<string> { "workspace/repo-a" } }
    };

    _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.GitLabHash, RedisKeys.Fields.ReleaseBranches))
        .ReturnsAsync((string?)null);
    _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.BitbucketHash, RedisKeys.Fields.ReleaseBranches))
        .ReturnsAsync(branchData.ToJson());

    var task = CreateTask();

    // Act
    await task.ExecuteAsync();

    // Assert
    _redisServiceMock.Verify(
        x => x.SetAsync(
            RedisKeys.ReleaseSetting,
            It.Is<string>(json =>
            {
                var result = json.ToTypedObject<ReleaseSettingOutput>();
                return result != null
                    && result.Bitbucket.Projects.Count == 1
                    && result.Bitbucket.Projects[0].TargetBranch == "develop"
                    && result.Bitbucket.Projects[0].FetchMode == FetchMode.BranchDiff
                    && result.Bitbucket.Projects[0].SourceBranch == "release/20250401"
                    && result.GitLab.Projects.Count == 0;
            }),
            It.IsAny<TimeSpan?>()),
        Times.Once);
}
```

- [ ] **Step 9: 執行測試確認通過**

Run: `dotnet test tests/ReleaseKit.Application.Tests/ReleaseKit.Application.Tests.csproj --no-restore --filter "GetReleaseSettingTaskTests" -v q`
Expected: Passed! 3 tests passed

### 4-4: 測試 — NotFound 專案使用 DateTimeRange

- [ ] **Step 10: 新增測試 — NotFound 專案**

```csharp
[Fact]
public async Task ExecuteAsync_當有NotFound專案_應使用DateTimeRange模式()
{
    // Arrange
    var branchData = new Dictionary<string, List<string>>
    {
        { "release/20250401", new List<string> { "group/project-a" } },
        { "NotFound", new List<string> { "group/project-b" } }
    };

    _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.GitLabHash, RedisKeys.Fields.ReleaseBranches))
        .ReturnsAsync(branchData.ToJson());
    _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.BitbucketHash, RedisKeys.Fields.ReleaseBranches))
        .ReturnsAsync((string?)null);

    var task = CreateTask();

    // Act
    await task.ExecuteAsync();

    // Assert
    _redisServiceMock.Verify(
        x => x.SetAsync(
            RedisKeys.ReleaseSetting,
            It.Is<string>(json =>
            {
                var result = json.ToTypedObject<ReleaseSettingOutput>();
                var notFoundProject = result!.GitLab.Projects.First(p => p.ProjectPath == "group/project-b");
                return notFoundProject.FetchMode == FetchMode.DateTimeRange
                    && notFoundProject.SourceBranch == null
                    && notFoundProject.StartDateTime == null
                    && notFoundProject.EndDateTime == null;
            }),
            It.IsAny<TimeSpan?>()),
        Times.Once);
}
```

- [ ] **Step 11: 執行測試確認通過**

Run: `dotnet test tests/ReleaseKit.Application.Tests/ReleaseKit.Application.Tests.csproj --no-restore --filter "GetReleaseSettingTaskTests" -v q`
Expected: Passed! 4 tests passed

### 4-5: 測試 — 格式不符合 release/yyyyMMdd 退回 DateTimeRange

- [ ] **Step 12: 新增測試 — 格式不符合**

```csharp
[Fact]
public async Task ExecuteAsync_當ReleaseBranch格式不符合yyyyMMdd_應使用DateTimeRange模式()
{
    // Arrange
    var branchData = new Dictionary<string, List<string>>
    {
        { "release/hotfix-123", new List<string> { "group/project-a" } }
    };

    _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.GitLabHash, RedisKeys.Fields.ReleaseBranches))
        .ReturnsAsync(branchData.ToJson());
    _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.BitbucketHash, RedisKeys.Fields.ReleaseBranches))
        .ReturnsAsync((string?)null);

    var task = CreateTask();

    // Act
    await task.ExecuteAsync();

    // Assert
    _redisServiceMock.Verify(
        x => x.SetAsync(
            RedisKeys.ReleaseSetting,
            It.Is<string>(json =>
            {
                var result = json.ToTypedObject<ReleaseSettingOutput>();
                return result!.GitLab.Projects.Count == 1
                    && result.GitLab.Projects[0].FetchMode == FetchMode.DateTimeRange
                    && result.GitLab.Projects[0].SourceBranch == null;
            }),
            It.IsAny<TimeSpan?>()),
        Times.Once);
}
```

- [ ] **Step 13: 執行測試確認通過**

Run: `dotnet test tests/ReleaseKit.Application.Tests/ReleaseKit.Application.Tests.csproj --no-restore --filter "GetReleaseSettingTaskTests" -v q`
Expected: Passed! 5 tests passed

### 4-6: 測試 — Release branch 日期超過 3 個月

- [ ] **Step 14: 新增測試 — 日期超過 3 個月**

```csharp
[Fact]
public async Task ExecuteAsync_當ReleaseBranch日期超過三個月_應使用DateTimeRange模式()
{
    // Arrange — 目前時間 2025-04-25，release/20250101 距離超過 3 個月
    var branchData = new Dictionary<string, List<string>>
    {
        { "release/20250101", new List<string> { "group/project-a" } }
    };

    _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.GitLabHash, RedisKeys.Fields.ReleaseBranches))
        .ReturnsAsync(branchData.ToJson());
    _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.BitbucketHash, RedisKeys.Fields.ReleaseBranches))
        .ReturnsAsync((string?)null);

    var task = CreateTask();

    // Act
    await task.ExecuteAsync();

    // Assert
    _redisServiceMock.Verify(
        x => x.SetAsync(
            RedisKeys.ReleaseSetting,
            It.Is<string>(json =>
            {
                var result = json.ToTypedObject<ReleaseSettingOutput>();
                return result!.GitLab.Projects.Count == 1
                    && result.GitLab.Projects[0].FetchMode == FetchMode.DateTimeRange
                    && result.GitLab.Projects[0].SourceBranch == null;
            }),
            It.IsAny<TimeSpan?>()),
        Times.Once);
}
```

- [ ] **Step 15: 執行測試確認通過**

Run: `dotnet test tests/ReleaseKit.Application.Tests/ReleaseKit.Application.Tests.csproj --no-restore --filter "GetReleaseSettingTaskTests" -v q`
Expected: Passed! 6 tests passed

### 4-7: 測試 — 3 個月邊界行為

- [ ] **Step 16: 新增測試 — 剛好 3 個月不應退回 DateTimeRange**

```csharp
[Fact]
public async Task ExecuteAsync_當ReleaseBranch日期剛好三個月_應使用BranchDiff模式()
{
    // Arrange — 目前時間 2025-04-25，cutoff = 2025-01-25
    // release/20250125 剛好等於 cutoff，不應退回 DateTimeRange
    _nowMock.Setup(x => x.UtcNow).Returns(new DateTimeOffset(2025, 4, 25, 0, 0, 0, TimeSpan.Zero));

    var branchData = new Dictionary<string, List<string>>
    {
        { "release/20250125", new List<string> { "group/project-a" } }
    };

    _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.GitLabHash, RedisKeys.Fields.ReleaseBranches))
        .ReturnsAsync(branchData.ToJson());
    _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.BitbucketHash, RedisKeys.Fields.ReleaseBranches))
        .ReturnsAsync((string?)null);

    var task = CreateTask();

    // Act
    await task.ExecuteAsync();

    // Assert
    _redisServiceMock.Verify(
        x => x.SetAsync(
            RedisKeys.ReleaseSetting,
            It.Is<string>(json =>
            {
                var result = json.ToTypedObject<ReleaseSettingOutput>();
                return result!.GitLab.Projects.Count == 1
                    && result.GitLab.Projects[0].FetchMode == FetchMode.BranchDiff
                    && result.GitLab.Projects[0].SourceBranch == "release/20250125";
            }),
            It.IsAny<TimeSpan?>()),
        Times.Once);
}

[Fact]
public async Task ExecuteAsync_當ReleaseBranch日期超過三個月一天_應使用DateTimeRange模式()
{
    // Arrange — 目前時間 2025-04-25，cutoff = 2025-01-25
    // release/20250124 比 cutoff 早一天，應退回 DateTimeRange
    _nowMock.Setup(x => x.UtcNow).Returns(new DateTimeOffset(2025, 4, 25, 0, 0, 0, TimeSpan.Zero));

    var branchData = new Dictionary<string, List<string>>
    {
        { "release/20250124", new List<string> { "group/project-a" } }
    };

    _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.GitLabHash, RedisKeys.Fields.ReleaseBranches))
        .ReturnsAsync(branchData.ToJson());
    _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.BitbucketHash, RedisKeys.Fields.ReleaseBranches))
        .ReturnsAsync((string?)null);

    var task = CreateTask();

    // Act
    await task.ExecuteAsync();

    // Assert
    _redisServiceMock.Verify(
        x => x.SetAsync(
            RedisKeys.ReleaseSetting,
            It.Is<string>(json =>
            {
                var result = json.ToTypedObject<ReleaseSettingOutput>();
                return result!.GitLab.Projects.Count == 1
                    && result.GitLab.Projects[0].FetchMode == FetchMode.DateTimeRange
                    && result.GitLab.Projects[0].SourceBranch == null;
            }),
            It.IsAny<TimeSpan?>()),
        Times.Once);
}
```

- [ ] **Step 17: 執行測試確認通過**

Run: `dotnet test tests/ReleaseKit.Application.Tests/ReleaseKit.Application.Tests.csproj --no-restore --filter "GetReleaseSettingTaskTests" -v q`
Expected: Passed! 8 tests passed

### 4-8: 測試 — 多個 branch 分組的完整場景

- [ ] **Step 18: 新增測試 — GitLab + Bitbucket 完整場景**

```csharp
[Fact]
public async Task ExecuteAsync_當兩個平台都有資料_應正確產生完整設定()
{
    // Arrange
    var gitLabData = new Dictionary<string, List<string>>
    {
        { "release/20250401", new List<string> { "group/project-a" } },
        { "release/20250315", new List<string> { "group/project-b" } },
        { "NotFound", new List<string> { "group/project-c" } }
    };
    var bitbucketData = new Dictionary<string, List<string>>
    {
        { "release/20250401", new List<string> { "workspace/repo-a" } },
        { "NotFound", new List<string> { "workspace/repo-b" } }
    };

    _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.GitLabHash, RedisKeys.Fields.ReleaseBranches))
        .ReturnsAsync(gitLabData.ToJson());
    _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.BitbucketHash, RedisKeys.Fields.ReleaseBranches))
        .ReturnsAsync(bitbucketData.ToJson());

    var task = CreateTask();

    // Act
    await task.ExecuteAsync();

    // Assert
    _redisServiceMock.Verify(
        x => x.SetAsync(
            RedisKeys.ReleaseSetting,
            It.Is<string>(json =>
            {
                var result = json.ToTypedObject<ReleaseSettingOutput>();
                // GitLab: 3 projects
                var gl = result!.GitLab;
                var glA = gl.Projects.First(p => p.ProjectPath == "group/project-a");
                var glB = gl.Projects.First(p => p.ProjectPath == "group/project-b");
                var glC = gl.Projects.First(p => p.ProjectPath == "group/project-c");

                // Bitbucket: 2 projects
                var bb = result.Bitbucket;
                var bbA = bb.Projects.First(p => p.ProjectPath == "workspace/repo-a");
                var bbB = bb.Projects.First(p => p.ProjectPath == "workspace/repo-b");

                return gl.Projects.Count == 3
                    && glA.FetchMode == FetchMode.BranchDiff && glA.SourceBranch == "release/20250401" && glA.TargetBranch == "master"
                    && glB.FetchMode == FetchMode.BranchDiff && glB.SourceBranch == "release/20250315" && glB.TargetBranch == "master"
                    && glC.FetchMode == FetchMode.DateTimeRange && glC.SourceBranch == null && glC.TargetBranch == "master"
                    && bb.Projects.Count == 2
                    && bbA.FetchMode == FetchMode.BranchDiff && bbA.SourceBranch == "release/20250401" && bbA.TargetBranch == "develop"
                    && bbB.FetchMode == FetchMode.DateTimeRange && bbB.SourceBranch == null && bbB.TargetBranch == "develop";
            }),
            It.IsAny<TimeSpan?>()),
        Times.Once);
}
```

- [ ] **Step 19: 執行測試確認通過**

Run: `dotnet test tests/ReleaseKit.Application.Tests/ReleaseKit.Application.Tests.csproj --no-restore --filter "GetReleaseSettingTaskTests" -v q`
Expected: Passed! 9 tests passed

- [ ] **Step 20: Commit**

```bash
git add tests/ReleaseKit.Application.Tests/Tasks/GetReleaseSettingTaskTests.cs
git commit -m "test: 新增 GetReleaseSettingTask 完整測試案例"
```

---

## Task 5: 註冊 Task 至 DI 與 CLI

**Files:**
- Modify: `src/ReleaseKit.Application/Tasks/TaskFactory.cs`
- Modify: `src/ReleaseKit.Console/Parsers/CommandLineParser.cs`
- Modify: `src/ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs`

- [ ] **Step 1: 更新 TaskFactory**

在 `TaskFactory.CreateTask` 的 switch expression 中，在 `TaskType.EnhanceTitles` 行之後新增：

```csharp
TaskType.GetReleaseSetting => _serviceProvider.GetRequiredService<GetReleaseSettingTask>(),
```

- [ ] **Step 2: 更新 CommandLineParser**

在 `_taskMappings` dictionary 中新增：

```csharp
{ "get-release-setting", TaskType.GetReleaseSetting },
```

- [ ] **Step 3: 更新 ServiceCollectionExtensions**

在 `AddApplicationServices` 方法中，在 `services.AddTransient<EnhanceTitlesWithCopilotTask>();` 行之後新增：

```csharp
services.AddTransient<GetReleaseSettingTask>();
```

- [ ] **Step 4: 驗證建置**

Run: `dotnet build src/ReleaseKit.Application/ReleaseKit.Application.csproj -v q && dotnet build src/ReleaseKit.Console/ReleaseKit.Console.csproj -v q`
Expected: Build succeeded（Console 可能因私有 NuGet feed 問題失敗，Application 必須成功）

- [ ] **Step 5: Commit**

```bash
git add src/ReleaseKit.Application/Tasks/TaskFactory.cs \
        src/ReleaseKit.Console/Parsers/CommandLineParser.cs \
        src/ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs
git commit -m "feat: 註冊 GetReleaseSettingTask 至 DI 容器與 CLI 解析器"
```

---

## Task 6: 更新既有測試

**Files:**
- Modify: `tests/ReleaseKit.Application.Tests/Tasks/TaskFactoryTests.cs`
- Modify: `tests/ReleaseKit.Console.Tests/Parsers/CommandLineParserTests.cs`

- [ ] **Step 1: 新增 TaskFactory 測試**

在 `TaskFactoryTests.cs` 中：

1. 在建構子的 DI 註冊區塊中新增：

```csharp
services.AddSingleton(new Mock<ILogger<GetReleaseSettingTask>>().Object);
```

以及：

```csharp
// 註冊 INow mock
var mockNow = new Mock<INow>();
mockNow.Setup(x => x.UtcNow).Returns(DateTimeOffset.UtcNow);
services.AddSingleton(mockNow.Object);
```

以及在 Tasks 註冊區塊中新增：

```csharp
services.AddTransient<GetReleaseSettingTask>();
```

2. 新增測試方法：

```csharp
[Fact]
public void CreateTask_WithGetReleaseSetting_ShouldReturnCorrectTaskType()
{
    // Act
    var task = _factory.CreateTask(TaskType.GetReleaseSetting);

    // Assert
    Assert.NotNull(task);
    Assert.IsType<GetReleaseSettingTask>(task);
}
```

- [ ] **Step 2: 新增 CommandLineParser 測試**

在 `CommandLineParserTests.cs` 中新增：

```csharp
[Fact]
public void Parse_WithGetReleaseSetting_ShouldReturnCorrectTaskType()
{
    // Arrange
    var args = new[] { "get-release-setting" };

    // Act
    var result = _parser.Parse(args);

    // Assert
    Assert.True(result.IsSuccess);
    Assert.Equal(TaskType.GetReleaseSetting, result.TaskType.Value);
}
```

- [ ] **Step 3: 執行全部測試**

Run: `dotnet test tests/ReleaseKit.Application.Tests/ReleaseKit.Application.Tests.csproj --no-restore -v q`
Expected: All tests passed（之前 220 + 新增 10 = 至少 230）

- [ ] **Step 4: Commit**

```bash
git add tests/ReleaseKit.Application.Tests/Tasks/TaskFactoryTests.cs \
        tests/ReleaseKit.Console.Tests/Parsers/CommandLineParserTests.cs
git commit -m "test: 更新 TaskFactory 與 CommandLineParser 測試以涵蓋 GetReleaseSetting"
```

---

## Task 7: 最終驗證

- [ ] **Step 1: 執行完整建置**

Run: `dotnet build src/ReleaseKit.Application/ReleaseKit.Application.csproj -v q`
Expected: Build succeeded, 0 Error(s)

- [ ] **Step 2: 執行完整測試**

Run: `dotnet test tests/ReleaseKit.Application.Tests/ReleaseKit.Application.Tests.csproj --no-restore -v q`
Expected: All tests passed

- [ ] **Step 3: 執行 Domain 測試**

Run: `dotnet test tests/ReleaseKit.Domain.Tests/ReleaseKit.Domain.Tests.csproj --no-restore -v q`
Expected: All tests passed（確認未破壞 Domain 層）

- [ ] **Step 4: 確認 git 狀態乾淨**

Run: `git status`
Expected: working tree clean（或僅有非追蹤的 docs 檔案）
