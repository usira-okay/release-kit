using ReleaseKit.Domain.Entities;

namespace ReleaseKit.Domain.Abstractions;

/// <summary>
/// AI 風險分析介面，負責執行各階段的風險評估
/// </summary>
public interface IRiskAnalyzer
{
    /// <summary>
    /// Phase 2：批次初篩風險分類
    /// </summary>
    /// <param name="inputs">初篩輸入資料清單</param>
    /// <returns>各 PR 的風險評估結果</returns>
    Task<IReadOnlyList<PullRequestRisk>> ScreenRisksAsync(
        IReadOnlyList<ScreenRiskInput> inputs);

    /// <summary>
    /// Phase 3：深度分析高風險 PR
    /// </summary>
    /// <param name="inputs">深度分析輸入資料清單</param>
    /// <returns>深度分析後的風險評估結果</returns>
    Task<IReadOnlyList<PullRequestRisk>> DeepAnalyzeAsync(
        IReadOnlyList<DeepAnalyzeInput> inputs);

    /// <summary>
    /// Phase 4：跨服務關聯分析
    /// </summary>
    /// <param name="input">跨服務分析輸入資料</param>
    /// <returns>跨服務風險關聯清單</returns>
    Task<IReadOnlyList<CrossServiceRisk>> AnalyzeCrossServiceImpactAsync(
        CrossServiceAnalysisInput input);
}
