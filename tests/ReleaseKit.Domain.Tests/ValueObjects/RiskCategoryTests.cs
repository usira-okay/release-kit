using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Tests.ValueObjects;

/// <summary>
/// RiskCategory 列舉值測試
/// </summary>
public class RiskCategoryTests
{
    [Fact]
    public void RiskCategory_ShouldHaveEightValues()
    {
        var values = Enum.GetValues<RiskCategory>();
        Assert.Equal(8, values.Length);
    }

    [Theory]
    [InlineData(RiskCategory.CrossServiceApiBreaking)]
    [InlineData(RiskCategory.SharedLibraryChange)]
    [InlineData(RiskCategory.DatabaseSchemaChange)]
    [InlineData(RiskCategory.DatabaseDataChange)]
    [InlineData(RiskCategory.ConfigurationChange)]
    [InlineData(RiskCategory.SecurityChange)]
    [InlineData(RiskCategory.PerformanceChange)]
    [InlineData(RiskCategory.CoreBusinessLogicChange)]
    public void RiskCategory_ShouldContainExpectedValue(RiskCategory category)
    {
        Assert.True(Enum.IsDefined(category));
    }
}
