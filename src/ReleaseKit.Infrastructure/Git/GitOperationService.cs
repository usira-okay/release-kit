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
    public async Task<Result<IReadOnlyList<FileDiff>>> GetCommitDiffAsync(string repoPath, string commitSha, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(Path.Combine(repoPath, ".git")))
        {
            return Result<IReadOnlyList<FileDiff>>.Failure(
                Error.Git.DiffFailed(commitSha, $"'{repoPath}' 不是有效的 Git 倉庫"));
        }

        // 使用 {commitSha}^1 對比第一個 parent，可正確處理 merge commit（2 parents 時 diff-tree/show 會輸出空結果）
        var nameStatusResult = await RunGitCommandAsync(
            $"diff {commitSha}^1 {commitSha} --name-status",
            repoPath, cancellationToken);

        if (!nameStatusResult.IsSuccess)
        {
            return Result<IReadOnlyList<FileDiff>>.Failure(
                Error.Git.DiffFailed(commitSha, nameStatusResult.Error!.Message));
        }

        var diffResult = await RunGitCommandAsync(
            $"diff {commitSha}^1 {commitSha} --unified=3",
            repoPath, cancellationToken);

        if (!diffResult.IsSuccess)
        {
            return Result<IReadOnlyList<FileDiff>>.Failure(
                Error.Git.DiffFailed(commitSha, diffResult.Error!.Message));
        }

        var fileDiffs = ParseDiffOutput(nameStatusResult.Value!, diffResult.Value!, commitSha);
        return Result<IReadOnlyList<FileDiff>>.Success(fileDiffs);
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
    /// 解析 diff 輸出為 FileDiff 清單
    /// </summary>
    internal static IReadOnlyList<FileDiff> ParseDiffOutput(string nameStatusOutput, string diffOutput, string commitSha)
    {
        var fileDiffs = new List<FileDiff>();
        var fileChanges = ParseNameStatus(nameStatusOutput);
        var fileDiffContents = SplitDiffByFile(diffOutput);

        foreach (var (changeType, filePath) in fileChanges)
        {
            var diffContent = fileDiffContents.GetValueOrDefault(filePath, string.Empty);
            fileDiffs.Add(new FileDiff
            {
                FilePath = filePath,
                ChangeType = changeType,
                DiffContent = diffContent,
                CommitSha = commitSha
            });
        }

        return fileDiffs;
    }

    /// <summary>
    /// 解析 git diff-tree --name-status 的輸出
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
    /// 將完整 diff 輸出依檔案拆分
    /// </summary>
    private static Dictionary<string, string> SplitDiffByFile(string diffOutput)
    {
        var result = new Dictionary<string, string>();
        var diffPattern = new Regex(@"^diff --git a/(.+?) b/", RegexOptions.Multiline);
        var matches = diffPattern.Matches(diffOutput);

        for (var i = 0; i < matches.Count; i++)
        {
            var filePath = matches[i].Groups[1].Value;
            var startIndex = matches[i].Index;
            var endIndex = i + 1 < matches.Count ? matches[i + 1].Index : diffOutput.Length;
            result[filePath] = diffOutput[startIndex..endIndex].Trim();
        }

        return result;
    }

    /// <summary>
    /// 移除 URL 中的認證資訊（用於日誌記錄）
    /// </summary>
    private static string SanitizeUrl(string urlOrArgs)
    {
        return Regex.Replace(urlOrArgs, @"://[^@]+@", "://***@");
    }
}
