using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Abstractions;

/// <summary>
/// Shell 指令執行器介面（供測試 Mock 使用）
/// </summary>
public interface IShellCommandExecutor
{
    /// <summary>在指定工作目錄執行 shell 指令</summary>
    /// <param name="command">要執行的 shell 指令</param>
    /// <param name="workingDirectory">工作目錄路徑</param>
    /// <param name="timeout">超時時間</param>
    /// <param name="cancellationToken">取消權杖</param>
    /// <returns>指令執行結果</returns>
    Task<ShellCommandResult> ExecuteAsync(
        string command,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
