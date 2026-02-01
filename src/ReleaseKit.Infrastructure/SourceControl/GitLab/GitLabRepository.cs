using System.Net;
using System.Web;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Common;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Infrastructure.SourceControl.GitLab.Models;

namespace ReleaseKit.Infrastructure.SourceControl.GitLab;

/// <summary>
/// GitLab Repository 實作
/// </summary>
public class GitLabRepository : ISourceControlRepository
{
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="httpClientFactory">HttpClient 工廠</param>
    public GitLabRepository(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<MergeRequest>>> GetMergeRequestsByDateRangeAsync(
        string projectPath,
        string targetBranch,
        DateTimeOffset startDateTime,
        DateTimeOffset endDateTime,
        CancellationToken cancellationToken = default)
    {
        var httpClient = _httpClientFactory.CreateClient("GitLab");
        var encodedProjectPath = HttpUtility.UrlEncode(projectPath);
        var allMergeRequests = new List<MergeRequest>();

        var page = 1;
        const int perPage = 100;

        while (true)
        {
            var url = $"projects/{encodedProjectPath}/merge_requests?" +
                      $"state=merged&" +
                      $"target_branch={HttpUtility.UrlEncode(targetBranch)}&" +
                      $"updated_after={startDateTime:yyyy-MM-ddTHH:mm:ssZ}&" +
                      $"updated_before={endDateTime:yyyy-MM-ddTHH:mm:ssZ}&" +
                      $"scope=all&" +
                      $"page={page}&" +
                      $"per_page={perPage}";

            var response = await httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return Result<IReadOnlyList<MergeRequest>>.Failure(
                    response.StatusCode == HttpStatusCode.Unauthorized
                        ? Error.SourceControl.Unauthorized
                        : Error.SourceControl.ApiError($"HTTP {(int)response.StatusCode}"));
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var gitLabResponses = content.ToTypedObject<List<GitLabMergeRequestResponse>>();

            if (gitLabResponses == null || gitLabResponses.Count == 0)
            {
                break;
            }

            // 二次過濾：僅保留 merged_at 在時間範圍內的 MR
            var filteredMergeRequests = gitLabResponses
                .Where(mr => mr.MergedAt.HasValue &&
                             mr.MergedAt.Value >= startDateTime &&
                             mr.MergedAt.Value <= endDateTime)
                .Select(mr => GitLabMergeRequestMapper.ToDomain(mr, projectPath))
                .ToList();

            allMergeRequests.AddRange(filteredMergeRequests);

            // 如果回傳的筆數少於 perPage，表示已經是最後一頁
            if (gitLabResponses.Count < perPage)
            {
                break;
            }

            page++;
        }

        return Result<IReadOnlyList<MergeRequest>>.Success(allMergeRequests);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<MergeRequest>>> GetMergeRequestsByBranchDiffAsync(
        string projectPath,
        string sourceBranch,
        string targetBranch,
        CancellationToken cancellationToken = default)
    {
        var httpClient = _httpClientFactory.CreateClient("GitLab");
        var encodedProjectPath = HttpUtility.UrlEncode(projectPath);

        // 1. 取得分支差異的 commits
        var compareUrl = $"projects/{encodedProjectPath}/repository/compare?" +
                         $"from={HttpUtility.UrlEncode(sourceBranch)}&" +
                         $"to={HttpUtility.UrlEncode(targetBranch)}&" +
                         $"straight=false";

        var compareResponse = await httpClient.GetAsync(compareUrl, cancellationToken);

        if (!compareResponse.IsSuccessStatusCode)
        {
            return Result<IReadOnlyList<MergeRequest>>.Failure(
                compareResponse.StatusCode == HttpStatusCode.NotFound
                    ? Error.SourceControl.BranchNotFound($"{sourceBranch} or {targetBranch}")
                    : Error.SourceControl.ApiError($"HTTP {(int)compareResponse.StatusCode}"));
        }

        var compareContent = await compareResponse.Content.ReadAsStringAsync(cancellationToken);
        var compareResult = compareContent.ToTypedObject<GitLabCompareResponse>();

        if (compareResult == null || compareResult.Commits.Count == 0)
        {
            return Result<IReadOnlyList<MergeRequest>>.Success(Array.Empty<MergeRequest>());
        }

        // 2. 對每個 commit 取得關聯的 MR
        var allMergeRequests = new List<MergeRequest>();
        var processedMRUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var commit in compareResult.Commits)
        {
            var mrResult = await GetMergeRequestsByCommitAsync(projectPath, commit.Id, cancellationToken);
            if (mrResult.IsSuccess && mrResult.Value != null)
            {
                // 去重複 - 使用明確的 Where 過濾並立即執行
                var uniqueMRs = mrResult.Value.Where(mr => processedMRUrls.Add(mr.PRUrl)).ToList();
                allMergeRequests.AddRange(uniqueMRs);
            }
        }

        return Result<IReadOnlyList<MergeRequest>>.Success(allMergeRequests);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<string>>> GetBranchesAsync(
        string projectPath,
        string? pattern = null,
        CancellationToken cancellationToken = default)
    {
        var httpClient = _httpClientFactory.CreateClient("GitLab");
        var encodedProjectPath = HttpUtility.UrlEncode(projectPath);
        var allBranches = new List<string>();

        var page = 1;
        const int perPage = 100;

        while (true)
        {
            var url = $"projects/{encodedProjectPath}/repository/branches?" +
                      $"page={page}&" +
                      $"per_page={perPage}";

            // GitLab API search parameter searches for branches that contain the search term,
            // but for prefix matching we need to filter client-side
            // Only use search if pattern doesn't end with / (indicating a prefix search)
            if (!string.IsNullOrEmpty(pattern) && !pattern.EndsWith('/'))
            {
                url += $"&search={HttpUtility.UrlEncode(pattern)}";
            }

            var response = await httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return Result<IReadOnlyList<string>>.Failure(
                    Error.SourceControl.ApiError($"HTTP {(int)response.StatusCode}"));
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var branches = content.ToTypedObject<List<GitLabBranchResponse>>();

            if (branches == null || branches.Count == 0)
            {
                break;
            }

            var branchNames = branches
                .Select(b => b.Name)
                .Where(name => !string.IsNullOrEmpty(name))
                .ToList();

            allBranches.AddRange(branchNames);

            if (branches.Count < perPage)
            {
                break;
            }

            page++;
        }

        // Client-side filtering for pattern matching (prefix match)
        if (!string.IsNullOrEmpty(pattern))
        {
            allBranches = allBranches
                .Where(name => name.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return Result<IReadOnlyList<string>>.Success(allBranches);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<MergeRequest>>> GetMergeRequestsByCommitAsync(
        string projectPath,
        string commitSha,
        CancellationToken cancellationToken = default)
    {
        var httpClient = _httpClientFactory.CreateClient("GitLab");
        var encodedProjectPath = HttpUtility.UrlEncode(projectPath);

        var url = $"projects/{encodedProjectPath}/repository/commits/{commitSha}/merge_requests";

        var response = await httpClient.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return Result<IReadOnlyList<MergeRequest>>.Success(Array.Empty<MergeRequest>());
            }

            return Result<IReadOnlyList<MergeRequest>>.Failure(
                Error.SourceControl.ApiError($"HTTP {(int)response.StatusCode}"));
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var gitLabResponses = content.ToTypedObject<List<GitLabMergeRequestResponse>>();

        if (gitLabResponses == null)
        {
            return Result<IReadOnlyList<MergeRequest>>.Success(Array.Empty<MergeRequest>());
        }

        var mergeRequests = gitLabResponses
            .Select(mr => GitLabMergeRequestMapper.ToDomain(mr, projectPath))
            .ToList();

        return Result<IReadOnlyList<MergeRequest>>.Success(mergeRequests);
    }
}
