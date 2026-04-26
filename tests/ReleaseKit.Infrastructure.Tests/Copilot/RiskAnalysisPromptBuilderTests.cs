using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Infrastructure.Copilot;

namespace ReleaseKit.Infrastructure.Tests.Copilot;

/// <summary>
/// RiskAnalysisPromptBuilder 與 CopilotRiskAnalyzer 純邏輯方法的單元測試
/// </summary>
public class RiskAnalysisPromptBuilderTests
{
    private static FileDiff CreateFileDiff(string path, string content, ChangeType changeType = ChangeType.Modified)
        => new()
        {
            FilePath = path,
            DiffContent = content,
            ChangeType = changeType,
            CommitSha = "abc123"
        };

    private static CopilotRiskAnalyzer CreateAnalyzer()
    {
        var options = Options.Create(new CopilotOptions());
        return new CopilotRiskAnalyzer(options, NullLogger<CopilotRiskAnalyzer>.Instance);
    }

    // ──────────────────────────────
    // BuildSystemPrompt
    // ──────────────────────────────

    [Fact]
    public void BuildSystemPrompt_應回傳非空字串()
    {
        var result = RiskAnalysisPromptBuilder.BuildSystemPrompt();
        result.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("ApiContractBreak")]
    [InlineData("DatabaseSchemaChange")]
    [InlineData("MessageQueueFormat")]
    [InlineData("ConfigEnvChange")]
    [InlineData("DataSemanticChange")]
    public void BuildSystemPrompt_應包含五種分析情境(string scenario)
    {
        var result = RiskAnalysisPromptBuilder.BuildSystemPrompt();
        result.Should().Contain(scenario);
    }

    [Theory]
    [InlineData("High")]
    [InlineData("Medium")]
    [InlineData("Low")]
    public void BuildSystemPrompt_應包含三種風險等級(string level)
    {
        var result = RiskAnalysisPromptBuilder.BuildSystemPrompt();
        result.Should().Contain(level);
    }

    [Fact]
    public void BuildSystemPrompt_應包含JSON格式說明()
    {
        var result = RiskAnalysisPromptBuilder.BuildSystemPrompt();
        result.Should().Contain("RiskLevel");
        result.Should().Contain("PotentiallyAffectedProjects");
        result.Should().Contain("RecommendedAction");
    }

    // ──────────────────────────────
    // BuildUserPrompt
    // ──────────────────────────────

    [Fact]
    public void BuildUserPrompt_應包含專案路徑()
    {
        var diffs = new List<FileDiff> { CreateFileDiff("src/Foo.cs", "+ int x;") };
        var scenarios = new List<RiskScenario> { RiskScenario.ApiContractBreak };

        var result = RiskAnalysisPromptBuilder.BuildUserPrompt("my-project/api", diffs, null, scenarios);

        result.Should().Contain("my-project/api");
    }

    [Fact]
    public void BuildUserPrompt_應包含diff內容()
    {
        var diffs = new List<FileDiff> { CreateFileDiff("src/Foo.cs", "+ public void NewMethod() {}") };
        var scenarios = new List<RiskScenario> { RiskScenario.ApiContractBreak };

        var result = RiskAnalysisPromptBuilder.BuildUserPrompt("proj", diffs, null, scenarios);

        result.Should().Contain("+ public void NewMethod() {}");
        result.Should().Contain("src/Foo.cs");
    }

    [Fact]
    public void BuildUserPrompt_應包含情境名稱()
    {
        var diffs = new List<FileDiff> { CreateFileDiff("src/Foo.cs", "+ x") };
        var scenarios = new List<RiskScenario>
        {
            RiskScenario.ApiContractBreak,
            RiskScenario.DatabaseSchemaChange
        };

        var result = RiskAnalysisPromptBuilder.BuildUserPrompt("proj", diffs, null, scenarios);

        result.Should().Contain("ApiContractBreak");
        result.Should().Contain("DatabaseSchemaChange");
    }

    [Fact]
    public void BuildUserPrompt_大型diff應在2000字元處截斷()
    {
        var largeDiff = new string('+', 3000);
        var diffs = new List<FileDiff> { CreateFileDiff("src/Big.cs", largeDiff) };
        var scenarios = new List<RiskScenario> { RiskScenario.ConfigEnvChange };

        var result = RiskAnalysisPromptBuilder.BuildUserPrompt("proj", diffs, null, scenarios);

        result.Should().Contain("已截斷");
        result.Should().NotContain(largeDiff);
    }

