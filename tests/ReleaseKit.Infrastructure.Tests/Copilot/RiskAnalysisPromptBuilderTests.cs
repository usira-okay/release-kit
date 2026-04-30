using FluentAssertions;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Infrastructure.Copilot;

namespace ReleaseKit.Infrastructure.Tests.Copilot;

/// <summary>
/// RiskAnalysisPromptBuilder 靜態方法單元測試
/// </summary>
public class RiskAnalysisPromptBuilderTests
{
    private static CommitSummary BuildCommitSummary(
        string sha = "abc123def456",
        int linesAdded = 100,
        int linesRemoved = 50) =>
        new()
        {
            CommitSha = sha,
            ChangedFiles = new List<FileDiff>(),
            TotalFilesChanged = 1,
            TotalLinesAdded = linesAdded,
            TotalLinesRemoved = linesRemoved
        };

    // ──────────────────────────────
    // BuildDispatcherSystemPrompt
    // ──────────────────────────────

    [Fact]
    public void BuildDispatcherSystemPrompt_應回傳非空字串()
    {
        var result = RiskAnalysisPromptBuilder.BuildDispatcherSystemPrompt();
        result.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void BuildDispatcherSystemPrompt_應包含分組說明()
    {
        var result = RiskAnalysisPromptBuilder.BuildDispatcherSystemPrompt();
        result.Should().Contain("3000");
    }

    [Fact]
    public void BuildDispatcherSystemPrompt_應包含dispatch_project_analysis工具名稱()
    {
        var result = RiskAnalysisPromptBuilder.BuildDispatcherSystemPrompt();
        result.Should().Contain("dispatch_project_analysis");
    }

    // ──────────────────────────────
    // BuildDispatcherUserPrompt
    // ──────────────────────────────

    [Fact]
    public void BuildDispatcherUserPrompt_應包含專案路徑()
    {
        var summaries = new List<CommitSummary> { BuildCommitSummary() };

        var result = RiskAnalysisPromptBuilder.BuildDispatcherUserPrompt("my-project/api", summaries);

        result.Should().Contain("my-project/api");
    }

    [Fact]
    public void BuildDispatcherUserPrompt_應包含CommitSha統計表格()
    {
        var summaries = new List<CommitSummary> { BuildCommitSummary("abc123def456") };

        var result = RiskAnalysisPromptBuilder.BuildDispatcherUserPrompt("proj", summaries);

        // BuildDispatcherUserPrompt 取前 8 碼
        result.Should().Contain("abc123de");
    }

    [Fact]
    public void BuildDispatcherUserPrompt_應顯示總Commit數量()
    {
        var summaries = new List<CommitSummary> { BuildCommitSummary() };

        var result = RiskAnalysisPromptBuilder.BuildDispatcherUserPrompt("proj", summaries);

        result.Should().Contain("共 1 個 Commit");
    }

    // ──────────────────────────────
    // BuildAnalyzerSystemPrompt
    // ──────────────────────────────

    [Fact]
    public void BuildAnalyzerSystemPrompt_應回傳非空字串()
    {
        var result = RiskAnalysisPromptBuilder.BuildAnalyzerSystemPrompt();
        result.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("ApiContractBreak")]
    [InlineData("DatabaseSchemaChange")]
    [InlineData("MessageQueueFormat")]
    [InlineData("ConfigEnvChange")]
    [InlineData("DataSemanticChange")]
    public void BuildAnalyzerSystemPrompt_應包含五種分析情境(string scenario)
    {
        var result = RiskAnalysisPromptBuilder.BuildAnalyzerSystemPrompt();
        result.Should().Contain(scenario);
    }

    [Theory]
    [InlineData("High")]
    [InlineData("Medium")]
    [InlineData("Low")]
    public void BuildAnalyzerSystemPrompt_應包含三種風險等級(string level)
    {
        var result = RiskAnalysisPromptBuilder.BuildAnalyzerSystemPrompt();
        result.Should().Contain(level);
    }

    [Fact]
    public void BuildAnalyzerSystemPrompt_應包含JSON格式說明()
    {
        var result = RiskAnalysisPromptBuilder.BuildAnalyzerSystemPrompt();
        result.Should().Contain("RiskLevel");
        result.Should().Contain("PotentiallyAffectedProjects");
        result.Should().Contain("RecommendedAction");
    }

    [Fact]
    public void BuildAnalyzerSystemPrompt_應包含get_diff工具說明()
    {
        var result = RiskAnalysisPromptBuilder.BuildAnalyzerSystemPrompt();
        result.Should().Contain("get_diff");
    }

    // ──────────────────────────────
    // BuildAnalyzerUserPrompt
    // ──────────────────────────────

    [Fact]
    public void BuildAnalyzerUserPrompt_應包含專案路徑()
    {
        var commitShas = new List<string> { "abc123def456" };
        var scenarios = new List<RiskScenario> { RiskScenario.ApiContractBreak };

        var result = RiskAnalysisPromptBuilder.BuildAnalyzerUserPrompt("my-project/api", commitShas, scenarios);

        result.Should().Contain("my-project/api");
    }

    [Fact]
    public void BuildAnalyzerUserPrompt_應包含CommitSha清單()
    {
        var commitShas = new List<string> { "abc123def456", "def789ghi012" };
        var scenarios = new List<RiskScenario> { RiskScenario.ApiContractBreak };

        var result = RiskAnalysisPromptBuilder.BuildAnalyzerUserPrompt("proj", commitShas, scenarios);

        result.Should().Contain("abc123def456");
        result.Should().Contain("def789ghi012");
    }

    [Fact]
    public void BuildAnalyzerUserPrompt_應包含情境名稱()
    {
        var commitShas = new List<string> { "abc123" };
        var scenarios = new List<RiskScenario>
        {
            RiskScenario.ApiContractBreak,
            RiskScenario.DatabaseSchemaChange
        };

        var result = RiskAnalysisPromptBuilder.BuildAnalyzerUserPrompt("proj", commitShas, scenarios);

        result.Should().Contain("ApiContractBreak");
        result.Should().Contain("DatabaseSchemaChange");
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
}
