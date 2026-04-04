using ReleaseKit.Application.Common.RiskAnalysis;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Application.Tests.Common.RiskAnalysis;

/// <summary>
/// RiskReportGenerator 的單元測試
/// </summary>
public sealed class RiskReportGeneratorTests
{
    private readonly RiskReportGenerator _sut = new();

    [Fact]
    public void GenerateMarkdown_WithEmptyReport_ShouldContainHeaderAndEmptySummary()
    {
        // Arrange
        var report = CreateReport(
            analyzedAt: new DateTimeOffset(2024, 3, 15, 14, 30, 0, TimeSpan.Zero),
            repoResults: Array.Empty<RepositoryRiskResult>());

        // Act
        var result = _sut.GenerateMarkdown(report);

        // Assert
        Assert.Contains("Release Risk Analysis Report", result);
        Assert.Contains("0 個 Repository", result);
        Assert.Contains("0 個 Pull Request", result);
    }

    [Fact]
    public void GenerateMarkdown_WithCriticalRisk_ShouldContainRedEmoji()
    {
        // Arrange
        var prRisk = CreatePrRisk("456", "OrderService", RiskLevel.Critical,
            RiskCategory.CoreBusinessLogicChange, RiskCategory.CrossServiceApiBreaking);
        var repo = CreateRepoResult("OrderService", "GitLab", prRisk);
        var report = CreateReport(
            analyzedAt: new DateTimeOffset(2024, 3, 15, 14, 30, 0, TimeSpan.Zero),
            repoResults: new[] { repo });

        // Act
        var result = _sut.GenerateMarkdown(report);

        // Assert
        Assert.Contains("🔴", result);
        Assert.Contains("Critical", result);
    }

    [Fact]
    public void GenerateMarkdown_WithMultipleRisks_ShouldShowCorrectCounts()
    {
        // Arrange
        var critical = CreatePrRisk("1", "RepoA", RiskLevel.Critical, RiskCategory.CoreBusinessLogicChange);
        var high = CreatePrRisk("2", "RepoA", RiskLevel.High, RiskCategory.SecurityChange);
        var medium = CreatePrRisk("3", "RepoB", RiskLevel.Medium, RiskCategory.PerformanceChange);
        var low1 = CreatePrRisk("4", "RepoB", RiskLevel.Low, RiskCategory.ConfigurationChange);
        var low2 = CreatePrRisk("5", "RepoB", RiskLevel.Low, RiskCategory.ConfigurationChange);

        var repoA = CreateRepoResult("RepoA", "GitLab", critical, high);
        var repoB = CreateRepoResult("RepoB", "Bitbucket", medium, low1, low2);

        var report = CreateReport(
            analyzedAt: new DateTimeOffset(2024, 3, 15, 14, 30, 0, TimeSpan.Zero),
            repoResults: new[] { repoA, repoB });

        // Act
        var result = _sut.GenerateMarkdown(report);

        // Assert — 風險等級摘要區塊應包含正確計數
        Assert.Contains("🔴 Critical | 1", result);
        Assert.Contains("🟠 High | 1", result);
        Assert.Contains("🟡 Medium | 1", result);
        Assert.Contains("🟢 Low | 2", result);
    }

    [Fact]
    public void GenerateMarkdown_WithCrossServiceRisks_ShouldContainCrossServiceSection()
    {
        // Arrange
        var prRisk = CreatePrRisk("456", "OrderService", RiskLevel.Critical,
            RiskCategory.CrossServiceApiBreaking);
        var repo = CreateRepoResult("OrderService", "GitLab", prRisk);

        var crossRisk = new CrossServiceRisk
        {
            SourceService = "OrderService",
            AffectedServices = new[] { "PaymentService", "NotificationService" },
            RiskLevel = RiskLevel.Critical,
            ImpactDescription = "訂單服務 API 回傳格式變更影響下游服務",
            SuggestedAction = "需同步部署",
            RelatedPrIds = new[] { "456", "789" }
        };

        var report = CreateReport(
            analyzedAt: new DateTimeOffset(2024, 3, 15, 14, 30, 0, TimeSpan.Zero),
            repoResults: new[] { repo },
            crossServiceRisks: new[] { crossRisk });

        // Act
        var result = _sut.GenerateMarkdown(report);

        // Assert
        Assert.Contains("跨服務影響分析", result);
        Assert.Contains("OrderService", result);
        Assert.Contains("PaymentService", result);
        Assert.Contains("NotificationService", result);
        Assert.Contains("訂單服務 API 回傳格式變更影響下游服務", result);
        Assert.Contains("需同步部署", result);
    }

