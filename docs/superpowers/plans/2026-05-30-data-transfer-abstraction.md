# Data Transfer Abstraction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 將指令間的資料傳遞機制從 Redis 硬性依賴，抽象化為 `IDataTransferService`，並新增 FileSystem 後端，透過 `DataTransfer:Provider` 設定切換。

**Architecture:** 在 Domain 層定義 `IDataTransferService`（Group* 中性命名取代 Hash* Redis 術語），在 Infrastructure 層分別實作 `RedisDataTransferService` 與 `FileSystemDataTransferService`，Console 層的 DI 依設定值選擇注入哪個實作，Application 層所有 Task 改用新介面。

**Tech Stack:** .NET 10, C#, StackExchange.Redis, xUnit, Moq

---

## File Map

### 新增
- `src/ReleaseKit.Domain/Abstractions/IDataTransferService.cs`
- `src/ReleaseKit.Infrastructure/DataTransfer/Redis/RedisDataTransferService.cs`
- `src/ReleaseKit.Infrastructure/DataTransfer/FileSystem/FileSystemDataTransferService.cs`
- `tests/ReleaseKit.Infrastructure.Tests/DataTransfer/Redis/RedisDataTransferServiceTests.cs`
- `tests/ReleaseKit.Infrastructure.Tests/DataTransfer/FileSystem/FileSystemDataTransferServiceTests.cs`

### 修改
- `src/ReleaseKit.Common/Constants/RedisKeys.cs` → rename class/comments to `DataTransferKeys`
- `src/ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs`
- `src/ReleaseKit.Console/appsettings.json`
- `src/ReleaseKit.Application/Tasks/BaseFetchPullRequestsTask.cs`
- `src/ReleaseKit.Application/Tasks/BaseFetchReleaseBranchTask.cs`
- `src/ReleaseKit.Application/Tasks/BaseFilterPullRequestsByUserTask.cs`
- `src/ReleaseKit.Application/Tasks/FetchGitLabPullRequestsTask.cs`
- `src/ReleaseKit.Application/Tasks/FetchBitbucketPullRequestsTask.cs`
- `src/ReleaseKit.Application/Tasks/FetchGitLabReleaseBranchTask.cs`
- `src/ReleaseKit.Application/Tasks/FetchBitbucketReleaseBranchTask.cs`
- `src/ReleaseKit.Application/Tasks/FilterGitLabPullRequestsByUserTask.cs`
- `src/ReleaseKit.Application/Tasks/FilterBitbucketPullRequestsByUserTask.cs`
- `src/ReleaseKit.Application/Tasks/FetchAzureDevOpsWorkItemsTask.cs`
- `src/ReleaseKit.Application/Tasks/GetUserStoryTask.cs`
- `src/ReleaseKit.Application/Tasks/ConsolidateReleaseDataTask.cs`
- `src/ReleaseKit.Application/Tasks/EnhanceTitlesWithCopilotTask.cs`
- `src/ReleaseKit.Application/Tasks/GetReleaseSettingTask.cs`
- `src/ReleaseKit.Application/Tasks/UpdateGoogleSheetsTask.cs`
- 所有 `tests/ReleaseKit.Application.Tests/Tasks/*.cs` 中使用 `IRedisService` 的測試

### 刪除（Task 6）
- `src/ReleaseKit.Domain/Abstractions/IRedisService.cs`
- `src/ReleaseKit.Infrastructure/Redis/RedisService.cs`
- `tests/ReleaseKit.Infrastructure.Tests/Redis/RedisServiceTests.cs`

---

## Task 1: 定義 IDataTransferService 介面 + 重命名 DataTransferKeys

**Files:**
- Create: `src/ReleaseKit.Domain/Abstractions/IDataTransferService.cs`
- Modify: `src/ReleaseKit.Common/Constants/RedisKeys.cs`

- [ ] **Step 1: 建立 `IDataTransferService.cs`**

```csharp
// src/ReleaseKit.Domain/Abstractions/IDataTransferService.cs
namespace ReleaseKit.Domain.Abstractions;

/// <summary>
/// 指令間資料傳遞服務介面
/// </summary>
public interface IDataTransferService
{
    /// <summary>
    /// 設定指定 Key 的值
    /// </summary>
    /// <param name="key">鍵值</param>
    /// <param name="value">內容</param>
    /// <param name="expiry">過期時間（選用；FileSystem 實作忽略此參數）</param>
    Task<bool> SetAsync(string key, string value, TimeSpan? expiry = null);

    /// <summary>
    /// 取得指定 Key 的值
    /// </summary>
    /// <param name="key">鍵值</param>
    /// <returns>內容，若不存在則回傳 null</returns>
    Task<string?> GetAsync(string key);

    /// <summary>
    /// 刪除指定 Key
    /// </summary>
    /// <param name="key">鍵值</param>
    Task<bool> DeleteAsync(string key);

    /// <summary>
    /// 檢查指定 Key 是否存在
    /// </summary>
    /// <param name="key">鍵值</param>
    Task<bool> ExistsAsync(string key);

    /// <summary>
    /// 設定群組中指定欄位的值
    /// </summary>
    /// <param name="groupKey">群組鍵值</param>
    /// <param name="field">欄位名稱</param>
    /// <param name="value">欄位內容</param>
    Task<bool> GroupSetAsync(string groupKey, string field, string value);

    /// <summary>
    /// 取得群組中指定欄位的值
    /// </summary>
    /// <param name="groupKey">群組鍵值</param>
    /// <param name="field">欄位名稱</param>
    /// <returns>欄位內容，若不存在則回傳 null</returns>
    Task<string?> GroupGetAsync(string groupKey, string field);

    /// <summary>
    /// 刪除群組中指定欄位
    /// </summary>
    /// <param name="groupKey">群組鍵值</param>
    /// <param name="field">欄位名稱</param>
    Task<bool> GroupDeleteAsync(string groupKey, string field);

    /// <summary>
    /// 檢查群組中指定欄位是否存在
    /// </summary>
    /// <param name="groupKey">群組鍵值</param>
    /// <param name="field">欄位名稱</param>
    Task<bool> GroupExistsAsync(string groupKey, string field);

    /// <summary>
    /// 取得群組中所有欄位與值
    /// </summary>
    /// <param name="groupKey">群組鍵值</param>
    /// <returns>欄位名稱與內容的字典，若群組不存在則回傳空字典</returns>
    Task<IReadOnlyDictionary<string, string>> GroupGetAllAsync(string groupKey);
}
```

- [ ] **Step 2: 更新 `RedisKeys.cs` — 重新命名類別與所有 XML 註解中的 "Redis" 字樣**

將 `src/ReleaseKit.Common/Constants/RedisKeys.cs` 的全部內容替換為：

```csharp
namespace ReleaseKit.Common.Constants;

/// <summary>
/// 資料傳遞鍵值常數
/// </summary>
public static class DataTransferKeys
{
    /// <summary>
    /// GitLab 資料的群組鍵值
    /// </summary>
    public const string GitLabHash = "GitLab";

    /// <summary>
    /// Bitbucket 資料的群組鍵值
    /// </summary>
    public const string BitbucketHash = "Bitbucket";

    /// <summary>
    /// Azure DevOps 資料的群組鍵值
    /// </summary>
    public const string AzureDevOpsHash = "AzureDevOps";

    /// <summary>
    /// 整合後的 Release 資料的群組鍵值
    /// </summary>
    public const string ReleaseDataHash = "ReleaseData";

    /// <summary>
    /// Release Setting 設定的鍵值
    /// </summary>
    public const string ReleaseSetting = "ReleaseSetting";

    /// <summary>
    /// 群組欄位名稱常數
    /// </summary>
    public static class Fields
    {
        /// <summary>
        /// Pull Request 資料的欄位名稱
        /// </summary>
        public const string PullRequests = "PullRequests";

        /// <summary>
        /// 過濾後（依使用者）的 Pull Request 資料欄位名稱
        /// </summary>
        public const string PullRequestsByUser = "PullRequests:ByUser";

        /// <summary>
        /// Release Branch 資料的欄位名稱
        /// </summary>
        public const string ReleaseBranches = "ReleaseBranches";

        /// <summary>
        /// Work Items 資料的欄位名稱
        /// </summary>
        public const string WorkItems = "WorkItems";

        /// <summary>
        /// User Story 層級 Work Items 資料的欄位名稱
        /// </summary>
        public const string WorkItemsUserStories = "WorkItems:UserStories";

        /// <summary>
        /// 整合後的 Release 資料欄位名稱
        /// </summary>
        public const string Consolidated = "Consolidated";

        /// <summary>
        /// 增強標題後的 Release 資料欄位名稱
        /// </summary>
        public const string EnhancedTitles = "EnhancedTitles";
    }
}
```

> **注意：** 僅改 class 名稱，所有 const 值（"GitLab"、"Bitbucket" 等字串）**不變**，因為它們是實際存儲路徑/key 名，改了會破壞現有資料。

- [ ] **Step 3: 建置確認無錯誤**

```bash
cd src && dotnet build release-kit.sln
```

Expected: `Build succeeded.`（此時 `IRedisService` 與 `RedisKeys` 仍存在，不衝突）

- [ ] **Step 4: Commit**

