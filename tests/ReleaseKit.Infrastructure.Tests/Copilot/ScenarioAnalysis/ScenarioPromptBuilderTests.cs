using FluentAssertions;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis;
using ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis.Models;

namespace ReleaseKit.Infrastructure.Tests.Copilot.ScenarioAnalysis;

/// <summary>
/// ScenarioPromptBuilder 單元測試
/// </summary>
public class ScenarioPromptBuilderTests
{
    [Fact]
    public void BuildCoordinatorSystemPrompt_應回傳非空字串()
    {
        var result = ScenarioPromptBuilder.BuildCoordinatorSystemPrompt();
        result.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void BuildCoordinatorSystemPrompt_應包含分配規則()
    {
        var result = ScenarioPromptBuilder.BuildCoordinatorSystemPrompt();
        result.Should().Contain("分配");
    }

    [Fact]
    public void BuildCoordinatorUserPrompt_應包含專案路徑與CommitSha()
    {
        var summaries = new List<CommitSummary>
        {
            new()
            {
                CommitSha = "abc123",
                ChangedFiles = new List<FileDiff>
                {
                    new() { FilePath = "Controllers/UserController.cs", ChangeType = ChangeType.Modified, CommitSha = "abc123" }
                },
                TotalFilesChanged = 1,
                TotalLinesAdded = 10,
                TotalLinesRemoved = 3
            }
        };

        var result = ScenarioPromptBuilder.BuildCoordinatorUserPrompt("team-a/api", summaries);

        result.Should().Contain("team-a/api");
        result.Should().Contain("abc123");
        result.Should().Contain("UserController.cs");
    }

    [Theory]
    [InlineData(RiskScenario.ApiContractBreak)]
    [InlineData(RiskScenario.DatabaseSchemaChange)]
    [InlineData(RiskScenario.MessageQueueFormat)]
    [InlineData(RiskScenario.ConfigEnvChange)]
    [InlineData(RiskScenario.DataSemanticChange)]
    public void BuildExpertSystemPrompt_各情境應回傳非空字串(RiskScenario scenario)
    {
        var result = ScenarioPromptBuilder.BuildExpertSystemPrompt(scenario);
        result.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void BuildExpertSystemPrompt_API情境應包含Controller關鍵字()
    {
        var result = ScenarioPromptBuilder.BuildExpertSystemPrompt(RiskScenario.ApiContractBreak);
        result.Should().Contain("Controller");
    }

    [Fact]
    public void BuildExpertUserPrompt_應包含CommitSha清單()
    {
        var shas = new List<string> { "sha1", "sha2" };
        var result = ScenarioPromptBuilder.BuildExpertUserPrompt("project-a", shas, RiskScenario.ApiContractBreak);

        result.Should().Contain("sha1");
        result.Should().Contain("sha2");
    }

    [Fact]
    public void BuildSynthesisSystemPrompt_應回傳非空字串()
    {
        var result = ScenarioPromptBuilder.BuildSynthesisSystemPrompt();
        result.Should().NotBeNullOrWhiteSpace();
        result.Should().Contain("去重");
    }

    [Fact]
    public void BuildSynthesisUserPrompt_應包含各情境結果與專案摘要()
    {
        var input = new SynthesisInput
        {
            ProjectPath = "team-b/service",
            ExpertResults = new Dictionary<RiskScenario, ExpertFindings>
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
                            Description = "移除必填欄位",
                            AffectedFile = "UserController.cs",
                            DiffSnippet = "-public string Name",
                            PotentiallyAffectedProjects = new List<string> { "frontend" },
                            RecommendedAction = "確認前端已移除對該欄位的依賴",
                            ChangedBy = "dev1"
                        }
                    },
                    Failed = false
                },
                [RiskScenario.DatabaseSchemaChange] = new ExpertFindings
                {
                    Scenario = RiskScenario.DatabaseSchemaChange,
                    Findings = new List<RiskFinding>(),
                    Failed = true,
                    FailureReason = "Session 超時"
                }
            },
            OtherProjectsSummary = new List<string> { "team-a/api: 3 commits, 修改 UserService" }
        };

        var result = ScenarioPromptBuilder.BuildSynthesisUserPrompt(input);

        result.Should().Contain("team-b/service");
        result.Should().Contain("移除必填欄位");
        result.Should().Contain("Session 超時");
        result.Should().Contain("team-a/api");
    }
}
