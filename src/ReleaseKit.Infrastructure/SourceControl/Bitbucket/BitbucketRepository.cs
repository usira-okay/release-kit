using System.Net;
using System.Text.Json;
using System.Web;
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
            var pageResponse = JsonSerializer.Deserialize<BitbucketPageResponse<BitbucketPullRequestResponse>>(content);

            if (pageResponse == null || pageResponse.Values.Count == 0)
            {
                break;
            }

            // 二次過濾：僅保留 closed_on 在時間範圍內的 PR
            var filteredPullRequests = pageResponse.Values
                .Where(pr => pr.ClosedOn.HasValue &&
                             pr.ClosedOn.Value >= startDateTime &&
                             pr.ClosedOn.Value <= endDateTime)
                .Select(pr => BitbucketPullRequestMapper.ToDomain(pr, projectPath))
                .ToList();

            allMergeRequests.AddRange(filteredPullRequests);

            // 使用 next 連結取得下一頁
            url = pageResponse.Next;
        }

        return Result<IReadOnlyList<MergeRequest>>.Success(allMergeRequests);
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<MergeRequest>>> GetMergeRequestsByBranchDiffAsync(
        string projectPath,
        string sourceBranch,
        string targetBranch,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<string>>> GetBranchesAsync(
        string projectPath,
        string? pattern = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<MergeRequest>>> GetMergeRequestsByCommitAsync(
        string projectPath,
        string commitSha,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
