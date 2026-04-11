namespace ReleaseKit.Domain.ValueObjects;

/// <summary>
/// Shell 指令執行結果
/// </summary>
public sealed record ShellCommandResult
{
    /// <summary>標準輸出</summary>
    public required string StandardOutput { get; init; }

    /// <summary>標準錯誤</summary>
    public required string StandardError { get; init; }

    /// <summary>結束碼</summary>
    public required int ExitCode { get; init; }

    /// <summary>是否因超時而終止</summary>
    public required bool TimedOut { get; init; }
}
