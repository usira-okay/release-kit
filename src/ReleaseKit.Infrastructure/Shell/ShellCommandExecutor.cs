using System.Diagnostics;
using System.Runtime.InteropServices;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Infrastructure.Shell;

/// <summary>
/// Shell 指令執行器實作
/// </summary>
/// <remarks>
/// 透過 Process.Start 執行 shell 指令，支援超時控制與路徑安全驗證。
/// </remarks>
public class ShellCommandExecutor : IShellCommandExecutor
{
    private readonly string? _allowedBasePath;

    /// <summary>
    /// 初始化 <see cref="ShellCommandExecutor"/> 類別的新執行個體
    /// </summary>
    /// <param name="allowedBasePath">
    /// 允許的工作目錄基底路徑（可選）。
    /// 若提供，所有指令的工作目錄必須在此路徑下，防止路徑逃逸。
    /// </param>
    public ShellCommandExecutor(string? allowedBasePath = null)
    {
        _allowedBasePath = allowedBasePath;
    }

    /// <summary>在指定工作目錄執行 shell 指令</summary>
    public async Task<ShellCommandResult> ExecuteAsync(
        string command,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ValidateWorkingDirectory(workingDirectory);

        var (shellFileName, shellArgs) = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ("cmd.exe", $"/c \"{command.Replace("\"", "\\\"")}\"")
            : ("/bin/bash", $"-c \"{command.Replace("\"", "\\\"")}\"");

        var startInfo = new ProcessStartInfo
        {
            FileName = shellFileName,
            Arguments = shellArgs,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        // 使用外部取消 token（非超時 token）讀取串流，確保超時後仍可收集部分輸出
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(cts.Token);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            return new ShellCommandResult
            {
                StandardOutput = stdout,
                StandardError = stderr,
                ExitCode = process.ExitCode,
                TimedOut = false
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // 超時而非外部取消：終止程序並收集部分輸出
            SafeKillProcess(process);

            var partialStdout = "";
            var partialStderr = "";
            try { partialStdout = await stdoutTask; } catch (OperationCanceledException) { }
            try { partialStderr = await stderrTask; } catch (OperationCanceledException) { }

            return new ShellCommandResult
            {
                StandardOutput = partialStdout,
                StandardError = string.IsNullOrEmpty(partialStderr)
                    ? "指令執行超時"
                    : $"指令執行超時\n{partialStderr}",
                ExitCode = -1,
                TimedOut = true
            };
        }
    }

    /// <summary>驗證工作目錄是否在允許的基底路徑下</summary>
    private void ValidateWorkingDirectory(string workingDirectory)
    {
        if (_allowedBasePath is null)
            return;

        var normalizedBase = Path.GetFullPath(_allowedBasePath);
        var normalizedDir = Path.GetFullPath(workingDirectory);

        var relativePath = Path.GetRelativePath(normalizedBase, normalizedDir);
        if (relativePath.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException(
                $"工作目錄 '{workingDirectory}' 不在允許的路徑 '{_allowedBasePath}' 下",
                nameof(workingDirectory));
        }
    }

    /// <summary>安全終止程序</summary>
    private static void SafeKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // 程序已結束，忽略
        }
    }
}
