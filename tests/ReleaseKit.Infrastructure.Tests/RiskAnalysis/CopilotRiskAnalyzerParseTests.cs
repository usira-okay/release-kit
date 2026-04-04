using FluentAssertions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Infrastructure.RiskAnalysis;
using ReleaseKit.Infrastructure.RiskAnalysis.Models;

namespace ReleaseKit.Infrastructure.Tests.RiskAnalysis;

/// <summary>
/// CopilotRiskAnalyzer 的解析與對應邏輯單元測試
/// </summary>
public class CopilotRiskAnalyzerParseTests
{
    [Fact]
    public void ParseScreenRiskResponse_WithValidJson_ShouldParseCorrectly()
    {
        // Arrange
        var json = """
            [
              {
                "prId": "123",
                "repositoryName": "my-service",
                "riskLevel": "High",
                "riskCategories": ["DatabaseSchemaChange"],
                "riskDescription": "包含資料庫 Schema 變更",
                "needsDeepAnalysis": true,
                "affectedComponents": ["UserTable"],
                "suggestedAction": "請 DBA 審查"
              }
            ]
            """;

        // Act
        var result = CopilotRiskAnalyzer.ParseScreenRiskResponse(json);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result![0].PrId.Should().Be("123");
        result[0].RepositoryName.Should().Be("my-service");
        result[0].RiskLevel.Should().Be("High");
        result[0].RiskCategories.Should().ContainSingle("DatabaseSchemaChange");
        result[0].RiskDescription.Should().Be("包含資料庫 Schema 變更");
        result[0].NeedsDeepAnalysis.Should().BeTrue();
        result[0].AffectedComponents.Should().ContainSingle("UserTable");
        result[0].SuggestedAction.Should().Be("請 DBA 審查");
    }

    [Fact]
    public void ParseScreenRiskResponse_WithMarkdownCodeBlock_ShouldCleanAndParse()
    {
        // Arrange
        var json = """
            ```json
            [
              {
                "prId": "456",
                "repositoryName": "api-gateway",
                "riskLevel": "Critical",
                "riskCategories": ["SecurityChange"],
                "riskDescription": "認證邏輯修改",
                "needsDeepAnalysis": true,
                "affectedComponents": ["AuthModule"],
                "suggestedAction": "安全審查"
              }
            ]
            ```
            """;

        // Act
        var result = CopilotRiskAnalyzer.ParseScreenRiskResponse(json);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result![0].PrId.Should().Be("456");
        result[0].RiskLevel.Should().Be("Critical");
    }

