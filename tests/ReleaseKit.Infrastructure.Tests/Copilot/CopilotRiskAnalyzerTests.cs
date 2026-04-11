using FluentAssertions;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Infrastructure.Copilot;

namespace ReleaseKit.Infrastructure.Tests.Copilot;

/// <summary>
/// CopilotRiskAnalyzer（Agentic 模式）單元測試
/// </summary>
public class CopilotRiskAnalyzerTests
{
    private static readonly DateTimeOffset FixedTime = new(2025, 1, 15, 10, 0, 0, TimeSpan.Zero);

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
    public void ParseProjectRiskResponse_有效JSON_應正確解析()
    {
        var json = """
            {
              "riskItems": [
                {
                  "category": "ApiContract",
                  "level": "High",
                  "changeSummary": "修改了 API 回傳格式",
                  "affectedFiles": ["src/Controller.cs"],
                  "potentiallyAffectedServices": ["Frontend"],
                  "impactDescription": "前端可能解析失敗",
                  "suggestedValidationSteps": ["確認前端 API 呼叫"]
                }
              ],
              "summary": "發現 1 項高風險",
              "analysisLog": "執行了 git diff abc123"
            }
            """;

        var result = CopilotRiskAnalyzer.ParseProjectRiskResponse(json, "my-svc", FixedTime);

        result.Should().NotBeNull();
        result!.Sequence.Should().Be(0);
        result.ProjectName.Should().Be("my-svc");
        result.Summary.Should().Be("發現 1 項高風險");
        result.AnalysisLog.Should().Be("執行了 git diff abc123");
        result.RiskItems.Should().HaveCount(1);
        result.RiskItems[0].Category.Should().Be(RiskCategory.ApiContract);
    }

    [Fact]
    public void ParseProjectRiskResponse_無效JSON_應回傳null()
    {
        var result = CopilotRiskAnalyzer.ParseProjectRiskResponse("not json {{{", "svc", FixedTime);
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ParseProjectRiskResponse_空白或null_應回傳null(string? content)
    {
        var result = CopilotRiskAnalyzer.ParseProjectRiskResponse(content!, "svc", FixedTime);
        result.Should().BeNull();
    }

    [Fact]
    public void CleanMarkdownWrapper_包含json代碼區塊_應清除標記()
    {
        var wrapped = """
            ```json
            {"riskItems":[],"summary":"無風險"}
            ```
            """;

        var result = CopilotRiskAnalyzer.CleanMarkdownWrapper(wrapped);
        result.Should().Be("""{"riskItems":[],"summary":"無風險"}""");
    }

    [Fact]
    public void CleanMarkdownWrapper_無包裝_應原樣回傳()
    {
        var plain = """{"riskItems":[],"summary":"ok"}""";
        var result = CopilotRiskAnalyzer.CleanMarkdownWrapper(plain);
        result.Should().Be(plain);
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
