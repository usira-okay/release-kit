using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ReleaseKit.Application.Common;
using ReleaseKit.Application.Common.RiskAnalysis;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Common;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Application.Tests.Tasks;

/// <summary>
/// AnalyzeReleaseRiskTask 單元測試
/// </summary>
public class AnalyzeReleaseRiskTaskTests : IDisposable
{
    private readonly Mock<IRedisService> _redisServiceMock = new();
    private readonly Mock<IRiskAnalyzer> _riskAnalyzerMock = new();
    private readonly Mock<INow> _nowMock = new();
    private readonly Mock<IDiffProvider> _gitLabDiffMock = new();
    private readonly Mock<IDiffProvider> _bitbucketDiffMock = new();
    private readonly Mock<IRepositoryCloner> _clonerMock = new();
    private readonly RiskReportGenerator _reportGenerator = new();
    private readonly Mock<ILogger<AnalyzeReleaseRiskTask>> _loggerMock = new();

    private readonly string _tempReportPath;
    private readonly DateTimeOffset _fixedNow = new(2024, 3, 15, 14, 30, 0, TimeSpan.Zero);

    public AnalyzeReleaseRiskTaskTests()
    {
        _tempReportPath = Path.Combine(Path.GetTempPath(), $"risk-test-{Guid.NewGuid()}");
        _nowMock.Setup(x => x.UtcNow).Returns(_fixedNow);

        _redisServiceMock
            .Setup(x => x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempReportPath))
        {
            Directory.Delete(_tempReportPath, recursive: true);
        }
    }

    private AnalyzeReleaseRiskTask CreateTask(
        RiskAnalysisOptions? riskOptions = null,
        GitLabOptions? gitLabOptions = null,
        BitbucketOptions? bitbucketOptions = null)
    {
        return new AnalyzeReleaseRiskTask(
            _redisServiceMock.Object,
            _riskAnalyzerMock.Object,
            _nowMock.Object,
            _gitLabDiffMock.Object,
            _bitbucketDiffMock.Object,
            _clonerMock.Object,
            _reportGenerator,
            Options.Create(riskOptions ?? new RiskAnalysisOptions { ReportOutputPath = _tempReportPath }),
            Options.Create(gitLabOptions ?? new GitLabOptions()),
            Options.Create(bitbucketOptions ?? new BitbucketOptions()),
            _loggerMock.Object);
    }

    private static FetchResult CreateFetchResult(params ProjectResult[] projects)
    {
        return new FetchResult { Results = projects.ToList() };
    }

    private static ProjectResult CreateProject(
        string projectPath,
        SourceControlPlatform platform = SourceControlPlatform.GitLab,
        params MergeRequestOutput[] prs)
    {
        return new ProjectResult
        {
            ProjectPath = projectPath,
            Platform = platform,
            PullRequests = prs.ToList()
        };
    }

    private static MergeRequestOutput CreatePr(
        string prId,
        string title = "PR Title",
        string sourceBranch = "feature/test",
        string targetBranch = "main")
    {
        return new MergeRequestOutput
        {
            PrId = prId,
            Title = title,
            SourceBranch = sourceBranch,
            TargetBranch = targetBranch,
            PRUrl = $"https://example.com/pr/{prId}",
            AuthorName = "Test Author",
            AuthorUserId = "user1",
            State = "merged",
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static PullRequestDiff CreateDiff(
        MergeRequestOutput pr,
        string repoName,
        string platform = "GitLab")
    {
        return new PullRequestDiff
        {
            PullRequest = pr,
            RepositoryName = repoName,
            Platform = platform,
            Files = new List<FileDiff>
            {
                new()
                {
                    FilePath = "src/Service.cs",
                    AddedLines = 10,
                    DeletedLines = 5,
                    DiffContent = "+added line\n-removed line",
                    IsNewFile = false,
                    IsDeletedFile = false
                }
            }
        };
    }

    private static PullRequestRisk CreateRisk(
        string prId,
        string repoName,
        RiskLevel riskLevel = RiskLevel.Low,
        bool needsDeepAnalysis = false)
    {
        return new PullRequestRisk
        {
            PrId = prId,
            RepositoryName = repoName,
            PrTitle = $"PR {prId}",
            PrUrl = $"https://example.com/pr/{prId}",
            RiskLevel = riskLevel,
            RiskCategories = new List<RiskCategory> { RiskCategory.CoreBusinessLogicChange },
            RiskDescription = "Test risk",
            NeedsDeepAnalysis = needsDeepAnalysis,
            AffectedComponents = new List<string> { "Service" },
            SuggestedAction = "Review carefully"
        };
    }

    #region Phase 1 測試

    /// <summary>
    /// 當 Redis 沒有 PR 資料時，應記錄警告並返回
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenNoPrData_ShouldLogWarningAndReturn()
    {
        // Arrange
        _redisServiceMock
            .Setup(x => x.HashGetAsync(RedisKeys.GitLabHash, RedisKeys.Fields.PullRequests))
            .ReturnsAsync((string?)null);
        _redisServiceMock
            .Setup(x => x.HashGetAsync(RedisKeys.BitbucketHash, RedisKeys.Fields.PullRequests))
            .ReturnsAsync((string?)null);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 不應呼叫 AI 分析
        _riskAnalyzerMock.Verify(
            x => x.ScreenRisksAsync(It.IsAny<IReadOnlyList<ScreenRiskInput>>()),
            Times.Never);
    }

    /// <summary>
    /// 應為每個 PR 呼叫對應的 DiffProvider
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldCallDiffProviderForEachPr()
    {
        // Arrange
        var pr1 = CreatePr("1", "Fix bug");
        var pr2 = CreatePr("2", "Add feature");
        var project = CreateProject("group/project-a", SourceControlPlatform.GitLab, pr1, pr2);
        var fetchResult = CreateFetchResult(project);

        _redisServiceMock
            .Setup(x => x.HashGetAsync(RedisKeys.GitLabHash, RedisKeys.Fields.PullRequests))
            .ReturnsAsync(fetchResult.ToJson());
        _redisServiceMock
            .Setup(x => x.HashGetAsync(RedisKeys.BitbucketHash, RedisKeys.Fields.PullRequests))
            .ReturnsAsync((string?)null);

        var diff1 = CreateDiff(pr1, "group/project-a");
        var diff2 = CreateDiff(pr2, "group/project-a");

        _gitLabDiffMock
            .Setup(x => x.GetDiffAsync("group/project-a", "1"))
            .ReturnsAsync(Result<PullRequestDiff>.Success(diff1));
        _gitLabDiffMock
            .Setup(x => x.GetDiffAsync("group/project-a", "2"))
            .ReturnsAsync(Result<PullRequestDiff>.Success(diff2));

        SetupDefaultScreenAndCrossService();

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _gitLabDiffMock.Verify(x => x.GetDiffAsync("group/project-a", "1"), Times.Once);
        _gitLabDiffMock.Verify(x => x.GetDiffAsync("group/project-a", "2"), Times.Once);
    }

    /// <summary>
    /// 當某個 diff 取得失敗時，應跳過並繼續處理其他 PR
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenDiffFails_ShouldSkipAndContinue()
    {
        // Arrange
        var pr1 = CreatePr("1", "Fix bug");
        var pr2 = CreatePr("2", "Add feature");
        var project = CreateProject("group/project-a", SourceControlPlatform.GitLab, pr1, pr2);
        var fetchResult = CreateFetchResult(project);

        _redisServiceMock
            .Setup(x => x.HashGetAsync(RedisKeys.GitLabHash, RedisKeys.Fields.PullRequests))
            .ReturnsAsync(fetchResult.ToJson());
        _redisServiceMock
            .Setup(x => x.HashGetAsync(RedisKeys.BitbucketHash, RedisKeys.Fields.PullRequests))
            .ReturnsAsync((string?)null);

        // PR 1 fails, PR 2 succeeds
        _gitLabDiffMock
            .Setup(x => x.GetDiffAsync("group/project-a", "1"))
            .ReturnsAsync(Result<PullRequestDiff>.Failure(Error.RiskAnalysis.DiffFetchFailed("group/project-a", "1")));

        var diff2 = CreateDiff(pr2, "group/project-a");
        _gitLabDiffMock
            .Setup(x => x.GetDiffAsync("group/project-a", "2"))
            .ReturnsAsync(Result<PullRequestDiff>.Success(diff2));

        SetupDefaultScreenAndCrossService();

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — ScreenRisksAsync 仍被呼叫（處理成功的 PR）
        _riskAnalyzerMock.Verify(
            x => x.ScreenRisksAsync(It.Is<IReadOnlyList<ScreenRiskInput>>(inputs => inputs.Count == 1)),
            Times.Once);
    }

    #endregion

    #region Phase 2 測試

    /// <summary>
    /// 應以正確的輸入呼叫 ScreenRisksAsync
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldCallScreenRisksWithCorrectInputs()
    {
        // Arrange
        var pr1 = CreatePr("1", "Fix critical bug");
        var project = CreateProject("group/project-a", SourceControlPlatform.GitLab, pr1);
        var fetchResult = CreateFetchResult(project);

        _redisServiceMock
            .Setup(x => x.HashGetAsync(RedisKeys.GitLabHash, RedisKeys.Fields.PullRequests))
            .ReturnsAsync(fetchResult.ToJson());
        _redisServiceMock
            .Setup(x => x.HashGetAsync(RedisKeys.BitbucketHash, RedisKeys.Fields.PullRequests))
            .ReturnsAsync((string?)null);

        var diff = CreateDiff(pr1, "group/project-a");
        _gitLabDiffMock
            .Setup(x => x.GetDiffAsync("group/project-a", "1"))
            .ReturnsAsync(Result<PullRequestDiff>.Success(diff));

        IReadOnlyList<ScreenRiskInput>? capturedInputs = null;
        _riskAnalyzerMock
            .Setup(x => x.ScreenRisksAsync(It.IsAny<IReadOnlyList<ScreenRiskInput>>()))
            .Callback<IReadOnlyList<ScreenRiskInput>>(inputs => capturedInputs = inputs)
            .ReturnsAsync(new List<PullRequestRisk> { CreateRisk("1", "group/project-a") });

        _riskAnalyzerMock
            .Setup(x => x.AnalyzeCrossServiceImpactAsync(It.IsAny<CrossServiceAnalysisInput>()))
            .ReturnsAsync(new List<CrossServiceRisk>());

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        Assert.NotNull(capturedInputs);
        Assert.Single(capturedInputs);
        Assert.Equal("1", capturedInputs[0].PrId);
        Assert.Equal("Fix critical bug", capturedInputs[0].PrTitle);
        Assert.Equal("group/project-a", capturedInputs[0].RepositoryName);
        Assert.Contains("Fix critical bug", capturedInputs[0].DiffSummary);
    }

    #endregion

    #region Phase 3 測試

    /// <summary>
    /// 當初篩發現高風險時，應觸發深度分析
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenHighRiskFound_ShouldTriggerDeepAnalysis()
    {
        // Arrange
        var pr1 = CreatePr("1", "Dangerous change");
        var project = CreateProject("group/project-a", SourceControlPlatform.GitLab, pr1);
        var fetchResult = CreateFetchResult(project);

        _redisServiceMock
            .Setup(x => x.HashGetAsync(RedisKeys.GitLabHash, RedisKeys.Fields.PullRequests))
            .ReturnsAsync(fetchResult.ToJson());
        _redisServiceMock
            .Setup(x => x.HashGetAsync(RedisKeys.BitbucketHash, RedisKeys.Fields.PullRequests))
            .ReturnsAsync((string?)null);

        var diff = CreateDiff(pr1, "group/project-a");
        _gitLabDiffMock
            .Setup(x => x.GetDiffAsync("group/project-a", "1"))
            .ReturnsAsync(Result<PullRequestDiff>.Success(diff));

        // 初篩回傳 High risk + NeedsDeepAnalysis
        var highRisk = CreateRisk("1", "group/project-a", RiskLevel.High, needsDeepAnalysis: true);
        _riskAnalyzerMock
            .Setup(x => x.ScreenRisksAsync(It.IsAny<IReadOnlyList<ScreenRiskInput>>()))
            .ReturnsAsync(new List<PullRequestRisk> { highRisk });

        _riskAnalyzerMock
            .Setup(x => x.DeepAnalyzeAsync(It.IsAny<IReadOnlyList<DeepAnalyzeInput>>()))
            .ReturnsAsync(new List<PullRequestRisk> { highRisk });

        _riskAnalyzerMock
            .Setup(x => x.AnalyzeCrossServiceImpactAsync(It.IsAny<CrossServiceAnalysisInput>()))
            .ReturnsAsync(new List<CrossServiceRisk>());

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _riskAnalyzerMock.Verify(
            x => x.DeepAnalyzeAsync(It.Is<IReadOnlyList<DeepAnalyzeInput>>(inputs => inputs.Count == 1)),
            Times.Once);
    }

    /// <summary>
    /// 當無高風險時，不應觸發深度分析
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenNoHighRisk_ShouldSkipDeepAnalysis()
    {
        // Arrange
        var pr1 = CreatePr("1", "Minor fix");
        var project = CreateProject("group/project-a", SourceControlPlatform.GitLab, pr1);
        var fetchResult = CreateFetchResult(project);

        _redisServiceMock
            .Setup(x => x.HashGetAsync(RedisKeys.GitLabHash, RedisKeys.Fields.PullRequests))
            .ReturnsAsync(fetchResult.ToJson());
        _redisServiceMock
            .Setup(x => x.HashGetAsync(RedisKeys.BitbucketHash, RedisKeys.Fields.PullRequests))
            .ReturnsAsync((string?)null);

        var diff = CreateDiff(pr1, "group/project-a");
        _gitLabDiffMock
            .Setup(x => x.GetDiffAsync("group/project-a", "1"))
            .ReturnsAsync(Result<PullRequestDiff>.Success(diff));

        // 初篩回傳 Low risk
        var lowRisk = CreateRisk("1", "group/project-a", RiskLevel.Low, needsDeepAnalysis: false);
        _riskAnalyzerMock
            .Setup(x => x.ScreenRisksAsync(It.IsAny<IReadOnlyList<ScreenRiskInput>>()))
            .ReturnsAsync(new List<PullRequestRisk> { lowRisk });

        _riskAnalyzerMock
            .Setup(x => x.AnalyzeCrossServiceImpactAsync(It.IsAny<CrossServiceAnalysisInput>()))
            .ReturnsAsync(new List<CrossServiceRisk>());

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _riskAnalyzerMock.Verify(
            x => x.DeepAnalyzeAsync(It.IsAny<IReadOnlyList<DeepAnalyzeInput>>()),
            Times.Never);
    }

    #endregion

    #region Phase 4 測試

    /// <summary>
    /// 應呼叫跨服務關聯分析
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldCallCrossServiceAnalysis()
    {
        // Arrange
        SetupSinglePrFlow();

        CrossServiceAnalysisInput? capturedInput = null;
        _riskAnalyzerMock
            .Setup(x => x.AnalyzeCrossServiceImpactAsync(It.IsAny<CrossServiceAnalysisInput>()))
            .Callback<CrossServiceAnalysisInput>(input => capturedInput = input)
            .ReturnsAsync(new List<CrossServiceRisk>());

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        Assert.NotNull(capturedInput);
        Assert.NotEmpty(capturedInput.AllRisks);
        Assert.NotEmpty(capturedInput.ServiceDependencyContext);
    }

    #endregion

    #region Phase 5 測試

    /// <summary>
    /// 應產生風險報告檔案
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldGenerateReport()
    {
        // Arrange
        SetupSinglePrFlow();

        _riskAnalyzerMock
            .Setup(x => x.AnalyzeCrossServiceImpactAsync(It.IsAny<CrossServiceAnalysisInput>()))
            .ReturnsAsync(new List<CrossServiceRisk>());

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 報告目錄應存在且包含 markdown 檔案
        Assert.True(Directory.Exists(_tempReportPath));
        var reportFiles = Directory.GetFiles(_tempReportPath, "risk-analysis-*.md");
        Assert.Single(reportFiles);
    }

    #endregion

    #region Redis 快取測試

    /// <summary>
    /// 應將各階段結果快取至 Redis
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldCacheResultsToRedis()
    {
        // Arrange
        SetupSinglePrFlow();

        _riskAnalyzerMock
            .Setup(x => x.AnalyzeCrossServiceImpactAsync(It.IsAny<CrossServiceAnalysisInput>()))
            .ReturnsAsync(new List<CrossServiceRisk>());

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 驗證各階段結果都有寫入 Redis
        _redisServiceMock.Verify(
            x => x.HashSetAsync(RedisKeys.ReleaseDataHash, RedisKeys.Fields.RiskDiffs, It.IsAny<string>()),
            Times.Once);
        _redisServiceMock.Verify(
            x => x.HashSetAsync(RedisKeys.ReleaseDataHash, RedisKeys.Fields.RiskScreenResults, It.IsAny<string>()),
            Times.Once);
        _redisServiceMock.Verify(
            x => x.HashSetAsync(RedisKeys.ReleaseDataHash, RedisKeys.Fields.RiskDeepResults, It.IsAny<string>()),
            Times.Once);
        _redisServiceMock.Verify(
            x => x.HashSetAsync(RedisKeys.ReleaseDataHash, RedisKeys.Fields.RiskCrossServiceResults, It.IsAny<string>()),
            Times.Once);
    }

    #endregion

    #region 輔助方法

    /// <summary>
    /// 設定預設的 Screen 與 CrossService 回傳值
    /// </summary>
    private void SetupDefaultScreenAndCrossService()
    {
        _riskAnalyzerMock
            .Setup(x => x.ScreenRisksAsync(It.IsAny<IReadOnlyList<ScreenRiskInput>>()))
            .ReturnsAsync((IReadOnlyList<ScreenRiskInput> inputs) =>
                inputs.Select(i => CreateRisk(i.PrId, i.RepositoryName)).ToList());

        _riskAnalyzerMock
            .Setup(x => x.AnalyzeCrossServiceImpactAsync(It.IsAny<CrossServiceAnalysisInput>()))
            .ReturnsAsync(new List<CrossServiceRisk>());
    }

    /// <summary>
    /// 設定單一 PR 的完整流程（用於 Phase 4/5 測試）
    /// </summary>
    private void SetupSinglePrFlow()
    {
        var pr = CreatePr("1", "Fix something");
        var project = CreateProject("group/project-a", SourceControlPlatform.GitLab, pr);
        var fetchResult = CreateFetchResult(project);

        _redisServiceMock
            .Setup(x => x.HashGetAsync(RedisKeys.GitLabHash, RedisKeys.Fields.PullRequests))
            .ReturnsAsync(fetchResult.ToJson());
        _redisServiceMock
            .Setup(x => x.HashGetAsync(RedisKeys.BitbucketHash, RedisKeys.Fields.PullRequests))
            .ReturnsAsync((string?)null);

        var diff = CreateDiff(pr, "group/project-a");
        _gitLabDiffMock
            .Setup(x => x.GetDiffAsync("group/project-a", "1"))
            .ReturnsAsync(Result<PullRequestDiff>.Success(diff));

        var risk = CreateRisk("1", "group/project-a");
        _riskAnalyzerMock
            .Setup(x => x.ScreenRisksAsync(It.IsAny<IReadOnlyList<ScreenRiskInput>>()))
            .ReturnsAsync(new List<PullRequestRisk> { risk });
    }

    #endregion
}