```bash
git add src/ReleaseKit.Domain/Abstractions/IDataTransferService.cs \
        src/ReleaseKit.Common/Constants/RedisKeys.cs
git commit -m "feat: 新增 IDataTransferService 介面並將 RedisKeys 重命名為 DataTransferKeys

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 2: 實作 RedisDataTransferService（TDD）

**Files:**
- Create: `tests/ReleaseKit.Infrastructure.Tests/DataTransfer/Redis/RedisDataTransferServiceTests.cs`
- Create: `src/ReleaseKit.Infrastructure/DataTransfer/Redis/RedisDataTransferService.cs`

- [ ] **Step 1: 建立測試目錄並撰寫失敗測試**

```bash
mkdir -p tests/ReleaseKit.Infrastructure.Tests/DataTransfer/Redis
```

建立 `tests/ReleaseKit.Infrastructure.Tests/DataTransfer/Redis/RedisDataTransferServiceTests.cs`：

```csharp
using Microsoft.Extensions.Logging;
using Moq;
using ReleaseKit.Infrastructure.DataTransfer.Redis;
using StackExchange.Redis;

namespace ReleaseKit.Infrastructure.Tests.DataTransfer.Redis;

/// <summary>
/// RedisDataTransferService 單元測試
/// </summary>
public class RedisDataTransferServiceTests
{
    private readonly Mock<IConnectionMultiplexer> _mockConnectionMultiplexer;
    private readonly Mock<IDatabase> _mockDatabase;
    private readonly Mock<ILogger<RedisDataTransferService>> _mockLogger;
    private readonly RedisDataTransferService _service;

    public RedisDataTransferServiceTests()
    {
        _mockConnectionMultiplexer = new Mock<IConnectionMultiplexer>();
        _mockDatabase = new Mock<IDatabase>();
        _mockLogger = new Mock<ILogger<RedisDataTransferService>>();

        _mockConnectionMultiplexer
            .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_mockDatabase.Object);

