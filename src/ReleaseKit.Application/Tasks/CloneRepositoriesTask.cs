using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Common.Git;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// Stage 1：Clone/Pull 所有專案 Repo
/// </summary>
public class CloneRepositoriesTask : ITask
{
    private readonly IGitOperationService _gitService;
    private readonly IRedisService _redisService;
    private readonly INow _now;
    private readonly IOptions<GitLabOptions> _gitLabOptions;
    private readonly IOptions<BitbucketOptions> _bitbucketOptions;
    private readonly IOptions<RiskAnalysisOptions> _riskOptions;
    private readonly ILogger<CloneRepositoriesTask> _logger;

    /// <summary>
    /// 最大並行 clone 數量
    /// </summary>
    private const int MaxConcurrency = 3;

    /// <summary>
    /// 初始化 <see cref="CloneRepositoriesTask"/> 類別的新執行個體
    /// </summary>
    /// <param name="gitService">Git 操作服務</param>
    /// <param name="redisService">Redis 快取服務</param>
    /// <param name="now">時間服務</param>
    /// <param name="gitLabOptions">GitLab 設定選項</param>
    /// <param name="bitbucketOptions">Bitbucket 設定選項</param>
    /// <param name="riskOptions">風險分析設定選項</param>
    /// <param name="logger">日誌記錄器</param>
    public CloneRepositoriesTask(
        IGitOperationService gitService,
        IRedisService redisService,
        INow now,
        IOptions<GitLabOptions> gitLabOptions,
        IOptions<BitbucketOptions> bitbucketOptions,
        IOptions<RiskAnalysisOptions> riskOptions,
        ILogger<CloneRepositoriesTask> logger)
    {
        _gitService = gitService;
        _redisService = redisService;
        _now = now;
        _gitLabOptions = gitLabOptions;
        _bitbucketOptions = bitbucketOptions;
        _riskOptions = riskOptions;
        _logger = logger;
    }

    /// <summary>
    /// 執行 Clone/Pull 所有專案 Repo
    /// </summary>
    public async Task ExecuteAsync()
    {
        var runId = _now.UtcNow.ToString("yyyyMMddHHmmss");
        _logger.LogInformation("開始 Stage 1: Clone Repositories, RunId={RunId}", runId);

        await _redisService.SetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey, runId);

        var cloneTasks = new List<(string ProjectPath, string CloneUrl)>();

        foreach (var project in _gitLabOptions.Value.Projects)
        {
            var url = CloneUrlBuilder.BuildGitLabCloneUrl(_gitLabOptions.Value, project.ProjectPath);
            cloneTasks.Add((project.ProjectPath, url));
        }

        foreach (var project in _bitbucketOptions.Value.Projects)
        {
            var url = CloneUrlBuilder.BuildBitbucketCloneUrl(_bitbucketOptions.Value, project.ProjectPath);
            cloneTasks.Add((project.ProjectPath, url));
        }

        _logger.LogInformation("共 {Count} 個專案需要 Clone/Pull", cloneTasks.Count);

        var semaphore = new SemaphoreSlim(MaxConcurrency);
        var tasks = cloneTasks.Select(t => CloneProjectAsync(t.ProjectPath, t.CloneUrl, runId, semaphore));
        await Task.WhenAll(tasks);

        _logger.LogInformation("Stage 1 完成, RunId={RunId}", runId);
    }

    /// <summary>
    /// Clone 或 Pull 單一專案，並將結果寫入 Redis
    /// </summary>
    /// <param name="projectPath">專案路徑</param>
    /// <param name="cloneUrl">Clone URL</param>
    /// <param name="runId">本次執行 ID</param>
    /// <param name="semaphore">並行控制旗標</param>
    private async Task CloneProjectAsync(string projectPath, string cloneUrl, string runId, SemaphoreSlim semaphore)
    {
        await semaphore.WaitAsync();
        try
        {
            var localPath = Path.Combine(
                _riskOptions.Value.CloneBasePath,
                projectPath.Replace('/', Path.DirectorySeparatorChar));

            var result = await _gitService.CloneOrPullAsync(cloneUrl, localPath);

            var status = result.IsSuccess ? "Success" : $"Failed: {result.Error!.Message}";
            var stageData = new { LocalPath = localPath, Status = status };

            await _redisService.HashSetAsync(
                RiskAnalysisRedisKeys.Stage1Hash(runId),
                projectPath,
                stageData.ToJson());

            if (result.IsSuccess)
                _logger.LogInformation("Clone/Pull 成功: {ProjectPath}", projectPath);
            else
                _logger.LogWarning("Clone/Pull 失敗: {ProjectPath} - {Error}", projectPath, result.Error!.Message);
        }
        finally
        {
            semaphore.Release();
        }
    }
}
