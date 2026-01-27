using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReleaseKit.Console.Services;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Infrastructure.Redis;
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
        var redisConnectionString = configuration["Redis:ConnectionString"] ?? "localhost:6379";
        var redisInstanceName = configuration["Redis:InstanceName"] ?? "ReleaseKit:";

        // 使用 Lazy 延遲初始化 Redis 連線，確保執行緒安全
        services.AddSingleton<Lazy<IConnectionMultiplexer>>(sp =>
            new Lazy<IConnectionMultiplexer>(
                () => ConnectionMultiplexer.Connect(redisConnectionString),
                LazyThreadSafetyMode.ExecutionAndPublication));

        // 註冊 IConnectionMultiplexer，從 Lazy 取得實例
        services.AddSingleton<IConnectionMultiplexer>(sp =>
            sp.GetRequiredService<Lazy<IConnectionMultiplexer>>().Value);

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
        services.AddTransient<AppStartupService>();
        services.AddTransient<RedisTestService>();

        return services;
    }
}
