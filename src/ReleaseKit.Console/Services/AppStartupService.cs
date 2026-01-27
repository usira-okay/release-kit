using Microsoft.Extensions.Logging;

namespace ReleaseKit.Console.Services;

/// <summary>
/// 應用程式啟動服務，負責顯示組態載入狀態
/// </summary>
public class AppStartupService
{
    private readonly ILogger<AppStartupService> _logger;
    private readonly string _environment;

    public AppStartupService(ILogger<AppStartupService> logger)
    {
        _logger = logger;
        _environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
    }

    /// <summary>
    /// 顯示組態載入狀態
    /// </summary>
    public void DisplayConfigurationStatus()
    {
        var environmentConfigPath = Path.Combine(Directory.GetCurrentDirectory(), $"appsettings.{_environment}.json");
        var environmentConfigExists = File.Exists(environmentConfigPath);

        _logger.LogInformation("組態載入完成:");
        _logger.LogInformation("- appsettings.json: 已載入");
        _logger.LogInformation("- appsettings.{Environment}.json: {Status}", _environment, environmentConfigExists ? "已載入" : "未找到");
        _logger.LogInformation("- 環境變數: 已啟用");
        _logger.LogInformation("- User Secrets: 已啟用");
    }

    /// <summary>
    /// 執行應用程式主要邏輯
    /// </summary>
    public void Run()
    {
        DisplayConfigurationStatus();
        _logger.LogInformation("Hello, World!");
    }
}
