using Microsoft.Extensions.Logging;
using ReleaseKit.Domain.Abstractions;
using StackExchange.Redis;

namespace ReleaseKit.Infrastructure.Redis;

/// <summary>
/// Redis 資料傳遞服務實作
/// </summary>
public class RedisService : IDataTransferService
{
    private readonly IConnectionMultiplexer _redisConnection;
    private readonly IDatabase _database;
    private readonly ILogger<RedisService> _logger;
    private readonly string _keyPrefix;

    public RedisService(
        IConnectionMultiplexer redisConnection,
        ILogger<RedisService> logger,
        string keyPrefix = "")
    {
        _redisConnection = redisConnection ?? throw new ArgumentNullException(nameof(redisConnection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _keyPrefix = keyPrefix;
        _database = _redisConnection.GetDatabase();
    }

    /// <summary>
    /// 設定資料值
    /// </summary>
    public async Task<bool> SetAsync(string key, string value, TimeSpan? expiry = null)
    {
        var storageKey = GetStorageKey(key);
        var result = await _database.StringSetAsync(storageKey, value, expiry);
        _logger.LogInformation("Redis SET: {Key} = {Value}, Expiry: {Expiry}, Result: {Result}", storageKey, value, expiry, result);
        return result;
    }

    /// <summary>
    /// 取得資料值
    /// </summary>
    public async Task<string?> GetAsync(string key)
    {
        var storageKey = GetStorageKey(key);
        var value = await _database.StringGetAsync(storageKey);
        _logger.LogInformation("Redis GET: {Key} = {Value}", storageKey, value.HasValue ? value.ToString() : "(null)");
        return value.HasValue ? value.ToString() : null;
    }

    /// <summary>
    /// 刪除資料值
    /// </summary>
    public async Task<bool> DeleteAsync(string key)
    {
        var storageKey = GetStorageKey(key);
        var result = await _database.KeyDeleteAsync(storageKey);
        _logger.LogInformation("Redis DELETE: {Key}, Result: {Result}", storageKey, result);
        return result;
    }

    /// <summary>
    /// 檢查資料鍵值是否存在
    /// </summary>
    public async Task<bool> ExistsAsync(string key)
    {
        var storageKey = GetStorageKey(key);
        var result = await _database.KeyExistsAsync(storageKey);
        _logger.LogInformation("Redis EXISTS: {Key} = {Result}", storageKey, result);
        return result;
    }

    /// <summary>
    /// 設定 Hash 欄位值
    /// </summary>
    public async Task<bool> HashSetAsync(string hashKey, string field, string value)
    {
        var storageKey = GetStorageKey(hashKey);
        var result = await _database.HashSetAsync(storageKey, field, value);
        _logger.LogInformation("Redis HSET: {Key} {Field} = {Value}, Result: {Result}", storageKey, field, value, result);
        return result;
    }

    /// <summary>
    /// 取得 Hash 欄位值
    /// </summary>
    public async Task<string?> HashGetAsync(string hashKey, string field)
    {
        var storageKey = GetStorageKey(hashKey);
        var value = await _database.HashGetAsync(storageKey, field);
        _logger.LogInformation("Redis HGET: {Key} {Field} = {Value}", storageKey, field, value.HasValue ? value.ToString() : "(null)");
        return value.HasValue ? value.ToString() : null;
    }

    /// <summary>
    /// 刪除 Hash 欄位
    /// </summary>
    public async Task<bool> HashDeleteAsync(string hashKey, string field)
    {
        var storageKey = GetStorageKey(hashKey);
        var result = await _database.HashDeleteAsync(storageKey, field);
        _logger.LogInformation("Redis HDEL: {Key} {Field}, Result: {Result}", storageKey, field, result);
        return result;
    }

    /// <summary>
    /// 檢查 Hash 欄位是否存在
    /// </summary>
    public async Task<bool> HashExistsAsync(string hashKey, string field)
    {
        var storageKey = GetStorageKey(hashKey);
        var result = await _database.HashExistsAsync(storageKey, field);
        _logger.LogInformation("Redis HEXISTS: {Key} {Field} = {Result}", storageKey, field, result);
        return result;
    }

    /// <summary>
    /// 取得 Hash 所有欄位與值
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> HashGetAllAsync(string hashKey)
    {
        var storageKey = GetStorageKey(hashKey);
        var entries = await _database.HashGetAllAsync(storageKey);
        _logger.LogInformation("Redis HGETALL: {Key}, Count: {Count}", storageKey, entries.Length);
        return entries.ToDictionary(e => e.Name.ToString(), e => e.Value.ToString());
    }

    /// <summary>
    /// 取得完整的資料鍵值（加上 Key Prefix）
    /// </summary>
    private string GetStorageKey(string key)
    {
        return string.IsNullOrEmpty(_keyPrefix) ? key : $"{_keyPrefix}{key}";
    }
}
