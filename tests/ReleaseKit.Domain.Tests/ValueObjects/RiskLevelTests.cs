using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Tests.ValueObjects;

/// <summary>
/// RiskLevel 列舉值測試
/// </summary>
public class RiskLevelTests
{
    [Fact]
    public void RiskLevel_ShouldHaveFiveValues()
    {
        var values = Enum.GetValues<RiskLevel>();
        Assert.Equal(5, values.Length);
    }

    [Theory]
    [InlineData(RiskLevel.None, 0)]
    [InlineData(RiskLevel.Low, 1)]
    [InlineData(RiskLevel.Medium, 2)]
    [InlineData(RiskLevel.High, 3)]
    [InlineData(RiskLevel.Critical, 4)]
    public void RiskLevel_ShouldHaveCorrectOrdinalOrder(RiskLevel level, int expected)
    {
        Assert.Equal(expected, (int)level);
    }

    [Fact]
    public void RiskLevel_Critical_ShouldBeGreaterThanHigh()
    {
        Assert.True(RiskLevel.Critical > RiskLevel.High);
    }

    [Fact]
    public void RiskLevel_None_ShouldBeLessThanLow()
    {
        Assert.True(RiskLevel.None < RiskLevel.Low);
    }
}
