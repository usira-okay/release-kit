using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReleaseKit.Console.Services;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Infrastructure.Redis;
using ReleaseKit.Infrastructure.Time;
using StackExchange.Redis;

namespace ReleaseKit.Console.Extensions;

/// <summary>
/// 服務註冊擴充方法
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 註冊 Redis 相關服務
    /// </summary>
    public static IServiceCollection AddRedisServices(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnectionString = configuration["Redis:ConnectionString"] 
            ?? throw new InvalidOperationException("Redis:ConnectionString 組態設定不得為空");
        var redisInstanceName = configuration["Redis:InstanceName"] 
            ?? throw new InvalidOperationException("Redis:InstanceName 組態設定不得為空");

        // 註冊 IConnectionMultiplexer，使用指數級重試機制
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<IConnectionMultiplexer>>();
            var configOptions = ConfigurationOptions.Parse(redisConnectionString);
            configOptions.AbortOnConnectFail = false; // 允許應用程式啟動即使 Redis 尚未就緒

            const int maxRetries = 5;
            var baseDelay = TimeSpan.FromSeconds(1);

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
        });

        // 註冊 Redis 服務
        services.AddSingleton<IRedisService>(sp =>
        {
            var connectionMultiplexer = sp.GetRequiredService<IConnectionMultiplexer>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RedisService>>();
            return new RedisService(connectionMultiplexer, logger, redisInstanceName);
        });

        return services;
    }

    /// <summary>
    /// 註冊應用程式服務
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // 註冊時間服務
        services.AddSingleton<INow, SystemNow>();
        
        // 註冊應用程式服務
        services.AddTransient<AppStartupService>();
        services.AddTransient<ApplicationRunner>();

        return services;
    }
}
