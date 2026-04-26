using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Infrastructure.Reporting;

namespace ReleaseKit.Infrastructure.Tests.Reporting;

/// <summary>
/// MarkdownReportGenerator 單元測試
/// </summary>
public class MarkdownReportGeneratorTests
{
    private readonly MarkdownReportGenerator _generator = new();

    private static RiskReport CreateTestReport()
    {
        var finding = new RiskFinding
        {
            Scenario = RiskScenario.ApiContractBreak,
            RiskLevel = RiskLevel.High,
            Description = "GET /api/users 移除了必填參數",
            AffectedFile = "Controllers/UserController.cs",
            DiffSnippet = "- public IActionResult Get(int id)\n+ public IActionResult Get()",
            PotentiallyAffectedProjects = new List<string> { "frontend-app" },
            RecommendedAction = "通知前端團隊",
            ChangedBy = "developer-a"
        };

        return new RiskReport
        {
            RunId = "20260426103000",
            ExecutedAt = new DateTimeOffset(2026, 4, 26, 10, 30, 0, TimeSpan.Zero),
            Correlation = new CrossProjectCorrelation
            {
                DependencyEdges = new List<DependencyEdge>
                {
                    new()
                    {
                        SourceProject = "backend-api",
                        TargetProject = "frontend-app",
                        DependencyType = DependencyType.HttpCall,
                        Target = "/api/users"
                    }
                },
                CorrelatedFindings = new List<CorrelatedRiskFinding>
                {
                    new()
                    {
                        OriginalFinding = finding,
                        ConfirmedAffectedProjects = new List<string> { "frontend-app" },
                        FinalRiskLevel = RiskLevel.High
                    }
                },
                NotificationTargets = new List<NotificationTarget>
                {
                    new()
                    {
                        PersonName = "developer-b",
                        RiskDescription = "API 契約破壞影響 frontend-app",
                        RelatedProject = "frontend-app"
                    }
                }
            },
            ProjectAnalyses = new List<ProjectRiskAnalysis>
            {
                new()
                {
                    ProjectPath = "backend-api",
                    Findings = new List<RiskFinding> { finding },
                    SessionCount = 1
                }
            },
            MarkdownContent = ""
        };
    }

    [Fact]
    public void Generate_應包含報告標題()
    {
        var report = CreateTestReport();
        var md = _generator.Generate(report);

        Assert.Contains("Release 風險分析報告", md);
        Assert.Contains("20260426103000", md);
    }

    [Fact]
    public void Generate_應包含風險摘要表格()
    {
        var report = CreateTestReport();
        var md = _generator.Generate(report);

        Assert.Contains("風險摘要", md);
        Assert.Contains("高風險", md);
        Assert.Contains("| 風險等級 |", md);
    }

    [Fact]
    public void Generate_應包含通知對象()
    {
        var report = CreateTestReport();
        var md = _generator.Generate(report);

        Assert.Contains("通知對象", md);
        Assert.Contains("developer-b", md);
    }

    [Fact]
    public void Generate_應包含Mermaid相依圖()
    {
        var report = CreateTestReport();
        var md = _generator.Generate(report);

        Assert.Contains("mermaid", md);
        Assert.Contains("graph LR", md);
        Assert.Contains("backend_api", md);
    }

    [Fact]
    public void Generate_應包含高風險詳情含diff()
    {
        var report = CreateTestReport();
        var md = _generator.Generate(report);

        Assert.Contains("高風險詳情", md);
        Assert.Contains("developer-a", md);
        Assert.Contains("UserController.cs", md);
        Assert.Contains("```diff", md);
    }

    [Fact]
    public void Generate_空報告應產出基本結構()
    {
        var report = new RiskReport
        {
            RunId = "20260426000000",
            ExecutedAt = DateTimeOffset.UtcNow,
            Correlation = new CrossProjectCorrelation
            {
                DependencyEdges = new List<DependencyEdge>(),
                CorrelatedFindings = new List<CorrelatedRiskFinding>(),
                NotificationTargets = new List<NotificationTarget>()
            },
            ProjectAnalyses = new List<ProjectRiskAnalysis>(),
            MarkdownContent = ""
        };

        var md = _generator.Generate(report);

        Assert.Contains("風險分析報告", md);
        Assert.Contains("風險摘要", md);
        Assert.DoesNotContain("高風險詳情", md);
    }
}
