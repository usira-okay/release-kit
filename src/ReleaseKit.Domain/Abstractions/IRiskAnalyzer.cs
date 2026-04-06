using ReleaseKit.Domain.Entities;

namespace ReleaseKit.Domain.Abstractions;

/// <summary>
/// AI 風險分析服務介面
/// </summary>
public interface IRiskAnalyzer
{
    /// <summary>分析單一專案的 PR 變更風險（Pass 1）</summary>
    Task<RiskAnalysisReport> AnalyzeProjectRiskAsync(
        string projectName,
        IReadOnlyList<PrDiffContext> diffs,
        CancellationToken cancellationToken = default);

    /// <summary>動態深度分析：接收前一層報告，產出下一層分析（Pass 2~10）</summary>
    Task<DynamicAnalysisResult> AnalyzeDeepAsync(
        int currentPass,
        IReadOnlyList<RiskAnalysisReport> previousPassReports,
        CancellationToken cancellationToken = default);

    /// <summary>產生最終整合報告 Markdown</summary>
    Task<string> GenerateFinalReportAsync(
        IReadOnlyList<RiskAnalysisReport> lastPassReports,
        CancellationToken cancellationToken = default);
}
