using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Tests.Entities;

/// <summary>
/// RepositoryRiskResult 測試
/// </summary>
public class RepositoryRiskResultTests
{
    [Fact]
    public void MaxRiskLevel_WithMultiplePrs_ShouldReturnHighest()
    {
        var result = new RepositoryRiskResult
        {
            RepositoryName = "OrderService",
            Platform = "GitLab",
            PullRequestRisks = new List<PullRequestRisk>
            {
                CreatePrRisk("1", RiskLevel.Low),
                CreatePrRisk("2", RiskLevel.Critical),
                CreatePrRisk("3", RiskLevel.Medium)
            }
        };

        Assert.Equal(RiskLevel.Critical, result.MaxRiskLevel);
    }

    [Fact]
    public void MaxRiskLevel_WithNoPrs_ShouldReturnNone()
    {
        var result = new RepositoryRiskResult
        {
            RepositoryName = "EmptyRepo",
            Platform = "Bitbucket",
            PullRequestRisks = new List<PullRequestRisk>()
        };

        Assert.Equal(RiskLevel.None, result.MaxRiskLevel);
    }

    private static PullRequestRisk CreatePrRisk(string prId, RiskLevel level)
    {
        return new PullRequestRisk
        {
            PrId = prId,
            RepositoryName = "TestRepo",
            RiskLevel = level,
            RiskCategories = new List<RiskCategory>(),
            RiskDescription = "test",
            NeedsDeepAnalysis = false,
            AffectedComponents = new List<string>(),
            SuggestedAction = "none"
        };
    }
}
