using Microsoft.Extensions.Logging;
using ReleaseKit.Console.Parsers;
using Serilog;
using AppTaskFactory = ReleaseKit.Application.Tasks.TaskFactory;

namespace ReleaseKit.Console.Services;

/// <summary>
/// 應用程式執行器，負責協調應用程式啟動流程
/// </summary>
public class ApplicationRunner
{
    private readonly AppStartupService _appStartupService;
    private readonly CommandLineParser _commandLineParser;
    private readonly AppTaskFactory _taskFactory;
    private readonly ILogger<ApplicationRunner> _logger;

    public ApplicationRunner(
        AppStartupService appStartupService,
        CommandLineParser commandLineParser,
        AppTaskFactory taskFactory,
        ILogger<ApplicationRunner> logger)
    {
        _appStartupService = appStartupService ?? throw new ArgumentNullException(nameof(appStartupService));
        _commandLineParser = commandLineParser ?? throw new ArgumentNullException(nameof(commandLineParser));
        _taskFactory = taskFactory ?? throw new ArgumentNullException(nameof(taskFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 執行應用程式
    /// </summary>
    /// <param name="args">命令列參數</param>
    public async Task RunAsync(string[] args)
    {
        _appStartupService.DisplayConfigurationStatus();
        
        // 解析命令列參數
        var parseResult = _commandLineParser.Parse(args);
        
        if (!parseResult.IsSuccess)
        {
            _logger.LogError("命令列參數解析失敗: {ErrorMessage}", parseResult.ErrorMessage);
            Environment.ExitCode = 1;
            await Log.CloseAndFlushAsync();
            return;
        }

        _logger.LogInformation("準備執行任務: {TaskType}", parseResult.TaskType);

        try
        {
            // 使用工廠模式建立任務並執行
            var task = _taskFactory.CreateTask(parseResult.TaskType!.Value);
            await task.ExecuteAsync();
            
            _logger.LogInformation("任務執行完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "任務執行失敗");
            Environment.ExitCode = 1;
        }

        await Log.CloseAndFlushAsync();
    }
}
