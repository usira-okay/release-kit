using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Application.Common;
using ReleaseKit.Application.Configuration;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Entities;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 拉取 Bitbucket Pull Request 資訊任務
/// </summary>
public class FetchBitbucketPullRequestsTask : ITask
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FetchBitbucketPullRequestsTask> _logger;
    private readonly BitbucketOptions _bitbucketOptions;
    private readonly FetchModeOptions _fetchModeOptions;

    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="serviceProvider">服務提供者</param>
    /// <param name="logger">日誌記錄器</param>
    /// <param name="bitbucketOptions">Bitbucket 配置選項</param>
    /// <param name="fetchModeOptions">拉取模式配置選項</param>
    public FetchBitbucketPullRequestsTask(
        IServiceProvider serviceProvider,
        ILogger<FetchBitbucketPullRequestsTask> logger,
        IOptions<BitbucketOptions> bitbucketOptions,
        IOptions<FetchModeOptions> fetchModeOptions)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _bitbucketOptions = bitbucketOptions.Value;
        _fetchModeOptions = fetchModeOptions.Value;
    }

    /// <summary>
    /// 執行拉取 Bitbucket Pull Request 資訊任務
    /// </summary>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始執行 Bitbucket Pull Request 拉取任務");

        var repository = _serviceProvider.GetRequiredKeyedService<ISourceControlRepository>("Bitbucket");
        var projectResults = new List<ProjectResult>();

        // 處理每個專案
        foreach (var project in _bitbucketOptions.Projects)
        {
            _logger.LogInformation("處理專案: {ProjectPath}", project.ProjectPath);

            var projectResult = new ProjectResult
            {
                ProjectPath = project.ProjectPath,
                Platform = "Bitbucket",
                PullRequests = new List<MergeRequestOutput>()
            };

            try
            {
                // 專案層級設定覆蓋全域設定
                var fetchMode = project.FetchMode ?? _fetchModeOptions.FetchMode;
                
                List<MergeRequest>? mergeRequests = null;
                
                if (fetchMode == FetchMode.DateTimeRange)
                {
                    mergeRequests = await ExecuteDateTimeRangeModeAsync(repository, project);
                }
                else if (fetchMode == FetchMode.BranchDiff)
                {
                    mergeRequests = await ExecuteBranchDiffModeAsync(repository, project);
                }

                // 轉換為輸出格式
                if (mergeRequests != null)
                {
                    projectResult = projectResult with
                    {
                        PullRequests = mergeRequests.Select(mr => new MergeRequestOutput
                        {
                            Title = mr.Title,
                            Description = mr.Description,
                            SourceBranch = mr.SourceBranch,
                            TargetBranch = mr.TargetBranch,
                            CreatedAt = mr.CreatedAt,
                            MergedAt = mr.MergedAt,
                            State = mr.State,
                            AuthorUserId = mr.AuthorUserId,
                            AuthorName = mr.AuthorName,
                            PRUrl = mr.PRUrl
                        }).ToList()
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "處理專案 {ProjectPath} 時發生例外", project.ProjectPath);
                projectResult = projectResult with
                {
                    Error = $"處理失敗: {ex.Message}"
                };
            }

            projectResults.Add(projectResult);
        }

        // 建立最終輸出
        var fetchResult = new FetchResult
        {
            Results = projectResults
        };

        // 輸出 JSON 結果
        var json = fetchResult.SerializeToJson(indented: true);
        System.Console.WriteLine(json);

        var totalPRs = projectResults.Sum(r => r.PullRequests.Count);
        var failedProjects = projectResults.Count(r => r.Error != null);
        _logger.LogInformation(
            "Bitbucket Pull Request 拉取任務完成，共處理 {ProjectCount} 個專案，取得 {Count} 筆 PR，{FailedCount} 個專案失敗",
            projectResults.Count,
            totalPRs,
            failedProjects);
    }

    /// <summary>
    /// 執行 DateTimeRange 模式
    /// </summary>
    private async Task<List<MergeRequest>?> ExecuteDateTimeRangeModeAsync(
        ISourceControlRepository repository,
        BitbucketProjectOptions project)
    {
        // 取得時間參數：專案層級優先，否則使用全域設定
        var startDateTime = project.StartDateTime ?? _fetchModeOptions.StartDateTime;
        var endDateTime = project.EndDateTime ?? _fetchModeOptions.EndDateTime;
        var targetBranch = project.TargetBranch ?? _fetchModeOptions.TargetBranch;

        // 驗證必填參數
        if (!startDateTime.HasValue)
        {
            _logger.LogError("專案 {ProjectPath} 缺少必填參數: StartDateTime", project.ProjectPath);
            return null;
        }

        if (!endDateTime.HasValue)
        {
            _logger.LogError("專案 {ProjectPath} 缺少必填參數: EndDateTime", project.ProjectPath);
            return null;
        }

        if (string.IsNullOrEmpty(targetBranch))
        {
            _logger.LogError("專案 {ProjectPath} 缺少必填參數: TargetBranch", project.ProjectPath);
            return null;
        }

        _logger.LogInformation(
            "使用 DateTimeRange 模式: {TargetBranch}, {StartDateTime} ~ {EndDateTime}",
            targetBranch,
            startDateTime.Value,
            endDateTime.Value);

        var result = await repository.GetMergeRequestsByDateRangeAsync(
            project.ProjectPath,
            targetBranch,
            startDateTime.Value,
            endDateTime.Value);

        if (result.IsFailure)
        {
            _logger.LogError("拉取專案 {ProjectPath} 的 PR 失敗: {Error}", project.ProjectPath, result.Error);
            return null;
        }

        _logger.LogInformation("專案 {ProjectPath} 取得 {Count} 筆 PR", project.ProjectPath, result.Value?.Count ?? 0);
        return result.Value?.ToList();
    }

    /// <summary>
    /// 執行 BranchDiff 模式
    /// </summary>
    private async Task<List<MergeRequest>?> ExecuteBranchDiffModeAsync(
        ISourceControlRepository repository,
        BitbucketProjectOptions project)
    {
        // 取得分支參數：專案層級優先，否則使用全域設定
        var sourceBranch = project.SourceBranch ?? _fetchModeOptions.SourceBranch;
        var targetBranch = project.TargetBranch ?? _fetchModeOptions.TargetBranch;

        // 驗證必填參數
        if (string.IsNullOrEmpty(sourceBranch))
        {
            _logger.LogError("專案 {ProjectPath} 缺少必填參數: SourceBranch", project.ProjectPath);
            return null;
        }

        if (string.IsNullOrEmpty(targetBranch))
        {
            _logger.LogError("專案 {ProjectPath} 缺少必填參數: TargetBranch", project.ProjectPath);
            return null;
        }

        _logger.LogInformation(
            "使用 BranchDiff 模式: {SourceBranch} -> {TargetBranch}",
            sourceBranch,
            targetBranch);

        var result = await repository.GetMergeRequestsByBranchDiffAsync(
            project.ProjectPath,
            sourceBranch,
            targetBranch);

        if (result.IsFailure)
        {
            _logger.LogError("拉取專案 {ProjectPath} 的 PR 失敗: {Error}", project.ProjectPath, result.Error);
            return null;
        }

        _logger.LogInformation("專案 {ProjectPath} 取得 {Count} 筆 PR", project.ProjectPath, result.Value?.Count ?? 0);
        return result.Value?.ToList();
    }
}
