using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Tests.Entities;

/// <summary>
/// RiskFinding 值物件測試
/// </summary>
public class RiskFindingTests
{
    [Fact]
    public void Constructor_WithValidData_ShouldCreateRiskFinding()
    {
        var finding = new RiskFinding
        {
            Category = RiskCategory.CrossServiceApiBreaking,
            Description = "修改了 API endpoint 回傳格式",
            AffectedComponent = "OrderController"
        };

        Assert.Equal(RiskCategory.CrossServiceApiBreaking, finding.Category);
        Assert.Equal("修改了 API endpoint 回傳格式", finding.Description);
        Assert.Equal("OrderController", finding.AffectedComponent);
    }
}
