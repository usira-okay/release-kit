namespace ReleaseKit.Domain.Tests.Entities;

using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

/// <summary>
/// RiskAnalysisReport 聚合根單元測試
/// </summary>
public class RiskAnalysisReportTests
{
    [Fact]
    public void RiskAnalysisReport_ShouldBeCreatedWithRequiredProperties()
    {
        var report = new RiskAnalysisReport
        {
            PassKey = new AnalysisPassKey { Pass = 1, Sequence = 1 },
            RiskItems = new List<RiskItem>(),
            Summary = "無風險項目",
            AnalyzedAt = DateTimeOffset.UtcNow
        };

        Assert.NotNull(report.PassKey);
        Assert.Empty(report.RiskItems);
        Assert.Null(report.ProjectName);
        Assert.Null(report.Category);
    }

    [Fact]
    public void RiskAnalysisReport_Pass1_ShouldHaveProjectName()
    {
        var report = new RiskAnalysisReport
        {
            PassKey = new AnalysisPassKey { Pass = 1, Sequence = 1 },
            ProjectName = "ServiceA",
            RiskItems = new List<RiskItem>(),
            Summary = "ServiceA 分析完成",
            AnalyzedAt = DateTimeOffset.UtcNow
        };

        Assert.Equal("ServiceA", report.ProjectName);
    }

    [Fact]
    public void RiskAnalysisReport_Pass2_ShouldHaveCategory()
    {
        var report = new RiskAnalysisReport
        {
            PassKey = new AnalysisPassKey { Pass = 2, Sequence = 1 },
            Category = RiskCategory.ApiContract,
            RiskItems = new List<RiskItem>(),
            Summary = "API 契約風險分析",
            AnalyzedAt = DateTimeOffset.UtcNow
        };

        Assert.Equal(RiskCategory.ApiContract, report.Category);
    }
}
