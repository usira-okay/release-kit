using Microsoft.Extensions.Logging;
using ReleaseKit.Domain.Abstractions;
using StackExchange.Redis;

namespace ReleaseKit.Infrastructure.Redis;

/// <summary>
/// Redis 快取服務實作
/// </summary>
public class RedisService : IRedisService
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly IDatabase _database;
    private readonly ILogger<RedisService> _logger;
    private readonly string _instanceName;

    public RedisService(
        IConnectionMultiplexer connectionMultiplexer,
        ILogger<RedisService> logger,
        string instanceName = "")
    {
        _connectionMultiplexer = connectionMultiplexer ?? throw new ArgumentNullException(nameof(connectionMultiplexer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _instanceName = instanceName;
        _database = _connectionMultiplexer.GetDatabase();
    }

    /// <summary>
    /// 設定快取值
    /// </summary>
    public async Task<bool> SetAsync(string key, string value, TimeSpan? expiry = null)
    {
        var fullKey = GetFullKey(key);
        var result = await _database.StringSetAsync(fullKey, value, expiry);
        _logger.LogInformation("Redis SET: {Key} = {Value}, Expiry: {Expiry}, Result: {Result}", fullKey, value, expiry, result);
        return result;
    }

    /// <summary>
    /// 取得快取值
    /// </summary>
    public async Task<string?> GetAsync(string key)
    {
        var fullKey = GetFullKey(key);
        var value = await _database.StringGetAsync(fullKey);
        _logger.LogInformation("Redis GET: {Key} = {Value}", fullKey, value.HasValue ? value.ToString() : "(null)");
        return value.HasValue ? value.ToString() : null;
    }

    /// <summary>
    /// 刪除快取值
    /// </summary>
    public async Task<bool> DeleteAsync(string key)
    {
        var fullKey = GetFullKey(key);
        var result = await _database.KeyDeleteAsync(fullKey);
        _logger.LogInformation("Redis DELETE: {Key}, Result: {Result}", fullKey, result);
        return result;
    }

    /// <summary>
    /// 檢查快取鍵值是否存在
    /// </summary>
    public async Task<bool> ExistsAsync(string key)
    {
        var fullKey = GetFullKey(key);
        var result = await _database.KeyExistsAsync(fullKey);
        _logger.LogInformation("Redis EXISTS: {Key} = {Result}", fullKey, result);
        return result;
    }

    /// <summary>
    /// 取得完整的快取鍵值（加上 Instance Name）
    /// </summary>
    private string GetFullKey(string key)
    {
        return string.IsNullOrEmpty(_instanceName) ? key : $"{_instanceName}{key}";
    }
}
