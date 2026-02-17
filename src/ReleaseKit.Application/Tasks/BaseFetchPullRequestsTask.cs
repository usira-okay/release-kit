using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Application.Common;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.Helpers;
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
    private readonly IRedisService _redisService;
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
    /// <param name="redisService">Redis 快取服務</param>
    /// <param name="platformOptions">平台配置選項</param>
    /// <param name="fetchModeOptions">拉取模式配置選項</param>
    protected BaseFetchPullRequestsTask(
        ISourceControlRepository repository,
        ILogger logger,
        IRedisService redisService,
        TOptions platformOptions,
        IOptions<FetchModeOptions> fetchModeOptions)
    {
        _repository = repository;
        _logger = logger;
        _redisService = redisService;
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
    /// 取得 Redis 儲存鍵值
    /// </summary>
    protected abstract string RedisKey { get; }

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

        // 檢查並清除 Redis 中的舊資料
        if (await _redisService.ExistsAsync(RedisKey))
        {
            _logger.LogInformation("清除 Redis 中的舊資料，Key: {RedisKey}", RedisKey);
            await _redisService.DeleteAsync(RedisKey);
        }

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
                        PRUrl = mr.PRUrl,
                        WorkItemId = mr.WorkItemId
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

        // 將結果存入 Redis
        var saveResult = await _redisService.SetAsync(RedisKey, json);
        if (saveResult)
        {
            _logger.LogInformation("成功將資料存入 Redis，Key: {RedisKey}", RedisKey);
        }
        else
        {
            _logger.LogWarning("將資料存入 Redis 失敗，Key: {RedisKey}", RedisKey);
        }

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
            throw new InvalidOperationException(
                $"拉取專案 {project.ProjectPath} 的 PR/MR 失敗: Code={result.Error.Code}, Message={result.Error.Message}");
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

        // 如果 SourceBranch 是 release/yyyyMMdd 格式，動態調整 TargetBranch
        targetBranch = await AdjustTargetBranchForReleaseBranchAsync(
            repository, 
            project.ProjectPath, 
            sourceBranch, 
            targetBranch);

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

    /// <summary>
    /// 針對 release branch 動態調整 TargetBranch
    /// </summary>
    /// <param name="repository">原始碼控制倉儲</param>
    /// <param name="projectPath">專案路徑</param>
    /// <param name="sourceBranch">來源分支</param>
    /// <param name="targetBranch">目標分支</param>
    /// <returns>調整後的目標分支</returns>
    private async Task<string> AdjustTargetBranchForReleaseBranchAsync(
        ISourceControlRepository repository,
        string projectPath,
        string sourceBranch,
        string targetBranch)
    {
        if (!ReleaseBranchHelper.IsReleaseBranch(sourceBranch))
        {
            return targetBranch;
        }

        _logger.LogInformation(
            "偵測到 SourceBranch 為 release branch 格式: {SourceBranch}，開始拉取所有 release branches",
            sourceBranch);

        var branchesResult = await repository.GetBranchesAsync(projectPath, "release/");
        if (!branchesResult.IsSuccess || branchesResult.Value == null || branchesResult.Value.Count == 0)
        {
            _logger.LogWarning(
                "無法拉取 release branches 或沒有找到任何 release branch，保持 TargetBranch 為 {TargetBranch}",
                targetBranch);
            return targetBranch;
        }

        var allReleaseBranches = branchesResult.Value.ToList();
        _logger.LogInformation(
            "找到 {Count} 個 release branches",
            allReleaseBranches.Count(ReleaseBranchHelper.IsReleaseBranch));

        // 判斷 SourceBranch 是否為最新的 release branch
        if (ReleaseBranchHelper.IsLatestReleaseBranch(sourceBranch, allReleaseBranches))
        {
            _logger.LogInformation(
                "SourceBranch {SourceBranch} 是最新的 release branch，保持 TargetBranch 為 {TargetBranch}",
                sourceBranch,
                targetBranch);
            return targetBranch;
        }

        // 找出下一個較新的 release branch
        var nextNewerBranch = ReleaseBranchHelper.FindNextNewerReleaseBranch(sourceBranch, allReleaseBranches);
        if (nextNewerBranch != null)
        {
            _logger.LogInformation(
                "SourceBranch {SourceBranch} 不是最新的 release branch，將 TargetBranch 從 {OldTargetBranch} 改為 {NewTargetBranch}",
                sourceBranch,
                targetBranch,
                nextNewerBranch);
            return nextNewerBranch;
        }

        _logger.LogWarning(
            "無法找到比 {SourceBranch} 更新的 release branch，保持 TargetBranch 為 {TargetBranch}",
            sourceBranch,
            targetBranch);
        return targetBranch;
    }
}
