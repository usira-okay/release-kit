using Microsoft.Extensions.Logging;
using Serilog;

namespace ReleaseKit.Console.Services;

/// <summary>
/// 應用程式執行器，負責協調應用程式啟動流程
/// </summary>
public class ApplicationRunner
{
    private readonly AppStartupService _appStartupService;
    private readonly ILogger<ApplicationRunner> _logger;

    public ApplicationRunner(
        AppStartupService appStartupService,
        ILogger<ApplicationRunner> logger)
    {
        _appStartupService = appStartupService ?? throw new ArgumentNullException(nameof(appStartupService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 執行應用程式
    /// </summary>
    public async Task RunAsync()
    {
        _appStartupService.Run();

        _logger.LogInformation("應用程式執行完成");
        await Log.CloseAndFlushAsync();
    }
}
