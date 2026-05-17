using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Infrastructure.FileStorage;

/// <summary>
/// 使用實體檔案保存資料傳遞內容的服務實作
/// </summary>
public class FileDataTransferService : IDataTransferService
{
    private const string FileExtension = ".json";
    private readonly string _valueDirectoryPath;
    private readonly string _hashDirectoryPath;
    private readonly INow _now;
    private readonly ILogger<FileDataTransferService> _logger;

    public FileDataTransferService(
        string basePath,
        INow now,
        ILogger<FileDataTransferService> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);

        _now = now ?? throw new ArgumentNullException(nameof(now));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var normalizedBasePath = Path.GetFullPath(basePath);
        _valueDirectoryPath = Path.Combine(normalizedBasePath, "values");
        _hashDirectoryPath = Path.Combine(normalizedBasePath, "hashes");

        Directory.CreateDirectory(_valueDirectoryPath);
        Directory.CreateDirectory(_hashDirectoryPath);
    }

    /// <summary>
    /// 設定儲存值
    /// </summary>
    public async Task<bool> SetAsync(string key, string value, TimeSpan? expiry = null)
    {
        var path = GetValueFilePath(key);
        var entry = new FileValueEntry
        {
            Value = value,
            ExpiresAtUtc = expiry.HasValue ? _now.UtcNow.Add(expiry.Value) : null
        };

        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(entry));
        _logger.LogInformation("檔案 SET: {Path}, Expiry: {Expiry}", path, expiry);

        return true;
    }

    /// <summary>
    /// 取得儲存值
    /// </summary>
    public async Task<string?> GetAsync(string key)
    {
        var path = GetValueFilePath(key);
        if (!File.Exists(path))
        {
            return null;
        }

        var entry = await GetValidValueEntryAsync(path, "GET");
        if (entry is null)
        {
            return null;
        }

        _logger.LogInformation("檔案 GET: {Path}", path);
        return entry.Value;
    }

    /// <summary>
    /// 刪除儲存值
    /// </summary>
    public Task<bool> DeleteAsync(string key)
    {
        var path = GetValueFilePath(key);
        if (!File.Exists(path))
        {
            return Task.FromResult(false);
        }

        File.Delete(path);
        _logger.LogInformation("檔案 DELETE: {Path}", path);

        return Task.FromResult(true);
    }

    /// <summary>
    /// 檢查儲存鍵值是否存在
    /// </summary>
    public async Task<bool> ExistsAsync(string key)
    {
        var path = GetValueFilePath(key);
        if (!File.Exists(path))
        {
            return false;
        }

        var entry = await GetValidValueEntryAsync(path, "EXISTS");
        if (entry is null)
        {
            return false;
        }

        _logger.LogInformation("檔案 EXISTS: {Path}", path);
        return true;
    }

    /// <summary>
    /// 設定欄位值
    /// </summary>
    public async Task<bool> HashSetAsync(string hashKey, string field, string value)
    {
        var directoryPath = GetHashKeyDirectoryPath(hashKey);
        Directory.CreateDirectory(directoryPath);

        var path = GetHashFieldFilePath(hashKey, field);
        await File.WriteAllTextAsync(path, value);
        _logger.LogInformation("檔案 HSET: {Path}", path);

        return true;
    }

    /// <summary>
    /// 取得欄位值
    /// </summary>
    public async Task<string?> HashGetAsync(string hashKey, string field)
    {
        var path = GetHashFieldFilePath(hashKey, field);
        if (!File.Exists(path))
        {
            return null;
        }

        _logger.LogInformation("檔案 HGET: {Path}", path);
        return await File.ReadAllTextAsync(path);
    }

    /// <summary>
    /// 刪除欄位
    /// </summary>
    public Task<bool> HashDeleteAsync(string hashKey, string field)
    {
        var path = GetHashFieldFilePath(hashKey, field);
        if (!File.Exists(path))
        {
            return Task.FromResult(false);
        }

        File.Delete(path);

        var directoryPath = GetHashKeyDirectoryPath(hashKey);
        if (Directory.Exists(directoryPath) &&
            !Directory.EnumerateFileSystemEntries(directoryPath).Any())
        {
            Directory.Delete(directoryPath);
        }

        _logger.LogInformation("檔案 HDEL: {Path}", path);
        return Task.FromResult(true);
    }

    /// <summary>
    /// 檢查欄位是否存在
    /// </summary>
    public Task<bool> HashExistsAsync(string hashKey, string field)
    {
        var path = GetHashFieldFilePath(hashKey, field);
        var exists = File.Exists(path);
        _logger.LogInformation("檔案 HEXISTS: {Path} = {Exists}", path, exists);
        return Task.FromResult(exists);
    }

    /// <summary>
    /// 取得所有欄位與值
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> HashGetAllAsync(string hashKey)
    {
        var directoryPath = GetHashKeyDirectoryPath(hashKey);
        if (!Directory.Exists(directoryPath))
        {
            return new Dictionary<string, string>();
        }

        var result = new Dictionary<string, string>();
        foreach (var path in Directory.EnumerateFiles(directoryPath, $"*{FileExtension}"))
        {
            var field = DecodeToken(Path.GetFileNameWithoutExtension(path));
            result[field] = await File.ReadAllTextAsync(path);
        }

        _logger.LogInformation("檔案 HGETALL: {DirectoryPath}, Count: {Count}", directoryPath, result.Count);
        return result;
    }

    private async Task<FileValueEntry> ReadValueEntryAsync(string path)
    {
        var content = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<FileValueEntry>(content)
            ?? throw new InvalidOperationException($"無法解析檔案內容: {path}");
    }

    private async Task<FileValueEntry?> GetValidValueEntryAsync(string path, string operationName)
    {
        var entry = await ReadValueEntryAsync(path);
        if (entry.ExpiresAtUtc.HasValue && entry.ExpiresAtUtc.Value <= _now.UtcNow)
        {
            File.Delete(path);
            _logger.LogInformation("檔案 {Operation} 發現已過期資料，已刪除: {Path}", operationName, path);
            return null;
        }

        return entry;
    }

    private string GetValueFilePath(string key)
    {
        return Path.Combine(_valueDirectoryPath, $"{EncodeToken(key)}{FileExtension}");
    }

    private string GetHashKeyDirectoryPath(string hashKey)
    {
        return Path.Combine(_hashDirectoryPath, EncodeToken(hashKey));
    }

    private string GetHashFieldFilePath(string hashKey, string field)
    {
        return Path.Combine(GetHashKeyDirectoryPath(hashKey), $"{EncodeToken(field)}{FileExtension}");
    }

    private static string EncodeToken(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string DecodeToken(string value)
    {
        const int Base64BlockSize = 4;
        var normalizedValue = value
            .Replace('-', '+')
            .Replace('_', '/');

        var paddingLength = (Base64BlockSize - normalizedValue.Length % Base64BlockSize) % Base64BlockSize;
        normalizedValue = normalizedValue.PadRight(normalizedValue.Length + paddingLength, '=');
        return Encoding.UTF8.GetString(Convert.FromBase64String(normalizedValue));
    }

    private sealed class FileValueEntry
    {
        public required string Value { get; init; }

        public DateTimeOffset? ExpiresAtUtc { get; init; }
    }
}
