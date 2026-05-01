using FluentAssertions;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis;
using ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis.Models;

namespace ReleaseKit.Infrastructure.Tests.Copilot.ScenarioAnalysis;

/// <summary>
/// SynthesisAgentRunner.ParseSynthesisFindings 單元測試
/// </summary>
public class SynthesisAgentRunnerTests
{
    [Fact]
    public void ParseSynthesisFindings_有效JSON含CompositeRisk_應正確解析()
    {
        var json = """
            ```json
            [
              {
                "Scenario": "ApiContractBreak",
                "RiskLevel": "High",
                "Description": "綜合判斷：API + DB 同時變更",
                "AffectedFile": "Controllers/UserController.cs",
                "DiffSnippet": "diff snippet",
                "PotentiallyAffectedProjects": ["team-b/portal"],
                "RecommendedAction": "通知團隊",
                "CompositeRisk": "與 DatabaseSchemaChange 相關"
              }
            ]
            ```
            """;

        var result = SynthesisAgentRunner.ParseSynthesisFindings(json);

        result.Should().HaveCount(1);
        result[0].Scenario.Should().Be(RiskScenario.ApiContractBreak);
        result[0].RiskLevel.Should().Be(RiskLevel.High);
    }

    [Fact]
    public void ParseSynthesisFindings_空陣列_應回傳空清單()
    {
        var json = "[]";
        var result = SynthesisAgentRunner.ParseSynthesisFindings(json);
        result.Should().BeEmpty();
    }

    [Fact]
    public void MergeExpertFindings_當Synthesis失敗_應回傳所有Expert結果合併()
    {
        var expertResults = new Dictionary<RiskScenario, ExpertFindings>
        {
            [RiskScenario.ApiContractBreak] = new ExpertFindings
            {
                Scenario = RiskScenario.ApiContractBreak,
                Findings = new List<RiskFinding>
                {
                    new()
                    {
                        Scenario = RiskScenario.ApiContractBreak,
                        RiskLevel = RiskLevel.High,
                        Description = "API risk",
                        AffectedFile = "file.cs",
                        DiffSnippet = "",
                        PotentiallyAffectedProjects = [],
                        RecommendedAction = "",
                        ChangedBy = ""
                    }
                }
            },
            [RiskScenario.DatabaseSchemaChange] = new ExpertFindings
            {
                Scenario = RiskScenario.DatabaseSchemaChange,
                Findings = new List<RiskFinding>
                {
                    new()
                    {
                        Scenario = RiskScenario.DatabaseSchemaChange,
                        RiskLevel = RiskLevel.Medium,
                        Description = "DB risk",
                        AffectedFile = "entity.cs",
                        DiffSnippet = "",
                        PotentiallyAffectedProjects = [],
                        RecommendedAction = "",
                        ChangedBy = ""
                    }
                }
            }
        };

        var result = SynthesisAgentRunner.MergeExpertFindings(expertResults);

        result.Should().HaveCount(2);
    }
}
