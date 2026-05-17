using Microsoft.Extensions.Logging;
using ReleaseKit.Domain.Abstractions;
using StackExchange.Redis;

namespace ReleaseKit.Infrastructure.Redis;

/// <summary>
/// 以 Redis 作為指令間資料交換媒介的實作
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
    /// 設定鍵值資料
    /// </summary>
    public async Task<bool> SetValueAsync(string key, string value, TimeSpan? expiry = null)
    {
        var fullKey = GetFullKey(key);
        var result = await _database.StringSetAsync(fullKey, value, expiry);
        _logger.LogInformation("Redis SET: {Key} = {Value}, Expiry: {Expiry}, Result: {Result}", fullKey, value, expiry, result);
        return result;
    }

    /// <summary>
    /// 取得鍵值資料
    /// </summary>
    public async Task<string?> GetValueAsync(string key)
    {
        var fullKey = GetFullKey(key);
        var value = await _database.StringGetAsync(fullKey);
        _logger.LogInformation("Redis GET: {Key} = {Value}", fullKey, value.HasValue ? value.ToString() : "(null)");
        return value.HasValue ? value.ToString() : null;
    }

    /// <summary>
    /// 刪除鍵值資料
    /// </summary>
    public async Task<bool> DeleteValueAsync(string key)
    {
        var fullKey = GetFullKey(key);
        var result = await _database.KeyDeleteAsync(fullKey);
        _logger.LogInformation("Redis DELETE: {Key}, Result: {Result}", fullKey, result);
        return result;
    }

    /// <summary>
    /// 檢查鍵值是否存在
    /// </summary>
    public async Task<bool> ExistsValueAsync(string key)
    {
        var fullKey = GetFullKey(key);
        var result = await _database.KeyExistsAsync(fullKey);
        _logger.LogInformation("Redis EXISTS: {Key} = {Result}", fullKey, result);
        return result;
    }

    /// <summary>
    /// 設定集合欄位值
    /// </summary>
    public async Task<bool> SetFieldAsync(string hashKey, string field, string value)
    {
        var fullKey = GetFullKey(hashKey);
        var result = await _database.HashSetAsync(fullKey, field, value);
        _logger.LogInformation("Redis HSET: {Key} {Field} = {Value}, Result: {Result}", fullKey, field, value, result);
        return result;
    }

    /// <summary>
    /// 取得集合欄位值
    /// </summary>
    public async Task<string?> GetFieldAsync(string hashKey, string field)
    {
        var fullKey = GetFullKey(hashKey);
        var value = await _database.HashGetAsync(fullKey, field);
        _logger.LogInformation("Redis HGET: {Key} {Field} = {Value}", fullKey, field, value.HasValue ? value.ToString() : "(null)");
        return value.HasValue ? value.ToString() : null;
    }

    /// <summary>
    /// 刪除集合欄位
    /// </summary>
    public async Task<bool> DeleteFieldAsync(string hashKey, string field)
    {
        var fullKey = GetFullKey(hashKey);
        var result = await _database.HashDeleteAsync(fullKey, field);
        _logger.LogInformation("Redis HDEL: {Key} {Field}, Result: {Result}", fullKey, field, result);
        return result;
    }

    /// <summary>
    /// 檢查集合欄位是否存在
    /// </summary>
    public async Task<bool> FieldExistsAsync(string hashKey, string field)
    {
        var fullKey = GetFullKey(hashKey);
        var result = await _database.HashExistsAsync(fullKey, field);
        _logger.LogInformation("Redis HEXISTS: {Key} {Field} = {Result}", fullKey, field, result);
        return result;
    }

    /// <summary>
    /// 取得集合所有欄位與值
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> GetAllFieldsAsync(string hashKey)
    {
        var fullKey = GetFullKey(hashKey);
        var entries = await _database.HashGetAllAsync(fullKey);
        _logger.LogInformation("Redis HGETALL: {Key}, Count: {Count}", fullKey, entries.Length);
        return entries.ToDictionary(e => e.Name.ToString(), e => e.Value.ToString());
    }

    /// <summary>
    /// 取得完整鍵值（加上 Instance Name）
    /// </summary>
    private string GetFullKey(string key)
    {
        return string.IsNullOrEmpty(_instanceName) ? key : $"{_instanceName}{key}";
    }
}
