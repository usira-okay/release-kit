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
/// 拉取 Pull Request 資訊任務抽象基礎類別
/// </summary>
/// <typeparam name="TOptions">平台配置選項類型</typeparam>
/// <typeparam name="TProjectOptions">專案配置選項類型</typeparam>
public abstract class BaseFetchPullRequestsTask<TOptions, TProjectOptions> : ITask
    where TProjectOptions : IProjectOptions
{
    private readonly ISourceControlRepository _repository;
    private readonly ILogger _logger;
    private readonly FetchModeOptions _fetchModeOptions;

    /// <summary>
    /// 平台配置選項
    /// </summary>
    protected TOptions PlatformOptions { get; }

    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="repository">原始碼控制倉儲</param>
    /// <param name="logger">日誌記錄器</param>
    /// <param name="platformOptions">平台配置選項</param>
    /// <param name="fetchModeOptions">拉取模式配置選項</param>
    protected BaseFetchPullRequestsTask(
        ISourceControlRepository repository,
        ILogger logger,
        TOptions platformOptions,
        IOptions<FetchModeOptions> fetchModeOptions)
    {
        _repository = repository;
        _logger = logger;
        PlatformOptions = platformOptions;
        _fetchModeOptions = fetchModeOptions.Value;
    }

    /// <summary>
    /// 取得平台名稱
    /// </summary>
    protected abstract string PlatformName { get; }

    /// <summary>
    /// 取得平台類型
    /// </summary>
    protected abstract SourceControlPlatform Platform { get; }

    /// <summary>
    /// 取得專案清單
    /// </summary>
    protected abstract IEnumerable<TProjectOptions> GetProjects();

    /// <summary>
    /// 執行拉取 Pull Request 資訊任務
    /// </summary>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始執行 {Platform} Pull Request 拉取任務", PlatformName);

        var projectResults = new List<ProjectResult>();

        // 處理每個專案
        foreach (var project in GetProjects())
        {
            _logger.LogInformation("處理專案: {ProjectPath}", project.ProjectPath);

            var projectResult = new ProjectResult
            {
                ProjectPath = project.ProjectPath,
                Platform = Platform,
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

        var totalPRs = projectResults.Sum(r => r.PullRequests.Count);
        var failedProjects = projectResults.Count(r => r.Error != null);
        _logger.LogInformation(
            "{Platform} Pull Request 拉取任務完成，共處理 {ProjectCount} 個專案，取得 {Count} 筆 PR，{FailedCount} 個專案失敗",
            PlatformName,
            projectResults.Count,
            totalPRs,
            failedProjects);
    }

    /// <summary>
    /// 執行 DateTimeRange 模式
    /// </summary>
    private async Task<List<MergeRequest>> ExecuteDateTimeRangeModeAsync(
        ISourceControlRepository repository,
        TProjectOptions project)
    {
        // 取得時間參數：專案層級優先，否則使用全域設定
        var startDateTime = project.StartDateTime ?? _fetchModeOptions.StartDateTime;
        var endDateTime = project.EndDateTime ?? _fetchModeOptions.EndDateTime;
        var targetBranch = string.IsNullOrWhiteSpace(project.TargetBranch)
            ? _fetchModeOptions.TargetBranch
            : project.TargetBranch;

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
            throw new InvalidOperationException($"拉取專案 {project.ProjectPath} 的 PR/MR 失敗: {result.Error}");
        }

        _logger.LogInformation("專案 {ProjectPath} 取得 {Count} 筆 PR/MR", project.ProjectPath, result.Value?.Count ?? 0);
        return result.Value?.ToList() ?? new List<MergeRequest>();
    }

    /// <summary>
    /// 執行 BranchDiff 模式
    /// </summary>
    private async Task<List<MergeRequest>> ExecuteBranchDiffModeAsync(
        ISourceControlRepository repository,
        TProjectOptions project)
    {
        // 取得分支參數：專案層級優先，否則使用全域設定
        var sourceBranch = string.IsNullOrWhiteSpace(project.SourceBranch)
            ? _fetchModeOptions.SourceBranch
            : project.SourceBranch;
        var targetBranch = string.IsNullOrWhiteSpace(project.TargetBranch)
            ? _fetchModeOptions.TargetBranch
            : project.TargetBranch;

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
            throw new InvalidOperationException($"拉取專案 {project.ProjectPath} 的 PR/MR 失敗: {result.Error}");
        }

        _logger.LogInformation("專案 {ProjectPath} 取得 {Count} 筆 PR/MR", project.ProjectPath, result.Value?.Count ?? 0);
        return result.Value?.ToList() ?? new List<MergeRequest>();
    }
}
