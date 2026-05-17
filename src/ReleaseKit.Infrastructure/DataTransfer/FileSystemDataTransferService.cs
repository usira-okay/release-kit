using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Infrastructure.DataTransfer;

/// <summary>
/// 以檔案系統作為指令間資料交換媒介的實作
/// </summary>
public class FileSystemDataTransferService : IDataTransferService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new() { WriteIndented = false };
    private readonly ILogger<FileSystemDataTransferService> _logger;
    private readonly string _kvDirectory;
    private readonly string _hashDirectory;

    public FileSystemDataTransferService(string rootDirectory, ILogger<FileSystemDataTransferService> logger)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new ArgumentException("資料交換目錄不得為空", nameof(rootDirectory));
        }

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _kvDirectory = Path.Combine(rootDirectory, "kv");
        _hashDirectory = Path.Combine(rootDirectory, "hash");

        Directory.CreateDirectory(_kvDirectory);
        Directory.CreateDirectory(_hashDirectory);
    }

    public async Task<bool> SetValueAsync(string key, string value, TimeSpan? expiry = null)
    {
        if (expiry.HasValue)
        {
            _logger.LogWarning("FileSystem 提供者不支援 expiry，將忽略此參數，Key: {Key}", key);
        }

        var filePath = GetValueFilePath(key);
        await File.WriteAllTextAsync(filePath, value, Encoding.UTF8);
        return true;
    }

    public async Task<string?> GetValueAsync(string key)
    {
        var filePath = GetValueFilePath(key);
        if (!File.Exists(filePath))
        {
            return null;
        }

        return await File.ReadAllTextAsync(filePath, Encoding.UTF8);
    }

    public Task<bool> DeleteValueAsync(string key)
    {
        var filePath = GetValueFilePath(key);
        if (!File.Exists(filePath))
        {
            return Task.FromResult(false);
        }

        File.Delete(filePath);
        return Task.FromResult(true);
    }

    public Task<bool> ExistsValueAsync(string key)
    {
        return Task.FromResult(File.Exists(GetValueFilePath(key)));
    }

    public async Task<bool> SetFieldAsync(string hashKey, string field, string value)
    {
        var hashData = await ReadHashAsync(hashKey);
        hashData[field] = value;
        await WriteHashAsync(hashKey, hashData);
        return true;
    }

    public async Task<string?> GetFieldAsync(string hashKey, string field)
    {
        var hashData = await ReadHashAsync(hashKey);
        return hashData.TryGetValue(field, out var value) ? value : null;
    }

    public async Task<bool> DeleteFieldAsync(string hashKey, string field)
    {
        var hashData = await ReadHashAsync(hashKey);
        if (!hashData.Remove(field))
        {
            return false;
        }

        await WriteHashAsync(hashKey, hashData);
        return true;
    }

    public async Task<bool> FieldExistsAsync(string hashKey, string field)
    {
        var hashData = await ReadHashAsync(hashKey);
        return hashData.ContainsKey(field);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllFieldsAsync(string hashKey)
    {
        var hashData = await ReadHashAsync(hashKey);
        return hashData;
    }

    private async Task<Dictionary<string, string>> ReadHashAsync(string hashKey)
    {
        var filePath = GetHashFilePath(hashKey);
        if (!File.Exists(filePath))
        {
            return new Dictionary<string, string>();
        }

        var json = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>();
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonSerializerOptions)
            ?? new Dictionary<string, string>();
    }

    private async Task WriteHashAsync(string hashKey, Dictionary<string, string> hashData)
    {
        var filePath = GetHashFilePath(hashKey);
        var json = JsonSerializer.Serialize(hashData, JsonSerializerOptions);
        await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
    }

    private string GetValueFilePath(string key)
    {
        return Path.Combine(_kvDirectory, $"{EncodeKey(key)}.txt");
    }

    private string GetHashFilePath(string hashKey)
    {
        return Path.Combine(_hashDirectory, $"{EncodeKey(hashKey)}.json");
    }

    private static string EncodeKey(string key)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(key))
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
