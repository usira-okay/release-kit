namespace ReleaseKit.Domain.Tests.Entities;

using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

/// <summary>
/// RiskItem 實體單元測試
/// </summary>
public class RiskItemTests
{
    [Fact]
    public void RiskItem_ShouldBeCreatedWithRequiredProperties()
    {
        var item = new RiskItem
        {
            Category = RiskCategory.ApiContract,
            Level = RiskLevel.High,
            ChangeSummary = "修改了 /api/v1/orders 的 Response 模型",
            AffectedFiles = new List<string> { "Controllers/OrderController.cs" },
            PotentiallyAffectedServices = new List<string> { "ServiceB" },
            ImpactDescription = "移除了 legacyOrderId 欄位",
            SuggestedValidationSteps = new List<string> { "確認 ServiceB 的呼叫邏輯" }
        };

        Assert.Equal(RiskCategory.ApiContract, item.Category);
        Assert.Equal(RiskLevel.High, item.Level);
        Assert.Null(item.SourceProject);
        Assert.Null(item.AffectedProject);
    }

    [Fact]
    public void RiskItem_WithOptionalProperties_ShouldSetCorrectly()
    {
        var item = new RiskItem
        {
            Category = RiskCategory.DatabaseData,
            Level = RiskLevel.Medium,
            ChangeSummary = "變更 Lookup table 資料",
            AffectedFiles = new List<string> { "Migrations/AddStatus.sql" },
            PotentiallyAffectedServices = new List<string> { "ServiceA", "ServiceC" },
            ImpactDescription = "新增狀態碼可能導致 switch/case 未涵蓋",
            SuggestedValidationSteps = new List<string> { "檢查 switch/case" },
            SourceProject = "ProjectA",
            AffectedProject = "ProjectB"
        };

        Assert.Equal("ProjectA", item.SourceProject);
        Assert.Equal("ProjectB", item.AffectedProject);
    }

    [Fact]
    public void RiskItem_Equality_SameValues_ShouldBeEqual()
    {
        var files = new List<string> { "appsettings.json" };
        var services = new List<string>();
        var steps = new List<string> { "驗證設定" };

        var item1 = CreateTestRiskItem(files, services, steps);
        var item2 = CreateTestRiskItem(files, services, steps);

        Assert.Equal(item1, item2);
    }

    private static RiskItem CreateTestRiskItem(
        IReadOnlyList<string> files,
        IReadOnlyList<string> services,
        IReadOnlyList<string> steps) => new()
    {
        Category = RiskCategory.Configuration,
        Level = RiskLevel.Low,
        ChangeSummary = "修改 appsettings",
        AffectedFiles = files,
        PotentiallyAffectedServices = services,
        ImpactDescription = "鍵值變更",
        SuggestedValidationSteps = steps
    };
}
