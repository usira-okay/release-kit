using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Console.Options;
using ReleaseKit.Console.Parsers;
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

            return ConnectionMultiplexerExtensions.ConnectWithRetry(configOptions, logger);
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
    /// 註冊設定選項
    /// </summary>
    public static IServiceCollection AddConfigurationOptions(this IServiceCollection services, IConfiguration configuration)
    {
        // 註冊 FetchMode 配置（Root Level）
        services.AddOptions<ReleaseKit.Infrastructure.Configuration.FetchModeOptions>()
            .Bind(configuration);

        // 註冊 GoogleSheet 配置
        services.AddOptions<ReleaseKit.Infrastructure.Configuration.GoogleSheetOptions>()
            .Bind(configuration.GetSection("GoogleSheet"));

        // 註冊 AzureDevOps 配置
        services.AddOptions<ReleaseKit.Infrastructure.Configuration.AzureDevOpsOptions>()
            .Bind(configuration.GetSection("AzureDevOps"));

        // 註冊 GitLab 配置
        services.AddOptions<ReleaseKit.Infrastructure.Configuration.GitLabOptions>()
            .Bind(configuration.GetSection("GitLab"));

        // 註冊 Bitbucket 配置
        services.AddOptions<ReleaseKit.Infrastructure.Configuration.BitbucketOptions>()
            .Bind(configuration.GetSection("Bitbucket"));
        
        // 保留舊有的配置註冊（向下相容）
        services.Configure<GitLabOptions>(configuration.GetSection("GitLab"));
        services.Configure<BitbucketOptions>(configuration.GetSection("Bitbucket"));
        services.Configure<UserMappingOptions>(configuration.GetSection("UserMapping"));

        return services;
    }

    /// <summary>
    /// 註冊應用程式服務
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // 註冊時間服務
        services.AddSingleton<INow, SystemNow>();
        
        // 註冊任務
        services.AddTransient<FetchGitLabPullRequestsTask>();
        services.AddTransient<FetchBitbucketPullRequestsTask>();
        services.AddTransient<FetchAzureDevOpsWorkItemsTask>();
        services.AddTransient<UpdateGoogleSheetsTask>();
        
        // 註冊任務工廠
        services.AddSingleton<Application.Tasks.TaskFactory>();
        
        // 註冊命令列解析器
        services.AddSingleton<CommandLineParser>();
        
        // 註冊應用程式服務
        services.AddTransient<AppStartupService>();
        services.AddTransient<ApplicationRunner>();

        return services;
    }
}
