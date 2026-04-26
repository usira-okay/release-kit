using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Tests.Entities;

/// <summary>
/// RiskFinding 與 ProjectRiskAnalysis 實體單元測試
/// </summary>
public class RiskFindingTests
{
    [Fact]
    public void RiskFinding_應正確建立含完整風險資訊()
    {
        var finding = new RiskFinding
        {
            Scenario = RiskScenario.ApiContractBreak,
            RiskLevel = RiskLevel.High,
            Description = "GET /api/v1/users 新增必填參數",
            AffectedFile = "Controllers/UserController.cs",
            DiffSnippet = "- GetUser(int id)\n+ GetUser(int id, bool details)",
            PotentiallyAffectedProjects = new List<string> { "frontend-app", "mobile-api" },
            RecommendedAction = "通知前端團隊更新 API 呼叫",
            ChangedBy = "John"
        };

        Assert.Equal(RiskScenario.ApiContractBreak, finding.Scenario);
        Assert.Equal(RiskLevel.High, finding.RiskLevel);
        Assert.Equal(2, finding.PotentiallyAffectedProjects.Count);
    }

    [Fact]
    public void ProjectRiskAnalysis_應正確建立()
    {
        var analysis = new ProjectRiskAnalysis
        {
            ProjectPath = "mygroup/backend-api",
            Findings = new List<RiskFinding>(),
            SessionCount = 2
        };

        Assert.Equal("mygroup/backend-api", analysis.ProjectPath);
        Assert.Empty(analysis.Findings);
        Assert.Equal(2, analysis.SessionCount);
    }

    [Fact]
    public void CrossProjectCorrelation_應正確建立含相依圖()
    {
        var correlation = new CrossProjectCorrelation
        {
            DependencyEdges = new List<DependencyEdge>
            {
                new()
                {
                    SourceProject = "backend-api",
                    TargetProject = "frontend-app",
                    DependencyType = DependencyType.HttpCall,
                    Target = "/api/v1/users"
                }
            },
            CorrelatedFindings = new List<CorrelatedRiskFinding>(),
            NotificationTargets = new List<NotificationTarget>()
        };

        Assert.Single(correlation.DependencyEdges);
    }
}
