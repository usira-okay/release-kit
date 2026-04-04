using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Tests.Entities;

/// <summary>
/// RiskAnalysisReport 聚合根測試
/// </summary>
public class RiskAnalysisReportTests
{
    [Fact]
    public void TotalRepositories_ShouldReturnCorrectCount()
    {
        var report = CreateReport(repoCount: 3, prsPerRepo: 2);
        Assert.Equal(3, report.TotalRepositories);
    }

    [Fact]
    public void TotalPullRequests_ShouldReturnSumAcrossRepos()
    {
        var report = CreateReport(repoCount: 3, prsPerRepo: 2);
        Assert.Equal(6, report.TotalPullRequests);
    }

    [Fact]
    public void RiskLevelSummary_ShouldGroupByLevel()
    {
        var pr1 = CreatePrRisk("1", RiskLevel.Critical);
        var pr2 = CreatePrRisk("2", RiskLevel.High);
        var pr3 = CreatePrRisk("3", RiskLevel.Critical);

        var report = new RiskAnalysisReport
        {
            AnalyzedAt = DateTimeOffset.UtcNow,
            RepositoryResults = new List<RepositoryRiskResult>
            {
                new()
                {
                    RepositoryName = "Repo1",
                    Platform = "GitLab",
                    PullRequestRisks = new List<PullRequestRisk> { pr1, pr2, pr3 }
                }
            },
            CrossServiceRisks = new List<CrossServiceRisk>()
        };

        var summary = report.RiskLevelSummary;
        Assert.Equal(2, summary[RiskLevel.Critical]);
        Assert.Equal(1, summary[RiskLevel.High]);
    }

    [Fact]
    public void RiskCategorySummary_ShouldCountCategoriesAcrossPrs()
    {
        var pr1 = CreatePrRiskWithCategories("1", RiskCategory.SecurityChange, RiskCategory.CoreBusinessLogicChange);
        var pr2 = CreatePrRiskWithCategories("2", RiskCategory.SecurityChange);

        var report = new RiskAnalysisReport
        {
            AnalyzedAt = DateTimeOffset.UtcNow,
            RepositoryResults = new List<RepositoryRiskResult>
            {
                new()
                {
                    RepositoryName = "Repo1",
                    Platform = "GitLab",
                    PullRequestRisks = new List<PullRequestRisk> { pr1, pr2 }
                }
            },
            CrossServiceRisks = new List<CrossServiceRisk>()
        };

        var summary = report.RiskCategorySummary;
        Assert.Equal(2, summary[RiskCategory.SecurityChange]);
        Assert.Equal(1, summary[RiskCategory.CoreBusinessLogicChange]);
    }

    [Fact]
    public void EmptyReport_ShouldHaveZeroCounts()
    {
        var report = new RiskAnalysisReport
        {
            AnalyzedAt = DateTimeOffset.UtcNow,
            RepositoryResults = new List<RepositoryRiskResult>(),
            CrossServiceRisks = new List<CrossServiceRisk>()
        };

        Assert.Equal(0, report.TotalRepositories);
        Assert.Equal(0, report.TotalPullRequests);
        Assert.Empty(report.RiskLevelSummary);
        Assert.Empty(report.RiskCategorySummary);
    }

    private static RiskAnalysisReport CreateReport(int repoCount, int prsPerRepo)
    {
        var repos = Enumerable.Range(1, repoCount).Select(i => new RepositoryRiskResult
        {
            RepositoryName = $"Repo{i}",
            Platform = "GitLab",
            PullRequestRisks = Enumerable.Range(1, prsPerRepo)
                .Select(j => CreatePrRisk($"{i}-{j}", RiskLevel.Medium))
                .ToList()
        }).ToList();

        return new RiskAnalysisReport
        {
            AnalyzedAt = DateTimeOffset.UtcNow,
            RepositoryResults = repos,
            CrossServiceRisks = new List<CrossServiceRisk>()
        };
    }

    private static PullRequestRisk CreatePrRisk(string prId, RiskLevel level)
    {
        return new PullRequestRisk
        {
            PrId = prId,
            RepositoryName = "TestRepo",
            RiskLevel = level,
            RiskCategories = new List<RiskCategory> { RiskCategory.CoreBusinessLogicChange },
            RiskDescription = "test",
            NeedsDeepAnalysis = false,
            AffectedComponents = new List<string>(),
            SuggestedAction = "none"
        };
    }

    private static PullRequestRisk CreatePrRiskWithCategories(string prId, params RiskCategory[] categories)
    {
        return new PullRequestRisk
        {
            PrId = prId,
            RepositoryName = "TestRepo",
            RiskLevel = RiskLevel.High,
            RiskCategories = categories.ToList(),
            RiskDescription = "test",
            NeedsDeepAnalysis = false,
            AffectedComponents = new List<string>(),
            SuggestedAction = "none"
        };
    }
}
