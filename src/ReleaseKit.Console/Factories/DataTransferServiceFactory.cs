using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Infrastructure.FileStorage;
using ReleaseKit.Infrastructure.Redis;

namespace ReleaseKit.Console.Factories;

/// <summary>
/// 資料傳遞服務工廠
/// </summary>
public class DataTransferServiceFactory
{
    private const string RedisProvider = "Redis";
    private const string FileSystemProvider = "FileSystem";
    private readonly IConfiguration _configuration;
    private readonly INow _now;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IRedisConnectionFactory _redisConnectionFactory;

    public DataTransferServiceFactory(
        IConfiguration configuration,
        INow now,
        ILoggerFactory loggerFactory,
        IRedisConnectionFactory redisConnectionFactory)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _now = now ?? throw new ArgumentNullException(nameof(now));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _redisConnectionFactory = redisConnectionFactory ?? throw new ArgumentNullException(nameof(redisConnectionFactory));
    }

    /// <summary>
    /// 依組態建立資料傳遞服務
    /// </summary>
    public IDataTransferService Create()
    {
        var provider = _configuration["DataTransfer:Provider"]
            ?? throw new InvalidOperationException("DataTransfer:Provider 組態設定不得為空");

        if (string.Equals(provider, RedisProvider, StringComparison.OrdinalIgnoreCase))
        {
            return CreateRedisService();
        }

        if (string.Equals(provider, FileSystemProvider, StringComparison.OrdinalIgnoreCase))
        {
            return CreateFileSystemService();
        }

        throw new InvalidOperationException(
            $"DataTransfer:Provider 組態設定僅支援 {RedisProvider} 或 {FileSystemProvider}，目前值: {provider}");
    }

    private IDataTransferService CreateRedisService()
    {
        var redisConnectionString = _configuration["Redis:ConnectionString"]
            ?? throw new InvalidOperationException("Redis:ConnectionString 組態設定不得為空");
        var redisInstanceName = _configuration["Redis:InstanceName"]
            ?? throw new InvalidOperationException("Redis:InstanceName 組態設定不得為空");

        var logger = _loggerFactory.CreateLogger<RedisService>();
        var connectionLogger = _loggerFactory.CreateLogger<RedisConnectionFactory>();
        var redisConnection = _redisConnectionFactory.Create(redisConnectionString, connectionLogger);

        return new RedisService(redisConnection, logger, redisInstanceName);
    }

    private IDataTransferService CreateFileSystemService()
    {
        var fileStorageBasePath = _configuration["FileStorage:BasePath"]
            ?? throw new InvalidOperationException("FileStorage:BasePath 組態設定不得為空");
        var normalizedBasePath = Path.GetFullPath(fileStorageBasePath);
        Directory.CreateDirectory(normalizedBasePath);

        var logger = _loggerFactory.CreateLogger<FileDataTransferService>();
        return new FileDataTransferService(normalizedBasePath, _now, logger);
    }
}
