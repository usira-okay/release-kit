using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Abstractions;

/// <summary>
/// AI 風險分析服務介面（Agentic 模式）
/// </summary>
public interface IRiskAnalyzer
{
    /// <summary>分析單一專案的變更風險（Agentic：Copilot 自主探索 repo）</summary>
    /// <param name="context">專案分析上下文（含 repo 路徑與 commit SHA）</param>
    /// <param name="cancellationToken">取消權杖</param>
    /// <returns>風險分析報告</returns>
    Task<RiskAnalysisReport> AnalyzeProjectRiskAsync(
        ProjectAnalysisContext context,
        CancellationToken cancellationToken = default);

    /// <summary>產生最終整合報告 Markdown</summary>
    /// <param name="reports">所有專案的中間分析報告</param>
    /// <param name="cancellationToken">取消權杖</param>
    /// <returns>Markdown 格式的最終報告</returns>
    Task<string> GenerateFinalReportAsync(
        IReadOnlyList<RiskAnalysisReport> reports,
        CancellationToken cancellationToken = default);
}
