using System.Text;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Application.Common.RiskAnalysis;

/// <summary>
/// 產生 Markdown 格式的風險分析報告
/// </summary>
public class RiskReportGenerator
{
    /// <summary>風險等級對應的 Emoji</summary>
    private static readonly Dictionary<RiskLevel, string> RiskLevelEmojis = new()
    {
        [RiskLevel.Critical] = "🔴",
        [RiskLevel.High] = "🟠",
        [RiskLevel.Medium] = "🟡",
        [RiskLevel.Low] = "🟢",
        [RiskLevel.None] = "⚪"
    };

    /// <summary>風險類別對應的中文名稱</summary>
    private static readonly Dictionary<RiskCategory, string> RiskCategoryNames = new()
    {
        [RiskCategory.CrossServiceApiBreaking] = "跨服務 API 破壞性變更",
        [RiskCategory.SharedLibraryChange] = "共用函式庫變更",
        [RiskCategory.DatabaseSchemaChange] = "資料庫 Schema 變更",
        [RiskCategory.DatabaseDataChange] = "資料庫資料異動",
        [RiskCategory.ConfigurationChange] = "設定檔變更",
        [RiskCategory.SecurityChange] = "安全性變更",
        [RiskCategory.PerformanceChange] = "效能變更",
        [RiskCategory.CoreBusinessLogicChange] = "核心商業邏輯修改"
    };

    /// <summary>風險等級由高到低的排列順序</summary>
    private static readonly RiskLevel[] RiskLevelOrder =
    {
        RiskLevel.Critical,
        RiskLevel.High,
        RiskLevel.Medium,
        RiskLevel.Low,
        RiskLevel.None
    };

    /// <summary>
    /// 產生 Markdown 格式的風險分析報告
    /// </summary>
    /// <param name="report">風險分析報告實體</param>
    /// <returns>Markdown 格式的報告字串</returns>
    public string GenerateMarkdown(RiskAnalysisReport report)
    {
        var sb = new StringBuilder();

        AppendHeader(sb, report);
        AppendRiskLevelSummary(sb, report);
        AppendRiskCategorySummary(sb, report);
        AppendHighRiskItems(sb, report);
        AppendCrossServiceRisks(sb, report);
        AppendRepositoryDetails(sb, report);

        return sb.ToString();
    }

