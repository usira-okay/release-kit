using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Common;

namespace ReleaseKit.Infrastructure.Git;

/// <summary>
/// Git 操作服務實作
/// </summary>
/// <remarks>
/// 透過 System.Diagnostics.Process 執行 git 命令，
/// 提供 Clone、Diff、Remote URL 等操作。
/// </remarks>
public sealed class GitService : IGitService
{
    private readonly ILogger<GitService> _logger;

    /// <summary>
    /// 初始化 <see cref="GitService"/> 類別的新執行個體
    /// </summary>
    public GitService(ILogger<GitService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<string>> CloneRepositoryAsync(
        string repoUrl, string targetPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repoUrl) || string.IsNullOrWhiteSpace(targetPath))
        {
            return Result<string>.Failure(Error.RiskAnalysis.CloneFailed("URL 或目標路徑不得為空"));
        }

        _logger.LogInformation("開始 Clone：{RepoUrl} → {TargetPath}", repoUrl, targetPath);

        var cloneResult = await RunGitCommandAsync(
            $"clone {repoUrl} {targetPath}",
            workingDirectory: null,
            cancellationToken);

        if (cloneResult.IsFailure)
        {
            return Result<string>.Failure(Error.RiskAnalysis.CloneFailed(repoUrl));
        }

        var fetchResult = await RunGitCommandAsync(
            "fetch --all",
            workingDirectory: targetPath,
            cancellationToken);

        if (fetchResult.IsFailure)
        {
            _logger.LogWarning("fetch --all 失敗，但 clone 已完成：{TargetPath}", targetPath);
        }

        _logger.LogInformation("Clone 完成：{TargetPath}", targetPath);
        return Result<string>.Success(targetPath);
    }

    /// <inheritdoc />
    public async Task<Result<string>> GetBranchDiffAsync(
        string repoPath, string baseBranch, string headBranch, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repoPath))
        {
            return Result<string>.Failure(Error.RiskAnalysis.GitCommandFailed("git diff", "repoPath 不得為空"));
        }

        return await RunGitCommandAsync(
            $"diff {baseBranch}...{headBranch}",
            workingDirectory: repoPath,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<string>> FindMergeCommitAsync(
        string repoPath, string branchName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repoPath))
        {
            return Result<string>.Failure(Error.RiskAnalysis.GitCommandFailed("git log", "repoPath 不得為空"));
        }

        var result = await RunGitCommandAsync(
            $"log --merges --format=%H --grep=\"Merge branch '{branchName}'\" -1",
            workingDirectory: repoPath,
            cancellationToken);

        if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Value))
        {
            return Result<string>.Success(result.Value.Trim());
        }

        return Result<string>.Failure(Error.RiskAnalysis.GitCommandFailed(
            "git log --merges --grep", $"找不到 branch '{branchName}' 的 merge commit"));
    }

    /// <inheritdoc />
    public async Task<Result<string>> GetCommitDiffAsync(
        string repoPath, string commitSha, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repoPath))
        {
            return Result<string>.Failure(Error.RiskAnalysis.GitCommandFailed("git show", "repoPath 不得為空"));
        }

        return await RunGitCommandAsync(
            $"show {commitSha}",
            workingDirectory: repoPath,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<string>> GetRemoteUrlAsync(
        string repoPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repoPath))
        {
            return Result<string>.Failure(Error.RiskAnalysis.GitCommandFailed("git remote", "repoPath 不得為空"));
        }

        var result = await RunGitCommandAsync(
            "remote get-url origin",
            workingDirectory: repoPath,
            cancellationToken);

        if (result.IsSuccess && result.Value is not null)
        {
            return Result<string>.Success(result.Value.Trim());
        }

        return result;
    }

    /// <summary>執行 git 命令並回傳輸出</summary>
    internal async Task<Result<string>> RunGitCommandAsync(
        string arguments, string? workingDirectory, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        _logger.LogDebug("執行 git 命令：git {Arguments}（工作目錄：{WorkingDirectory}）",
            arguments, workingDirectory ?? "(null)");

        var process = new Process { StartInfo = startInfo };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            _logger.LogWarning("git 命令失敗（exit code {ExitCode}）：git {Arguments}，錯誤：{Error}",
                process.ExitCode, arguments, error);
            return Result<string>.Failure(Error.RiskAnalysis.GitCommandFailed($"git {arguments}", error));
        }

        return Result<string>.Success(output);
    }
}
