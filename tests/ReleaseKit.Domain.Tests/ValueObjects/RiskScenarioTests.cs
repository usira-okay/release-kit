using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Tests.ValueObjects;

/// <summary>
/// RiskScenario 值物件單元測試
/// </summary>
public class RiskScenarioTests
{
    [Fact]
    public void RiskScenario_應包含五種情境()
    {
        var values = Enum.GetValues<RiskScenario>();
        Assert.Equal(5, values.Length);
        Assert.Contains(RiskScenario.ApiContractBreak, values);
        Assert.Contains(RiskScenario.DatabaseSchemaChange, values);
        Assert.Contains(RiskScenario.MessageQueueFormat, values);
        Assert.Contains(RiskScenario.ConfigEnvChange, values);
        Assert.Contains(RiskScenario.DataSemanticChange, values);
    }
}
