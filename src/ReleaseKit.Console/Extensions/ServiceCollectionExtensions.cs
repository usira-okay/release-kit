using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Common.Constants;
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
        services.Configure<ReleaseKit.Common.Configuration.FetchModeOptions>(configuration);

        // 註冊 GoogleSheet 配置
        services.Configure<ReleaseKit.Infrastructure.Configuration.GoogleSheetOptions>(configuration.GetSection("GoogleSheet"));

        // 註冊 AzureDevOps 配置
        services.Configure<ReleaseKit.Infrastructure.Configuration.AzureDevOpsOptions>(configuration.GetSection("AzureDevOps"));

        // 註冊 GitLab 配置
        services.Configure<ReleaseKit.Common.Configuration.GitLabOptions>(configuration.GetSection("GitLab"));

        // 註冊 Bitbucket 配置
        services.Configure<ReleaseKit.Common.Configuration.BitbucketOptions>(configuration.GetSection("Bitbucket"));

        // 註冊 UserMapping 配置
        services.Configure<UserMappingOptions>(configuration.GetSection("UserMapping"));

        return services;
    }

    /// <summary>
    /// 註冊 HttpClient 服務
    /// </summary>
    public static IServiceCollection AddHttpClientServices(this IServiceCollection services, IConfiguration configuration)
    {
        // 註冊 GitLab HttpClient
        services.AddHttpClient(HttpClientNames.GitLab, (sp, client) =>
        {
            var gitLabSection = configuration.GetSection("GitLab");
            var gitLabOptions = gitLabSection.Get<ReleaseKit.Common.Configuration.GitLabOptions>();

            // 依 AGENTS.md 規範，必要組態不提供預設值，缺失時應立即拋出例外並指出缺少的組態鍵
            if (gitLabOptions == null || string.IsNullOrWhiteSpace(gitLabOptions.ApiUrl))
            {
                throw new InvalidOperationException("缺少必要的組態鍵: GitLab:ApiUrl");
            }

            if (string.IsNullOrWhiteSpace(gitLabOptions.AccessToken))
            {
                throw new InvalidOperationException("缺少必要的組態鍵: GitLab:AccessToken");
            }

            var apiUri = new Uri(gitLabOptions.ApiUrl);

            // 確保使用 HTTPS
            if (apiUri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException(
                    $"GitLab API URL 必須使用 HTTPS 協定。目前使用的協定: {apiUri.Scheme}");
            }

            client.BaseAddress = apiUri;

            client.DefaultRequestHeaders.Add("PRIVATE-TOKEN", gitLabOptions.AccessToken);
        });

        // 註冊 Bitbucket HttpClient
        services.AddHttpClient(HttpClientNames.Bitbucket, (sp, client) =>
        {
            var bitbucketSection = configuration.GetSection("Bitbucket");
            var bitbucketOptions = bitbucketSection.Get<ReleaseKit.Common.Configuration.BitbucketOptions>();

            // 依 AGENTS.md 規範，必要組態不提供預設值，缺失時應立即拋出例外並指出缺少的組態鍵
            if (bitbucketOptions == null || string.IsNullOrWhiteSpace(bitbucketOptions.ApiUrl))
            {
                throw new InvalidOperationException("缺少必要的組態鍵: Bitbucket:ApiUrl");
            }

            if (string.IsNullOrWhiteSpace(bitbucketOptions.AccessToken))
            {
                throw new InvalidOperationException("缺少必要的組態鍵: Bitbucket:AccessToken");
            }

            if (string.IsNullOrWhiteSpace(bitbucketOptions.Email))
            {
                throw new InvalidOperationException("缺少必要的組態鍵: Bitbucket:Email");
            }

            var apiUri = new Uri(bitbucketOptions.ApiUrl);

            // 確保使用 HTTPS
            if (apiUri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException(
                    $"Bitbucket API URL 必須使用 HTTPS 協定。目前使用的協定: {apiUri.Scheme}");
            }

            client.BaseAddress = apiUri;

            var credentials = Convert.ToBase64String(
                            System.Text.Encoding.ASCII.GetBytes($"{bitbucketOptions.Email}:{bitbucketOptions.AccessToken}"));
            
            // 設定存取權杖；詳細格式與有效性交由 Bitbucket API 驗證
            client.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
        });

        return services;
    }

    /// <summary>
    /// 註冊應用程式服務
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // 註冊 HttpClient 服務（Repositories 依賴 IHttpClientFactory）
        services.AddHttpClientServices(configuration);
        
        // 註冊時間服務
        services.AddSingleton<INow, SystemNow>();
        
        // 註冊 Source Control Repositories
        services.AddKeyedTransient<ReleaseKit.Domain.Abstractions.ISourceControlRepository, 
            ReleaseKit.Infrastructure.SourceControl.GitLab.GitLabRepository>(HttpClientNames.GitLab);
        services.AddKeyedTransient<ReleaseKit.Domain.Abstractions.ISourceControlRepository, 
            ReleaseKit.Infrastructure.SourceControl.Bitbucket.BitbucketRepository>(HttpClientNames.Bitbucket);
        
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
