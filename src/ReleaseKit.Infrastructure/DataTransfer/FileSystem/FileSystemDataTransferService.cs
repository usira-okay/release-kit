using Microsoft.Extensions.Logging;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Infrastructure.DataTransfer.FileSystem;

/// <summary>
/// 以本地檔案系統實作的資料傳遞服務
/// </summary>
/// <remarks>
/// Key-Value 操作對應至 {fileDirectory}/{key} 檔案；
/// Group 操作對應至 {fileDirectory}/{groupKey}/{field} 子目錄結構。
/// expiry 參數在此實作中忽略，因 CLI 工具不需要 TTL。
/// </remarks>
public class FileSystemDataTransferService : IDataTransferService
{
    private readonly string _fileDirectory;
    private readonly ILogger<FileSystemDataTransferService> _logger;

    public FileSystemDataTransferService(
        string fileDirectory,
        ILogger<FileSystemDataTransferService> logger)
    {
        _fileDirectory = fileDirectory ?? throw new ArgumentNullException(nameof(fileDirectory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 設定指定 Key 的值（寫入 {fileDirectory}/{key}）
    /// </summary>
    public Task<bool> SetAsync(string key, string value, TimeSpan? expiry = null)
    {
        var path = GetKeyPath(key);
        EnsureDirectoryExists(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, value);
        _logger.LogInformation("DataTransfer SET: {Path}", path);
        return Task.FromResult(true);
    }

    /// <summary>
    /// 取得指定 Key 的值（讀取 {fileDirectory}/{key}）
    /// </summary>
    public Task<string?> GetAsync(string key)
    {
        var path = GetKeyPath(key);
        if (!File.Exists(path))
        {
            _logger.LogInformation("DataTransfer GET: {Path} = (null)", path);
            return Task.FromResult<string?>(null);
        }

        var value = File.ReadAllText(path);
        _logger.LogInformation("DataTransfer GET: {Path}", path);
        return Task.FromResult<string?>(value);
    }

    /// <summary>
    /// 刪除指定 Key（刪除 {fileDirectory}/{key}）
    /// </summary>
    public Task<bool> DeleteAsync(string key)
    {
        var path = GetKeyPath(key);
        if (!File.Exists(path))
        {
            _logger.LogInformation("DataTransfer DELETE: {Path} (not found)", path);
            return Task.FromResult(false);
        }

        File.Delete(path);
        _logger.LogInformation("DataTransfer DELETE: {Path}", path);
        return Task.FromResult(true);
    }

    /// <summary>
    /// 檢查指定 Key 是否存在（檢查 {fileDirectory}/{key}）
    /// </summary>
    public Task<bool> ExistsAsync(string key)
    {
        var path = GetKeyPath(key);
        var exists = File.Exists(path);
        _logger.LogInformation("DataTransfer EXISTS: {Path} = {Exists}", path, exists);
        return Task.FromResult(exists);
    }

    /// <summary>
    /// 設定群組欄位（寫入 {fileDirectory}/{groupKey}/{field}）
    /// </summary>
    public Task<bool> GroupSetAsync(string groupKey, string field, string value)
    {
        var path = GetGroupFieldPath(groupKey, field);
        EnsureDirectoryExists(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, value);
        _logger.LogInformation("DataTransfer GROUP-SET: {GroupKey}/{Field}", groupKey, field);
        return Task.FromResult(true);
    }

    /// <summary>
    /// 取得群組欄位（讀取 {fileDirectory}/{groupKey}/{field}）
    /// </summary>
    public Task<string?> GroupGetAsync(string groupKey, string field)
    {
        var path = GetGroupFieldPath(groupKey, field);
        if (!File.Exists(path))
        {
            _logger.LogInformation("DataTransfer GROUP-GET: {GroupKey}/{Field} = (null)", groupKey, field);
            return Task.FromResult<string?>(null);
        }

        var value = File.ReadAllText(path);
        _logger.LogInformation("DataTransfer GROUP-GET: {GroupKey}/{Field}", groupKey, field);
        return Task.FromResult<string?>(value);
    }

    /// <summary>
    /// 刪除群組欄位（刪除 {fileDirectory}/{groupKey}/{field}）
    /// </summary>
    public Task<bool> GroupDeleteAsync(string groupKey, string field)
    {
        var path = GetGroupFieldPath(groupKey, field);
        if (!File.Exists(path))
        {
            _logger.LogInformation("DataTransfer GROUP-DELETE: {GroupKey}/{Field} (not found)", groupKey, field);
            return Task.FromResult(false);
        }

        File.Delete(path);
        _logger.LogInformation("DataTransfer GROUP-DELETE: {GroupKey}/{Field}", groupKey, field);
        return Task.FromResult(true);
    }

    /// <summary>
    /// 檢查群組欄位是否存在（檢查 {fileDirectory}/{groupKey}/{field}）
    /// </summary>
    public Task<bool> GroupExistsAsync(string groupKey, string field)
    {
        var path = GetGroupFieldPath(groupKey, field);
        var exists = File.Exists(path);
        _logger.LogInformation("DataTransfer GROUP-EXISTS: {GroupKey}/{Field} = {Exists}", groupKey, field, exists);
        return Task.FromResult(exists);
    }

    /// <summary>
    /// 取得群組所有欄位（列舉 {fileDirectory}/{groupKey}/ 目錄內所有檔案）
    /// </summary>
    public Task<IReadOnlyDictionary<string, string>> GroupGetAllAsync(string groupKey)
    {
        var groupDir = Path.Combine(_fileDirectory, groupKey);
        if (!Directory.Exists(groupDir))
        {
            _logger.LogInformation("DataTransfer GROUP-GETALL: {GroupKey} (not found)", groupKey);
            return Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string>());
        }

        var result = Directory
            .GetFiles(groupDir)
            .ToDictionary(
                p => Path.GetFileName(p)!,
                p => File.ReadAllText(p));

        _logger.LogInformation("DataTransfer GROUP-GETALL: {GroupKey}, Count: {Count}", groupKey, result.Count);
        return Task.FromResult<IReadOnlyDictionary<string, string>>(result);
    }

    private string GetKeyPath(string key) => Path.Combine(_fileDirectory, key);

    private string GetGroupFieldPath(string groupKey, string field) =>
        Path.Combine(_fileDirectory, groupKey, field);

    private static void EnsureDirectoryExists(string directory)
    {
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);
    }
}
