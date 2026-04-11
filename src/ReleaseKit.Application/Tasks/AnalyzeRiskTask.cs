using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Application.Common;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// Agentic 風險分析任務
/// </summary>
/// <remarks>
/// 從 Redis 讀取 PR 資料與 clone 路徑，為每個專案組裝 <see cref="ProjectAnalysisContext"/>，
/// 並行建立 Copilot session 進行 agentic 風險分析。
/// Copilot 自主決定要執行的 shell 指令來探索 repo 並分析風險。
/// </remarks>
public sealed class AnalyzeRiskTask : ITask
{
    private readonly IRedisService _redisService;
    private readonly IRiskAnalyzer _riskAnalyzer;
    private readonly RiskAnalysisOptions _options;
    private readonly ILogger<AnalyzeRiskTask> _logger;

    /// <summary>
    /// 初始化 <see cref="AnalyzeRiskTask"/> 類別的新執行個體
    /// </summary>
    public AnalyzeRiskTask(
        IRedisService redisService,
        IRiskAnalyzer riskAnalyzer,
        IOptions<RiskAnalysisOptions> options,
        ILogger<AnalyzeRiskTask> logger)
    {
        _redisService = redisService;
        _riskAnalyzer = riskAnalyzer;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>執行 agentic 風險分析</summary>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始 Agentic 風險分析");

        var contexts = await BuildProjectContextsAsync();

        if (contexts.Count == 0)
        {
            _logger.LogInformation("無可分析的專案，跳過風險分析");
            return;
        }

        _logger.LogInformation("準備分析 {Count} 個專案", contexts.Count);

        var semaphore = new SemaphoreSlim(_options.MaxConcurrentAnalysis);
        var tasks = contexts.Select((ctx, index) =>
            AnalyzeProjectAsync(ctx, index + 1, semaphore));

        await Task.WhenAll(tasks);

        _logger.LogInformation("Agentic 風險分析完成，共處理 {Count} 個專案", contexts.Count);
    }

    /// <summary>
    /// 從 Redis 讀取 PR 資料與 clone 路徑，組裝各專案的分析上下文
    /// </summary>
    internal async Task<IReadOnlyList<ProjectAnalysisContext>> BuildProjectContextsAsync()
    {
        var gitLabJson = await _redisService.HashGetAsync(
            RedisKeys.GitLabHash, RedisKeys.Fields.PullRequestsByUser);
        var bitbucketJson = await _redisService.HashGetAsync(
            RedisKeys.BitbucketHash, RedisKeys.Fields.PullRequestsByUser);
        var clonePathsJson = await _redisService.HashGetAsync(
            RedisKeys.RiskAnalysisHash, RedisKeys.Fields.ClonePaths);

        var gitLabFetch = gitLabJson?.ToTypedObject<FetchResult>();
        var bitbucketFetch = bitbucketJson?.ToTypedObject<FetchResult>();
        var clonePaths = clonePathsJson?.ToTypedObject<Dictionary<string, string>>()
                         ?? new Dictionary<string, string>();

        if (clonePaths.Count == 0)
        {
            _logger.LogInformation("無 ClonePaths 資料，跳過分析");
            return [];
        }

        var allProjects = new List<ProjectResult>();
        if (gitLabFetch?.Results is not null)
            allProjects.AddRange(gitLabFetch.Results);
        if (bitbucketFetch?.Results is not null)
            allProjects.AddRange(bitbucketFetch.Results);

        var contexts = new List<ProjectAnalysisContext>();

        foreach (var project in allProjects.OrderBy(p => p.ProjectPath))
        {
            if (!clonePaths.TryGetValue(project.ProjectPath, out var clonePath))
            {
                _logger.LogWarning("找不到 {ProjectPath} 的 Clone 路徑，跳過", project.ProjectPath);
                continue;
            }

            var commitShas = project.PullRequests
                .Where(pr => !string.IsNullOrEmpty(pr.MergeCommitSha))
                .Select(pr => pr.MergeCommitSha!)
                .Distinct()
                .ToList();

            if (commitShas.Count == 0)
            {
                _logger.LogWarning("專案 {ProjectPath} 無有效 CommitSha，跳過", project.ProjectPath);
                continue;
            }

            contexts.Add(new ProjectAnalysisContext
            {
                ProjectName = project.ProjectPath,
                RepoPath = clonePath,
                CommitShas = commitShas
            });
        }

        return contexts;
    }

    /// <summary>
    /// 分析單一專案並將結果存入 Redis
    /// </summary>
    private async Task AnalyzeProjectAsync(
        ProjectAnalysisContext context,
        int sequence,
        SemaphoreSlim semaphore)
    {
        await semaphore.WaitAsync();
        try
        {
            _logger.LogInformation("分析專案 {ProjectName}（Sequence={Sequence}，CommitShas={Count}）",
                context.ProjectName, sequence, context.CommitShas.Count);

            var markdown = await _riskAnalyzer.AnalyzeProjectRiskAsync(context);

            await _redisService.HashSetAsync(
                RedisKeys.RiskAnalysisHash,
                $"{RedisKeys.Fields.IntermediatePrefix}{sequence}",
                markdown);

            await _redisService.HashSetAsync(
                RedisKeys.RiskAnalysisHash,
                $"{RedisKeys.Fields.AnalysisContextPrefix}{sequence}",
                context.ToJson());

            _logger.LogInformation("專案 {ProjectName} 分析完成", context.ProjectName);
        }
        finally
        {
            semaphore.Release();
        }
    }
}
