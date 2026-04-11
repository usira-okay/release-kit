namespace ReleaseKit.Domain.Tests.Entities;

using Xunit;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

/// <summary>
/// RiskAnalysisReport 實體測試
/// </summary>
public class RiskAnalysisReportTests
{
    [Fact]
    public void 建構_應正確設定所有屬性()
    {
        // Arrange & Act
        var report = new RiskAnalysisReport
        {
            Sequence = 1,
            ProjectName = "my-service",
            RiskItems = new List<RiskItem>
            {
                new()
                {
                    Category = RiskCategory.ApiContract,
                    Level = RiskLevel.High,
                    ChangeSummary = "API 變更",
                    AffectedFiles = new List<string> { "Controller.cs" },
                    PotentiallyAffectedServices = new List<string> { "Frontend" },
                    ImpactDescription = "影響前端",
                    SuggestedValidationSteps = new List<string> { "測試 API" }
                }
            },
            Summary = "測試摘要",
            AnalyzedAt = DateTimeOffset.UtcNow
        };

        // Assert
        Assert.Equal(1, report.Sequence);
        Assert.Equal("my-service", report.ProjectName);
        Assert.Single(report.RiskItems);
        Assert.Equal("測試摘要", report.Summary);
    }

    [Fact]
    public void AnalysisLog為可選屬性_預設為null()
    {
        // Arrange & Act
        var report = new RiskAnalysisReport
        {
            Sequence = 1,
            ProjectName = "svc",
            RiskItems = new List<RiskItem>(),
            Summary = "空報告",
            AnalyzedAt = DateTimeOffset.UtcNow
        };

        // Assert
        Assert.Null(report.AnalysisLog);
    }

    [Fact]
    public void AnalysisLog可設定值()
    {
        // Arrange & Act
        var report = new RiskAnalysisReport
        {
            Sequence = 1,
            ProjectName = "svc",
            RiskItems = new List<RiskItem>(),
            Summary = "摘要",
            AnalyzedAt = DateTimeOffset.UtcNow,
            AnalysisLog = "執行了 git diff 指令"
        };

        // Assert
        Assert.Equal("執行了 git diff 指令", report.AnalysisLog);
    }
}
