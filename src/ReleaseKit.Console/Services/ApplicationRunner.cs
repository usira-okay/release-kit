using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace ReleaseKit.Console.Services;

/// <summary>
/// 應用程式執行器，負責協調應用程式啟動流程
/// </summary>
public class ApplicationRunner
{
    private readonly IHost _host;

    public ApplicationRunner(IHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    /// <summary>
    /// 執行應用程式
    /// </summary>
    public async Task RunAsync()
    {
        // 取得應用程式啟動服務並執行
        var appStartupService = _host.Services.GetRequiredService<AppStartupService>();
        appStartupService.Run();

        Log.Information("應用程式執行完成");
        await Log.CloseAndFlushAsync();
    }
}
