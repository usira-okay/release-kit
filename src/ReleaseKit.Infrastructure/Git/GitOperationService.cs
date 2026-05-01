using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Common;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Infrastructure.Git;

/// <summary>
/// Git 操作服務，透過 shell 呼叫 git CLI 執行 clone/pull/diff
/// </summary>
public class GitOperationService : IGitOperationService
{
    private readonly ILogger<GitOperationService> _logger;

    /// <summary>
    /// 初始化 GitOperationService
    /// </summary>
    public GitOperationService(ILogger<GitOperationService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<string>> CloneOrPullAsync(string repoUrl, string localPath, CancellationToken cancellationToken = default)
    {
        if (Directory.Exists(Path.Combine(localPath, ".git")))
        {
            _logger.LogInformation("目錄已存在，執行 git pull: {LocalPath}", localPath);
            var pullResult = await RunGitCommandAsync("pull", localPath, cancellationToken);
            if (!pullResult.IsSuccess)
            {
                return Result<string>.Failure(Error.Git.PullFailed(localPath, pullResult.Error!.Message));
            }
            return Result<string>.Success(localPath);
        }

        _logger.LogInformation("目錄不存在，執行 git clone: {RepoUrl} -> {LocalPath}", SanitizeUrl(repoUrl), localPath);
        var parentDir = Path.GetDirectoryName(localPath) ?? localPath;
        Directory.CreateDirectory(parentDir);

        var cloneResult = await RunGitCommandAsync($"clone {repoUrl} {localPath}", parentDir, cancellationToken);
        if (!cloneResult.IsSuccess)
        {
            return Result<string>.Failure(Error.Git.CloneFailed(SanitizeUrl(repoUrl), cloneResult.Error!.Message));
        }

        return Result<string>.Success(localPath);
    }

    /// <inheritdoc />
    public async Task<Result<CommitSummary>> GetCommitStatAsync(string repoPath, string commitSha, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(Path.Combine(repoPath, ".git")))
        {
            return Result<CommitSummary>.Failure(
                Error.Git.DiffFailed(commitSha, $"'{repoPath}' 不是有效的 Git 倉庫"));
        }

        // 使用 {commitSha}^1 對比第一個 parent，可正確處理 merge commit
        var nameStatusResult = await RunGitCommandAsync(
            $"diff {commitSha}^1 {commitSha} --name-status",
            repoPath, cancellationToken);

        if (!nameStatusResult.IsSuccess)
        {
            return Result<CommitSummary>.Failure(
                Error.Git.DiffFailed(commitSha, nameStatusResult.Error!.Message));
        }

        var shortStatResult = await RunGitCommandAsync(
            $"diff {commitSha}^1 {commitSha} --shortstat",
            repoPath, cancellationToken);

        if (!shortStatResult.IsSuccess)
        {
            return Result<CommitSummary>.Failure(
                Error.Git.DiffFailed(commitSha, shortStatResult.Error!.Message));
        }

        var changedFiles = ParseNameStatusToFileDiffs(nameStatusResult.Value!, commitSha);
        var (linesAdded, linesRemoved) = ParseShortStat(shortStatResult.Value ?? string.Empty);

        return Result<CommitSummary>.Success(new CommitSummary
        {
            CommitSha = commitSha,
            ChangedFiles = changedFiles,
            TotalFilesChanged = changedFiles.Count,
            TotalLinesAdded = linesAdded,
            TotalLinesRemoved = linesRemoved
        });
    }

    /// <inheritdoc />
    public async Task<Result<string>> GetCommitRawDiffAsync(string repoPath, string commitSha, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(Path.Combine(repoPath, ".git")))
        {
            return Result<string>.Failure(
                Error.Git.DiffFailed(commitSha, $"'{repoPath}' 不是有效的 Git 倉庫"));
        }

        var diffResult = await RunGitCommandAsync(
            $"diff {commitSha}^1 {commitSha} --unified=3",
            repoPath, cancellationToken);

        if (!diffResult.IsSuccess)
        {
            return Result<string>.Failure(
                Error.Git.DiffFailed(commitSha, diffResult.Error!.Message));
        }

