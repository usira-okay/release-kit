using Serilog.Events;

namespace ReleaseKit.Console.Constants;

/// <summary>
/// 日誌層級常數
/// </summary>
public static class LogLevelConstants
{
    /// <summary>
    /// 預設最低日誌層級
    /// </summary>
    public const LogEventLevel DefaultMinimumLevel = LogEventLevel.Debug;
}
