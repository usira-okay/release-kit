using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis.Models;

/// <summary>
/// Expert Agent 的分析結果
/// </summary>
public sealed record ExpertFindings
{
    /// <summary>
    /// 分析情境
    /// </summary>
    public required RiskScenario Scenario { get; init; }

    /// <summary>
    /// 風險發現清單
    /// </summary>
    public required IReadOnlyList<RiskFinding> Findings { get; init; }

    /// <summary>
    /// 是否分析失敗（需人工檢視）
    /// </summary>
    public bool Failed { get; init; }

    /// <summary>
    /// 失敗原因
    /// </summary>
    public string? FailureReason { get; init; }
}
