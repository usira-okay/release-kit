using System.Text;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Infrastructure.Reporting;

/// <summary>
/// Markdown 風險報告產生器
/// </summary>
public class MarkdownReportGenerator : IMarkdownReportGenerator
{
    /// <summary>
    /// 從 RiskReport 資料模型產生 Markdown 報告
    /// </summary>
    public string Generate(RiskReport report)
    {
        var sb = new StringBuilder();

        AppendHeader(sb, report);
        AppendSummaryTable(sb, report);
        AppendNotificationTargets(sb, report.Correlation.NotificationTargets);
        AppendDependencyGraph(sb, report.Correlation.DependencyEdges);
        AppendRiskDetails(sb, report);

        return sb.ToString();
    }

    /// <summary>
    /// 報告標題與中繼資料
    /// </summary>
    private static void AppendHeader(StringBuilder sb, RiskReport report)
    {
        sb.AppendLine($"# 🔍 Release 風險分析報告");
        sb.AppendLine();
        sb.AppendLine($"- **執行 ID**: {report.RunId}");
        sb.AppendLine($"- **執行時間**: {report.ExecutedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"- **分析專案數**: {report.ProjectAnalyses.Count}");
        sb.AppendLine();
    }

    /// <summary>
    /// 風險摘要表格
    /// </summary>
    private static void AppendSummaryTable(StringBuilder sb, RiskReport report)
    {
        var allFindings = report.Correlation.CorrelatedFindings;
        var high = allFindings.Count(f => f.FinalRiskLevel == RiskLevel.High);
        var medium = allFindings.Count(f => f.FinalRiskLevel == RiskLevel.Medium);
        var low = allFindings.Count(f => f.FinalRiskLevel == RiskLevel.Low);

        sb.AppendLine("## 📊 風險摘要");
        sb.AppendLine();
        sb.AppendLine("| 風險等級 | 數量 |");
        sb.AppendLine("|---------|------|");
        sb.AppendLine($"| 🔴 高風險 | {high} |");
        sb.AppendLine($"| 🟡 中風險 | {medium} |");
        sb.AppendLine($"| 🟢 低風險 | {low} |");
        sb.AppendLine($"| **合計** | **{allFindings.Count}** |");
        sb.AppendLine();
    }

    /// <summary>
    /// 通知對象清單
    /// </summary>
    private static void AppendNotificationTargets(StringBuilder sb, IReadOnlyList<NotificationTarget> targets)
    {
        if (targets.Count == 0) return;

        sb.AppendLine("## 📢 通知對象");
        sb.AppendLine();
        sb.AppendLine("| 人員 | 風險描述 | 相關專案 |");
        sb.AppendLine("|------|---------|---------|");
        foreach (var target in targets)
        {
            sb.AppendLine($"| {target.PersonName} | {target.RiskDescription} | {target.RelatedProject} |");
        }
        sb.AppendLine();
    }

    /// <summary>
    /// Mermaid 相依圖
    /// </summary>
    private static void AppendDependencyGraph(StringBuilder sb, IReadOnlyList<DependencyEdge> edges)
    {
        if (edges.Count == 0) return;

        sb.AppendLine("## 🔗 專案相依圖");
        sb.AppendLine();
        sb.AppendLine("```mermaid");
        sb.AppendLine("graph LR");
        foreach (var edge in edges)
        {
            var label = $"{edge.DependencyType}: {edge.Target}";
            sb.AppendLine($"    {SanitizeMermaidId(edge.SourceProject)} -->|{label}| {SanitizeMermaidId(edge.TargetProject)}");
        }
        sb.AppendLine("```");
        sb.AppendLine();
    }

    /// <summary>
    /// 風險詳情（依等級分組）
    /// </summary>
    private static void AppendRiskDetails(StringBuilder sb, RiskReport report)
    {
        var allFindings = report.Correlation.CorrelatedFindings;

        var highFindings = allFindings.Where(f => f.FinalRiskLevel == RiskLevel.High).ToList();
        var mediumFindings = allFindings.Where(f => f.FinalRiskLevel == RiskLevel.Medium).ToList();
        var lowFindings = allFindings.Where(f => f.FinalRiskLevel == RiskLevel.Low).ToList();

        if (highFindings.Count > 0)
        {
            sb.AppendLine("## 🔴 高風險詳情");
            sb.AppendLine();
            foreach (var finding in highFindings)
                AppendFindingDetail(sb, finding);
        }

        if (mediumFindings.Count > 0)
        {
            sb.AppendLine("## 🟡 中風險詳情");
            sb.AppendLine();
            foreach (var finding in mediumFindings)
                AppendFindingDetail(sb, finding);
        }

        if (lowFindings.Count > 0)
        {
            sb.AppendLine("## 🟢 低風險詳情");
            sb.AppendLine();
            foreach (var finding in lowFindings)
                AppendFindingBrief(sb, finding);
        }
    }

    /// <summary>
    /// 完整風險項詳情（高/中風險用）
    /// </summary>
    private static void AppendFindingDetail(StringBuilder sb, CorrelatedRiskFinding finding)
    {
        var f = finding.OriginalFinding;
        sb.AppendLine($"### {f.Scenario}: {f.Description}");
        sb.AppendLine();
        sb.AppendLine($"- **變更者**: {f.ChangedBy}");
        sb.AppendLine($"- **異動檔案**: `{f.AffectedFile}`");
        sb.AppendLine($"- **受影響專案**: {string.Join(", ", finding.ConfirmedAffectedProjects)}");
        sb.AppendLine($"- **建議動作**: {f.RecommendedAction}");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(f.DiffSnippet))
        {
            sb.AppendLine("```diff");
            sb.AppendLine(f.DiffSnippet);
            sb.AppendLine("```");
            sb.AppendLine();
        }
    }

    /// <summary>
    /// 簡要風險項（低風險用）
    /// </summary>
    private static void AppendFindingBrief(StringBuilder sb, CorrelatedRiskFinding finding)
    {
        var f = finding.OriginalFinding;
        sb.AppendLine($"- **{f.Scenario}**: {f.Description} (`{f.AffectedFile}`) — {f.ChangedBy}");
    }

    /// <summary>
    /// 清理 Mermaid node ID（移除特殊字元）
    /// </summary>
    private static string SanitizeMermaidId(string id) =>
        id.Replace("/", "_").Replace("-", "_").Replace(".", "_");
}
