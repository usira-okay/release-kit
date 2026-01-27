using Microsoft.Extensions.Configuration;

// 建立組態建構器
var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>(optional: true)
    .Build();

// 顯示應用程式資訊
var appName = configuration["Application:Name"];
var appVersion = configuration["Application:Version"];
var appEnvironment = configuration["Application:Environment"] ?? environment;

Console.WriteLine("========================================");
Console.WriteLine($"應用程式: {appName}");
Console.WriteLine($"版本: {appVersion}");
Console.WriteLine($"環境: {appEnvironment}");
Console.WriteLine("========================================");
Console.WriteLine();

// 顯示設定檔載入狀態
var environmentConfigPath = Path.Combine(Directory.GetCurrentDirectory(), $"appsettings.{environment}.json");
var environmentConfigExists = File.Exists(environmentConfigPath);

Console.WriteLine("組態載入完成:");
Console.WriteLine($"- appsettings.json: 已載入");
Console.WriteLine($"- appsettings.{environment}.json: {(environmentConfigExists ? "已載入" : "未找到")}");
Console.WriteLine($"- 環境變數: 已啟用");
Console.WriteLine($"- User Secrets: 已啟用");
Console.WriteLine();

Console.WriteLine("Hello, World!");
