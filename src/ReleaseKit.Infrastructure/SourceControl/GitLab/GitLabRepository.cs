using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Logging;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Entities;

namespace ReleaseKit.Infrastructure.SourceControl.GitLab;

/// <summary>
/// GitLab Repository 實作，負責與 GitLab API 互動
/// </summary>
public class GitLabRepository : IGitLabRepository
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitLabRepository> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public GitLabRepository(
        HttpClient httpClient,
        ILogger<GitLabRepository> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// 根據時間區間拉取 Merge Request 資訊
    /// </summary>
    public async Task<IReadOnlyList<MergeRequest>> FetchMergeRequestsByTimeRangeAsync(
        string projectId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        string? state = null)
    {
        if (string.IsNullOrWhiteSpace(projectId))
            throw new ArgumentException("專案 ID 不得為空", nameof(projectId));
        
        if (startTime > endTime)
            throw new ArgumentException("開始時間不得大於結束時間");

        _logger.LogInformation(
            "開始拉取 GitLab MR 資訊，專案: {ProjectId}, 時間區間: {StartTime} ~ {EndTime}, 狀態: {State}",
            projectId, startTime, endTime, state ?? "all");

        var encodedProjectId = HttpUtility.UrlEncode(projectId);
        var queryParams = new List<string>
        {
            $"updated_after={startTime.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}",
            $"updated_before={endTime.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}",
            "per_page=100"
        };

        if (!string.IsNullOrWhiteSpace(state))
        {
            queryParams.Add($"state={state}");
        }

        var url = $"api/v4/projects/{encodedProjectId}/merge_requests?{string.Join("&", queryParams)}";
        
        var mergeRequests = new List<MergeRequest>();
        var page = 1;

        // GitLab API 使用分頁機制，需要持續拉取直到沒有更多資料
        while (true)
        {
            var pagedUrl = $"{url}&page={page}";
            _logger.LogDebug("正在拉取第 {Page} 頁: {Url}", page, pagedUrl);

            var response = await _httpClient.GetAsync(pagedUrl);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "GitLab API 請求失敗，狀態碼: {StatusCode}, 錯誤訊息: {ErrorContent}",
                    response.StatusCode, errorContent);
                throw new HttpRequestException(
                    $"GitLab API 請求失敗，狀態碼: {response.StatusCode}");
            }

            var dtos = await response.Content.ReadFromJsonAsync<List<GitLabMergeRequestDto>>(_jsonOptions);
            
            if (dtos == null || dtos.Count == 0)
            {
                break;
            }

            mergeRequests.AddRange(dtos.Select(MapToMergeRequest));
            page++;
        }

        _logger.LogInformation("成功拉取 {Count} 筆 MR 資料", mergeRequests.Count);
        return mergeRequests.AsReadOnly();
    }

    /// <summary>
    /// 比較兩個分支之間的 commit 差異，並取得相關的 Merge Request
    /// </summary>
    public async Task<IReadOnlyList<MergeRequest>> FetchMergeRequestsByBranchComparisonAsync(
        string projectId,
        string sourceBranch,
        string targetBranch)
    {
        if (string.IsNullOrWhiteSpace(projectId))
            throw new ArgumentException("專案 ID 不得為空", nameof(projectId));
        
        if (string.IsNullOrWhiteSpace(sourceBranch))
            throw new ArgumentException("來源分支不得為空", nameof(sourceBranch));
        
        if (string.IsNullOrWhiteSpace(targetBranch))
            throw new ArgumentException("目標分支不得為空", nameof(targetBranch));

        _logger.LogInformation(
            "開始比較分支差異並拉取相關 MR，專案: {ProjectId}, 來源分支: {SourceBranch}, 目標分支: {TargetBranch}",
            projectId, sourceBranch, targetBranch);

        var encodedProjectId = HttpUtility.UrlEncode(projectId);
        var encodedSourceBranch = HttpUtility.UrlEncode(sourceBranch);
        var encodedTargetBranch = HttpUtility.UrlEncode(targetBranch);
        
        // 步驟 1: 取得兩個分支之間的 commit 差異
        var compareUrl = $"api/v4/projects/{encodedProjectId}/repository/compare?from={encodedTargetBranch}&to={encodedSourceBranch}";
        _logger.LogDebug("正在比較分支差異: {Url}", compareUrl);

        var compareResponse = await _httpClient.GetAsync(compareUrl);
        
        if (!compareResponse.IsSuccessStatusCode)
        {
            var errorContent = await compareResponse.Content.ReadAsStringAsync();
            _logger.LogError(
                "GitLab API 比較分支請求失敗，狀態碼: {StatusCode}, 錯誤訊息: {ErrorContent}",
                compareResponse.StatusCode, errorContent);
            throw new HttpRequestException(
                $"GitLab API 比較分支請求失敗，狀態碼: {compareResponse.StatusCode}");
        }

        var compareResult = await compareResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var commits = compareResult.GetProperty("commits").Deserialize<List<GitLabCommitDto>>(_jsonOptions);

        if (commits == null || commits.Count == 0)
        {
            _logger.LogInformation("兩個分支之間沒有差異");
            return Array.Empty<MergeRequest>();
        }

        _logger.LogInformation("發現 {Count} 個 commit 差異", commits.Count);

        // 步驟 2: 取得這些 commit 相關的 MR
        // 使用 source_branch 和 target_branch 篩選 MR
        var mrUrl = $"api/v4/projects/{encodedProjectId}/merge_requests?source_branch={encodedSourceBranch}&target_branch={encodedTargetBranch}&per_page=100";
        _logger.LogDebug("正在拉取相關 MR: {Url}", mrUrl);

        var mrResponse = await _httpClient.GetAsync(mrUrl);
        
        if (!mrResponse.IsSuccessStatusCode)
        {
            var errorContent = await mrResponse.Content.ReadAsStringAsync();
            _logger.LogError(
                "GitLab API 拉取 MR 請求失敗，狀態碼: {StatusCode}, 錯誤訊息: {ErrorContent}",
                mrResponse.StatusCode, errorContent);
            throw new HttpRequestException(
                $"GitLab API 拉取 MR 請求失敗，狀態碼: {mrResponse.StatusCode}");
        }

        var dtos = await mrResponse.Content.ReadFromJsonAsync<List<GitLabMergeRequestDto>>(_jsonOptions);
        
        if (dtos == null || dtos.Count == 0)
        {
            _logger.LogInformation("找不到相關的 MR");
            return Array.Empty<MergeRequest>();
        }

        var mergeRequests = dtos.Select(MapToMergeRequest).ToList();
        _logger.LogInformation("成功拉取 {Count} 筆相關 MR 資料", mergeRequests.Count);
        
        return mergeRequests.AsReadOnly();
    }

    /// <summary>
    /// 將 GitLab DTO 轉換為領域實體
    /// </summary>
    private static MergeRequest MapToMergeRequest(GitLabMergeRequestDto dto)
    {
        return new MergeRequest
        {
            Id = dto.Id.ToString(CultureInfo.InvariantCulture),
            Number = dto.Iid,
            Title = dto.Title,
            Description = dto.Description,
            SourceBranch = dto.Source_Branch,
            TargetBranch = dto.Target_Branch,
            State = dto.State,
            Author = dto.Author.Username,
            CreatedAt = new DateTimeOffset(dto.Created_At, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(dto.Updated_At, TimeSpan.Zero),
            MergedAt = dto.Merged_At.HasValue 
                ? new DateTimeOffset(dto.Merged_At.Value, TimeSpan.Zero) 
                : null,
            WebUrl = dto.Web_Url
        };
    }
}
