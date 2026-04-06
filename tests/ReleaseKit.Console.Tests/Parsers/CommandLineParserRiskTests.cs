using ReleaseKit.Application.Tasks;
using ReleaseKit.Console.Parsers;

namespace ReleaseKit.Console.Tests.Parsers;

/// <summary>
/// CommandLineParser 風險分析命令單元測試
/// </summary>
public class CommandLineParserRiskTests
{
    private readonly CommandLineParser _parser = new();

    [Theory]
    [InlineData("clone-repos", TaskType.CloneRepositories)]
    [InlineData("extract-pr-diffs", TaskType.ExtractPrDiffs)]
    [InlineData("analyze-project-risk", TaskType.AnalyzeProjectRisk)]
    [InlineData("analyze-cross-project-risk", TaskType.AnalyzeCrossProjectRisk)]
    [InlineData("generate-risk-report", TaskType.GenerateRiskReport)]
    [InlineData("analyze-risk", TaskType.AnalyzeRisk)]
    public void Parse_WithRiskAnalysisCommand_ShouldReturnCorrectTaskType(string command, TaskType expected)
    {
        var result = _parser.Parse(new[] { command });

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.TaskType);
    }

    [Theory]
    [InlineData("CLONE-REPOS", TaskType.CloneRepositories)]
    [InlineData("ANALYZE-RISK", TaskType.AnalyzeRisk)]
    public void Parse_WithRiskAnalysisCommand_ShouldBeCaseInsensitive(string command, TaskType expected)
    {
        var result = _parser.Parse(new[] { command });

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.TaskType);
    }
}