    /// <summary>
    /// 產生報告標題與分析中繼資料
    /// </summary>
    private static void AppendHeader(StringBuilder sb, RiskAnalysisReport report)
    {
        sb.AppendLine("# 🔍 Release Risk Analysis Report");
        sb.AppendLine();
        sb.AppendLine($"- **分析時間**: {report.AnalyzedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"- **分析範圍**: {report.TotalRepositories} 個 Repository, {report.TotalPullRequests} 個 Pull Request");
        sb.AppendLine($"- **跨服務風險**: {report.CrossServiceRisks.Count} 個");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
    }

    /// <summary>
    /// 產生風險等級摘要表格
    /// </summary>
    private static void AppendRiskLevelSummary(StringBuilder sb, RiskAnalysisReport report)
    {
        sb.AppendLine("## 📊 風險等級摘要");
        sb.AppendLine();
        sb.AppendLine("| 等級 | 數量 |");
        sb.AppendLine("|------|------|");

        var summary = report.RiskLevelSummary;

        foreach (var level in RiskLevelOrder)
        {
            var count = summary.GetValueOrDefault(level, 0);
            var emoji = RiskLevelEmojis[level];
            sb.AppendLine($"| {emoji} {level} | {count} |");
        }

        sb.AppendLine();
    }

    /// <summary>
    /// 產生風險類別分布表格
    /// </summary>
    private static void AppendRiskCategorySummary(StringBuilder sb, RiskAnalysisReport report)
    {
        sb.AppendLine("## 📋 風險類別分布");
        sb.AppendLine();

        var summary = report.RiskCategorySummary;

        if (summary.Count == 0)
        {
            sb.AppendLine("無風險類別資料。");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("| 類別 | 數量 |");
        sb.AppendLine("|------|------|");

        foreach (var (category, count) in summary.OrderByDescending(kv => kv.Value))
        {
            var name = RiskCategoryNames.GetValueOrDefault(category, category.ToString());
            sb.AppendLine($"| {name} | {count} |");
        }

        sb.AppendLine();
    }

    /// <summary>
    /// 產生高風險項目（Critical 與 High）的詳細資訊
    /// </summary>
    private static void AppendHighRiskItems(StringBuilder sb, RiskAnalysisReport report)
    {
        var highRiskPrs = report.RepositoryResults
            .SelectMany(r => r.PullRequestRisks)
            .Where(pr => pr.RiskLevel >= RiskLevel.High)
            .OrderByDescending(pr => pr.RiskLevel)
            .ToList();

        if (highRiskPrs.Count == 0)
        {
            return;
        }

        sb.AppendLine("## 🚨 高風險項目");
        sb.AppendLine();

        foreach (var levelGroup in highRiskPrs.GroupBy(pr => pr.RiskLevel).OrderByDescending(g => g.Key))
        {
            var emoji = RiskLevelEmojis[levelGroup.Key];
            sb.AppendLine($"### {emoji} {levelGroup.Key}");
            sb.AppendLine();

            foreach (var pr in levelGroup)
            {
                sb.AppendLine($"#### {pr.RepositoryName} - PR #{pr.PrId}: {pr.PrTitle}");
                var categoryNames = pr.RiskCategories
                    .Select(c => RiskCategoryNames.GetValueOrDefault(c, c.ToString()));
                sb.AppendLine($"- **風險類別**: {string.Join(", ", categoryNames)}");
                sb.AppendLine($"- **風險描述**: {pr.RiskDescription}");
                sb.AppendLine($"- **受影響元件**: {string.Join(", ", pr.AffectedComponents)}");
                sb.AppendLine($"- **建議行動**: {pr.SuggestedAction}");
                sb.AppendLine($"- **PR 連結**: {pr.PrUrl}");
                sb.AppendLine();
            }
        }
    }

    /// <summary>
    /// 產生跨服務影響分析區段
    /// </summary>
    private static void AppendCrossServiceRisks(StringBuilder sb, RiskAnalysisReport report)
    {
        if (report.CrossServiceRisks.Count == 0)
        {
            return;
        }

        sb.AppendLine("## 🔗 跨服務影響分析");
        sb.AppendLine();

        foreach (var risk in report.CrossServiceRisks)
        {
            var affected = string.Join(", ", risk.AffectedServices);
            sb.AppendLine($"### {risk.SourceService} → {affected}");
            var emoji = RiskLevelEmojis.GetValueOrDefault(risk.RiskLevel, "⚪");
            sb.AppendLine($"- **風險等級**: {emoji} {risk.RiskLevel}");
            sb.AppendLine($"- **影響描述**: {risk.ImpactDescription}");
            sb.AppendLine($"- **建議行動**: {risk.SuggestedAction}");
            sb.AppendLine($"- **相關 PR**: {string.Join(", ", risk.RelatedPrIds.Select(id => $"#{id}"))}");
            sb.AppendLine();
        }
    }

    /// <summary>
    /// 產生各 Repository 的詳細分析區段
    /// </summary>
    private static void AppendRepositoryDetails(StringBuilder sb, RiskAnalysisReport report)
    {
        if (report.RepositoryResults.Count == 0)
        {
            return;
        }

        sb.AppendLine("## 📁 各 Repository 詳細分析");
        sb.AppendLine();

        foreach (var repo in report.RepositoryResults)
        {
            var maxEmoji = RiskLevelEmojis.GetValueOrDefault(repo.MaxRiskLevel, "⚪");
            sb.AppendLine($"### {repo.RepositoryName} ({maxEmoji} {repo.MaxRiskLevel})");
            sb.AppendLine();
            sb.AppendLine("| PR | 風險等級 | 風險類別 | 描述 |");
            sb.AppendLine("|----|---------|---------|------|");

            foreach (var pr in repo.PullRequestRisks)
            {
                var emoji = RiskLevelEmojis.GetValueOrDefault(pr.RiskLevel, "⚪");
                var categories = string.Join(", ",
                    pr.RiskCategories.Select(c => RiskCategoryNames.GetValueOrDefault(c, c.ToString())));
                sb.AppendLine($"| #{pr.PrId} | {emoji} {pr.RiskLevel} | {categories} | {pr.PrTitle} |");
            }

            sb.AppendLine();
        }
    }
}
