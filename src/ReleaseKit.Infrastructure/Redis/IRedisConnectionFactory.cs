using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ReleaseKit.Infrastructure.Redis;

/// <summary>
/// Redis 連線工廠介面
/// </summary>
public interface IRedisConnectionFactory
{
    /// <summary>
    /// 建立 Redis 連線
    /// </summary>
    /// <param name="connectionString">Redis 連線字串</param>
    /// <param name="logger">日誌記錄器</param>
    /// <returns>Redis 連線</returns>
    IConnectionMultiplexer Create(string connectionString, ILogger logger);
}
