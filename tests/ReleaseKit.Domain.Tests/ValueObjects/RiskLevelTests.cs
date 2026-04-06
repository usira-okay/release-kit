namespace ReleaseKit.Domain.Tests.ValueObjects;

using ReleaseKit.Domain.ValueObjects;

/// <summary>
/// RiskLevel 值物件單元測試
/// </summary>
public class RiskLevelTests
{
    [Fact]
    public void RiskLevel_ShouldHaveThreeValues()
    {
        var values = Enum.GetValues<RiskLevel>();
        Assert.Equal(3, values.Length);
    }

    [Theory]
    [InlineData(RiskLevel.High, 0)]
    [InlineData(RiskLevel.Medium, 1)]
    [InlineData(RiskLevel.Low, 2)]
    public void RiskLevel_ShouldHaveCorrectOrdinalValues(RiskLevel level, int expected)
    {
        Assert.Equal(expected, (int)level);
    }
}
