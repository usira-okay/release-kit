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
    public async Task GroupSetAsync_ShouldReturnTrue_WhenUpdatingExistingField()
    {
        _mockDatabase
            .Setup(x => x.HashSetAsync(
                It.Is<RedisKey>(k => k.ToString() == "Test:GroupA"),
                It.Is<RedisValue>(f => f.ToString() == "field1"),
                It.Is<RedisValue>(v => v.ToString() == "updated"),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);

        var result = await _service.GroupSetAsync("GroupA", "field1", "updated");

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