    [Fact]
    public void ParseScreenRiskResponse_WithInvalidJson_ShouldReturnNull()
    {
        // Arrange
        var invalidJson = "this is not valid json at all";

        // Act
        var result = CopilotRiskAnalyzer.ParseScreenRiskResponse(invalidJson);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void MapToRisk_WithValidResponse_ShouldMapAllFields()
    {
        // Arrange
        var response = new ScreenRiskResponse
        {
            PrId = "100",
            RepositoryName = "order-service",
            RiskLevel = "High",
            RiskCategories = new List<string> { "DatabaseSchemaChange", "CoreBusinessLogicChange" },
            RiskDescription = "資料庫與核心邏輯變更",
            NeedsDeepAnalysis = true,
            AffectedComponents = new List<string> { "OrderTable", "PaymentCalculator" },
            SuggestedAction = "請進行深度審查"
        };

        var input = new ScreenRiskInput
        {
            PrId = "100",
            PrTitle = "修改訂單計算邏輯",
            PrUrl = "https://gitlab.com/mr/100",
            RepositoryName = "order-service",
            DiffSummary = "changed order calculation"
        };

        // Act
        var result = CopilotRiskAnalyzer.MapToRisk(response, input);

        // Assert
        result.PrId.Should().Be("100");
        result.RepositoryName.Should().Be("order-service");
        result.PrTitle.Should().Be("修改訂單計算邏輯");
        result.PrUrl.Should().Be("https://gitlab.com/mr/100");
        result.RiskLevel.Should().Be(RiskLevel.High);
        result.RiskCategories.Should().HaveCount(2);
        result.RiskCategories.Should().Contain(RiskCategory.DatabaseSchemaChange);
        result.RiskCategories.Should().Contain(RiskCategory.CoreBusinessLogicChange);
        result.RiskDescription.Should().Be("資料庫與核心邏輯變更");
        result.NeedsDeepAnalysis.Should().BeTrue();
        result.AffectedComponents.Should().HaveCount(2);
        result.SuggestedAction.Should().Be("請進行深度審查");
    }

    [Fact]
    public void MapToRisk_WithInvalidRiskLevel_ShouldDefaultToMedium()
    {
        // Arrange
        var response = new ScreenRiskResponse
        {
            PrId = "200",
            RepositoryName = "test-repo",
            RiskLevel = "InvalidLevel",
            RiskCategories = new List<string>(),
            RiskDescription = "測試",
            NeedsDeepAnalysis = false,
            AffectedComponents = new List<string>(),
            SuggestedAction = "無"
        };

        var input = new ScreenRiskInput
        {
            PrId = "200",
            PrTitle = "Test PR",
            PrUrl = "https://gitlab.com/mr/200",
            RepositoryName = "test-repo",
            DiffSummary = "test diff"
        };

        // Act
        var result = CopilotRiskAnalyzer.MapToRisk(response, input);

        // Assert
        result.RiskLevel.Should().Be(RiskLevel.Medium);
    }

    [Fact]
    public void MapToRisk_WithInvalidCategory_ShouldSkipInvalidOnes()
    {
        // Arrange
        var response = new ScreenRiskResponse
        {
            PrId = "300",
            RepositoryName = "test-repo",
            RiskLevel = "Low",
            RiskCategories = new List<string>
            {
                "SecurityChange",
                "NonExistentCategory",
                "PerformanceChange"
            },
            RiskDescription = "混合風險",
            NeedsDeepAnalysis = false,
            AffectedComponents = new List<string>(),
            SuggestedAction = "留意即可"
        };

        var input = new ScreenRiskInput
        {
            PrId = "300",
            PrTitle = "Mixed categories",
            PrUrl = "https://gitlab.com/mr/300",
            RepositoryName = "test-repo",
            DiffSummary = "mixed diff"
        };

        // Act
        var result = CopilotRiskAnalyzer.MapToRisk(response, input);

        // Assert
        result.RiskCategories.Should().HaveCount(2);
        result.RiskCategories.Should().Contain(RiskCategory.SecurityChange);
        result.RiskCategories.Should().Contain(RiskCategory.PerformanceChange);
        result.RiskCategories.Should().NotContain(c => c.ToString() == "NonExistentCategory");
    }

    [Fact]
    public void ParseCrossServiceResponse_WithValidJson_ShouldParseCorrectly()
    {
        // Arrange
        var json = """
            [
              {
                "sourceService": "order-service",
                "affectedServices": ["payment-service", "notification-service"],
                "riskLevel": "High",
                "impactDescription": "訂單 API 變更影響下游服務",
                "suggestedAction": "需要同步部署",
                "relatedPrIds": ["PR-100", "PR-200"]
              }
            ]
            """;

        // Act
        var result = CopilotRiskAnalyzer.ParseCrossServiceResponse(json);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result![0].SourceService.Should().Be("order-service");
        result[0].AffectedServices.Should().HaveCount(2);
        result[0].AffectedServices.Should().Contain("payment-service");
        result[0].AffectedServices.Should().Contain("notification-service");
        result[0].RiskLevel.Should().Be("High");
        result[0].ImpactDescription.Should().Be("訂單 API 變更影響下游服務");
        result[0].SuggestedAction.Should().Be("需要同步部署");
        result[0].RelatedPrIds.Should().HaveCount(2);
    }

    [Fact]
    public void CreateScreenFallback_ShouldReturnMediumRisk()
    {
        // Arrange
        var input = new ScreenRiskInput
        {
            PrId = "999",
            PrTitle = "Some PR",
            PrUrl = "https://gitlab.com/mr/999",
            RepositoryName = "my-repo",
            DiffSummary = "some diff"
        };

        // Act
        var result = CopilotRiskAnalyzer.CreateScreenFallback(input);

        // Assert
        result.PrId.Should().Be("999");
        result.RepositoryName.Should().Be("my-repo");
        result.PrTitle.Should().Be("Some PR");
        result.PrUrl.Should().Be("https://gitlab.com/mr/999");
        result.RiskLevel.Should().Be(RiskLevel.Medium);
        result.RiskCategories.Should().BeEmpty();
        result.RiskDescription.Should().Be("AI 分析失敗，建議人工審查");
        result.NeedsDeepAnalysis.Should().BeTrue();
        result.AffectedComponents.Should().BeEmpty();
        result.SuggestedAction.Should().Be("建議人工審查此 PR 的變更內容");
    }
}
