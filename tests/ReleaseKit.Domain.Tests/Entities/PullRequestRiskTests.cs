using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Tests.Entities;

/// <summary>
/// PullRequestRisk 實體測試
/// </summary>
public class PullRequestRiskTests
{
    [Fact]
    public void Constructor_WithValidData_ShouldCreatePullRequestRisk()
    {
        var risk = new PullRequestRisk
        {
            PrId = "456",
            RepositoryName = "OrderService",
            PrTitle = "修改訂單折扣計算",
            PrUrl = "https://gitlab.example.com/mr/456",
            RiskLevel = RiskLevel.Critical,
            RiskCategories = new List<RiskCategory>
            {
                RiskCategory.CoreBusinessLogicChange,
                RiskCategory.CrossServiceApiBreaking
            },
            RiskDescription = "修改了訂單折扣計算公式",
            NeedsDeepAnalysis = true,
            AffectedComponents = new List<string> { "OrderCalculator", "DiscountService" },
            SuggestedAction = "建議進行完整回歸測試"
        };

        Assert.Equal("456", risk.PrId);
        Assert.Equal("OrderService", risk.RepositoryName);
        Assert.Equal(RiskLevel.Critical, risk.RiskLevel);
        Assert.Equal(2, risk.RiskCategories.Count);
        Assert.True(risk.NeedsDeepAnalysis);
        Assert.Equal(2, risk.AffectedComponents.Count);
    }

    [Fact]
    public void Constructor_WithNoRiskCategories_ShouldAllowEmptyList()
    {
        var risk = new PullRequestRisk
        {
            PrId = "100",
            RepositoryName = "DocService",
            RiskLevel = RiskLevel.None,
            RiskCategories = new List<RiskCategory>(),
            RiskDescription = "純文件更新",
            NeedsDeepAnalysis = false,
            AffectedComponents = new List<string>(),
            SuggestedAction = "無需額外行動"
        };

        Assert.Empty(risk.RiskCategories);
        Assert.Equal(RiskLevel.None, risk.RiskLevel);
        Assert.False(risk.NeedsDeepAnalysis);
    }

    [Fact]
    public void PrTitle_ShouldDefaultToEmpty()
    {
        var risk = new PullRequestRisk
        {
            PrId = "1",
            RepositoryName = "Test",
            RiskLevel = RiskLevel.Low,
            RiskCategories = new List<RiskCategory>(),
            RiskDescription = "test",
            NeedsDeepAnalysis = false,
            AffectedComponents = new List<string>(),
            SuggestedAction = "none"
        };

        Assert.Equal(string.Empty, risk.PrTitle);
        Assert.Equal(string.Empty, risk.PrUrl);
    }
}
