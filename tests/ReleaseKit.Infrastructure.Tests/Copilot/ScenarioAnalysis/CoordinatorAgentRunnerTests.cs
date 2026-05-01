using FluentAssertions;
using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis;

namespace ReleaseKit.Infrastructure.Tests.Copilot.ScenarioAnalysis;

/// <summary>
/// CoordinatorAgentRunner.ParseAssignment 單元測試
/// </summary>
public class CoordinatorAgentRunnerTests
{
    [Fact]
    public void ParseAssignment_有效JSON_應正確解析()
    {
        var json = """
            ```json
            {
              "assignments": {
                "ApiContractBreak": ["sha1", "sha2"],
                "DatabaseSchemaChange": ["sha3"],
                "MessageQueueFormat": [],
                "ConfigEnvChange": ["sha1"],
                "DataSemanticChange": []
              },
              "reasoning": "sha1 改了 Controller"
            }
            ```
            """;

        var result = CoordinatorAgentRunner.ParseAssignment(json);

        result.Should().NotBeNull();
        result!.Assignments[RiskScenario.ApiContractBreak].Should().Contain("sha1", "sha2");
        result.Assignments[RiskScenario.DatabaseSchemaChange].Should().Contain("sha3");
        result.Assignments[RiskScenario.MessageQueueFormat].Should().BeEmpty();
        result.Reasoning.Should().Contain("Controller");
    }

    [Fact]
    public void ParseAssignment_無效JSON_應回傳null()
    {
        var invalid = "這不是 JSON";

        var result = CoordinatorAgentRunner.ParseAssignment(invalid);

        result.Should().BeNull();
    }

    [Fact]
    public void ParseAssignment_無代碼塊的純JSON_應正確解析()
    {
        var json = """
            {
              "assignments": {
                "ApiContractBreak": ["sha1"],
                "DatabaseSchemaChange": [],
                "MessageQueueFormat": [],
                "ConfigEnvChange": [],
                "DataSemanticChange": []
              },
              "reasoning": "理由"
            }
            """;

        var result = CoordinatorAgentRunner.ParseAssignment(json);

        result.Should().NotBeNull();
        result!.Assignments[RiskScenario.ApiContractBreak].Should().Contain("sha1");
    }

    [Fact]
    public void BuildFallbackAssignment_應將所有commit分配給每個情境()
    {
        var commitShas = new List<string> { "sha1", "sha2", "sha3" };
        var scenarios = new List<RiskScenario>
        {
            RiskScenario.ApiContractBreak,
            RiskScenario.DatabaseSchemaChange
        };

        var result = CoordinatorAgentRunner.BuildFallbackAssignment(commitShas, scenarios);

        result.Assignments[RiskScenario.ApiContractBreak].Should().BeEquivalentTo(commitShas);
        result.Assignments[RiskScenario.DatabaseSchemaChange].Should().BeEquivalentTo(commitShas);
    }
}