    [Fact]
    public void BuildUserPrompt_小型diff不應截斷()
    {
        var smallDiff = new string('+', 500);
        var diffs = new List<FileDiff> { CreateFileDiff("src/Small.cs", smallDiff) };
        var scenarios = new List<RiskScenario> { RiskScenario.ConfigEnvChange };

        var result = RiskAnalysisPromptBuilder.BuildUserPrompt("proj", diffs, null, scenarios);

        result.Should().NotContain("已截斷");
        result.Should().Contain(smallDiff);
    }

    [Fact]
    public void BuildUserPrompt_有ProjectStructure時應包含結構資訊()
    {
        var structure = new ProjectStructure
        {
            ProjectPath = "proj",
            ApiEndpoints = new List<ApiEndpoint>
            {
                new() { HttpMethod = "GET", Route = "/api/users", ControllerName = "UsersController", ActionName = "GetAll" }
            },
            DbContextFiles = new List<string> { "AppDbContext.cs" },
            MessageContracts = new List<string> { "UserCreatedEvent.cs" },
            NuGetPackages = new List<string>(),
            MigrationFiles = new List<string>(),
            ConfigKeys = new List<string>(),
            InferredDependencies = new List<ServiceDependency>()
        };
        var diffs = new List<FileDiff> { CreateFileDiff("src/Foo.cs", "+ x") };
        var scenarios = new List<RiskScenario> { RiskScenario.ApiContractBreak };

        var result = RiskAnalysisPromptBuilder.BuildUserPrompt("proj", diffs, structure, scenarios);

        result.Should().Contain("GET /api/users");
        result.Should().Contain("AppDbContext.cs");
        result.Should().Contain("UserCreatedEvent.cs");
    }

    // ──────────────────────────────
    // EstimateTokens
    // ──────────────────────────────

    [Theory]
    [InlineData("", 0)]
    [InlineData("abcd", 1)]
    [InlineData("abcdefgh", 2)]
    [InlineData("0123456789012345678901234567890123456789", 10)] // 40 chars
    public void EstimateTokens_應回傳字元數除以四(string text, int expected)
    {
        RiskAnalysisPromptBuilder.EstimateTokens(text).Should().Be(expected);
    }

    // ──────────────────────────────
    // ParseFindings
    // ──────────────────────────────

    [Fact]
    public void ParseFindings_合法JSON應正確解析()
    {
        var analyzer = CreateAnalyzer();
        var json = """
            [
              {
                "Scenario": "ApiContractBreak",
                "RiskLevel": "High",
                "Description": "Route 已移除",
                "AffectedFile": "src/Controller.cs",
                "DiffSnippet": "- [Route(\"/api/old\")]",
                "PotentiallyAffectedProjects": ["service-a", "service-b"],
                "RecommendedAction": "通知相依服務"
              }
            ]
            """;

        var findings = analyzer.ParseFindings(json, "developer-a");

        findings.Should().HaveCount(1);
        var f = findings[0];
        f.Scenario.Should().Be(RiskScenario.ApiContractBreak);
        f.RiskLevel.Should().Be(RiskLevel.High);
        f.Description.Should().Be("Route 已移除");
        f.AffectedFile.Should().Be("src/Controller.cs");
        f.PotentiallyAffectedProjects.Should().BeEquivalentTo(new[] { "service-a", "service-b" });
        f.ChangedBy.Should().Be("developer-a");
    }

    [Fact]
    public void ParseFindings_空陣列應回傳空清單()
    {
        var analyzer = CreateAnalyzer();
        var findings = analyzer.ParseFindings("[]", "dev");
        findings.Should().BeEmpty();
    }

    [Fact]
    public void ParseFindings_有markdown程式碼區塊包裝應正確解析()
    {
        var analyzer = CreateAnalyzer();
        var json = """
            ```json
            [
              {
                "Scenario": "ConfigEnvChange",
                "RiskLevel": "Low",
                "Description": "新增設定項",
                "AffectedFile": "appsettings.json",
                "DiffSnippet": "+ NewKey: value",
                "PotentiallyAffectedProjects": [],
                "RecommendedAction": "更新部署腳本"
              }
            ]
            ```
            """;

        var findings = analyzer.ParseFindings(json, "dev");

        findings.Should().HaveCount(1);
        findings[0].Scenario.Should().Be(RiskScenario.ConfigEnvChange);
        findings[0].RiskLevel.Should().Be(RiskLevel.Low);
    }

