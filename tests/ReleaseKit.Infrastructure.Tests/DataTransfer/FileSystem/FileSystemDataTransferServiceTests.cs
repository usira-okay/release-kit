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
