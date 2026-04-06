namespace ReleaseKit.Domain.Tests.ValueObjects;

using ReleaseKit.Domain.ValueObjects;

/// <summary>
/// RiskCategory 值物件單元測試
/// </summary>
public class RiskCategoryTests
{
    [Fact]
    public void RiskCategory_ShouldHaveFiveValues()
    {
        var values = Enum.GetValues<RiskCategory>();
        Assert.Equal(5, values.Length);
    }

    [Theory]
    [InlineData(RiskCategory.ApiContract, 0)]
    [InlineData(RiskCategory.DatabaseSchema, 1)]
    [InlineData(RiskCategory.DatabaseData, 2)]
    [InlineData(RiskCategory.EventFormat, 3)]
    [InlineData(RiskCategory.Configuration, 4)]
    public void RiskCategory_ShouldHaveCorrectOrdinalValues(RiskCategory category, int expected)
    {
        Assert.Equal(expected, (int)category);
    }
}