    [Fact]
    public void ParseFindings_有無語言標籤markdown區塊應正確解析()
    {
        var analyzer = CreateAnalyzer();
        var json = "```\n[]\n```";
        var findings = analyzer.ParseFindings(json, "dev");
        findings.Should().BeEmpty();
    }

    [Fact]
    public void ParseFindings_無效JSON應回傳空清單()
    {
        var analyzer = CreateAnalyzer();
        var findings = analyzer.ParseFindings("this is not json", "dev");
        findings.Should().BeEmpty();
    }

    [Fact]
    public void ParseFindings_多筆結果應全部解析()
    {
        var analyzer = CreateAnalyzer();
        var json = """
            [
              { "Scenario": "ApiContractBreak", "RiskLevel": "High", "Description": "A", "AffectedFile": "a.cs", "DiffSnippet": "", "PotentiallyAffectedProjects": [], "RecommendedAction": "" },
              { "Scenario": "DatabaseSchemaChange", "RiskLevel": "Medium", "Description": "B", "AffectedFile": "b.cs", "DiffSnippet": "", "PotentiallyAffectedProjects": [], "RecommendedAction": "" },
              { "Scenario": "MessageQueueFormat", "RiskLevel": "Low", "Description": "C", "AffectedFile": "c.cs", "DiffSnippet": "", "PotentiallyAffectedProjects": [], "RecommendedAction": "" }
            ]
            """;

        var findings = analyzer.ParseFindings(json, "dev");

        findings.Should().HaveCount(3);
        findings[0].Scenario.Should().Be(RiskScenario.ApiContractBreak);
        findings[1].Scenario.Should().Be(RiskScenario.DatabaseSchemaChange);
        findings[2].Scenario.Should().Be(RiskScenario.MessageQueueFormat);
    }

    // ──────────────────────────────
    // SplitFileDiffs
    // ──────────────────────────────

    [Fact]
    public void SplitFileDiffs_不超過閾值時應維持單一群組()
    {
        var diffs = new List<FileDiff>
        {
            CreateFileDiff("a.cs", new string('+', 100)),
            CreateFileDiff("b.cs", new string('+', 100))
        };

        // 每個 diff 100 chars = 25 tokens；總計 50 tokens，遠低於 1000
        var groups = CopilotRiskAnalyzer.SplitFileDiffs(diffs, maxTokens: 1000);

        groups.Should().HaveCount(1);
        groups[0].Should().HaveCount(2);
    }

    [Fact]
    public void SplitFileDiffs_超過閾值時應分成多個群組()
    {
        // 每個 diff 內容 4000 chars = 1000 tokens；閾值 1500 tokens
        var diffs = new List<FileDiff>
        {
            CreateFileDiff("a.cs", new string('+', 4000)),
            CreateFileDiff("b.cs", new string('+', 4000)),
            CreateFileDiff("c.cs", new string('+', 4000))
        };

        var groups = CopilotRiskAnalyzer.SplitFileDiffs(diffs, maxTokens: 1500);

        groups.Should().HaveCount(3);
        groups.SelectMany(g => g).Should().HaveCount(3);
    }

    [Fact]
    public void SplitFileDiffs_可在閾值內合併小檔案()
    {
        // 每個 40 chars = 10 tokens；閾值 25 tokens → 前兩個合併，第三個獨立
        var diffs = new List<FileDiff>
        {
            CreateFileDiff("a.cs", new string('+', 40)),
            CreateFileDiff("b.cs", new string('+', 40)),
            CreateFileDiff("c.cs", new string('+', 40))
        };

        var groups = CopilotRiskAnalyzer.SplitFileDiffs(diffs, maxTokens: 25);

        groups.Should().HaveCount(2);
        groups[0].Should().HaveCount(2);
        groups[1].Should().HaveCount(1);
    }

    [Fact]
    public void SplitFileDiffs_空清單應回傳空群組()
    {
        var groups = CopilotRiskAnalyzer.SplitFileDiffs(new List<FileDiff>(), maxTokens: 1000);
        groups.Should().BeEmpty();
    }

    [Fact]
    public void SplitFileDiffs_單一檔案應回傳單一群組()
    {
        var diffs = new List<FileDiff> { CreateFileDiff("a.cs", "+ code") };
        var groups = CopilotRiskAnalyzer.SplitFileDiffs(diffs, maxTokens: 100);

        groups.Should().HaveCount(1);
        groups[0].Should().HaveCount(1);
    }
}
