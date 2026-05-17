using Microsoft.Extensions.Logging;
using Moq;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Infrastructure.FileStorage;

namespace ReleaseKit.Infrastructure.Tests.FileStorage;

/// <summary>
/// 實體檔案資料傳遞服務單元測試
/// </summary>
public class FileDataTransferServiceTests : IDisposable
{
    private static readonly DateTimeOffset FixedTime = new(2025, 5, 1, 8, 0, 0, TimeSpan.Zero);
    private readonly string _basePath;
    private readonly FakeNow _now;
    private readonly Mock<ILogger<FileDataTransferService>> _loggerMock;
    private readonly FileDataTransferService _service;

    public FileDataTransferServiceTests()
    {
        _basePath = Path.Combine(Path.GetTempPath(), "release-kit-tests", Guid.NewGuid().ToString("N"));
        _now = new FakeNow { UtcNow = FixedTime };
        _loggerMock = new Mock<ILogger<FileDataTransferService>>();
        _service = new FileDataTransferService(_basePath, _now, _loggerMock.Object);
    }

    [Fact]
    public async Task SetAsync_ShouldReturnTrue_WhenWriteSucceeds()
    {
        var result = await _service.SetAsync("test-key", "test-value");

        Assert.True(result);
        Assert.Equal("test-value", await _service.GetAsync("test-key"));
    }

    [Fact]
    public async Task GetAsync_ShouldReturnNull_WhenKeyDoesNotExist()
    {
        var result = await _service.GetAsync("missing-key");

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnTrue_WhenKeyIsDeleted()
    {
        await _service.SetAsync("test-key", "test-value");

        var result = await _service.DeleteAsync("test-key");

        Assert.True(result);
        Assert.Null(await _service.GetAsync("test-key"));
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_WhenKeyExists()
    {
        await _service.SetAsync("test-key", "test-value");

        var result = await _service.ExistsAsync("test-key");

        Assert.True(result);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalse_WhenKeyDoesNotExist()
    {
        var result = await _service.ExistsAsync("missing-key");

        Assert.False(result);
    }

    [Fact]
    public async Task SetAsync_WithExpiry_ShouldExpireAfterDeadline()
    {
        await _service.SetAsync("test-key", "test-value", TimeSpan.FromMinutes(5));
        _now.UtcNow = FixedTime.AddMinutes(6);

        var value = await _service.GetAsync("test-key");
        var exists = await _service.ExistsAsync("test-key");

        Assert.Null(value);
        Assert.False(exists);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenBasePathIsEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            new FileDataTransferService("", _now, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenNowIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new FileDataTransferService(_basePath, null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenLoggerIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new FileDataTransferService(_basePath, _now, null!));
    }

    [Fact]
    public async Task HashSetAsync_ShouldReturnTrue_WhenWriteSucceeds()
    {
        var result = await _service.HashSetAsync("test-hash", "test-field", "test-value");

        Assert.True(result);
        Assert.Equal("test-value", await _service.HashGetAsync("test-hash", "test-field"));
    }

    [Fact]
    public async Task HashGetAsync_ShouldReturnNull_WhenFieldDoesNotExist()
    {
        var result = await _service.HashGetAsync("test-hash", "missing-field");

        Assert.Null(result);
    }

    [Fact]
    public async Task HashDeleteAsync_ShouldReturnTrue_WhenFieldIsDeleted()
    {
        await _service.HashSetAsync("test-hash", "test-field", "test-value");

        var result = await _service.HashDeleteAsync("test-hash", "test-field");

        Assert.True(result);
        Assert.Null(await _service.HashGetAsync("test-hash", "test-field"));
    }

    [Fact]
    public async Task HashExistsAsync_ShouldReturnTrue_WhenFieldExists()
    {
        await _service.HashSetAsync("test-hash", "test-field", "test-value");

        var result = await _service.HashExistsAsync("test-hash", "test-field");

        Assert.True(result);
    }

    [Fact]
    public async Task HashExistsAsync_ShouldReturnFalse_WhenFieldDoesNotExist()
    {
        var result = await _service.HashExistsAsync("test-hash", "missing-field");

        Assert.False(result);
    }

    [Fact]
    public async Task HashGetAllAsync_ShouldReturnAllFields_WhenHashExists()
    {
        await _service.HashSetAsync("test-hash", "field-1", "value-1");
        await _service.HashSetAsync("test-hash", "field-2", "value-2");

        var result = await _service.HashGetAllAsync("test-hash");

        Assert.Equal(2, result.Count);
        Assert.Equal("value-1", result["field-1"]);
        Assert.Equal("value-2", result["field-2"]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_basePath))
        {
            Directory.Delete(_basePath, recursive: true);
        }
    }

    private sealed class FakeNow : INow
    {
        public DateTimeOffset UtcNow { get; set; }
    }
}
