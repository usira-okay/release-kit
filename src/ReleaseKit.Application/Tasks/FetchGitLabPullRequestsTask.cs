using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Application.Common;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 拉取 GitLab Pull Request 資訊任務
/// </summary>
public class FetchGitLabPullRequestsTask : ITask
{
    private readonly ISourceControlRepository _repository;
    private readonly ILogger<FetchGitLabPullRequestsTask> _logger;
    private readonly GitLabOptions _gitLabOptions;
    private readonly FetchModeOptions _fetchModeOptions;

    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="serviceProvider">服務提供者</param>
    /// <param name="logger">日誌記錄器</param>
    /// <param name="gitLabOptions">GitLab 配置選項</param>
    /// <param name="fetchModeOptions">拉取模式配置選項</param>
    public FetchGitLabPullRequestsTask(
        IServiceProvider serviceProvider,
        ILogger<FetchGitLabPullRequestsTask> logger,
        IOptions<GitLabOptions> gitLabOptions,
        IOptions<FetchModeOptions> fetchModeOptions)
    {
        _repository = serviceProvider.GetRequiredKeyedService<ISourceControlRepository>("GitLab");
        _logger = logger;
        _gitLabOptions = gitLabOptions.Value;
        _fetchModeOptions = fetchModeOptions.Value;
    }

    /// <summary>
    /// 執行拉取 GitLab Pull Request 資訊任務
    /// </summary>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始執行 GitLab Pull Request 拉取任務");

        var projectResults = new List<ProjectResult>();

        // 處理每個專案
        foreach (var project in _gitLabOptions.Projects)
        {
            _logger.LogInformation("處理專案: {ProjectPath}", project.ProjectPath);

            var projectResult = new ProjectResult
            {
                ProjectPath = project.ProjectPath,
                Platform = SourceControlPlatform.GitLab,
                PullRequests = new List<MergeRequestOutput>()
            };

            try
            {
                // 專案層級設定覆蓋全域設定
                var fetchMode = project.FetchMode ?? _fetchModeOptions.FetchMode;
                
                List<MergeRequest> mergeRequests;
                
                if (fetchMode == FetchMode.DateTimeRange)
                {
                    mergeRequests = await ExecuteDateTimeRangeModeAsync(_repository, project);
                }
                else if (fetchMode == FetchMode.BranchDiff)
                {
                    mergeRequests = await ExecuteBranchDiffModeAsync(_repository, project);
                }
                else
                {
                    throw new InvalidOperationException($"不支援的擷取模式: {fetchMode}");
                }

                // 轉換為輸出格式
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
        var json = fetchResult.ToJson();
        System.Console.WriteLine(json);

        var totalMRs = projectResults.Sum(r => r.PullRequests.Count);
        var failedProjects = projectResults.Count(r => r.Error != null);
        _logger.LogInformation(
            "GitLab Pull Request 拉取任務完成，共處理 {ProjectCount} 個專案，取得 {Count} 筆 MR，{FailedCount} 個專案失敗",
            projectResults.Count,
            totalMRs,
            failedProjects);
    }

    /// <summary>
    /// 執行 DateTimeRange 模式
    /// </summary>
    private async Task<List<MergeRequest>> ExecuteDateTimeRangeModeAsync(
        ISourceControlRepository repository,
        GitLabProjectOptions project)
    {
        // 取得時間參數：專案層級優先，否則使用全域設定
        var startDateTime = project.StartDateTime ?? _fetchModeOptions.StartDateTime;
        var endDateTime = project.EndDateTime ?? _fetchModeOptions.EndDateTime;
        var targetBranch = project.TargetBranch ?? _fetchModeOptions.TargetBranch;

        // 驗證必填參數
        if (!startDateTime.HasValue)
        {
            throw new InvalidOperationException($"專案 {project.ProjectPath} 缺少必填參數: StartDateTime");
        }

        if (!endDateTime.HasValue)
        {
            throw new InvalidOperationException($"專案 {project.ProjectPath} 缺少必填參數: EndDateTime");
        }

        if (string.IsNullOrEmpty(targetBranch))
        {
            throw new InvalidOperationException($"專案 {project.ProjectPath} 缺少必填參數: TargetBranch");
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
            throw new InvalidOperationException($"拉取專案 {project.ProjectPath} 的 MR 失敗: {result.Error}");
        }

        _logger.LogInformation("專案 {ProjectPath} 取得 {Count} 筆 MR", project.ProjectPath, result.Value?.Count ?? 0);
        return result.Value?.ToList() ?? new List<MergeRequest>();
    }

    /// <summary>
    /// 執行 BranchDiff 模式
    /// </summary>
    private async Task<List<MergeRequest>> ExecuteBranchDiffModeAsync(
        ISourceControlRepository repository,
        GitLabProjectOptions project)
    {
        // 取得分支參數：專案層級優先，否則使用全域設定
        var sourceBranch = project.SourceBranch ?? _fetchModeOptions.SourceBranch;
        var targetBranch = project.TargetBranch ?? _fetchModeOptions.TargetBranch;

        // 驗證必填參數
        if (string.IsNullOrEmpty(sourceBranch))
        {
            throw new InvalidOperationException($"專案 {project.ProjectPath} 缺少必填參數: SourceBranch");
        }

        if (string.IsNullOrEmpty(targetBranch))
        {
            throw new InvalidOperationException($"專案 {project.ProjectPath} 缺少必填參數: TargetBranch");
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
            throw new InvalidOperationException($"拉取專案 {project.ProjectPath} 的 MR 失敗: {result.Error}");
        }

        _logger.LogInformation("專案 {ProjectPath} 取得 {Count} 筆 MR", project.ProjectPath, result.Value?.Count ?? 0);
        return result.Value?.ToList() ?? new List<MergeRequest>();
    }
}
