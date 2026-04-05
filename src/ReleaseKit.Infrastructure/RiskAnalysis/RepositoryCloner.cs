using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ReleaseKit.Application.Common.RiskAnalysis;
using ReleaseKit.Domain.Common;

namespace ReleaseKit.Infrastructure.RiskAnalysis;

/// <summary>
/// 負責 clone 或更新 Git Repository 的實作
/// </summary>
public class RepositoryCloner : IRepositoryCloner
{
    private readonly IProcessRunner _processRunner;
    private readonly ILogger<RepositoryCloner> _logger;

    /// <summary>
    /// 允許作為 git branch 名稱的字元集合（英數字、/、-、_、.）
    /// </summary>
    private static readonly Regex SafeBranchNameRegex = new(
        @"^[a-zA-Z0-9/_.\-]+$",
        RegexOptions.Compiled);

    /// <summary>
    /// 用於隱藏 clone URL 中的認證資訊（user:password@host）
    /// </summary>
    private static readonly Regex CredentialsInUrlRegex = new(
        @"(?<=https?://)([^@]+)@",
        RegexOptions.Compiled);

    /// <summary>
    /// 初始化 RepositoryCloner
    /// </summary>
    /// <param name="processRunner">外部程序執行器</param>
    /// <param name="logger">日誌記錄器</param>
    public RepositoryCloner(IProcessRunner processRunner, ILogger<RepositoryCloner> logger)
    {
        _processRunner = processRunner;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<string>> CloneAsync(string cloneUrl, string targetPath)
    {
        if (Directory.Exists(Path.Combine(targetPath, ".git")))
        {
            _logger.LogInformation("Repository 已存在，執行 git pull: {TargetPath}", targetPath);

            var pullResult = await _processRunner.RunAsync("git", "pull", targetPath);
            if (pullResult.ExitCode != 0)
            {
                return Result<string>.Failure(
                    Error.RiskAnalysis.CloneFailed(MaskCredentials(cloneUrl), pullResult.StandardError));
            }

            return Result<string>.Success(targetPath);
        }

        var parentDir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
        {
            Directory.CreateDirectory(parentDir);
        }

        _logger.LogInformation("正在 clone repository: {CloneUrl} → {TargetPath}", MaskCredentials(cloneUrl), targetPath);

        var cloneResult = await _processRunner.RunAsync("git", $"clone {cloneUrl} {targetPath}");
        if (cloneResult.ExitCode != 0)
        {
            return Result<string>.Failure(
                Error.RiskAnalysis.CloneFailed(MaskCredentials(cloneUrl), cloneResult.StandardError));
        }

        return Result<string>.Success(targetPath);
    }

    /// <inheritdoc />
    public Task CleanupAsync(string localPath)
    {
        if (Directory.Exists(localPath))
        {
            _logger.LogInformation("正在清理 repository: {LocalPath}", localPath);
            Directory.Delete(localPath, recursive: true);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<Result<string>> CheckoutAsync(string localPath, string branch)
    {
        if (!SafeBranchNameRegex.IsMatch(branch))
        {
            return Result<string>.Failure(
                Error.RiskAnalysis.CloneFailed(localPath, $"分支名稱包含不允許的字元: {branch}"));
        }

        _logger.LogInformation("正在切換分支: {Branch} at {LocalPath}", branch, localPath);

        var result = await _processRunner.RunAsync("git", $"checkout {branch}", localPath);
        if (result.ExitCode != 0)
        {
            return Result<string>.Failure(
                Error.RiskAnalysis.CloneFailed(localPath, result.StandardError));
        }

        return Result<string>.Success(localPath);
    }

    /// <summary>
    /// 隱藏 URL 中的認證資訊，用於安全日誌記錄
    /// </summary>
    private static string MaskCredentials(string url) =>
        CredentialsInUrlRegex.Replace(url, "***@");
}
