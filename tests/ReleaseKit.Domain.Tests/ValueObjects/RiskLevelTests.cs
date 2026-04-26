using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Tests.ValueObjects;

/// <summary>
/// RiskLevel 值物件單元測試
/// </summary>
public class RiskLevelTests
{
    [Fact]
    public void RiskLevel_應包含三個等級()
    {
        var values = Enum.GetValues<RiskLevel>();
        Assert.Equal(3, values.Length);
        Assert.Contains(RiskLevel.High, values);
        Assert.Contains(RiskLevel.Medium, values);
        Assert.Contains(RiskLevel.Low, values);
    }
}
