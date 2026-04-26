namespace ReleaseKit.Domain.Entities;

/// <summary>
/// 單一專案的風險分析結果
/// </summary>
public sealed record ProjectRiskAnalysis
{
    /// <summary>
    /// 專案路徑
    /// </summary>
    public required string ProjectPath { get; init; }

    /// <summary>
    /// 風險發現清單
    /// </summary>
    public required IReadOnlyList<RiskFinding> Findings { get; init; }

    /// <summary>
    /// 使用的 Copilot session 數量
    /// </summary>
    public required int SessionCount { get; init; }
}
