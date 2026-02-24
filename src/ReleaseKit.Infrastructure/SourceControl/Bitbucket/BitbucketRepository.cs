using System.Net;
using System.Web;
using Microsoft.Extensions.Logging;
using ReleaseKit.Common.Constants;
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
    private readonly ILogger<BitbucketRepository> _logger;

    /// <summary>
    /// Rate Limit 重試次數上限
    /// </summary>
    private const int RateLimitMaxRetries = 5;

    /// <summary>
    /// Rate Limit 重試等待時間
    /// </summary>
    private readonly TimeSpan _rateLimitDelay;

    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="httpClientFactory">HttpClient 工廠</param>
    /// <param name="logger">日誌記錄器</param>
    public BitbucketRepository(IHttpClientFactory httpClientFactory, ILogger<BitbucketRepository> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _rateLimitDelay = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// 測試用建構子（可指定 Rate Limit 重試等待時間）
    /// </summary>
    /// <param name="httpClientFactory">HttpClient 工廠</param>
    /// <param name="logger">日誌記錄器</param>
    /// <param name="rateLimitDelay">Rate Limit 重試等待時間</param>
    internal BitbucketRepository(IHttpClientFactory httpClientFactory, ILogger<BitbucketRepository> logger, TimeSpan rateLimitDelay)
        : this(httpClientFactory, logger)
    {
        _rateLimitDelay = rateLimitDelay;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<MergeRequest>>> GetMergeRequestsByDateRangeAsync(
        string projectPath,
        string targetBranch,
        DateTimeOffset startDateTime,
        DateTimeOffset endDateTime,
        CancellationToken cancellationToken = default)
    {
        var httpClient = _httpClientFactory.CreateClient(HttpClientNames.Bitbucket);
        var allMergeRequests = new List<MergeRequest>();

        // 使用 q 參數進行日期篩選與狀態過濾
        // BitBucket API 查詢語法: updated_on >= "2025-01-01" AND state="MERGED"
        var query = $"updated_on>={startDateTime:yyyy-MM-ddTHH:mm:ss.fffZ} AND state=\"MERGED\"";

        // Bitbucket API 路徑格式: /2.0/repositories/{workspace}/{repo_slug}/pullrequests
        
        var url = $"/2.0/repositories/{projectPath}/pullrequests?" +
                  $"q={Uri.EscapeDataString(query)}&" +
                  $"sort=-updated_on&" +
                  $"pagelen=50&" +
                  $"fields=*.*";

        while (!string.IsNullOrEmpty(url))
        {
            var response = await SendWithRateLimitRetryAsync(httpClient, url, cancellationToken);

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
        var httpClient = _httpClientFactory.CreateClient(HttpClientNames.Bitbucket);
        var allBranches = new List<string>();

        // Bitbucket API: GET /2.0//2.0/repositories/{workspace}/{repo_slug}/refs/branches
        var url = $"/2.0/repositories/{projectPath}/refs/branches?pagelen=100";

        while (!string.IsNullOrEmpty(url))
        {
            var response = await SendWithRateLimitRetryAsync(httpClient, url, cancellationToken);

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
        var httpClient = _httpClientFactory.CreateClient(HttpClientNames.Bitbucket);

        // Bitbucket API: GET /2.0//2.0/repositories/{workspace}/{repo_slug}/commit/{commit}/pullrequests
        var url = $"/2.0/repositories/{projectPath}/commit/{commitSha}/pullrequests?fields=*.*";

        var response = await SendWithRateLimitRetryAsync(httpClient, url, cancellationToken);

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
        var httpClient = _httpClientFactory.CreateClient(HttpClientNames.Bitbucket);

        // 1. 取得兩個分支之間的 commits
        // Bitbucket API: GET /2.0//2.0/repositories/{workspace}/{repo_slug}/commits/{revision}
        // Use exclude parameter to get commits in target but not in source
        var commitsUrl = $"/2.0/repositories/{projectPath}/commits/{HttpUtility.UrlEncode(targetBranch)}?" +
                         $"exclude={HttpUtility.UrlEncode(sourceBranch)}&" +
                         $"pagelen=100";

        var allCommits = new List<BitbucketCommitResponse>();

        while (!string.IsNullOrEmpty(commitsUrl))
        {
            var commitsResponse = await SendWithRateLimitRetryAsync(httpClient, commitsUrl, cancellationToken);

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
        var processedPRUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        _logger.LogInformation("開始查詢 {CommitCount} 個 commit 的關聯 PR", allCommits.Count);
        var processedCount = 0;
        foreach (var commit in allCommits)
        {
            processedCount++;
            _logger.LogInformation("處理 commit {CurrentCount}/{TotalCount}：{CommitHash}", processedCount, allCommits.Count, commit.Hash);
            
            var prResult = await GetMergeRequestsByCommitAsync(projectPath, commit.Hash, cancellationToken);
            if (prResult.IsSuccess && prResult.Value != null)
            {
                // 去重複：HashSet.Add() 只在元素尚不存在時才回傳 true，
                // 利用此特性在 Where 過濾器中實現去重邏輯，並立即執行以確保過濾重複項目
                var uniquePRs = prResult.Value.Where(pr => processedPRUrls.Add(pr.PRUrl)).ToList();
                allMergeRequests.AddRange(uniquePRs);
                
                if (uniquePRs.Count > 0)
                {
                    _logger.LogInformation("commit {CommitHash} 找到 {PRCount} 個 PR", commit.Hash, uniquePRs.Count);
                }
            }
        }
        _logger.LogInformation("完成 commit PR 查詢，共找到 {TotalPRCount} 個不重複的 PR", allMergeRequests.Count);

        return Result<IReadOnlyList<MergeRequest>>.Success(allMergeRequests);
    }

    /// <summary>
    /// 發送 HTTP GET 請求，並在遇到 Rate Limit (HTTP 429) 時自動重試
    /// </summary>
    /// <param name="httpClient">HTTP 客戶端</param>
    /// <param name="url">請求 URL</param>
    /// <param name="cancellationToken">取消權杖</param>
    /// <returns>HTTP 回應訊息</returns>
    /// <remarks>
    /// 當收到 HTTP 429 Too Many Requests 時，會等待 30 秒後重試，最多重試 5 次。
    /// </remarks>
    private async Task<HttpResponseMessage> SendWithRateLimitRetryAsync(
        HttpClient httpClient,
        string url,
        CancellationToken cancellationToken)
    {
        var retryCount = 0;
        while (true)
        {
            var response = await httpClient.GetAsync(url, cancellationToken);

            if (response.StatusCode != HttpStatusCode.TooManyRequests || retryCount >= RateLimitMaxRetries)
            {
                return response;
            }

            retryCount++;
            _logger.LogWarning(
                "達到 Bitbucket API 請求限制 (HTTP 429)，等待 {Delay} 秒後重試（第 {RetryCount}/{MaxRetries} 次）",
                _rateLimitDelay.TotalSeconds,
                retryCount,
                RateLimitMaxRetries);
            await Task.Delay(_rateLimitDelay, cancellationToken);
        }
    }
}
