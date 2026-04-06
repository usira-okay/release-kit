namespace ReleaseKit.Domain.Tests.Entities;

using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

/// <summary>
/// DynamicAnalysisResult 實體單元測試
/// </summary>
public class DynamicAnalysisResultTests
{
    [Fact]
    public void DynamicAnalysisResult_ContinueAnalysis_ShouldIndicateMorePasses()
    {
        var result = new DynamicAnalysisResult
        {
            Reports = new List<RiskAnalysisReport>(),
            ContinueAnalysis = true,
            ContinueReason = "發現需要進一步交叉比對的風險",
            AnalysisStrategy = "按風險類別分組交叉比對"
        };

        Assert.True(result.ContinueAnalysis);
        Assert.NotNull(result.ContinueReason);
    }

    [Fact]
    public void DynamicAnalysisResult_StopAnalysis_ShouldIndicateComplete()
    {
        var result = new DynamicAnalysisResult
        {
            Reports = new List<RiskAnalysisReport>(),
            ContinueAnalysis = false,
            ContinueReason = null,
            AnalysisStrategy = "最終驗證"
        };

        Assert.False(result.ContinueAnalysis);
        Assert.Null(result.ContinueReason);
    }
}
