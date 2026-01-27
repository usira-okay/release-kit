using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReleaseKit.Console.Constants;
using ReleaseKit.Console.Extensions;
using ReleaseKit.Console.Services;
using Serilog;

// 設定 Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Is(LogLevelConstants.DefaultMinimumLevel)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information("正在啟動 Release-Kit 應用程式...");

// 建立 Host Builder 以使用 DI 容器
var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        var env = Environment.GetEnvironmentVariable(EnvironmentVariableNames.AspNetCoreEnvironment) ?? EnvironmentNames.Development;
        
        config
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddUserSecrets<Program>(optional: true);
    })
    .ConfigureServices((context, services) =>
    {
        // 註冊組態設定
        services.AddConfigurationOptions(context.Configuration);

        // 註冊 Redis 服務
        services.AddRedisServices(context.Configuration);

        // 註冊應用程式服務
        services.AddApplicationServices();
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

// 從 DI 容器取得應用程式執行器並執行
var runner = host.Services.GetRequiredService<ApplicationRunner>();
await runner.RunAsync(args);

