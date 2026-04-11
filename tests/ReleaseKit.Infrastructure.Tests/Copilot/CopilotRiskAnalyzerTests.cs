using FluentAssertions;
using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Infrastructure.Copilot;

namespace ReleaseKit.Infrastructure.Tests.Copilot;

/// <summary>
/// CopilotRiskAnalyzer（Agentic 模式）單元測試
/// </summary>
public class CopilotRiskAnalyzerTests
{
    [Theory]
    [InlineData("API 契約")]
    [InlineData("DB Schema")]
    [InlineData("DB 資料異動")]
    [InlineData("事件/訊息格式")]
    [InlineData("設定檔")]
    public void AgenticSystemPrompt_應包含所有風險類別關鍵字(string keyword)
    {
        CopilotRiskAnalyzer.AgenticSystemPrompt.Should().Contain(keyword);
    }

    [Fact]
    public void AgenticSystemPrompt_應包含run_command工具說明()
    {
        CopilotRiskAnalyzer.AgenticSystemPrompt.Should().Contain("run_command");
    }

    [Fact]
    public void AgenticSystemPrompt_應包含git指令建議()
    {
        CopilotRiskAnalyzer.AgenticSystemPrompt.Should().Contain("git diff");
        CopilotRiskAnalyzer.AgenticSystemPrompt.Should().Contain("git log");
    }

    [Fact]
    public void AgenticSystemPrompt_應要求Markdown格式輸出()
    {
        CopilotRiskAnalyzer.AgenticSystemPrompt.Should().Contain("Markdown");
        CopilotRiskAnalyzer.AgenticSystemPrompt.Should().NotContain("純 JSON");
    }

    [Fact]
    public void BuildUserPrompt_應包含專案名稱和CommitSha()
    {
        var context = new ProjectAnalysisContext
        {
            ProjectName = "my-service",
            RepoPath = "/repos/my-service",
            CommitShas = new List<string> { "abc123", "def456" }
        };

        var prompt = CopilotRiskAnalyzer.BuildUserPrompt(context);

        prompt.Should().Contain("my-service");
        prompt.Should().Contain("abc123");
        prompt.Should().Contain("def456");
        prompt.Should().Contain("/repos/my-service");
    }
}
