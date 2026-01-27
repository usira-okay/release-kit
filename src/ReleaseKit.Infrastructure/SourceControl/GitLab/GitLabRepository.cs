using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Logging;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

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
    /// 根據請求拉取 Merge Request 資訊
    /// </summary>
    public async Task<IReadOnlyList<MergeRequest>> FetchMergeRequestsAsync(IGitLabFetchRequest request)
    {
        request.Validate();

        return request.FetchMode switch
        {
            GitLabFetchMode.DateTimeRange => await FetchByDateTimeRangeAsync((DateTimeRangeFetchRequest)request),
            GitLabFetchMode.BranchDiff => await FetchByBranchDiffAsync((BranchDiffFetchRequest)request),
            _ => throw new ArgumentException($"不支援的拉取模式: {request.FetchMode}")
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
        var request = new DateTimeRangeFetchRequest
        {
            ProjectId = projectId,
            StartDateTime = startTime,
            EndDateTime = endTime,
            State = state
        };

        return await FetchByDateTimeRangeAsync(request);
    }

    /// <summary>
    /// 比較兩個分支之間的 commit 差異，並取得相關的 Merge Request
    /// </summary>
    public async Task<IReadOnlyList<MergeRequest>> FetchMergeRequestsByBranchComparisonAsync(
        string projectId,
        string sourceBranch,
        string targetBranch)
    {
        var request = new BranchDiffFetchRequest
        {
            ProjectId = projectId,
            SourceBranch = sourceBranch,
            TargetBranch = targetBranch
        };

        return await FetchByBranchDiffAsync(request);
    }

    /// <summary>
    /// 根據時間區間拉取
    /// </summary>
    private async Task<IReadOnlyList<MergeRequest>> FetchByDateTimeRangeAsync(DateTimeRangeFetchRequest request)
    {
        request.Validate();

        _logger.LogInformation(
            "開始拉取 GitLab MR 資訊，專案: {ProjectId}, 時間區間: {StartTime} ~ {EndTime}, 狀態: {State}",
            request.ProjectId, request.StartDateTime, request.EndDateTime, request.State ?? "all");

        var encodedProjectId = HttpUtility.UrlEncode(request.ProjectId);
        var queryParams = new List<string>
        {
            $"updated_after={request.StartDateTime.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}",
            $"updated_before={request.EndDateTime.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}",
            "per_page=100"
        };

        if (!string.IsNullOrWhiteSpace(request.State))
        {
            queryParams.Add($"state={request.State}");
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

            // 以 merged_at 過濾時間區間
            var filteredDtos = dtos.Where(dto => 
            {
                if (dto.MergedAt.HasValue)
                {
                    var mergedAt = new DateTimeOffset(dto.MergedAt.Value, TimeSpan.Zero);
                    return mergedAt >= request.StartDateTime && mergedAt <= request.EndDateTime;
                }
                // 如果沒有 merged_at，使用 updated_at 判斷
                return true;
            }).ToList();

            mergeRequests.AddRange(filteredDtos.Select(MapToMergeRequest));
            page++;
        }

        _logger.LogInformation("成功拉取 {Count} 筆 MR 資料", mergeRequests.Count);
        return mergeRequests.AsReadOnly();
    }

    /// <summary>
    /// 根據分支差異拉取
    /// </summary>
    private async Task<IReadOnlyList<MergeRequest>> FetchByBranchDiffAsync(BranchDiffFetchRequest request)
    {
        request.Validate();

        _logger.LogInformation(
            "開始比較分支差異並拉取相關 MR，專案: {ProjectId}, 來源分支: {SourceBranch}, 目標分支: {TargetBranch}",
            request.ProjectId, request.SourceBranch, request.TargetBranch);

        var encodedProjectId = HttpUtility.UrlEncode(request.ProjectId);
        var encodedSourceBranch = HttpUtility.UrlEncode(request.SourceBranch);
        var encodedTargetBranch = HttpUtility.UrlEncode(request.TargetBranch);
        
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

        var compareResult = await compareResponse.Content.ReadFromJsonAsync<GitLabCompareResultDto>(_jsonOptions);

        if (compareResult == null || compareResult.Commits.Count == 0)
        {
            _logger.LogInformation("兩個分支之間沒有差異");
            return Array.Empty<MergeRequest>();
        }

        _logger.LogInformation("發現 {Count} 個 commit 差異", compareResult.Commits.Count);

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
            SourceBranch = dto.SourceBranch,
            TargetBranch = dto.TargetBranch,
            State = dto.State,
            Author = dto.Author.Username,
            AuthorId = dto.Author.Id,
            CreatedAt = new DateTimeOffset(dto.CreatedAt, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(dto.UpdatedAt, TimeSpan.Zero),
            MergedAt = dto.MergedAt.HasValue 
                ? new DateTimeOffset(dto.MergedAt.Value, TimeSpan.Zero) 
                : null,
            WebUrl = dto.WebUrl
        };
    }
}
