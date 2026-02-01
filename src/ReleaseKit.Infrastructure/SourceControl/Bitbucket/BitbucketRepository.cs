using System.Net;
using System.Web;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Common;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Infrastructure.SourceControl.Bitbucket.Models;

namespace ReleaseKit.Infrastructure.SourceControl.Bitbucket;

/// <summary>
/// Bitbucket Repository 實作
/// </summary>
public class BitbucketRepository : ISourceControlRepository
{
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="httpClientFactory">HttpClient 工廠</param>
    public BitbucketRepository(IHttpClientFactory httpClientFactory)
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
        var httpClient = _httpClientFactory.CreateClient("Bitbucket");
        var allMergeRequests = new List<MergeRequest>();

        // Bitbucket API 路徑格式: repositories/{workspace}/{repo_slug}/pullrequests
        var url = $"repositories/{HttpUtility.UrlEncode(projectPath)}/pullrequests?" +
                  $"state=MERGED&" +
                  $"fields=*.*";

        while (!string.IsNullOrEmpty(url))
        {
            var response = await httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return Result<IReadOnlyList<MergeRequest>>.Failure(
                    response.StatusCode == HttpStatusCode.Unauthorized
                        ? Error.SourceControl.Unauthorized
                        : Error.SourceControl.ApiError($"HTTP {(int)response.StatusCode}"));
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var pageResponse = content.ToTypedObject<BitbucketPageResponse<BitbucketPullRequestResponse>>();

            if (pageResponse == null || pageResponse.Values.Count == 0)
            {
                break;
            }

            // 二次過濾：僅保留 closed_on 在時間範圍內且合併到目標分支的 PR
            var filteredPullRequests = pageResponse.Values
                .Where(pr => pr.ClosedOn.HasValue &&
                             pr.ClosedOn.Value >= startDateTime &&
                             pr.ClosedOn.Value <= endDateTime &&
                             pr.Destination?.Branch?.Name == targetBranch)
                .Select(pr => BitbucketPullRequestMapper.ToDomain(pr, projectPath))
                .ToList();

            allMergeRequests.AddRange(filteredPullRequests);

            // 使用 next 連結取得下一頁
            url = pageResponse.Next;
        }

        return Result<IReadOnlyList<MergeRequest>>.Success(allMergeRequests);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<string>>> GetBranchesAsync(
        string projectPath,
        string? pattern = null,
        CancellationToken cancellationToken = default)
    {
        var httpClient = _httpClientFactory.CreateClient("Bitbucket");
        var allBranches = new List<string>();

        // Bitbucket API: GET /2.0/repositories/{workspace}/{repo_slug}/refs/branches
        var url = $"repositories/{HttpUtility.UrlEncode(projectPath)}/refs/branches?pagelen=100";

        while (!string.IsNullOrEmpty(url))
        {
            var response = await httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return Result<IReadOnlyList<string>>.Failure(
                    Error.SourceControl.ApiError($"HTTP {(int)response.StatusCode}"));
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var pageResponse = content.ToTypedObject<BitbucketPageResponse<BitbucketBranchResponse>>();

            if (pageResponse == null || pageResponse.Values.Count == 0)
            {
                break;
            }

            var branchNames = pageResponse.Values
                .Select(b => b.Name)
                .Where(name => !string.IsNullOrEmpty(name))
                .ToList();

            allBranches.AddRange(branchNames);

            // 使用 next 連結取得下一頁
            url = pageResponse.Next;
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
        var httpClient = _httpClientFactory.CreateClient("Bitbucket");

        // Bitbucket API: GET /2.0/repositories/{workspace}/{repo_slug}/commit/{commit}/pullrequests
        var url = $"repositories/{HttpUtility.UrlEncode(projectPath)}/commit/{commitSha}/pullrequests?fields=*.*";

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
        var pageResponse = content.ToTypedObject<BitbucketPageResponse<BitbucketPullRequestResponse>>();

        if (pageResponse == null || pageResponse.Values.Count == 0)
        {
            return Result<IReadOnlyList<MergeRequest>>.Success(Array.Empty<MergeRequest>());
        }

        var mergeRequests = pageResponse.Values
            .Select(pr => BitbucketPullRequestMapper.ToDomain(pr, projectPath))
            .ToList();

        return Result<IReadOnlyList<MergeRequest>>.Success(mergeRequests);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<MergeRequest>>> GetMergeRequestsByBranchDiffAsync(
        string projectPath,
        string sourceBranch,
        string targetBranch,
        CancellationToken cancellationToken = default)
    {
        var httpClient = _httpClientFactory.CreateClient("Bitbucket");

        // 1. 取得兩個分支之間的 commits
        // Bitbucket API: GET /2.0/repositories/{workspace}/{repo_slug}/commits/{revision}
        // Use exclude parameter to get commits in target but not in source
        var commitsUrl = $"repositories/{HttpUtility.UrlEncode(projectPath)}/commits/{HttpUtility.UrlEncode(targetBranch)}?" +
                         $"exclude={HttpUtility.UrlEncode(sourceBranch)}&" +
                         $"pagelen=100";

        var allCommits = new List<BitbucketCommitResponse>();

        while (!string.IsNullOrEmpty(commitsUrl))
        {
            var commitsResponse = await httpClient.GetAsync(commitsUrl, cancellationToken);

            if (!commitsResponse.IsSuccessStatusCode)
            {
                return Result<IReadOnlyList<MergeRequest>>.Failure(
                    commitsResponse.StatusCode == HttpStatusCode.NotFound
                        ? Error.SourceControl.BranchNotFound($"{sourceBranch} or {targetBranch}")
                        : Error.SourceControl.ApiError($"HTTP {(int)commitsResponse.StatusCode}"));
            }

            var commitsContent = await commitsResponse.Content.ReadAsStringAsync(cancellationToken);
            var commitsPageResponse = commitsContent.ToTypedObject<BitbucketPageResponse<BitbucketCommitResponse>>();

            if (commitsPageResponse == null || commitsPageResponse.Values.Count == 0)
            {
                break;
            }

            allCommits.AddRange(commitsPageResponse.Values);

            // 使用 next 連結取得下一頁
            commitsUrl = commitsPageResponse.Next;
        }

        if (allCommits.Count == 0)
        {
            return Result<IReadOnlyList<MergeRequest>>.Success(Array.Empty<MergeRequest>());
        }

        // 2. 對每個 commit 取得關聯的 PR
        var allMergeRequests = new List<MergeRequest>();
        var processedPRIds = new HashSet<string>();

        foreach (var commit in allCommits)
        {
            var prResult = await GetMergeRequestsByCommitAsync(projectPath, commit.Hash, cancellationToken);
            if (prResult.IsSuccess && prResult.Value != null)
            {
                // 去重複 - 使用明確的 Where 過濾
                var uniquePRs = prResult.Value.Where(pr => processedPRIds.Add(pr.PRUrl));
                allMergeRequests.AddRange(uniquePRs);
            }
        }

        return Result<IReadOnlyList<MergeRequest>>.Success(allMergeRequests);
    }
}
