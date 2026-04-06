using FluentAssertions;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Infrastructure.Copilot;

namespace ReleaseKit.Infrastructure.Tests.Copilot;

/// <summary>
/// CopilotRiskAnalyzer 的單元測試
/// </summary>
/// <remarks>
/// 因 CopilotClient 為 sealed 類別無法 mock，
/// 測試聚焦於 internal static 解析方法與提示詞內容驗證。
/// </remarks>
public class CopilotRiskAnalyzerTests
{
    private static readonly DateTimeOffset FixedTime = new(2025, 1, 15, 10, 0, 0, TimeSpan.Zero);

    // ──────────────────────────────────────────────
    // 1. Pass1SystemPrompt 包含預期關鍵字
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData("API 契約")]
    [InlineData("DB Schema")]
    [InlineData("DB 資料異動")]
    [InlineData("事件/訊息格式")]
    [InlineData("設定檔")]
    public void Pass1SystemPrompt_應包含所有風險類別關鍵字(string keyword)
    {
        // Assert
        CopilotRiskAnalyzer.Pass1SystemPrompt.Should().Contain(keyword);
    }

    // ──────────────────────────────────────────────
    // 2. ParseProjectRiskResponse 正確解析有效 JSON
    // ──────────────────────────────────────────────

    [Fact]
    public void ParseProjectRiskResponse_有效JSON_應正確解析為RiskAnalysisReport()
    {
        // Arrange
        var json = """
            {
              "riskItems": [
                {
                  "category": "ApiContract",
                  "level": "High",
                  "changeSummary": "修改了使用者 API 回傳格式",
                  "affectedFiles": ["src/Controllers/UserController.cs"],
                  "potentiallyAffectedServices": ["Frontend", "MobileApp"],
                  "sourceProject": "UserService",
                  "affectedProject": "Gateway",
                  "impactDescription": "回傳欄位名稱變更可能導致前端解析失敗",
                  "suggestedValidationSteps": ["確認前端 API 呼叫", "執行整合測試"]
                }
              ],
              "summary": "發現 1 項高風險 API 契約變更"
            }
            """;

        // Act
        var result = CopilotRiskAnalyzer.ParseProjectRiskResponse(json, "UserService", FixedTime, 1);

        // Assert
        result.Should().NotBeNull();
        result!.PassKey.Pass.Should().Be(1);
        result.PassKey.Sequence.Should().Be(1);
        result.ProjectName.Should().Be("UserService");
        result.Summary.Should().Be("發現 1 項高風險 API 契約變更");
        result.AnalyzedAt.Should().Be(FixedTime);
        result.RiskItems.Should().HaveCount(1);

        var item = result.RiskItems[0];
        item.Category.Should().Be(RiskCategory.ApiContract);
        item.Level.Should().Be(RiskLevel.High);
        item.ChangeSummary.Should().Be("修改了使用者 API 回傳格式");
        item.AffectedFiles.Should().ContainSingle("src/Controllers/UserController.cs");
        item.PotentiallyAffectedServices.Should().Contain("Frontend");
        item.SourceProject.Should().Be("UserService");
        item.AffectedProject.Should().Be("Gateway");
        item.ImpactDescription.Should().Be("回傳欄位名稱變更可能導致前端解析失敗");
        item.SuggestedValidationSteps.Should().HaveCount(2);
    }

    // ──────────────────────────────────────────────
    // 3. ParseProjectRiskResponse 處理無效 JSON
    // ──────────────────────────────────────────────

    [Fact]
    public void ParseProjectRiskResponse_無效JSON_應回傳null()
    {
        // Arrange
        var invalidJson = "this is not valid json {{{";

        // Act
        var result = CopilotRiskAnalyzer.ParseProjectRiskResponse(invalidJson, "TestProject", FixedTime, 1);

        // Assert
        result.Should().BeNull();
    }

    // ──────────────────────────────────────────────
    // 4. ParseProjectRiskResponse 處理空回應
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ParseProjectRiskResponse_空白或null回應_應回傳null(string? content)
    {
        // Act
        var result = CopilotRiskAnalyzer.ParseProjectRiskResponse(content!, "TestProject", FixedTime, 1);

        // Assert
        result.Should().BeNull();
    }

    // ──────────────────────────────────────────────
    // 5. CleanMarkdownWrapper 清除 markdown 包裝
    // ──────────────────────────────────────────────

    [Fact]
    public void CleanMarkdownWrapper_包含json代碼區塊_應清除標記並保留JSON內容()
    {
        // Arrange
        var wrapped = """
            ```json
            {"riskItems":[],"summary":"無風險"}
            ```
            """;

        // Act
        var result = CopilotRiskAnalyzer.CleanMarkdownWrapper(wrapped);

        // Assert
        result.Should().Be("""{"riskItems":[],"summary":"無風險"}""");
    }

    [Fact]
    public void CleanMarkdownWrapper_無markdown包裝_應原樣回傳()
    {
        // Arrange
        var plain = """{"riskItems":[],"summary":"無風險"}""";

        // Act
        var result = CopilotRiskAnalyzer.CleanMarkdownWrapper(plain);

        // Assert
        result.Should().Be(plain);
    }

    // ──────────────────────────────────────────────
    // 6. ParseDynamicAnalysisResponse 正確解析
    // ──────────────────────────────────────────────

    [Fact]
    public void ParseDynamicAnalysisResponse_有效JSON_應正確解析為DynamicAnalysisResult()
    {
        // Arrange
        var json = """
            {
              "analysisStrategy": "跨專案影響分析",
              "continueAnalysis": true,
              "continueReason": "發現潛在的跨服務影響需要更深入分析",
              "riskItems": [
                {
                  "category": "DatabaseSchema",
                  "level": "Medium",
                  "changeSummary": "新增欄位至 Users 表",
                  "affectedFiles": ["migrations/001_add_column.sql"],
                  "potentiallyAffectedServices": ["ReportService"],
                  "impactDescription": "報表服務可能需要更新查詢",
                  "suggestedValidationSteps": ["檢查報表服務的 SQL 查詢"]
                }
              ],
              "summary": "識別到跨服務 DB Schema 影響"
            }
            """;

        // Act
        var result = CopilotRiskAnalyzer.ParseDynamicAnalysisResponse(json, FixedTime, 2);

        // Assert
        result.Should().NotBeNull();
        result!.ContinueAnalysis.Should().BeTrue();
        result.ContinueReason.Should().Be("發現潛在的跨服務影響需要更深入分析");
        result.AnalysisStrategy.Should().Be("跨專案影響分析");
        result.Reports.Should().HaveCount(1);

        var report = result.Reports[0];
        report.PassKey.Pass.Should().Be(2);
        report.PassKey.Sequence.Should().Be(1);
        report.Summary.Should().Be("識別到跨服務 DB Schema 影響");
        report.RiskItems.Should().HaveCount(1);
        report.RiskItems[0].Category.Should().Be(RiskCategory.DatabaseSchema);
        report.RiskItems[0].Level.Should().Be(RiskLevel.Medium);
    }

    // ──────────────────────────────────────────────
    // 7. ParseDynamicAnalysisResponse ContinueAnalysis=false
    // ──────────────────────────────────────────────

    [Fact]
    public void ParseDynamicAnalysisResponse_ContinueAnalysisFalse_應正確解析終止信號()
    {
        // Arrange
        var json = """
            {
              "analysisStrategy": "最終確認",
              "continueAnalysis": false,
              "riskItems": [],
              "summary": "未發現新的風險項目，分析完成"
            }
            """;

        // Act
        var result = CopilotRiskAnalyzer.ParseDynamicAnalysisResponse(json, FixedTime, 3);

        // Assert
        result.Should().NotBeNull();
        result!.ContinueAnalysis.Should().BeFalse();
        result.ContinueReason.Should().BeNull();
        result.AnalysisStrategy.Should().Be("最終確認");
        result.Reports.Should().HaveCount(1);
        result.Reports[0].RiskItems.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────
    // 8. DynamicAnalysisSystemPrompt 包含動態層數佔位符
    // ──────────────────────────────────────────────

    [Fact]
    public void DynamicAnalysisSystemPrompt_應包含currentPass佔位符()
    {
        // Assert
        CopilotRiskAnalyzer.DynamicAnalysisSystemPrompt.Should().Contain("{currentPass}");
    }
}
