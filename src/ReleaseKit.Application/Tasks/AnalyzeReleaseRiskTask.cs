using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Application.Common;
using ReleaseKit.Application.Common.RiskAnalysis;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// Release Risk Analysis 主要協調任務，負責執行五階段風險分析管線
/// </summary>
/// <remarks>
/// Phase 1: 收集 PR Diff（透過 API）
/// Phase 2: AI 初篩風險
/// Phase 3: 深度分析（Clone Repos + 完整上下文）
/// Phase 4: 跨服務關聯分析
/// Phase 5: 產生報告
/// </remarks>
public class AnalyzeReleaseRiskTask : ITask
{
    private readonly IRedisService _redisService;
    private readonly IRiskAnalyzer _riskAnalyzer;
    private readonly INow _now;
    private readonly IDiffProvider _gitLabDiffProvider;
    private readonly IDiffProvider _bitbucketDiffProvider;
    private readonly IRepositoryCloner _repositoryCloner;
    private readonly RiskReportGenerator _reportGenerator;
    private readonly IOptions<RiskAnalysisOptions> _riskOptions;
    private readonly IOptions<GitLabOptions> _gitLabOptions;
    private readonly IOptions<BitbucketOptions> _bitbucketOptions;
    private readonly ILogger<AnalyzeReleaseRiskTask> _logger;

    /// <summary>
    /// 初始化 <see cref="AnalyzeReleaseRiskTask"/> 類別的新執行個體
    /// </summary>
    /// <param name="redisService">Redis 快取服務</param>
    /// <param name="riskAnalyzer">AI 風險分析服務</param>
    /// <param name="now">時間提供者</param>
    /// <param name="gitLabDiffProvider">GitLab Diff 提供者</param>
    /// <param name="bitbucketDiffProvider">Bitbucket Diff 提供者</param>
    /// <param name="repositoryCloner">Repository Clone 服務</param>
    /// <param name="reportGenerator">風險報告產生器</param>
    /// <param name="riskOptions">風險分析設定</param>
    /// <param name="gitLabOptions">GitLab 設定</param>
    /// <param name="bitbucketOptions">Bitbucket 設定</param>
    /// <param name="logger">日誌記錄器</param>
    public AnalyzeReleaseRiskTask(
        IRedisService redisService,
        IRiskAnalyzer riskAnalyzer,
        INow now,
        [FromKeyedServices("GitLab")] IDiffProvider gitLabDiffProvider,
        [FromKeyedServices("Bitbucket")] IDiffProvider bitbucketDiffProvider,
        IRepositoryCloner repositoryCloner,
        RiskReportGenerator reportGenerator,
        IOptions<RiskAnalysisOptions> riskOptions,
        IOptions<GitLabOptions> gitLabOptions,
        IOptions<BitbucketOptions> bitbucketOptions,
        ILogger<AnalyzeReleaseRiskTask> logger)
    {
        _redisService = redisService;
        _riskAnalyzer = riskAnalyzer;
        _now = now;
        _gitLabDiffProvider = gitLabDiffProvider;
        _bitbucketDiffProvider = bitbucketDiffProvider;
        _repositoryCloner = repositoryCloner;
        _reportGenerator = reportGenerator;
        _riskOptions = riskOptions;
        _gitLabOptions = gitLabOptions;
        _bitbucketOptions = bitbucketOptions;
        _logger = logger;
    }

    /// <summary>
    /// 執行 Release Risk Analysis 五階段管線
    /// </summary>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始執行 Release Risk Analysis");

        // Phase 1: 從 Redis 載入 PR 資料並透過 API 取得 Diff
        var allDiffs = await CollectDiffsAsync();
        if (allDiffs.Count == 0)
        {
            _logger.LogWarning("沒有找到 PR diff 資料，跳過風險分析");
            return;
        }

        await _redisService.HashSetAsync(
            RedisKeys.ReleaseDataHash, RedisKeys.Fields.RiskDiffs, allDiffs.ToJson());

        // Phase 2: AI 初篩
        var screenResults = await ScreenRisksAsync(allDiffs);
        await _redisService.HashSetAsync(
            RedisKeys.ReleaseDataHash, RedisKeys.Fields.RiskScreenResults, screenResults.ToJson());

        // Phase 3: 深度分析高風險 PR
        var riskThreshold = Enum.TryParse<RiskLevel>(
            _riskOptions.Value.RiskThresholdForDeepAnalysis, out var threshold)
                ? threshold
                : RiskLevel.Medium;

