using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

// 建立組態建構器
var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>(optional: true)
    .Build();

// 建立 Logger Factory
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddConfiguration(configuration.GetSection("Logging"))
        .AddConsole();
});

var logger = loggerFactory.CreateLogger<Program>();

// 顯示應用程式資訊
var appName = configuration["Application:Name"];
var appVersion = configuration["Application:Version"];
var appEnvironment = configuration["Application:Environment"] ?? environment;

logger.LogInformation("========================================");
logger.LogInformation("應用程式: {AppName}", appName);
logger.LogInformation("版本: {AppVersion}", appVersion);
logger.LogInformation("環境: {AppEnvironment}", appEnvironment);
logger.LogInformation("========================================");

// 顯示設定檔載入狀態
var environmentConfigPath = Path.Combine(Directory.GetCurrentDirectory(), $"appsettings.{environment}.json");
var environmentConfigExists = File.Exists(environmentConfigPath);

logger.LogInformation("組態載入完成:");
logger.LogInformation("- appsettings.json: 已載入");
logger.LogInformation("- appsettings.{Environment}.json: {Status}", environment, environmentConfigExists ? "已載入" : "未找到");
logger.LogInformation("- 環境變數: 已啟用");
logger.LogInformation("- User Secrets: 已啟用");

logger.LogInformation("Hello, World!");