    [Fact]
    public void GenerateMarkdown_WithMultipleRepositories_ShouldHavePerRepoSections()
    {
        // Arrange
        var pr1 = CreatePrRisk("1", "OrderService", RiskLevel.High, RiskCategory.CoreBusinessLogicChange);
        var pr2 = CreatePrRisk("2", "PaymentService", RiskLevel.Medium, RiskCategory.SecurityChange);

        var repo1 = CreateRepoResult("OrderService", "GitLab", pr1);
        var repo2 = CreateRepoResult("PaymentService", "Bitbucket", pr2);

        var report = CreateReport(
            analyzedAt: new DateTimeOffset(2024, 3, 15, 14, 30, 0, TimeSpan.Zero),
            repoResults: new[] { repo1, repo2 });

        // Act
        var result = _sut.GenerateMarkdown(report);

        // Assert — 各 Repository 區段標題
        Assert.Contains("各 Repository 詳細分析", result);
        Assert.Contains("OrderService", result);
        Assert.Contains("PaymentService", result);
    }

    [Fact]
    public void GenerateMarkdown_ShouldShowAnalysisTimeInHeader()
    {
        // Arrange
        var analyzedAt = new DateTimeOffset(2024, 6, 20, 9, 15, 30, TimeSpan.Zero);
        var report = CreateReport(
            analyzedAt: analyzedAt,
            repoResults: Array.Empty<RepositoryRiskResult>());

        // Act
        var result = _sut.GenerateMarkdown(report);

        // Assert
        Assert.Contains("2024-06-20 09:15:30 UTC", result);
    }

    [Fact]
    public void GenerateMarkdown_WithNoHighRiskItems_ShouldOmitHighRiskSection()
    {
        // Arrange
        var low = CreatePrRisk("1", "RepoA", RiskLevel.Low, RiskCategory.ConfigurationChange);
        var none = CreatePrRisk("2", "RepoA", RiskLevel.None, RiskCategory.PerformanceChange);
        var repo = CreateRepoResult("RepoA", "GitLab", low, none);

        var report = CreateReport(
            analyzedAt: new DateTimeOffset(2024, 3, 15, 14, 30, 0, TimeSpan.Zero),
            repoResults: new[] { repo });

        // Act
        var result = _sut.GenerateMarkdown(report);

        // Assert — 無 Critical/High 時不應顯示詳細高風險區塊內容
        Assert.DoesNotContain("### 🔴 Critical", result);
        Assert.DoesNotContain("### 🟠 High", result);
    }

    #region Test Helpers

    private static RiskAnalysisReport CreateReport(
        DateTimeOffset analyzedAt,
        IReadOnlyList<RepositoryRiskResult> repoResults,
        IReadOnlyList<CrossServiceRisk>? crossServiceRisks = null)
    {
        return new RiskAnalysisReport
        {
            AnalyzedAt = analyzedAt,
            RepositoryResults = repoResults,
            CrossServiceRisks = crossServiceRisks ?? Array.Empty<CrossServiceRisk>()
        };
    }

    private static RepositoryRiskResult CreateRepoResult(
        string repoName, string platform, params PullRequestRisk[] risks)
    {
        return new RepositoryRiskResult
        {
            RepositoryName = repoName,
            Platform = platform,
            PullRequestRisks = risks.ToList()
        };
    }

    private static PullRequestRisk CreatePrRisk(
        string prId, string repoName, RiskLevel level, params RiskCategory[] categories)
    {
        return new PullRequestRisk
        {
            PrId = prId,
            RepositoryName = repoName,
            PrTitle = $"Test PR {prId}",
            PrUrl = $"https://example.com/pr/{prId}",
            RiskLevel = level,
            RiskCategories = categories.ToList(),
            RiskDescription = $"Test risk description for PR {prId}",
            NeedsDeepAnalysis = level >= RiskLevel.Medium,
            AffectedComponents = new List<string> { "TestComponent" },
            SuggestedAction = "建議人工審查"
        };
    }

    #endregion
}
