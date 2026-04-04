using System.Net;
using System.Web;
using Microsoft.Extensions.Logging;
using ReleaseKit.Application.Common.RiskAnalysis;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Common;

namespace ReleaseKit.Infrastructure.RiskAnalysis.DiffProviders;

/// <summary>
/// 透過 GitLab API 取得 MR diff 的提供者
/// </summary>
public class GitLabDiffProvider : IDiffProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GitLabDiffProvider> _logger;

    /// <summary>
    /// 初始化 <see cref="GitLabDiffProvider"/> 實例
    /// </summary>
    /// <param name="httpClientFactory">HTTP 客戶端工廠</param>
    /// <param name="logger">日誌記錄器</param>
    public GitLabDiffProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<GitLabDiffProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<PullRequestDiff>> GetDiffAsync(string projectPath, string prId)
    {
        var httpClient = _httpClientFactory.CreateClient(HttpClientNames.GitLab);
        var encodedProjectPath = HttpUtility.UrlEncode(projectPath);

        var url = $"/api/v4/projects/{encodedProjectPath}/merge_requests/{prId}/changes";
        _logger.LogInformation("正在取得 GitLab MR diff: {ProjectPath} MR!{PrId}", projectPath, prId);

        var response = await httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("GitLab MR diff 取得失敗: HTTP {StatusCode}", (int)response.StatusCode);
            return Result<PullRequestDiff>.Failure(
                response.StatusCode == HttpStatusCode.Unauthorized
                    ? Error.SourceControl.Unauthorized
                    : Error.RiskAnalysis.DiffFetchFailed(projectPath, prId));
        }

        var content = await response.Content.ReadAsStringAsync();
        var changesResponse = content.ToTypedObject<GitLabMrChangesResponse>();

        if (changesResponse == null)
        {
            return Result<PullRequestDiff>.Failure(Error.SourceControl.InvalidResponse);
        }

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