        return Result<string>.Success(diffResult.Value ?? string.Empty);
    }

    /// <inheritdoc />
    public async Task<Result<string>> SearchPatternAsync(
        string repoPath,
        string pattern,
        string? fileGlob = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(Path.Combine(repoPath, ".git")))
        {
            return Result<string>.Failure(
                Error.Git.SearchFailed($"'{repoPath}' 不是有效的 Git 倉庫"));
        }

        var arguments = fileGlob != null
            ? $"grep -n -E \"{pattern}\" -- \"{fileGlob}\""
            : $"grep -n -E \"{pattern}\"";

        // git grep exit code 1 = 無符合結果，屬正常情境
        var result = await RunGitCommandAsync(arguments, repoPath, [1], cancellationToken);

        if (!result.IsSuccess)
        {
            return Result<string>.Failure(
                Error.Git.SearchFailed(result.Error!.Message));
        }

        return Result<string>.Success(result.Value ?? string.Empty);
    }

    /// <summary>
    /// 執行 git 命令
    /// </summary>
    private async Task<Result<string>> RunGitCommandAsync(string arguments, string workingDirectory, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            return Result<string>.Failure(new Error("Git.ProcessFailed", "無法啟動 git 程序"));
        }

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            _logger.LogError("git {Arguments} 失敗 (exit code {ExitCode}): {Error}",
                SanitizeUrl(arguments), process.ExitCode, error);
            return Result<string>.Failure(new Error("Git.CommandFailed", error.Trim()));
        }

        return Result<string>.Success(output);
    }

    /// <summary>
    /// 執行 git 命令（允許指定可接受的 exit code）
    /// </summary>
    private async Task<Result<string>> RunGitCommandAsync(
        string arguments,
        string workingDirectory,
        IReadOnlyList<int> acceptableExitCodes,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            return Result<string>.Failure(new Error("Git.ProcessFailed", "無法啟動 git 程序"));
        }

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0 && !acceptableExitCodes.Contains(process.ExitCode))
        {
            _logger.LogError("git {Arguments} 失敗 (exit code {ExitCode}): {Error}",
                SanitizeUrl(arguments), process.ExitCode, error);
            return Result<string>.Failure(new Error("Git.CommandFailed", error.Trim()));
        }

        return Result<string>.Success(output);
    }

    /// <summary>
    /// 解析 git diff --name-status 輸出為 FileDiff 清單（不含 diff 內容）
    /// </summary>
    internal static IReadOnlyList<FileDiff> ParseNameStatusToFileDiffs(string nameStatusOutput, string commitSha)
    {
        return ParseNameStatus(nameStatusOutput)
            .Select(fc => new FileDiff
            {
                FilePath = fc.FilePath,
                ChangeType = fc.ChangeType,
                CommitSha = commitSha
            })
            .ToList();
    }

    /// <summary>
    /// 解析 git diff --name-status 的輸出
    /// </summary>
    private static List<(ChangeType ChangeType, string FilePath)> ParseNameStatus(string output)
    {
        var results = new List<(ChangeType, string)>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t', 2);
            if (parts.Length < 2) continue;

            var changeType = parts[0].Trim() switch
            {
                "A" => ChangeType.Added,
                "D" => ChangeType.Deleted,
                _ => ChangeType.Modified
            };

            results.Add((changeType, parts[1].Trim()));
        }
        return results;
    }

    /// <summary>
    /// 解析 git diff --shortstat 輸出，取得新增與刪除行數
    /// </summary>
    /// <remarks>
    /// 輸出格式範例：" 5 files changed, 120 insertions(+), 45 deletions(-)"
    /// </remarks>
    internal static (int LinesAdded, int LinesRemoved) ParseShortStat(string shortStatOutput)
    {
        var linesAdded = 0;
        var linesRemoved = 0;

        var insertionMatch = Regex.Match(shortStatOutput, @"(\d+) insertion");
        if (insertionMatch.Success)
            int.TryParse(insertionMatch.Groups[1].Value, out linesAdded);

        var deletionMatch = Regex.Match(shortStatOutput, @"(\d+) deletion");
        if (deletionMatch.Success)
            int.TryParse(deletionMatch.Groups[1].Value, out linesRemoved);

        return (linesAdded, linesRemoved);
    }

    /// <summary>
    /// 移除 URL 中的認證資訊（用於日誌記錄）
    /// </summary>
    private static string SanitizeUrl(string urlOrArgs)
    {
        return Regex.Replace(urlOrArgs, @"://[^@]+@", "://***@");
    }
}
