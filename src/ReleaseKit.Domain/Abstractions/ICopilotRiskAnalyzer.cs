using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Abstractions;

/// <summary>
/// Copilot 風險分析器介面
/// </summary>
public interface ICopilotRiskAnalyzer
{
    /// <summary>
    /// 分析指定專案的風險
    /// </summary>
    /// <param name="projectPath">專案路徑</param>
    /// <param name="fileDiffs">異動檔案</param>
    /// <param name="projectStructure">專案結構（可為 null）</param>
    /// <param name="scenarios">分析情境</param>
    /// <param name="changedBy">變更者</param>
    /// <returns>風險發現清單與使用的 session 數量</returns>
    Task<(List<RiskFinding> Findings, int SessionCount)> AnalyzeAsync(
        string projectPath,
        IReadOnlyList<FileDiff> fileDiffs,
        ProjectStructure? projectStructure,
        IReadOnlyList<RiskScenario> scenarios,
        string changedBy);
}
