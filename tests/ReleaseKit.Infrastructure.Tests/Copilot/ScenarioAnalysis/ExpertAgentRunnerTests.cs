using FluentAssertions;
using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis;

namespace ReleaseKit.Infrastructure.Tests.Copilot.ScenarioAnalysis;

/// <summary>
/// ExpertAgentRunner.ParseFindings 單元測試
/// </summary>
public class ExpertAgentRunnerTests
{
    [Fact]
    public void ParseFindings_有效JSON_應正確解析()
    {
        var json = """
            ```json
            [
              {
                "Scenario": "ApiContractBreak",
                "RiskLevel": "High",
                "Description": "UserController 新增必填參數",
                "AffectedFile": "Controllers/UserController.cs",
                "DiffSnippet": "- GetUser(int id)\n+ GetUser(int id, string tenantId)",
                "PotentiallyAffectedProjects": ["team-b/portal"],
                "RecommendedAction": "通知 portal 團隊"
              }
            ]
            ```
            """;

        var result = ExpertAgentRunner.ParseFindings(json, RiskScenario.ApiContractBreak);

        result.Should().HaveCount(1);
        result[0].Scenario.Should().Be(RiskScenario.ApiContractBreak);
        result[0].RiskLevel.Should().Be(RiskLevel.High);
        result[0].AffectedFile.Should().Be("Controllers/UserController.cs");
    }

    [Fact]
    public void ParseFindings_空陣列_應回傳空清單()
    {
        var json = """
            ```json
            []
            ```
            """;

        var result = ExpertAgentRunner.ParseFindings(json, RiskScenario.DatabaseSchemaChange);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseFindings_無效JSON_應回傳空清單()
    {
        var invalid = "這不是有效的 JSON 回應";

        var result = ExpertAgentRunner.ParseFindings(invalid, RiskScenario.ApiContractBreak);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseFindings_無代碼塊的純JSON_應正確解析()
    {
        var json = """
            [
              {
                "Scenario": "ConfigEnvChange",
                "RiskLevel": "Medium",
                "Description": "新增必填 Redis key",
                "AffectedFile": "appsettings.json",
                "DiffSnippet": "+ \"NewKey\": \"\"",
                "PotentiallyAffectedProjects": [],
                "RecommendedAction": "確認所有環境已設定"
              }
            ]
            """;

        var result = ExpertAgentRunner.ParseFindings(json, RiskScenario.ConfigEnvChange);

        result.Should().HaveCount(1);
        result[0].Scenario.Should().Be(RiskScenario.ConfigEnvChange);
    }
}
