using System;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ReleaseKit.Console.Extensions;

/// <summary>
/// IConnectionMultiplexer 擴充方法
/// </summary>
public static class ConnectionMultiplexerExtensions
{
    /// <summary>
    /// 使用指數級退避策略連線至 Redis
    /// </summary>
    /// <param name="configOptions">Redis 連線設定</param>
    /// <param name="logger">日誌記錄器</param>
    /// <param name="maxRetries">最大重試次數，預設為 5 次</param>
    /// <param name="baseDelaySeconds">初始延遲秒數，預設為 1 秒</param>
    /// <returns>已連線的 IConnectionMultiplexer 實例</returns>
    /// <exception cref="InvalidOperationException">達到最大重試次數仍無法連線時拋出</exception>
    public static IConnectionMultiplexer ConnectWithRetry(
        ConfigurationOptions configOptions,
        ILogger logger,
        int maxRetries = 5,
        int baseDelaySeconds = 1)
    {
        var baseDelay = TimeSpan.FromSeconds(baseDelaySeconds);

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            if (attempt > 0)
            {
                var delay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
                logger.LogWarning("Redis 連線失敗，等待 {Delay}ms 後重試 (嘗試 {Attempt}/{MaxRetries})", 
                    delay.TotalMilliseconds, attempt, maxRetries);
                Thread.Sleep(delay);
            }

            var connection = ConnectionMultiplexer.Connect(configOptions);
            if (connection.IsConnected)
            {
                logger.LogInformation("Redis 連線成功 (嘗試 {Attempt}/{MaxRetries})", attempt + 1, maxRetries + 1);
                return connection;
            }

            if (attempt < maxRetries)
            {
                logger.LogWarning("Redis 連線未就緒，準備重試");
            }
        }

        logger.LogError("Redis 連線失敗，已達最大重試次數 {MaxRetries}", maxRetries);
        throw new InvalidOperationException($"無法連線至 Redis，已重試 {maxRetries} 次");
    }
}