        var highRiskPrs = screenResults
            .Where(r => r.RiskLevel >= riskThreshold && r.NeedsDeepAnalysis)
            .ToList();

        var deepResults = screenResults.ToList();
        if (highRiskPrs.Count > 0)
        {
            var deepAnalyzed = await DeepAnalyzeAsync(highRiskPrs, allDiffs);
            var deepPrIds = deepAnalyzed.Select(d => d.PrId).ToHashSet();
            deepResults = deepResults
                .Where(r => !deepPrIds.Contains(r.PrId))
                .Concat(deepAnalyzed)
                .ToList();
        }

        await _redisService.HashSetAsync(
            RedisKeys.ReleaseDataHash, RedisKeys.Fields.RiskDeepResults, deepResults.ToJson());

        // Phase 4: 跨服務關聯分析
        var crossServiceRisks = await AnalyzeCrossServiceAsync(deepResults);
        await _redisService.HashSetAsync(
            RedisKeys.ReleaseDataHash, RedisKeys.Fields.RiskCrossServiceResults, crossServiceRisks.ToJson());

        // Phase 5: 產生報告
        await GenerateReportAsync(deepResults, crossServiceRisks);

        _logger.LogInformation("Release Risk Analysis 完成");
    }

    /// <summary>
    /// Phase 1: 從 Redis 載入 PR 資料，並透過 DiffProvider 取得每個 PR 的 Diff
    /// </summary>
    private async Task<List<PullRequestDiff>> CollectDiffsAsync()
    {
        using var semaphore = new SemaphoreSlim(5, 5);
        var diffTasks = new List<Task<PullRequestDiff?>>();

        // 收集 GitLab 平台的 diff 任務
        var gitLabJson = await _redisService.HashGetAsync(RedisKeys.GitLabHash, RedisKeys.Fields.PullRequests);
        if (!string.IsNullOrEmpty(gitLabJson))
        {
            var gitLabResult = gitLabJson.ToTypedObject<FetchResult>();
            if (gitLabResult != null)
            {
                diffTasks.AddRange(
                    CreateDiffTasks(gitLabResult, _gitLabDiffProvider, semaphore, "GitLab"));
            }
        }

        // 收集 Bitbucket 平台的 diff 任務
        var bitbucketJson = await _redisService.HashGetAsync(RedisKeys.BitbucketHash, RedisKeys.Fields.PullRequests);
        if (!string.IsNullOrEmpty(bitbucketJson))
        {
            var bitbucketResult = bitbucketJson.ToTypedObject<FetchResult>();
            if (bitbucketResult != null)
            {
                diffTasks.AddRange(
                    CreateDiffTasks(bitbucketResult, _bitbucketDiffProvider, semaphore, "Bitbucket"));
            }
        }

        var results = await Task.WhenAll(diffTasks);
        var allDiffs = results.OfType<PullRequestDiff>().ToList();

        _logger.LogInformation("共取得 {Count} 個 PR diff", allDiffs.Count);
        return allDiffs;
    }

    /// <summary>
    /// 為指定平台的所有 PR 建立並行的 diff 擷取任務清單（共用 Semaphore，最多 5 個同時執行）
    /// </summary>
    private IEnumerable<Task<PullRequestDiff?>> CreateDiffTasks(
        FetchResult fetchResult,
        IDiffProvider diffProvider,
        SemaphoreSlim semaphore,
        string platformName)
    {
        foreach (var project in fetchResult.Results.Where(r => r.Error == null))
        {
            foreach (var pr in project.PullRequests)
            {
                yield return FetchDiffWithSemaphoreAsync(
                    diffProvider, semaphore, project.ProjectPath, pr, platformName);
            }
        }
    }

    /// <summary>
    /// 使用 Semaphore 控制並行上限，擷取單一 PR 的 diff
    /// </summary>
    private async Task<PullRequestDiff?> FetchDiffWithSemaphoreAsync(
        IDiffProvider diffProvider,
        SemaphoreSlim semaphore,
        string projectPath,
        MergeRequestOutput pr,
        string platformName)
    {
        await semaphore.WaitAsync();
        try
        {
            var diffResult = await diffProvider.GetDiffAsync(projectPath, pr.PrId);
            if (diffResult.IsSuccess)
            {
                return diffResult.Value! with { PullRequest = pr };
            }

            _logger.LogWarning("{Platform} diff 取得失敗: {Project} PR#{PrId} - {Error}",
                platformName, projectPath, pr.PrId, diffResult.Error?.Message);
            return null;
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Phase 2: AI 批次初篩風險分類
    /// </summary>
    private async Task<IReadOnlyList<PullRequestRisk>> ScreenRisksAsync(List<PullRequestDiff> diffs)
    {
        _logger.LogInformation("Phase 2: 開始 AI 初篩，共 {Count} 個 PR", diffs.Count);

        var inputs = diffs.Select(d => new ScreenRiskInput
        {
            PrId = d.PullRequest.PrId,
            PrTitle = d.PullRequest.Title,
            PrUrl = d.PullRequest.PRUrl,
            RepositoryName = d.RepositoryName,
            DiffSummary = BuildDiffSummary(d)
        }).ToList();

        var allResults = new List<PullRequestRisk>();
        var batchSize = _riskOptions.Value.BatchSize;

        for (var i = 0; i < inputs.Count; i += batchSize)
        {
            var batch = inputs.Skip(i).Take(batchSize).ToList();
            _logger.LogInformation("Phase 2 批次 {BatchNum}: 分析 {Count} 個 PR",
                (i / batchSize) + 1, batch.Count);
            var results = await _riskAnalyzer.ScreenRisksAsync(batch);
            allResults.AddRange(results);
        }

        return allResults;
    }

    /// <summary>
    /// 組建 PR diff 摘要，供 AI 初篩使用
    /// </summary>
    private static string BuildDiffSummary(PullRequestDiff diff)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"PR: {diff.PullRequest.Title}");
        sb.AppendLine($"Branch: {diff.PullRequest.SourceBranch} → {diff.PullRequest.TargetBranch}");
        sb.AppendLine($"Files changed: {diff.Files.Count}");

        foreach (var file in diff.Files)
        {
            sb.AppendLine($"  {file.FilePath} (+{file.AddedLines}/-{file.DeletedLines})");
            if (file.DiffContent.Length <= 500)
            {
                sb.AppendLine(file.DiffContent);
            }
            else
            {
                sb.AppendLine(string.Concat(file.DiffContent.AsSpan(0, 500), "... (truncated)"));
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Phase 3: 深度分析高風險 PR（Clone Repos 取得完整上下文）
    /// </summary>
    private async Task<IReadOnlyList<PullRequestRisk>> DeepAnalyzeAsync(
        List<PullRequestRisk> highRiskPrs,
        List<PullRequestDiff> allDiffs)
    {
        _logger.LogInformation("Phase 3: 深度分析 {Count} 個高風險 PR", highRiskPrs.Count);

        var clonedPaths = await CloneAllRepositoriesAsync();

        var inputs = new List<DeepAnalyzeInput>();
        foreach (var risk in highRiskPrs)
        {
            var diff = allDiffs.FirstOrDefault(d => d.PullRequest.PrId == risk.PrId);
            if (diff == null) continue;

            var fullContext = BuildFullContext(risk, diff, clonedPaths);

            inputs.Add(new DeepAnalyzeInput
            {
                PrId = risk.PrId,
                RepositoryName = risk.RepositoryName,
                InitialRiskSummary = $"Level: {risk.RiskLevel}, Categories: {string.Join(", ", risk.RiskCategories)}, Description: {risk.RiskDescription}",
                FullContext = fullContext
            });
        }

        if (inputs.Count == 0)
        {
            return highRiskPrs;
        }

        var results = await _riskAnalyzer.DeepAnalyzeAsync(inputs);

        if (_riskOptions.Value.CleanupAfterAnalysis)
        {
            foreach (var path in clonedPaths.Values)
            {
                await _repositoryCloner.CleanupAsync(path);
            }
        }

        return results;
    }

    /// <summary>
    /// Clone 所有已設定的 Repository
    /// </summary>
    private async Task<Dictionary<string, string>> CloneAllRepositoriesAsync()
    {
        var clonedPaths = new Dictionary<string, string>();
        var basePath = _riskOptions.Value.CloneBasePath;

        foreach (var project in _gitLabOptions.Value.Projects)
        {
            var apiUrl = _gitLabOptions.Value.ApiUrl;
            var host = new Uri(apiUrl).Host;
            var token = _gitLabOptions.Value.AccessToken;
            var cloneUrl = $"https://oauth2:{token}@{host}/{project.ProjectPath}.git";
            var targetPath = Path.Combine(basePath, project.ProjectPath.Replace("/", "_"));

            var result = await _repositoryCloner.CloneAsync(cloneUrl, targetPath);
            if (result.IsSuccess)
            {
                clonedPaths[project.ProjectPath] = result.Value!;
            }
            else
            {
                _logger.LogWarning("Clone 失敗: {Project} - {Error}",
                    project.ProjectPath, result.Error?.Message);
            }
        }

        foreach (var project in _bitbucketOptions.Value.Projects)
        {
            var email = Uri.EscapeDataString(_bitbucketOptions.Value.Email);
            var token = Uri.EscapeDataString(_bitbucketOptions.Value.AccessToken);
            var cloneUrl = $"https://{email}:{token}@bitbucket.org/{project.ProjectPath}.git";
            var targetPath = Path.Combine(basePath, project.ProjectPath.Replace("/", "_"));

            var result = await _repositoryCloner.CloneAsync(cloneUrl, targetPath);
            if (result.IsSuccess)
            {
                clonedPaths[project.ProjectPath] = result.Value!;
            }
            else
            {
                _logger.LogWarning("Clone 失敗: {Project} - {Error}",
                    project.ProjectPath, result.Error?.Message);
            }
        }

        _logger.LogInformation("Clone 完成，共 {Count} 個 repository", clonedPaths.Count);
        return clonedPaths;
    }

    /// <summary>
    /// 組建深度分析的完整上下文（含 diff 與完整檔案內容）
    /// </summary>
    private static string BuildFullContext(
        PullRequestRisk risk,
        PullRequestDiff diff,
        Dictionary<string, string> clonedPaths)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== PR: {risk.PrTitle} ({risk.RepositoryName}) ===");
        sb.AppendLine($"Initial Risk: {risk.RiskLevel} - {risk.RiskDescription}");
        sb.AppendLine();

        foreach (var file in diff.Files)
        {
            sb.AppendLine($"--- {file.FilePath} ---");
            sb.AppendLine(file.DiffContent);
            sb.AppendLine();

            if (clonedPaths.TryGetValue(diff.RepositoryName, out var repoPath))
            {
                var fullFilePath = Path.Combine(repoPath, file.FilePath);
                if (File.Exists(fullFilePath))
                {
                    var content = File.ReadAllText(fullFilePath);
                    if (content.Length > 5000)
                    {
                        content = content[..5000] + "\n... (truncated)";
                    }

                    sb.AppendLine($"--- Full file: {file.FilePath} ---");
                    sb.AppendLine(content);
                    sb.AppendLine();
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Phase 4: 跨服務關聯分析
    /// </summary>
    private async Task<IReadOnlyList<CrossServiceRisk>> AnalyzeCrossServiceAsync(
        List<PullRequestRisk> allRisks)
    {
        _logger.LogInformation("Phase 4: 跨服務關聯分析");

        var input = new CrossServiceAnalysisInput
        {
            AllRisks = allRisks,
            ServiceDependencyContext = BuildServiceDependencyContext()
        };

        return await _riskAnalyzer.AnalyzeCrossServiceImpactAsync(input);
    }

    /// <summary>
    /// 組建服務相依性上下文（列出所有已設定的服務清單）
    /// </summary>
    private string BuildServiceDependencyContext()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== 已設定的服務清單 ===");

        foreach (var project in _gitLabOptions.Value.Projects)
        {
            sb.AppendLine($"- GitLab: {project.ProjectPath}");
        }

        foreach (var project in _bitbucketOptions.Value.Projects)
        {
            sb.AppendLine($"- Bitbucket: {project.ProjectPath}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Phase 5: 產生 Markdown 風險報告並寫入檔案
    /// </summary>
    private async Task GenerateReportAsync(
        List<PullRequestRisk> allRisks,
        IReadOnlyList<CrossServiceRisk> crossServiceRisks)
    {
        _logger.LogInformation("Phase 5: 產生風險報告");

        var repoResults = allRisks
            .GroupBy(r => r.RepositoryName)
            .Select(g => new RepositoryRiskResult
            {
                RepositoryName = g.Key,
                Platform = "Unknown",
                PullRequestRisks = g.ToList()
            })
            .ToList();

        var report = new RiskAnalysisReport
        {
            AnalyzedAt = _now.UtcNow,
            RepositoryResults = repoResults,
            CrossServiceRisks = crossServiceRisks
        };

        var markdown = _reportGenerator.GenerateMarkdown(report);

        var outputPath = _riskOptions.Value.ReportOutputPath;
        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        var fileName = $"risk-analysis-{report.AnalyzedAt:yyyy-MM-dd-HHmmss}.md";
        var filePath = Path.Combine(outputPath, fileName);
        await File.WriteAllTextAsync(filePath, markdown);

        _logger.LogInformation("風險報告已產出: {FilePath}", filePath);

        Console.WriteLine(markdown);
    }
}
