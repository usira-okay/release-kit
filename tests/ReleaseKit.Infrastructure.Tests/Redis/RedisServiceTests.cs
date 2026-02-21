using Microsoft.Extensions.Logging;
using Moq;
using ReleaseKit.Infrastructure.Redis;
using StackExchange.Redis;

namespace ReleaseKit.Infrastructure.Tests.Redis;

/// <summary>
/// Redis 服務單元測試
/// </summary>
public class RedisServiceTests
{
    private readonly Mock<IConnectionMultiplexer> _mockConnectionMultiplexer;
    private readonly Mock<IDatabase> _mockDatabase;
    private readonly Mock<ILogger<RedisService>> _mockLogger;
    private readonly RedisService _redisService;

    public RedisServiceTests()
    {
        _mockConnectionMultiplexer = new Mock<IConnectionMultiplexer>();
        _mockDatabase = new Mock<IDatabase>();
        _mockLogger = new Mock<ILogger<RedisService>>();

        _mockConnectionMultiplexer.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_mockDatabase.Object);

        _redisService = new RedisService(_mockConnectionMultiplexer.Object, _mockLogger.Object, "Test:");
    }

    [Fact]
    public async Task SetAsync_ShouldReturnTrue_WhenSetSucceeds()
    {
        // Arrange
        var key = "test-key";
        var value = "test-value";
        _mockDatabase.Setup(x => x.StringSetAsync(
            It.Is<RedisKey>(k => k.ToString() == "Test:test-key"),
            It.Is<RedisValue>(v => v.ToString() == value),
            null,
            It.IsAny<bool>(),
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        var result = await _redisService.SetAsync(key, value);

        // Assert
        Assert.True(result);
        _mockDatabase.Verify(x => x.StringSetAsync(
            It.Is<RedisKey>(k => k.ToString() == "Test:test-key"),
            It.Is<RedisValue>(v => v.ToString() == value),
            null,
            It.IsAny<bool>(),
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnValue_WhenKeyExists()
    {
        // Arrange
        var key = "test-key";
        var expectedValue = "test-value";
        _mockDatabase.Setup(x => x.StringGetAsync(
            It.Is<RedisKey>(k => k.ToString() == "Test:test-key"),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue(expectedValue));

        // Act
        var result = await _redisService.GetAsync(key);

        // Assert
        Assert.Equal(expectedValue, result);
        _mockDatabase.Verify(x => x.StringGetAsync(
            It.Is<RedisKey>(k => k.ToString() == "Test:test-key"),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnNull_WhenKeyDoesNotExist()
    {
        // Arrange
        var key = "non-existent-key";
        _mockDatabase.Setup(x => x.StringGetAsync(
            It.Is<RedisKey>(k => k.ToString() == "Test:non-existent-key"),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        // Act
        var result = await _redisService.GetAsync(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnTrue_WhenKeyIsDeleted()
    {
        // Arrange
        var key = "test-key";
        _mockDatabase.Setup(x => x.KeyDeleteAsync(
            It.Is<RedisKey>(k => k.ToString() == "Test:test-key"),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        var result = await _redisService.DeleteAsync(key);

        // Assert
        Assert.True(result);
        _mockDatabase.Verify(x => x.KeyDeleteAsync(
            It.Is<RedisKey>(k => k.ToString() == "Test:test-key"),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_WhenKeyExists()
    {
        // Arrange
        var key = "test-key";
        _mockDatabase.Setup(x => x.KeyExistsAsync(
            It.Is<RedisKey>(k => k.ToString() == "Test:test-key"),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        var result = await _redisService.ExistsAsync(key);

        // Assert
        Assert.True(result);
        _mockDatabase.Verify(x => x.KeyExistsAsync(
            It.Is<RedisKey>(k => k.ToString() == "Test:test-key"),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalse_WhenKeyDoesNotExist()
    {
        // Arrange
        var key = "non-existent-key";
        _mockDatabase.Setup(x => x.KeyExistsAsync(
            It.Is<RedisKey>(k => k.ToString() == "Test:non-existent-key"),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);

        // Act
        var result = await _redisService.ExistsAsync(key);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task SetAsync_WithExpiry_ShouldSetExpirationTime()
    {
        // Arrange
        var key = "test-key";
        var value = "test-value";
        var expiry = TimeSpan.FromMinutes(5);
        _mockDatabase.Setup(x => x.StringSetAsync(
            It.Is<RedisKey>(k => k.ToString() == "Test:test-key"),
            It.Is<RedisValue>(v => v.ToString() == value),
            expiry,
            It.IsAny<bool>(),
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        var result = await _redisService.SetAsync(key, value, expiry);

        // Assert
        Assert.True(result);
        _mockDatabase.Verify(x => x.StringSetAsync(
            It.Is<RedisKey>(k => k.ToString() == "Test:test-key"),
            It.Is<RedisValue>(v => v.ToString() == value),
            expiry,
            It.IsAny<bool>(),
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenConnectionMultiplexerIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new RedisService(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenLoggerIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new RedisService(_mockConnectionMultiplexer.Object, null!));
    }

    [Fact]
    public async Task HashSetAsync_ShouldReturnTrue_WhenSetSucceeds()
    {
        // Arrange
        var hashKey = "test-hash";
        var field = "test-field";
        var value = "test-value";
        _mockDatabase.Setup(x => x.HashSetAsync(
            It.Is<RedisKey>(k => k.ToString() == "Test:test-hash"),
            It.Is<RedisValue>(f => f.ToString() == field),
            It.Is<RedisValue>(v => v.ToString() == value),
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        var result = await _redisService.HashSetAsync(hashKey, field, value);

        // Assert
        Assert.True(result);
        _mockDatabase.Verify(x => x.HashSetAsync(
            It.Is<RedisKey>(k => k.ToString() == "Test:test-hash"),
            It.Is<RedisValue>(f => f.ToString() == field),
            It.Is<RedisValue>(v => v.ToString() == value),
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task HashGetAsync_ShouldReturnValue_WhenFieldExists()
    {
        // Arrange
        var hashKey = "test-hash";
        var field = "test-field";
        var expectedValue = "test-value";
        _mockDatabase.Setup(x => x.HashGetAsync(
            It.Is<RedisKey>(k => k.ToString() == "Test:test-hash"),
            It.Is<RedisValue>(f => f.ToString() == field),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue(expectedValue));

        // Act
        var result = await _redisService.HashGetAsync(hashKey, field);

        // Assert
        Assert.Equal(expectedValue, result);
    }

    [Fact]
    public async Task HashGetAsync_ShouldReturnNull_WhenFieldDoesNotExist()
    {
        // Arrange
        var hashKey = "test-hash";
        var field = "non-existent-field";
        _mockDatabase.Setup(x => x.HashGetAsync(
            It.Is<RedisKey>(k => k.ToString() == "Test:test-hash"),
            It.Is<RedisValue>(f => f.ToString() == field),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        // Act
        var result = await _redisService.HashGetAsync(hashKey, field);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task HashDeleteAsync_ShouldReturnTrue_WhenFieldIsDeleted()
    {
        // Arrange
        var hashKey = "test-hash";
        var field = "test-field";
        _mockDatabase.Setup(x => x.HashDeleteAsync(
            It.Is<RedisKey>(k => k.ToString() == "Test:test-hash"),
            It.Is<RedisValue>(f => f.ToString() == field),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        var result = await _redisService.HashDeleteAsync(hashKey, field);

        // Assert
        Assert.True(result);
        _mockDatabase.Verify(x => x.HashDeleteAsync(
            It.Is<RedisKey>(k => k.ToString() == "Test:test-hash"),
            It.Is<RedisValue>(f => f.ToString() == field),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task HashExistsAsync_ShouldReturnTrue_WhenFieldExists()
    {
        // Arrange
        var hashKey = "test-hash";
        var field = "test-field";
        _mockDatabase.Setup(x => x.HashExistsAsync(
            It.Is<RedisKey>(k => k.ToString() == "Test:test-hash"),
            It.Is<RedisValue>(f => f.ToString() == field),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        var result = await _redisService.HashExistsAsync(hashKey, field);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task HashExistsAsync_ShouldReturnFalse_WhenFieldDoesNotExist()
    {
        // Arrange
        var hashKey = "test-hash";
        var field = "non-existent-field";
        _mockDatabase.Setup(x => x.HashExistsAsync(
            It.Is<RedisKey>(k => k.ToString() == "Test:test-hash"),
            It.Is<RedisValue>(f => f.ToString() == field),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);

        // Act
        var result = await _redisService.HashExistsAsync(hashKey, field);

        // Assert
        Assert.False(result);
    }
}
