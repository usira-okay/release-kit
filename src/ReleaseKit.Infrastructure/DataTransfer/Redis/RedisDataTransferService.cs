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
