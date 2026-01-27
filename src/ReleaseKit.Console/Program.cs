using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReleaseKit.Console.Services;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Infrastructure.Redis;
using Serilog;
using StackExchange.Redis;

// 設定 Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information("正在啟動 Release-Kit 應用程式...");

// 建立 Host Builder 以使用 DI 容器
var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        
        config
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddUserSecrets<Program>(optional: true);
    })
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        // 註冊 Redis 連線
        var redisConnectionString = configuration["Redis:ConnectionString"] ?? "localhost:6379";
        var redisInstanceName = configuration["Redis:InstanceName"] ?? "ReleaseKit:";
        
        services.AddSingleton<IConnectionMultiplexer>(sp => 
            ConnectionMultiplexer.Connect(redisConnectionString));
        
        services.AddSingleton<IRedisService>(sp =>
        {
            var connectionMultiplexer = sp.GetRequiredService<IConnectionMultiplexer>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RedisService>>();
            return new RedisService(connectionMultiplexer, logger, redisInstanceName);
        });

        // 註冊應用程式服務
        services.AddTransient<AppStartupService>();
        services.AddTransient<RedisTestService>();
    })
    .UseSerilog((context, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .WriteTo.Console();

        var seqServerUrl = context.Configuration["Seq:ServerUrl"];
        var seqApiKey = context.Configuration["Seq:ApiKey"];

        if (!string.IsNullOrEmpty(seqServerUrl))
        {
            configuration.WriteTo.Seq(seqServerUrl, apiKey: seqApiKey);
            Log.Information("Seq 日誌已啟用: {SeqUrl}", seqServerUrl);
        }
    })
    .Build();

// 使用 DI 容器取得服務並執行
var app = host.Services.GetRequiredService<AppStartupService>();
app.Run();

// 執行 Redis 測試
var redisTest = host.Services.GetRequiredService<RedisTestService>();
await redisTest.RunTestAsync();

Log.Information("應用程式執行完成");
await Log.CloseAndFlushAsync();

