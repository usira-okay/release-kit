using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Console.Parsers;
using ReleaseKit.Console.Services;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Infrastructure.Redis;
using ReleaseKit.Infrastructure.SourceControl.GitLab;
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
    /// 註冊 GitLab 相關服務
    /// </summary>
    public static IServiceCollection AddGitLabServices(this IServiceCollection services, IConfiguration configuration)
    {
        // 綁定 GitLab 設定
        var gitLabSettings = new GitLabSettings
        {
            Domain = configuration["GitLab:Domain"] 
                ?? throw new InvalidOperationException("GitLab:Domain 組態設定不得為空"),
            AccessToken = configuration["GitLab:AccessToken"] 
                ?? throw new InvalidOperationException("GitLab:AccessToken 組態設定不得為空")
        };
        
        // 讀取專案列表
        var projectsSection = configuration.GetSection("GitLab:Projects");
        if (projectsSection.Exists())
        {
            gitLabSettings = new GitLabSettings
            {
                Domain = gitLabSettings.Domain,
                AccessToken = gitLabSettings.AccessToken,
                Projects = projectsSection.Get<List<GitLabProjectSettings>>() ?? new List<GitLabProjectSettings>()
            };
        }
        
        // 註冊設定為單例
        services.AddSingleton(gitLabSettings);

        // 註冊 GitLab HttpClient
        services.AddHttpClient<IGitLabRepository, GitLabRepository>(client =>
        {
            client.BaseAddress = new Uri(gitLabSettings.Domain);
            client.DefaultRequestHeaders.Add("PRIVATE-TOKEN", gitLabSettings.AccessToken);
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