        _service = new RedisDataTransferService(
            _mockConnectionMultiplexer.Object, _mockLogger.Object, "Test:");
    }

    [Fact]
    public async Task SetAsync_ShouldReturnTrue_WhenSetSucceeds()
    {
        _mockDatabase
            .Setup(x => x.StringSetAsync(
                It.Is<RedisKey>(k => k.ToString() == "Test:my-key"),
                It.Is<RedisValue>(v => v.ToString() == "hello"),
                null,
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var result = await _service.SetAsync("my-key", "hello");

        Assert.True(result);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnValue_WhenKeyExists()
    {
        _mockDatabase
            .Setup(x => x.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString() == "Test:my-key"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("hello"));

        var result = await _service.GetAsync("my-key");

        Assert.Equal("hello", result);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnNull_WhenKeyDoesNotExist()
    {
        _mockDatabase
            .Setup(x => x.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var result = await _service.GetAsync("missing");

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnTrue_WhenDeleted()
    {
        _mockDatabase
            .Setup(x => x.KeyDeleteAsync(
                It.Is<RedisKey>(k => k.ToString() == "Test:my-key"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var result = await _service.DeleteAsync("my-key");

        Assert.True(result);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_WhenKeyExists()
    {
        _mockDatabase
            .Setup(x => x.KeyExistsAsync(
                It.Is<RedisKey>(k => k.ToString() == "Test:my-key"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var result = await _service.ExistsAsync("my-key");

        Assert.True(result);
    }

    [Fact]
    public async Task GroupSetAsync_ShouldReturnTrue_WhenSetSucceeds()
    {
        _mockDatabase
            .Setup(x => x.HashSetAsync(
                It.Is<RedisKey>(k => k.ToString() == "Test:GroupA"),
                It.Is<RedisValue>(f => f.ToString() == "field1"),
                It.Is<RedisValue>(v => v.ToString() == "value1"),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var result = await _service.GroupSetAsync("GroupA", "field1", "value1");

        Assert.True(result);
    }

    [Fact]
    public async Task GroupGetAsync_ShouldReturnValue_WhenFieldExists()
    {
        _mockDatabase
            .Setup(x => x.HashGetAsync(
                It.Is<RedisKey>(k => k.ToString() == "Test:GroupA"),
                It.Is<RedisValue>(f => f.ToString() == "field1"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("value1"));

        var result = await _service.GroupGetAsync("GroupA", "field1");

        Assert.Equal("value1", result);
    }

    [Fact]
    public async Task GroupGetAsync_ShouldReturnNull_WhenFieldDoesNotExist()
    {
        _mockDatabase
            .Setup(x => x.HashGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var result = await _service.GroupGetAsync("GroupA", "missing");

        Assert.Null(result);
    }

    [Fact]
    public async Task GroupDeleteAsync_ShouldReturnTrue_WhenDeleted()
    {
        _mockDatabase
            .Setup(x => x.HashDeleteAsync(
                It.Is<RedisKey>(k => k.ToString() == "Test:GroupA"),
                It.Is<RedisValue>(f => f.ToString() == "field1"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var result = await _service.GroupDeleteAsync("GroupA", "field1");

        Assert.True(result);
    }

    [Fact]
    public async Task GroupExistsAsync_ShouldReturnTrue_WhenFieldExists()
    {
        _mockDatabase
            .Setup(x => x.HashExistsAsync(
                It.Is<RedisKey>(k => k.ToString() == "Test:GroupA"),
                It.Is<RedisValue>(f => f.ToString() == "field1"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var result = await _service.GroupExistsAsync("GroupA", "field1");

        Assert.True(result);
    }

    [Fact]
    public async Task GroupGetAllAsync_ShouldReturnDictionary_WhenGroupExists()
    {
        _mockDatabase
            .Setup(x => x.HashGetAllAsync(
                It.Is<RedisKey>(k => k.ToString() == "Test:GroupA"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(new HashEntry[] {
                new HashEntry("f1", "v1"),
                new HashEntry("f2", "v2")
            });

        var result = await _service.GroupGetAllAsync("GroupA");

        Assert.Equal(2, result.Count);
        Assert.Equal("v1", result["f1"]);
        Assert.Equal("v2", result["f2"]);
    }
}
```

- [ ] **Step 2: 執行測試 — 預期編譯失敗**

```bash
cd src && dotnet test ../tests/ReleaseKit.Infrastructure.Tests/ReleaseKit.Infrastructure.Tests.csproj \
  --filter "FullyQualifiedName~RedisDataTransferServiceTests" 2>&1 | head -20
```

Expected: 編譯錯誤，`RedisDataTransferService` 不存在。

- [ ] **Step 3: 建立 `RedisDataTransferService.cs`**

```bash
mkdir -p src/ReleaseKit.Infrastructure/DataTransfer/Redis
```

建立 `src/ReleaseKit.Infrastructure/DataTransfer/Redis/RedisDataTransferService.cs`：

```csharp
using Microsoft.Extensions.Logging;
using ReleaseKit.Domain.Abstractions;
using StackExchange.Redis;

namespace ReleaseKit.Infrastructure.DataTransfer.Redis;

/// <summary>
/// 以 Redis 實作的資料傳遞服務
/// </summary>
public class RedisDataTransferService : IDataTransferService
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly IDatabase _database;
    private readonly ILogger<RedisDataTransferService> _logger;
    private readonly string _instanceName;

    public RedisDataTransferService(
        IConnectionMultiplexer connectionMultiplexer,
        ILogger<RedisDataTransferService> logger,
        string instanceName = "")
    {
        _connectionMultiplexer = connectionMultiplexer ?? throw new ArgumentNullException(nameof(connectionMultiplexer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _instanceName = instanceName;
        _database = _connectionMultiplexer.GetDatabase();
    }

    /// <summary>
    /// 設定指定 Key 的值
    /// </summary>
    public async Task<bool> SetAsync(string key, string value, TimeSpan? expiry = null)
    {
        var fullKey = GetFullKey(key);
        var result = await _database.StringSetAsync(fullKey, value, expiry);
        _logger.LogInformation("DataTransfer SET: {Key}, Expiry: {Expiry}, Result: {Result}", fullKey, expiry, result);
        return result;
    }

    /// <summary>
    /// 取得指定 Key 的值
    /// </summary>
    public async Task<string?> GetAsync(string key)
    {
        var fullKey = GetFullKey(key);
        var value = await _database.StringGetAsync(fullKey);
        _logger.LogInformation("DataTransfer GET: {Key} = {Value}", fullKey, value.HasValue ? value.ToString() : "(null)");
        return value.HasValue ? value.ToString() : null;
    }

    /// <summary>
    /// 刪除指定 Key
    /// </summary>
    public async Task<bool> DeleteAsync(string key)
    {
        var fullKey = GetFullKey(key);
        var result = await _database.KeyDeleteAsync(fullKey);
        _logger.LogInformation("DataTransfer DELETE: {Key}, Result: {Result}", fullKey, result);
        return result;
    }

    /// <summary>
    /// 檢查指定 Key 是否存在
    /// </summary>
    public async Task<bool> ExistsAsync(string key)
    {
        var fullKey = GetFullKey(key);
        var result = await _database.KeyExistsAsync(fullKey);
        _logger.LogInformation("DataTransfer EXISTS: {Key} = {Result}", fullKey, result);
        return result;
    }

    /// <summary>
    /// 設定群組中指定欄位的值
    /// </summary>
    public async Task<bool> GroupSetAsync(string groupKey, string field, string value)
    {
        var fullKey = GetFullKey(groupKey);
        var result = await _database.HashSetAsync(fullKey, field, value);
        _logger.LogInformation("DataTransfer GROUP-SET: {Key} {Field} = {Value}, Result: {Result}", fullKey, field, value, result);
        return result;
    }

    /// <summary>
    /// 取得群組中指定欄位的值
    /// </summary>
    public async Task<string?> GroupGetAsync(string groupKey, string field)
    {
        var fullKey = GetFullKey(groupKey);
        var value = await _database.HashGetAsync(fullKey, field);
        _logger.LogInformation("DataTransfer GROUP-GET: {Key} {Field} = {Value}", fullKey, field, value.HasValue ? value.ToString() : "(null)");
        return value.HasValue ? value.ToString() : null;
    }

    /// <summary>
    /// 刪除群組中指定欄位
    /// </summary>
    public async Task<bool> GroupDeleteAsync(string groupKey, string field)
    {
        var fullKey = GetFullKey(groupKey);
        var result = await _database.HashDeleteAsync(fullKey, field);
        _logger.LogInformation("DataTransfer GROUP-DELETE: {Key} {Field}, Result: {Result}", fullKey, field, result);
        return result;
    }

    /// <summary>
    /// 檢查群組中指定欄位是否存在
    /// </summary>
    public async Task<bool> GroupExistsAsync(string groupKey, string field)
    {
        var fullKey = GetFullKey(groupKey);
        var result = await _database.HashExistsAsync(fullKey, field);
        _logger.LogInformation("DataTransfer GROUP-EXISTS: {Key} {Field} = {Result}", fullKey, field, result);
        return result;
    }

    /// <summary>
    /// 取得群組中所有欄位與值
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> GroupGetAllAsync(string groupKey)
    {
        var fullKey = GetFullKey(groupKey);
        var entries = await _database.HashGetAllAsync(fullKey);
        _logger.LogInformation("DataTransfer GROUP-GETALL: {Key}, Count: {Count}", fullKey, entries.Length);
        return entries.ToDictionary(e => e.Name.ToString(), e => e.Value.ToString());
    }

    /// <summary>
    /// 取得完整鍵值（加上 Instance Name 前綴）
    /// </summary>
    private string GetFullKey(string key) =>
        string.IsNullOrEmpty(_instanceName) ? key : $"{_instanceName}{key}";
}
```

- [ ] **Step 4: 執行測試 — 預期全數通過**

```bash
cd src && dotnet test ../tests/ReleaseKit.Infrastructure.Tests/ReleaseKit.Infrastructure.Tests.csproj \
  --filter "FullyQualifiedName~RedisDataTransferServiceTests" -v minimal
```

Expected: 全部 PASS（10 個測試）。

- [ ] **Step 5: Commit**

```bash
git add src/ReleaseKit.Infrastructure/DataTransfer/Redis/RedisDataTransferService.cs \
        tests/ReleaseKit.Infrastructure.Tests/DataTransfer/Redis/RedisDataTransferServiceTests.cs
git commit -m "feat: 新增 RedisDataTransferService 實作 IDataTransferService

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 3: 實作 FileSystemDataTransferService（TDD）

**Files:**
- Create: `tests/ReleaseKit.Infrastructure.Tests/DataTransfer/FileSystem/FileSystemDataTransferServiceTests.cs`
- Create: `src/ReleaseKit.Infrastructure/DataTransfer/FileSystem/FileSystemDataTransferService.cs`

- [ ] **Step 1: 建立測試目錄並撰寫失敗測試**

```bash
mkdir -p tests/ReleaseKit.Infrastructure.Tests/DataTransfer/FileSystem
```

建立 `tests/ReleaseKit.Infrastructure.Tests/DataTransfer/FileSystem/FileSystemDataTransferServiceTests.cs`：

```csharp
using Microsoft.Extensions.Logging;
using Moq;
using ReleaseKit.Infrastructure.DataTransfer.FileSystem;

namespace ReleaseKit.Infrastructure.Tests.DataTransfer.FileSystem;

/// <summary>
/// FileSystemDataTransferService 單元測試
/// </summary>
public class FileSystemDataTransferServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileSystemDataTransferService _service;

    public FileSystemDataTransferServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"dt-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        var logger = new Mock<ILogger<FileSystemDataTransferService>>().Object;
        _service = new FileSystemDataTransferService(_tempDir, logger);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    // ── Key-Value 操作 ──

    [Fact]
    public async Task SetAsync_AndGetAsync_ShouldRoundtrip()
    {
        await _service.SetAsync("my-key", "hello world");
        var result = await _service.GetAsync("my-key");
        Assert.Equal("hello world", result);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnNull_WhenKeyDoesNotExist()
    {
        var result = await _service.GetAsync("nonexistent");
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnTrue_WhenFileExists()
    {
        await _service.SetAsync("del-key", "data");
        var result = await _service.DeleteAsync("del-key");
        Assert.True(result);
        Assert.Null(await _service.GetAsync("del-key"));
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnFalse_WhenFileDoesNotExist()
    {
        var result = await _service.DeleteAsync("missing-key");
        Assert.False(result);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_WhenKeyExists()
    {
        await _service.SetAsync("exists-key", "data");
        var result = await _service.ExistsAsync("exists-key");
        Assert.True(result);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalse_WhenKeyDoesNotExist()
    {
        var result = await _service.ExistsAsync("no-such-key");
        Assert.False(result);
    }

    [Fact]
    public async Task SetAsync_ShouldReturnTrue()
    {
        var result = await _service.SetAsync("k", "v");
        Assert.True(result);
    }

    // ── Group 操作 ──

    [Fact]
    public async Task GroupSetAsync_AndGroupGetAsync_ShouldRoundtrip()
    {
        await _service.GroupSetAsync("GroupA", "field1", "value1");
        var result = await _service.GroupGetAsync("GroupA", "field1");
        Assert.Equal("value1", result);
    }

    [Fact]
    public async Task GroupGetAsync_ShouldReturnNull_WhenFieldDoesNotExist()
    {
        var result = await _service.GroupGetAsync("GroupA", "missing");
        Assert.Null(result);
    }

    [Fact]
    public async Task GroupGetAsync_ShouldReturnNull_WhenGroupDoesNotExist()
    {
        var result = await _service.GroupGetAsync("NoGroup", "field");
        Assert.Null(result);
    }

    [Fact]
    public async Task GroupDeleteAsync_ShouldReturnTrue_WhenFieldExists()
    {
        await _service.GroupSetAsync("GroupA", "f1", "v1");
        var result = await _service.GroupDeleteAsync("GroupA", "f1");
        Assert.True(result);
        Assert.Null(await _service.GroupGetAsync("GroupA", "f1"));
    }

    [Fact]
    public async Task GroupDeleteAsync_ShouldReturnFalse_WhenFieldDoesNotExist()
    {
        var result = await _service.GroupDeleteAsync("GroupA", "missing");
        Assert.False(result);
    }

    [Fact]
    public async Task GroupExistsAsync_ShouldReturnTrue_WhenFieldExists()
    {
        await _service.GroupSetAsync("GroupB", "f1", "v1");
        var result = await _service.GroupExistsAsync("GroupB", "f1");
        Assert.True(result);
    }

    [Fact]
    public async Task GroupExistsAsync_ShouldReturnFalse_WhenFieldDoesNotExist()
    {
        var result = await _service.GroupExistsAsync("GroupB", "missing");
        Assert.False(result);
    }

    [Fact]
    public async Task GroupGetAllAsync_ShouldReturnAllFields_WhenGroupExists()
    {
        await _service.GroupSetAsync("GroupC", "f1", "v1");
        await _service.GroupSetAsync("GroupC", "f2", "v2");

        var result = await _service.GroupGetAllAsync("GroupC");

        Assert.Equal(2, result.Count);
        Assert.Equal("v1", result["f1"]);
        Assert.Equal("v2", result["f2"]);
    }

    [Fact]
    public async Task GroupGetAllAsync_ShouldReturnEmptyDictionary_WhenGroupDoesNotExist()
    {
        var result = await _service.GroupGetAllAsync("NoSuchGroup");
        Assert.Empty(result);
    }

    [Fact]
    public async Task GroupSetAsync_ShouldCreateDirectory_WhenItDoesNotExist()
    {
        // 確認目錄不存在
        var groupDir = Path.Combine(_tempDir, "NewGroup");
        Assert.False(Directory.Exists(groupDir));

        await _service.GroupSetAsync("NewGroup", "f1", "v1");

        Assert.True(Directory.Exists(groupDir));
    }

    [Fact]
    public async Task SetAsync_ShouldIgnoreExpiry()
    {
        // FileSystem 實作忽略 expiry 參數，仍應成功寫入
        var result = await _service.SetAsync("ttl-key", "data", TimeSpan.FromSeconds(1));
        Assert.True(result);
        Assert.Equal("data", await _service.GetAsync("ttl-key"));
    }
}
```

- [ ] **Step 2: 執行測試 — 預期編譯失敗**

```bash
cd src && dotnet test ../tests/ReleaseKit.Infrastructure.Tests/ReleaseKit.Infrastructure.Tests.csproj \
  --filter "FullyQualifiedName~FileSystemDataTransferServiceTests" 2>&1 | head -20
```

Expected: 編譯錯誤，`FileSystemDataTransferService` 不存在。

- [ ] **Step 3: 建立 `FileSystemDataTransferService.cs`**

```bash
mkdir -p src/ReleaseKit.Infrastructure/DataTransfer/FileSystem
```

建立 `src/ReleaseKit.Infrastructure/DataTransfer/FileSystem/FileSystemDataTransferService.cs`：

```csharp
using Microsoft.Extensions.Logging;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Infrastructure.DataTransfer.FileSystem;

/// <summary>
/// 以本地檔案系統實作的資料傳遞服務
/// </summary>
/// <remarks>
/// Key-Value 操作對應至 {fileDirectory}/{key} 檔案；
/// Group 操作對應至 {fileDirectory}/{groupKey}/{field} 子目錄結構。
/// expiry 參數在此實作中忽略，因 CLI 工具不需要 TTL。
/// </remarks>
public class FileSystemDataTransferService : IDataTransferService
{
    private readonly string _fileDirectory;
    private readonly ILogger<FileSystemDataTransferService> _logger;

    public FileSystemDataTransferService(
        string fileDirectory,
        ILogger<FileSystemDataTransferService> logger)
    {
        _fileDirectory = fileDirectory ?? throw new ArgumentNullException(nameof(fileDirectory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 設定指定 Key 的值（寫入 {fileDirectory}/{key}）
    /// </summary>
    public Task<bool> SetAsync(string key, string value, TimeSpan? expiry = null)
    {
        var path = GetKeyPath(key);
        EnsureDirectoryExists(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, value);
        _logger.LogInformation("DataTransfer SET: {Path}", path);
        return Task.FromResult(true);
    }

    /// <summary>
    /// 取得指定 Key 的值（讀取 {fileDirectory}/{key}）
    /// </summary>
    public Task<string?> GetAsync(string key)
    {
        var path = GetKeyPath(key);
        if (!File.Exists(path))
        {
            _logger.LogInformation("DataTransfer GET: {Path} = (null)", path);
            return Task.FromResult<string?>(null);
        }

        var value = File.ReadAllText(path);
        _logger.LogInformation("DataTransfer GET: {Path}", path);
        return Task.FromResult<string?>(value);
    }

    /// <summary>
    /// 刪除指定 Key（刪除 {fileDirectory}/{key}）
    /// </summary>
    public Task<bool> DeleteAsync(string key)
    {
        var path = GetKeyPath(key);
        if (!File.Exists(path))
        {
            _logger.LogInformation("DataTransfer DELETE: {Path} (not found)", path);
            return Task.FromResult(false);
        }

        File.Delete(path);
        _logger.LogInformation("DataTransfer DELETE: {Path}", path);
        return Task.FromResult(true);
    }

    /// <summary>
    /// 檢查指定 Key 是否存在（檢查 {fileDirectory}/{key}）
    /// </summary>
    public Task<bool> ExistsAsync(string key)
    {
        var path = GetKeyPath(key);
        var exists = File.Exists(path);
        _logger.LogInformation("DataTransfer EXISTS: {Path} = {Exists}", path, exists);
        return Task.FromResult(exists);
    }

    /// <summary>
    /// 設定群組欄位（寫入 {fileDirectory}/{groupKey}/{field}）
    /// </summary>
    public Task<bool> GroupSetAsync(string groupKey, string field, string value)
    {
        var path = GetGroupFieldPath(groupKey, field);
        EnsureDirectoryExists(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, value);
        _logger.LogInformation("DataTransfer GROUP-SET: {GroupKey}/{Field}", groupKey, field);
        return Task.FromResult(true);
    }

    /// <summary>
    /// 取得群組欄位（讀取 {fileDirectory}/{groupKey}/{field}）
    /// </summary>
    public Task<string?> GroupGetAsync(string groupKey, string field)
    {
        var path = GetGroupFieldPath(groupKey, field);
        if (!File.Exists(path))
        {
            _logger.LogInformation("DataTransfer GROUP-GET: {GroupKey}/{Field} = (null)", groupKey, field);
            return Task.FromResult<string?>(null);
        }

        var value = File.ReadAllText(path);
        _logger.LogInformation("DataTransfer GROUP-GET: {GroupKey}/{Field}", groupKey, field);
        return Task.FromResult<string?>(value);
    }

    /// <summary>
    /// 刪除群組欄位（刪除 {fileDirectory}/{groupKey}/{field}）
    /// </summary>
    public Task<bool> GroupDeleteAsync(string groupKey, string field)
    {
        var path = GetGroupFieldPath(groupKey, field);
        if (!File.Exists(path))
        {
            _logger.LogInformation("DataTransfer GROUP-DELETE: {GroupKey}/{Field} (not found)", groupKey, field);
            return Task.FromResult(false);
        }

        File.Delete(path);
        _logger.LogInformation("DataTransfer GROUP-DELETE: {GroupKey}/{Field}", groupKey, field);
        return Task.FromResult(true);
    }

    /// <summary>
    /// 檢查群組欄位是否存在（檢查 {fileDirectory}/{groupKey}/{field}）
    /// </summary>
    public Task<bool> GroupExistsAsync(string groupKey, string field)
    {
        var path = GetGroupFieldPath(groupKey, field);
        var exists = File.Exists(path);
        _logger.LogInformation("DataTransfer GROUP-EXISTS: {GroupKey}/{Field} = {Exists}", groupKey, field, exists);
        return Task.FromResult(exists);
    }

    /// <summary>
    /// 取得群組所有欄位（列舉 {fileDirectory}/{groupKey}/ 目錄內所有檔案）
    /// </summary>
    public Task<IReadOnlyDictionary<string, string>> GroupGetAllAsync(string groupKey)
    {
        var groupDir = Path.Combine(_fileDirectory, groupKey);
        if (!Directory.Exists(groupDir))
        {
            _logger.LogInformation("DataTransfer GROUP-GETALL: {GroupKey} (not found)", groupKey);
            return Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string>());
        }

        var result = Directory
            .GetFiles(groupDir)
            .ToDictionary(
                p => Path.GetFileName(p)!,
                p => File.ReadAllText(p));

        _logger.LogInformation("DataTransfer GROUP-GETALL: {GroupKey}, Count: {Count}", groupKey, result.Count);
        return Task.FromResult<IReadOnlyDictionary<string, string>>(result);
    }

    private string GetKeyPath(string key) => Path.Combine(_fileDirectory, key);

    private string GetGroupFieldPath(string groupKey, string field) =>
        Path.Combine(_fileDirectory, groupKey, field);

    private static void EnsureDirectoryExists(string directory)
    {
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);
    }
}
```

- [ ] **Step 4: 執行測試 — 預期全數通過**

```bash
cd src && dotnet test ../tests/ReleaseKit.Infrastructure.Tests/ReleaseKit.Infrastructure.Tests.csproj \
  --filter "FullyQualifiedName~FileSystemDataTransferServiceTests" -v minimal
```

Expected: 全部 PASS（18 個測試）。

- [ ] **Step 5: Commit**

```bash
git add src/ReleaseKit.Infrastructure/DataTransfer/FileSystem/FileSystemDataTransferService.cs \
        tests/ReleaseKit.Infrastructure.Tests/DataTransfer/FileSystem/FileSystemDataTransferServiceTests.cs
git commit -m "feat: 新增 FileSystemDataTransferService 實作 IDataTransferService

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 4: 更新 DI 注冊 + appsettings.json

**Files:**
- Modify: `src/ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs`
- Modify: `src/ReleaseKit.Console/appsettings.json`

- [ ] **Step 1: 更新 `ServiceCollectionExtensions.cs`**

將 `AddRedisServices` 方法替換（同時新增私有輔助方法），並更新 using 指令：

在 `ServiceCollectionExtensions.cs` 最頂端的 using 區塊加入：
```csharp
using ReleaseKit.Infrastructure.DataTransfer.FileSystem;
using ReleaseKit.Infrastructure.DataTransfer.Redis;
```

移除：
```csharp
using ReleaseKit.Infrastructure.Redis;
```

將整個 `AddRedisServices` 方法（第 28-54 行）替換為：

```csharp
/// <summary>
/// 依設定值 DataTransfer:Provider 選擇並注冊資料傳遞服務
/// </summary>
public static IServiceCollection AddDataTransferServices(
    this IServiceCollection services, IConfiguration configuration)
{
    var provider = configuration["DataTransfer:Provider"]
        ?? throw new InvalidOperationException("DataTransfer:Provider 組態設定不得為空");

    return provider switch
    {
        "Redis"      => services.AddRedisDataTransfer(configuration),
        "FileSystem" => services.AddFileSystemDataTransfer(configuration),
        _            => throw new InvalidOperationException($"不支援的資料傳遞方式: {provider}")
    };
}

private static IServiceCollection AddRedisDataTransfer(
    this IServiceCollection services, IConfiguration configuration)
{
    var redisConnectionString = configuration["Redis:ConnectionString"]
        ?? throw new InvalidOperationException("Redis:ConnectionString 組態設定不得為空");
    var redisInstanceName = configuration["Redis:InstanceName"]
        ?? throw new InvalidOperationException("Redis:InstanceName 組態設定不得為空");

    services.AddSingleton<IConnectionMultiplexer>(sp =>
    {
        var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<IConnectionMultiplexer>>();
        var configOptions = ConfigurationOptions.Parse(redisConnectionString);
        configOptions.AbortOnConnectFail = false;
        return ConnectionMultiplexerExtensions.ConnectWithRetry(configOptions, logger);
    });

    services.AddSingleton<IDataTransferService>(sp =>
    {
        var connectionMultiplexer = sp.GetRequiredService<IConnectionMultiplexer>();
        var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RedisDataTransferService>>();
        return new RedisDataTransferService(connectionMultiplexer, logger, redisInstanceName);
    });

    return services;
}

private static IServiceCollection AddFileSystemDataTransfer(
    this IServiceCollection services, IConfiguration configuration)
{
    var fileDirectory = configuration["DataTransfer:FileDirectory"]
        ?? throw new InvalidOperationException("DataTransfer:FileDirectory 組態設定不得為空");

    services.AddSingleton<IDataTransferService>(sp =>
    {
        var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<FileSystemDataTransferService>>();
        return new FileSystemDataTransferService(fileDirectory, logger);
    });

    return services;
}
```

- [ ] **Step 2: 更新 `Program.cs` — 呼叫 `AddDataTransferServices`**

在 `Program.cs` 中，將：
```csharp
// 註冊 Redis 服務
services.AddRedisServices(context.Configuration);
```
改為：
```csharp
// 依設定選擇資料傳遞服務（Redis 或 FileSystem）
services.AddDataTransferServices(context.Configuration);
```

- [ ] **Step 3: 更新 `appsettings.json` — 新增 DataTransfer 區段**

在 `Redis` 區段**之前**插入：
```json
"DataTransfer": {
  "Provider": "FileSystem",
  "FileDirectory": "/tmp/release-kit"
},
```

（預設值設為 FileSystem，方便本機測試不需啟動 Redis）

- [ ] **Step 4: 建置確認**

```bash
cd src && dotnet build release-kit.sln
```

Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add src/ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs \
        src/ReleaseKit.Console/Program.cs \
        src/ReleaseKit.Console/appsettings.json
git commit -m "feat: 更新 DI 注冊支援 Redis/FileSystem 資料傳遞後端

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 5: 遷移 Application Layer Tasks + 同步更新測試

> 此 Task 同步更新所有 Task 類別與測試，最後一次 commit。中途因為 Task 改好但測試尚未更新，建置會暫時失敗，這是正常的 Red 狀態。

**Files:**
- Modify: 11 個 Task 類別
- Modify: 12 個測試檔案

### 5A: 更新 Base Task 類別

- [ ] **Step 1: 更新 `BaseFetchPullRequestsTask.cs`**

以下列出所有需要替換的差異點（僅改這些，其餘保持不變）：

1. `using ReleaseKit.Domain.Abstractions;` 維持不變（新介面同在此 namespace）
2. `private readonly IRedisService _redisService;` → `private readonly IDataTransferService _dataTransferService;`
3. 建構子 XML 註解 `/// <param name="redisService">Redis 快取服務</param>` → `/// <param name="dataTransferService">資料傳遞服務</param>`
4. 建構子參數 `IRedisService redisService,` → `IDataTransferService dataTransferService,`
5. `_redisService = redisService;` → `_dataTransferService = dataTransferService;`
6. 抽象屬性 XML 文件：`/// <summary>取得 Redis Hash 鍵值</summary>` → `/// <summary>取得資料傳遞群組鍵值</summary>`
7. `protected abstract string RedisHashKey { get; }` → `protected abstract string DataTransferGroupKey { get; }`
8. `/// <summary>取得 Redis Hash 欄位名稱</summary>` → `/// <summary>取得資料傳遞群組欄位名稱</summary>`
9. `protected abstract string RedisHashField { get; }` → `protected abstract string DataTransferGroupField { get; }`
10. `if (await _redisService.HashExistsAsync(RedisHashKey, RedisHashField))` → `if (await _dataTransferService.GroupExistsAsync(DataTransferGroupKey, DataTransferGroupField))`
11. 日誌 `"清除 Redis 中的舊資料，Hash: {RedisHashKey} Field: {RedisHashField}"` → `"清除資料傳遞存放區中的舊資料，Group: {DataTransferGroupKey} Field: {DataTransferGroupField}"`
12. `await _redisService.HashDeleteAsync(RedisHashKey, RedisHashField);` → `await _dataTransferService.GroupDeleteAsync(DataTransferGroupKey, DataTransferGroupField);`
13. `var saveResult = await _redisService.HashSetAsync(RedisHashKey, RedisHashField, json);` → `var saveResult = await _dataTransferService.GroupSetAsync(DataTransferGroupKey, DataTransferGroupField, json);`
14. 日誌 `"成功將資料存入 Redis，Hash: {RedisHashKey} Field: {RedisHashField}"` → `"成功儲存資料，Group: {DataTransferGroupKey} Field: {DataTransferGroupField}"`
15. 日誌 `"將資料存入 Redis 失敗，Hash: {RedisHashKey} Field: {RedisHashField}"` → `"儲存資料失敗，Group: {DataTransferGroupKey} Field: {DataTransferGroupField}"`

- [ ] **Step 2: 更新 `BaseFetchReleaseBranchTask.cs`**

1. `private readonly IRedisService _redisService;` → `private readonly IDataTransferService _dataTransferService;`
2. 建構子 XML 註解 `/// <param name="redisService">Redis 快取服務</param>` → `/// <param name="dataTransferService">資料傳遞服務</param>`
3. 建構子參數 `IRedisService redisService,` → `IDataTransferService dataTransferService,`
4. `_redisService = redisService;` → `_dataTransferService = dataTransferService;`
5. `/// <summary>取得 Redis Hash 鍵值</summary>` → `/// <summary>取得資料傳遞群組鍵值</summary>`
6. `protected abstract string RedisHashKey { get; }` → `protected abstract string DataTransferGroupKey { get; }`
7. `/// <summary>取得 Redis Hash 欄位名稱</summary>` → `/// <summary>取得資料傳遞群組欄位名稱</summary>`
8. `protected abstract string RedisHashField { get; }` → `protected abstract string DataTransferGroupField { get; }`
9. `if (await _redisService.HashExistsAsync(RedisHashKey, RedisHashField))` → `if (await _dataTransferService.GroupExistsAsync(DataTransferGroupKey, DataTransferGroupField))`
10. 日誌 `"清除 Redis 中的舊資料，Hash: {RedisHashKey} Field: {RedisHashField}"` → `"清除資料傳遞存放區中的舊資料，Group: {DataTransferGroupKey} Field: {DataTransferGroupField}"`
11. `await _redisService.HashDeleteAsync(RedisHashKey, RedisHashField);` → `await _dataTransferService.GroupDeleteAsync(DataTransferGroupKey, DataTransferGroupField);`
12. `await _redisService.HashSetAsync(RedisHashKey, RedisHashField, json);` → `await _dataTransferService.GroupSetAsync(DataTransferGroupKey, DataTransferGroupField, json);`

- [ ] **Step 3: 更新 `BaseFilterPullRequestsByUserTask.cs`**

1. `protected readonly IRedisService RedisService;` → `protected readonly IDataTransferService DataTransferService;`
2. XML 文件 `/// <summary>Redis 服務</summary>` → `/// <summary>資料傳遞服務</summary>`
3. 建構子 XML 文件 `/// <param name="redisService">Redis 服務</param>` → `/// <param name="dataTransferService">資料傳遞服務</param>`
4. 建構子參數 `IRedisService redisService,` → `IDataTransferService dataTransferService,`
5. `RedisService = redisService;` → `DataTransferService = dataTransferService;`
6. 抽象屬性 `/// <summary>來源 Redis Hash 鍵值（讀取未過濾的 PR 資料）</summary>` → `/// <summary>來源群組鍵值（讀取未過濾的 PR 資料）</summary>`
7. `protected abstract string SourceRedisHashKey { get; }` → `protected abstract string SourceGroupKey { get; }`
8. `/// <summary>來源 Redis Hash 欄位名稱</summary>` → `/// <summary>來源群組欄位名稱</summary>`
9. `protected abstract string SourceRedisHashField { get; }` → `protected abstract string SourceGroupField { get; }`
10. `/// <summary>目標 Redis Hash 鍵值（寫入過濾後的 PR 資料）</summary>` → `/// <summary>目標群組鍵值（寫入過濾後的 PR 資料）</summary>`
11. `protected abstract string TargetRedisHashKey { get; }` → `protected abstract string TargetGroupKey { get; }`
12. `/// <summary>目標 Redis Hash 欄位名稱</summary>` → `/// <summary>目標群組欄位名稱</summary>`
13. `protected abstract string TargetRedisHashField { get; }` → `protected abstract string TargetGroupField { get; }`
14. `ExecuteAsync` 中所有 `RedisService.Hash*` → `DataTransferService.Group*`，且屬性名對應替換：
    - `await RedisService.HashExistsAsync(TargetRedisHashKey, TargetRedisHashField)` → `await DataTransferService.GroupExistsAsync(TargetGroupKey, TargetGroupField)`
    - `await RedisService.HashDeleteAsync(TargetRedisHashKey, TargetRedisHashField)` → `await DataTransferService.GroupDeleteAsync(TargetGroupKey, TargetGroupField)`
    - 日誌 `"清除 Redis 中的舊資料，Hash: {HashKey} Field: {Field}"` → `"清除資料傳遞存放區中的舊資料，Group: {GroupKey} Field: {Field}"`（參數對應改為 `TargetGroupKey, TargetGroupField`）
    - `await RedisService.HashGetAsync(SourceRedisHashKey, SourceRedisHashField)` → `await DataTransferService.GroupGetAsync(SourceGroupKey, SourceGroupField)`
    - 錯誤訊息 `$"Redis Hash {SourceRedisHashKey} Field {SourceRedisHashField} 中無 PR 資料"` → `$"資料傳遞存放區 Group {SourceGroupKey} Field {SourceGroupField} 中無 PR 資料"`
    - `await RedisService.HashSetAsync(TargetRedisHashKey, TargetRedisHashField, targetJson)` → `await DataTransferService.GroupSetAsync(TargetGroupKey, TargetGroupField, targetJson)`
    - 日誌 `"過濾完成，結果已寫入 Redis Hash {HashKey} Field {Field}"` → `"過濾完成，結果已寫入 Group {GroupKey} Field {Field}"`（參數對應改為 `TargetGroupKey, TargetGroupField`）

### 5B: 更新 Leaf Task 類別

- [ ] **Step 4: 更新 `FetchGitLabPullRequestsTask.cs`**

1. 建構子 XML 文件 `/// <param name="redisService">Redis 快取服務</param>` → `/// <param name="dataTransferService">資料傳遞服務</param>`
2. 建構子參數 `IRedisService redisService,` → `IDataTransferService dataTransferService,`
3. base() 呼叫中 `redisService,` → `dataTransferService,`
4. `protected override string RedisHashKey => DataTransferKeys.GitLabHash;` （注意：原 `RedisKeys.GitLabHash` → `DataTransferKeys.GitLabHash`）
5. `protected override string RedisHashField => DataTransferKeys.Fields.PullRequests;`
6. using 指令：`using ReleaseKit.Common.Constants;` 維持，但確保引用的是 `DataTransferKeys` 而非 `RedisKeys`

完整更新後的 `FetchGitLabPullRequestsTask.cs`：

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 拉取 GitLab Pull Request 資訊任務
/// </summary>
public class FetchGitLabPullRequestsTask : BaseFetchPullRequestsTask<GitLabOptions, GitLabProjectOptions>
{
    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="serviceProvider">服務提供者</param>
    /// <param name="logger">日誌記錄器</param>
    /// <param name="dataTransferService">資料傳遞服務</param>
    /// <param name="gitLabOptions">GitLab 配置選項</param>
    /// <param name="fetchModeOptions">拉取模式配置選項</param>
    public FetchGitLabPullRequestsTask(
        IServiceProvider serviceProvider,
        ILogger<FetchGitLabPullRequestsTask> logger,
        IDataTransferService dataTransferService,
        IOptions<GitLabOptions> gitLabOptions,
        IOptions<FetchModeOptions> fetchModeOptions)
        : base(
            serviceProvider.GetRequiredKeyedService<ISourceControlRepository>("GitLab"),
            logger,
            dataTransferService,
            gitLabOptions.Value,
            fetchModeOptions)
    {
    }

    /// <inheritdoc />
    protected override string PlatformName => "GitLab";

    /// <inheritdoc />
    protected override SourceControlPlatform Platform => SourceControlPlatform.GitLab;

    /// <inheritdoc />
    protected override string DataTransferGroupKey => DataTransferKeys.GitLabHash;

    /// <inheritdoc />
    protected override string DataTransferGroupField => DataTransferKeys.Fields.PullRequests;

    /// <inheritdoc />
    protected override IEnumerable<GitLabProjectOptions> GetProjects() => PlatformOptions.Projects;
}
```

- [ ] **Step 5: 更新 `FetchBitbucketPullRequestsTask.cs`**

與 Step 4 相同模式，完整更新後：

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 拉取 Bitbucket Pull Request 資訊任務
/// </summary>
public class FetchBitbucketPullRequestsTask : BaseFetchPullRequestsTask<BitbucketOptions, BitbucketProjectOptions>
{
    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="serviceProvider">服務提供者</param>
    /// <param name="logger">日誌記錄器</param>
    /// <param name="dataTransferService">資料傳遞服務</param>
    /// <param name="bitbucketOptions">Bitbucket 配置選項</param>
    /// <param name="fetchModeOptions">拉取模式配置選項</param>
    public FetchBitbucketPullRequestsTask(
        IServiceProvider serviceProvider,
        ILogger<FetchBitbucketPullRequestsTask> logger,
        IDataTransferService dataTransferService,
        IOptions<BitbucketOptions> bitbucketOptions,
        IOptions<FetchModeOptions> fetchModeOptions)
        : base(
            serviceProvider.GetRequiredKeyedService<ISourceControlRepository>("Bitbucket"),
            logger,
            dataTransferService,
            bitbucketOptions.Value,
            fetchModeOptions)
    {
    }

    /// <inheritdoc />
    protected override string PlatformName => "Bitbucket";

    /// <inheritdoc />
    protected override SourceControlPlatform Platform => SourceControlPlatform.Bitbucket;

    /// <inheritdoc />
    protected override string DataTransferGroupKey => DataTransferKeys.BitbucketHash;

    /// <inheritdoc />
    protected override string DataTransferGroupField => DataTransferKeys.Fields.PullRequests;

    /// <inheritdoc />
    protected override int MaxConcurrentProjects => 5;

    /// <inheritdoc />
    protected override IEnumerable<BitbucketProjectOptions> GetProjects() => PlatformOptions.Projects;
}
```

- [ ] **Step 6: 更新 `FetchGitLabReleaseBranchTask.cs`**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 取得 GitLab 各專案最新 Release Branch 任務
/// </summary>
public class FetchGitLabReleaseBranchTask : BaseFetchReleaseBranchTask<GitLabOptions, GitLabProjectOptions>
{
    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="serviceProvider">服務提供者</param>
    /// <param name="logger">日誌記錄器</param>
    /// <param name="dataTransferService">資料傳遞服務</param>
    /// <param name="gitLabOptions">GitLab 配置選項</param>
    public FetchGitLabReleaseBranchTask(
        IServiceProvider serviceProvider,
        ILogger<FetchGitLabReleaseBranchTask> logger,
        IDataTransferService dataTransferService,
        IOptions<GitLabOptions> gitLabOptions)
        : base(
            serviceProvider.GetRequiredKeyedService<ISourceControlRepository>("GitLab"),
            logger,
            dataTransferService,
            gitLabOptions.Value)
    {
    }

    /// <inheritdoc />
    protected override string PlatformName => "GitLab";

    /// <inheritdoc />
    protected override string DataTransferGroupKey => DataTransferKeys.GitLabHash;

    /// <inheritdoc />
    protected override string DataTransferGroupField => DataTransferKeys.Fields.ReleaseBranches;

    /// <inheritdoc />
    protected override IEnumerable<GitLabProjectOptions> GetProjects() => PlatformOptions.Projects;
}
```

- [ ] **Step 7: 更新 `FetchBitbucketReleaseBranchTask.cs`**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 取得 Bitbucket 各專案最新 Release Branch 任務
/// </summary>
public class FetchBitbucketReleaseBranchTask : BaseFetchReleaseBranchTask<BitbucketOptions, BitbucketProjectOptions>
{
    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="serviceProvider">服務提供者</param>
    /// <param name="logger">日誌記錄器</param>
    /// <param name="dataTransferService">資料傳遞服務</param>
    /// <param name="bitbucketOptions">Bitbucket 配置選項</param>
    public FetchBitbucketReleaseBranchTask(
        IServiceProvider serviceProvider,
        ILogger<FetchBitbucketReleaseBranchTask> logger,
        IDataTransferService dataTransferService,
        IOptions<BitbucketOptions> bitbucketOptions)
        : base(
            serviceProvider.GetRequiredKeyedService<ISourceControlRepository>("Bitbucket"),
            logger,
            dataTransferService,
            bitbucketOptions.Value)
    {
    }

    /// <inheritdoc />
    protected override string PlatformName => "Bitbucket";

    /// <inheritdoc />
    protected override string DataTransferGroupKey => DataTransferKeys.BitbucketHash;

    /// <inheritdoc />
    protected override string DataTransferGroupField => DataTransferKeys.Fields.ReleaseBranches;

    /// <inheritdoc />
    protected override IEnumerable<BitbucketProjectOptions> GetProjects() => PlatformOptions.Projects;
}
```

- [ ] **Step 8: 更新 `FilterGitLabPullRequestsByUserTask.cs`**

```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 過濾 GitLab Pull Request 依使用者任務
/// </summary>
/// <remarks>
/// 從 Group Key `GitLab` Field `PullRequests` 讀取資料，依 UserMapping 的 GitLabUserId 過濾，
/// 將結果寫入 Field `PullRequests:ByUser`。
/// </remarks>
public class FilterGitLabPullRequestsByUserTask : BaseFilterPullRequestsByUserTask
{
    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="logger">日誌記錄器</param>
    /// <param name="dataTransferService">資料傳遞服務</param>
    /// <param name="userMappingOptions">使用者對應設定</param>
    public FilterGitLabPullRequestsByUserTask(
        ILogger<FilterGitLabPullRequestsByUserTask> logger,
        IDataTransferService dataTransferService,
        IOptions<UserMappingOptions> userMappingOptions)
        : base(
            logger,
            dataTransferService,
            ExtractGitLabUserIdToDisplayName(userMappingOptions.Value))
    {
    }

    /// <inheritdoc />
    protected override string SourceGroupKey => DataTransferKeys.GitLabHash;

    /// <inheritdoc />
    protected override string SourceGroupField => DataTransferKeys.Fields.PullRequests;

    /// <inheritdoc />
    protected override string TargetGroupKey => DataTransferKeys.GitLabHash;

    /// <inheritdoc />
    protected override string TargetGroupField => DataTransferKeys.Fields.PullRequestsByUser;

    /// <inheritdoc />
    protected override string PlatformName => "GitLab";

    private static IReadOnlyDictionary<string, string> ExtractGitLabUserIdToDisplayName(
        UserMappingOptions options) =>
        options.Mappings
            .Where(m => !string.IsNullOrWhiteSpace(m.GitLabUserId) && !string.IsNullOrWhiteSpace(m.DisplayName))
            .GroupBy(m => m.GitLabUserId)
            .ToDictionary(g => g.Key, g => g.First().DisplayName);
}
```

- [ ] **Step 9: 更新 `FilterBitbucketPullRequestsByUserTask.cs`**

```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 過濾 Bitbucket Pull Request 依使用者任務
/// </summary>
/// <remarks>
/// 從 Group Key `Bitbucket` Field `PullRequests` 讀取資料，依 UserMapping 的 BitbucketUserId 過濾，
/// 將結果寫入 Field `PullRequests:ByUser`。
/// </remarks>
public class FilterBitbucketPullRequestsByUserTask : BaseFilterPullRequestsByUserTask
{
    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="logger">日誌記錄器</param>
    /// <param name="dataTransferService">資料傳遞服務</param>
    /// <param name="userMappingOptions">使用者對應設定</param>
    public FilterBitbucketPullRequestsByUserTask(
        ILogger<FilterBitbucketPullRequestsByUserTask> logger,
        IDataTransferService dataTransferService,
        IOptions<UserMappingOptions> userMappingOptions)
        : base(
            logger,
            dataTransferService,
            ExtractBitbucketUserIdToDisplayName(userMappingOptions.Value))
    {
    }

    /// <inheritdoc />
    protected override string SourceGroupKey => DataTransferKeys.BitbucketHash;

    /// <inheritdoc />
    protected override string SourceGroupField => DataTransferKeys.Fields.PullRequests;

    /// <inheritdoc />
    protected override string TargetGroupKey => DataTransferKeys.BitbucketHash;

    /// <inheritdoc />
    protected override string TargetGroupField => DataTransferKeys.Fields.PullRequestsByUser;

    /// <inheritdoc />
    protected override string PlatformName => "Bitbucket";

    private static IReadOnlyDictionary<string, string> ExtractBitbucketUserIdToDisplayName(
        UserMappingOptions options) =>
        options.Mappings
            .Where(m => !string.IsNullOrWhiteSpace(m.BitbucketUserId) && !string.IsNullOrWhiteSpace(m.DisplayName))
            .GroupBy(m => m.BitbucketUserId)
            .ToDictionary(g => g.Key, g => g.First().DisplayName);
}
```

- [ ] **Step 10: 更新 `FetchAzureDevOpsWorkItemsTask.cs`**

以下列出替換點（其餘邏輯不動）：

1. `private readonly IRedisService _redisService;` → `private readonly IDataTransferService _dataTransferService;`
2. XML 文件 `/// <param name="redisService">Redis 服務</param>` → `/// <param name="dataTransferService">資料傳遞服務</param>`
3. 建構子參數 `IRedisService redisService,` → `IDataTransferService dataTransferService,`
4. `_redisService = redisService;` → `_dataTransferService = dataTransferService;`
5. `await _redisService.HashDeleteAsync(RedisKeys.AzureDevOpsHash, RedisKeys.Fields.WorkItems);` → `await _dataTransferService.GroupDeleteAsync(DataTransferKeys.AzureDevOpsHash, DataTransferKeys.Fields.WorkItems);`
6. `await _redisService.HashSetAsync(RedisKeys.AzureDevOpsHash, RedisKeys.Fields.WorkItems, result.ToJson());` → `await _dataTransferService.GroupSetAsync(DataTransferKeys.AzureDevOpsHash, DataTransferKeys.Fields.WorkItems, result.ToJson());`
7. `var json = await _redisService.HashGetAsync(hashKey, field);` → `var json = await _dataTransferService.GroupGetAsync(hashKey, field);`
8. `(HashKey: RedisKeys.GitLabHash, Field: RedisKeys.Fields.PullRequestsByUser, ...)` → `(HashKey: DataTransferKeys.GitLabHash, Field: DataTransferKeys.Fields.PullRequestsByUser, ...)`
9. `(HashKey: RedisKeys.BitbucketHash, Field: RedisKeys.Fields.PullRequestsByUser, ...)` → `(HashKey: DataTransferKeys.BitbucketHash, Field: DataTransferKeys.Fields.PullRequestsByUser, ...)`
10. 私有方法 `LoadPullRequestsFromRedisAsync` → `LoadPullRequestsAsync`（同步更新內部呼叫 `await LoadPullRequestsFromRedisAsync()` → `await LoadPullRequestsAsync()`）
11. 所有日誌中的 `"Redis"` 或 `"Redis Hash"` 替換為 `"資料傳遞存放區"`
12. 所有 `using ReleaseKit.Common.Constants;` 維持（因為 `DataTransferKeys` 也在此 namespace）；確保全檔不再出現 `RedisKeys`

- [ ] **Step 11: 更新 `GetUserStoryTask.cs`**

1. `private readonly IRedisService _redisService;` → `private readonly IDataTransferService _dataTransferService;`
2. 建構子參數 `IRedisService redisService,` → `IDataTransferService dataTransferService,`
3. `_redisService = redisService;` → `_dataTransferService = dataTransferService;`
4. `if (await _redisService.HashExistsAsync(RedisKeys.AzureDevOpsHash, RedisKeys.Fields.WorkItemsUserStories))` → `if (await _dataTransferService.GroupExistsAsync(DataTransferKeys.AzureDevOpsHash, DataTransferKeys.Fields.WorkItemsUserStories))`
5. 日誌 `"清除 Redis 中的舊資料，Hash: {HashKey} Field: {Field}"` → `"清除資料傳遞存放區中的舊資料，Group: {GroupKey} Field: {Field}"`
6. `await _redisService.HashDeleteAsync(RedisKeys.AzureDevOpsHash, RedisKeys.Fields.WorkItemsUserStories);` → `await _dataTransferService.GroupDeleteAsync(DataTransferKeys.AzureDevOpsHash, DataTransferKeys.Fields.WorkItemsUserStories);`
7. 日誌 `"從 Redis 讀取到 {Count} 筆 Work Item"` → `"從資料傳遞存放區讀取到 {Count} 筆 Work Item"`
8. `LoadWorkItemsFromRedisAsync` 方法名稱 → `LoadWorkItemsAsync`
9. `var json = await _redisService.HashGetAsync(RedisKeys.AzureDevOpsHash, RedisKeys.Fields.WorkItems);` → `var json = await _dataTransferService.GroupGetAsync(DataTransferKeys.AzureDevOpsHash, DataTransferKeys.Fields.WorkItems);`
10. 錯誤訊息 `$"Redis Hash {RedisKeys.AzureDevOpsHash} Field {RedisKeys.Fields.WorkItems} 中無 Work Item 資料"` → `$"資料傳遞存放區 Group {DataTransferKeys.AzureDevOpsHash} Field {DataTransferKeys.Fields.WorkItems} 中無 Work Item 資料"`
11. `SaveResultAsync` 中 `await _redisService.HashSetAsync(...)` → `await _dataTransferService.GroupSetAsync(...)`

- [ ] **Step 12: 更新 `ConsolidateReleaseDataTask.cs`**

1. `private readonly IRedisService _redisService;` → `private readonly IDataTransferService _dataTransferService;`
2. 建構子 XML 文件 `/// <param name="redisService">Redis 服務</param>` → `/// <param name="dataTransferService">資料傳遞服務</param>`
3. 建構子參數 `IRedisService redisService,` → `IDataTransferService dataTransferService,`
4. `_redisService = redisService;` → `_dataTransferService = dataTransferService;`
5. `if (await _redisService.HashExistsAsync(RedisKeys.ReleaseDataHash, RedisKeys.Fields.Consolidated))` → `if (await _dataTransferService.GroupExistsAsync(DataTransferKeys.ReleaseDataHash, DataTransferKeys.Fields.Consolidated))`
6. 日誌與錯誤訊息中的 `"Redis"` → `"資料傳遞存放區"`
7. `await _redisService.HashDeleteAsync(...)` → `await _dataTransferService.GroupDeleteAsync(...)`
8. `await _redisService.HashSetAsync(...)` → `await _dataTransferService.GroupSetAsync(...)`
9. `var bitbucketJson = await _redisService.HashGetAsync(RedisKeys.BitbucketHash, RedisKeys.Fields.PullRequestsByUser);` → `var bitbucketJson = await _dataTransferService.GroupGetAsync(DataTransferKeys.BitbucketHash, DataTransferKeys.Fields.PullRequestsByUser);`
10. `var gitLabJson = await _redisService.HashGetAsync(RedisKeys.GitLabHash, RedisKeys.Fields.PullRequestsByUser);` → `var gitLabJson = await _dataTransferService.GroupGetAsync(DataTransferKeys.GitLabHash, DataTransferKeys.Fields.PullRequestsByUser);`
11. `LoadUserStoriesAsync` 中的 `_redisService.HashGetAsync(RedisKeys.AzureDevOpsHash, RedisKeys.Fields.WorkItemsUserStories)` → `_dataTransferService.GroupGetAsync(DataTransferKeys.AzureDevOpsHash, DataTransferKeys.Fields.WorkItemsUserStories)`

- [ ] **Step 13: 更新 `EnhanceTitlesWithCopilotTask.cs`**

1. `private readonly IRedisService _redisService;` → `private readonly IDataTransferService _dataTransferService;`
2. 建構子 XML 文件 `/// <param name="redisService">Redis 服務</param>` → `/// <param name="dataTransferService">資料傳遞服務</param>`
3. 建構子參數 `IRedisService redisService,` → `IDataTransferService dataTransferService,`
4. `_redisService = redisService;` → `_dataTransferService = dataTransferService;`
5. `if (await _redisService.HashExistsAsync(RedisKeys.ReleaseDataHash, RedisKeys.Fields.EnhancedTitles))` → `if (await _dataTransferService.GroupExistsAsync(DataTransferKeys.ReleaseDataHash, DataTransferKeys.Fields.EnhancedTitles))`
6. 日誌中 `"Redis"` → `"資料傳遞存放區"`
7. `await _redisService.HashDeleteAsync(...)` → `await _dataTransferService.GroupDeleteAsync(...)`
8. `await _redisService.HashSetAsync(...)` → `await _dataTransferService.GroupSetAsync(...)`
9. `LoadConsolidatedDataAsync` 中 `_redisService.HashGetAsync(RedisKeys.ReleaseDataHash, RedisKeys.Fields.Consolidated)` → `_dataTransferService.GroupGetAsync(DataTransferKeys.ReleaseDataHash, DataTransferKeys.Fields.Consolidated)`
10. 錯誤訊息更新（移除 `"Redis Hash"` 字樣）

- [ ] **Step 14: 更新 `GetReleaseSettingTask.cs`**

1. `private readonly IRedisService _redisService;` → `private readonly IDataTransferService _dataTransferService;`
2. 建構子參數 `IRedisService redisService,` → `IDataTransferService dataTransferService,`
3. `_redisService = redisService;` → `_dataTransferService = dataTransferService;`
4. 所有 `_redisService.HashGetAsync(...)` → `_dataTransferService.GroupGetAsync(...)`
5. `if (await _redisService.ExistsAsync(RedisKeys.ReleaseSetting))` → `if (await _dataTransferService.ExistsAsync(DataTransferKeys.ReleaseSetting))`
6. `await _redisService.DeleteAsync(RedisKeys.ReleaseSetting)` → `await _dataTransferService.DeleteAsync(DataTransferKeys.ReleaseSetting)`
7. `await _redisService.SetAsync(RedisKeys.ReleaseSetting, json)` → `await _dataTransferService.SetAsync(DataTransferKeys.ReleaseSetting, json)`
8. 所有日誌/錯誤訊息中的 `"Redis"` 字樣 → 移除或改為泛化描述（如 `"資料傳遞存放區"`）
9. `ReadReleaseBranchDataAsync` 方法中日誌 `"{Platform} 無前置 release branch 資料"` 維持，移除 `"Redis"` 字樣
10. 所有 `RedisKeys.*` → `DataTransferKeys.*`

- [ ] **Step 15: 更新 `UpdateGoogleSheetsTask.cs`**

1. `private readonly IRedisService _redisService;` → `private readonly IDataTransferService _dataTransferService;`
2. 建構子 XML 文件 `/// <param name="redisService">Redis 服務</param>` → `/// <param name="dataTransferService">資料傳遞服務</param>`
3. 建構子參數 `IRedisService redisService,` → `IDataTransferService dataTransferService,`
4. `_redisService = redisService;` → `_dataTransferService = dataTransferService;`
5. `ReadConsolidatedDataAsync` 中所有 `_redisService.HashGetAsync(...)` → `_dataTransferService.GroupGetAsync(...)`
6. 所有 `RedisKeys.*` → `DataTransferKeys.*`
7. 日誌/錯誤訊息中 `"Redis Hash"` → `"資料傳遞存放區"`

### 5C: 更新測試檔案

> 在這個步驟之前，由於 Task 類別已改用 `IDataTransferService`，所有使用 `Mock<IRedisService>` 並將其傳入 Task 建構子的測試**將無法編譯**。以下步驟將修復所有測試。

**每個測試檔案都需要：**
- `Mock<IRedisService>` → `Mock<IDataTransferService>`
- `_redisServiceMock` → `_dataTransferServiceMock`
- `redisServiceMock` → `dataTransferServiceMock`
- `redisService` → `dataTransferService`（建構子傳入的參數）
- `HashExistsAsync` → `GroupExistsAsync`
- `HashDeleteAsync` → `GroupDeleteAsync`
- `HashSetAsync` → `GroupSetAsync`
- `HashGetAsync` → `GroupGetAsync`
- `HashGetAllAsync` → `GroupGetAllAsync`
- `RedisKeys.*` → `DataTransferKeys.*`
- `using ReleaseKit.Domain.Abstractions;` 維持不變（`IDataTransferService` 也在此 namespace）

- [ ] **Step 16: 更新 `TasksTests.cs`**

替換點：
- `var redisServiceMock = new Mock<IRedisService>();` → `var dataTransferServiceMock = new Mock<IDataTransferService>();`
- `redisServiceMock.Setup(x => x.HashExistsAsync(...))` → `dataTransferServiceMock.Setup(x => x.GroupExistsAsync(...))`
- `redisServiceMock.Setup(x => x.HashSetAsync(...))` → `dataTransferServiceMock.Setup(x => x.GroupSetAsync(...))`
- 傳入建構子時 `redisServiceMock.Object` → `dataTransferServiceMock.Object`

- [ ] **Step 17: 更新 `RedisIntegrationTests.cs`（同時改類別名稱）**

將類別名稱 `RedisIntegrationTests` → `DataTransferIntegrationTests`，並套用 Step 16 所列的相同替換模式。所有 `RedisKeys.*` → `DataTransferKeys.*`。

- [ ] **Step 18: 更新 `ReleaseBranchRedisIntegrationTests.cs`（同時改類別名稱）**

將類別名稱 `ReleaseBranchRedisIntegrationTests` → `ReleaseBranchDataTransferIntegrationTests`，並套用相同替換模式。

- [ ] **Step 19: 更新 `FilterPullRequestsByUserTaskTests.cs`**

套用 Step 16 所列的相同替換模式。

- [ ] **Step 20: 更新 `ConsolidateReleaseDataTaskTests.cs`**

套用相同替換模式，並將所有 `RedisKeys.*` → `DataTransferKeys.*`。

- [ ] **Step 21: 更新 `EnhanceTitlesWithCopilotTaskTests.cs`**

套用相同替換模式。

- [ ] **Step 22: 更新 `FetchAzureDevOpsWorkItemsTaskTests.cs`**

套用相同替換模式。

- [ ] **Step 23: 更新 `FetchBitbucketReleaseBranchTaskTests.cs`**

套用相同替換模式。

- [ ] **Step 24: 更新 `FetchGitLabReleaseBranchTaskTests.cs`**

套用相同替換模式。

- [ ] **Step 25: 更新 `GetReleaseSettingTaskTests.cs`**

套用相同替換模式。注意：`_redisServiceMock.Setup(x => x.SetAsync(RedisKeys.ReleaseSetting, ...))` → `_dataTransferServiceMock.Setup(x => x.SetAsync(DataTransferKeys.ReleaseSetting, ...))` （此方法不是 `GroupSet`，要用 `SetAsync`）。

- [ ] **Step 26: 更新 `GetUserStoryTaskTests.cs`**

套用相同替換模式。

- [ ] **Step 27: 更新 `UpdateGoogleSheetsTaskTests.cs`**

套用相同替換模式。

- [ ] **Step 28: 建置並執行全套測試**

```bash
cd src && dotnet build release-kit.sln && dotnet test release-kit.sln -v minimal
```

Expected: `Build succeeded.` 且全部測試通過。

- [ ] **Step 29: Commit**

```bash
git add src/ReleaseKit.Application/Tasks/ \
        tests/ReleaseKit.Application.Tests/
git commit -m "refactor: 將所有 Task 由 IRedisService 遷移至 IDataTransferService

- Hash* 操作改為 Group* 中性命名
- RedisKeys 參照改為 DataTransferKeys
- 測試 mock 同步更新

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 6: 清除舊檔案並最終驗證

**Files:**
- Delete: `src/ReleaseKit.Domain/Abstractions/IRedisService.cs`
- Delete: `src/ReleaseKit.Infrastructure/Redis/RedisService.cs`
- Delete: `tests/ReleaseKit.Infrastructure.Tests/Redis/RedisServiceTests.cs`

- [ ] **Step 1: 刪除 `IRedisService.cs`**

```bash
git rm src/ReleaseKit.Domain/Abstractions/IRedisService.cs
```

- [ ] **Step 2: 刪除舊 `RedisService.cs`**

```bash
git rm src/ReleaseKit.Infrastructure/Redis/RedisService.cs
```

若 `src/ReleaseKit.Infrastructure/Redis/` 目錄現在為空，一起刪除：
```bash
rmdir src/ReleaseKit.Infrastructure/Redis/ 2>/dev/null || true
```

- [ ] **Step 3: 刪除舊 `RedisServiceTests.cs`（已被 `RedisDataTransferServiceTests` 取代）**

```bash
git rm tests/ReleaseKit.Infrastructure.Tests/Redis/RedisServiceTests.cs
```

- [ ] **Step 4: 建置確認無錯誤**

```bash
cd src && dotnet build release-kit.sln
```

Expected: `Build succeeded.`（如有任何仍引用 `IRedisService` 或 `RedisService` 的地方會在此出現編譯錯誤，依錯誤訊息修復）

- [ ] **Step 5: 執行全套測試**

```bash
cd src && dotnet test release-kit.sln -v minimal
```

Expected: 全部通過，無失敗。

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "refactor: 刪除已被取代的 IRedisService、RedisService 與舊測試

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```
