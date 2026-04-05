using Microsoft.Extensions.Logging;
using ReleaseKit.Application.Common.RiskAnalysis;
using ReleaseKit.Domain.Common;
using ReleaseKit.Infrastructure.SourceControl.GitLab;

namespace ReleaseKit.Infrastructure.RiskAnalysis.DiffProviders;

/// <summary>
/// 透過 GitLab API 取得 MR diff 的提供者
/// </summary>
public class GitLabDiffProvider : IDiffProvider
{
    private readonly GitLabRepository _gitLabRepository;
    private readonly ILogger<GitLabDiffProvider> _logger;

    /// <summary>
    /// 初始化 <see cref="GitLabDiffProvider"/> 實例
    /// </summary>
    /// <param name="gitLabRepository">GitLab Repository</param>
    /// <param name="logger">日誌記錄器</param>
    public GitLabDiffProvider(
        GitLabRepository gitLabRepository,
        ILogger<GitLabDiffProvider> logger)
    {
        _gitLabRepository = gitLabRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<PullRequestDiff>> GetDiffAsync(string projectPath, string prId)
    {
        var changesResult = await _gitLabRepository.GetMergeRequestChangesAsync(projectPath, prId);

        if (changesResult.IsFailure)
        {
            return Result<PullRequestDiff>.Failure(changesResult.Error!);
        }

        var changesResponse = changesResult.Value!;

        var files = changesResponse.Changes.Select(change => new FileDiff
        {
            FilePath = change.NewPath,
            AddedLines = CountLines(change.Diff, '+'),
            DeletedLines = CountLines(change.Diff, '-'),
            DiffContent = change.Diff,
            IsNewFile = change.NewFile,
            IsDeletedFile = change.DeletedFile
        }).ToList();

        var diff = new PullRequestDiff
        {
            PullRequest = null!,
            RepositoryName = projectPath,
            Platform = "GitLab",
            Files = files
        };

        _logger.LogInformation("GitLab MR diff 取得完成: {FileCount} 個檔案變更", files.Count);
        return Result<PullRequestDiff>.Success(diff);
    }

    /// <summary>
    /// 計算 diff 中新增或刪除的行數
    /// </summary>
    internal static int CountLines(string diff, char prefix)
    {
        if (string.IsNullOrEmpty(diff)) return 0;

        return diff.Split('\n')
            .Count(line => line.Length > 0
                           && line[0] == prefix
                           && !(line.Length >= 3 && line[1] == prefix && line[2] == prefix));
    }
}
