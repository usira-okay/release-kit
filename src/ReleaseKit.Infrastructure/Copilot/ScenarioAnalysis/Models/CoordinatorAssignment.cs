using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis.Models;

/// <summary>
/// Coordinator Agent 的分配結果
/// </summary>
public sealed record CoordinatorAssignment
{
    /// <summary>
    /// 各情境對應的 CommitSha 清單
    /// </summary>
    public required Dictionary<RiskScenario, List<string>> Assignments { get; init; }

    /// <summary>
    /// Coordinator 的推理說明
    /// </summary>
    public string Reasoning { get; init; } = string.Empty;

    /// <summary>
    /// 取得指定情境的 CommitSha 清單
    /// </summary>
    public IReadOnlyList<string> GetShas(RiskScenario scenario)
        => Assignments.TryGetValue(scenario, out var shas) ? shas : [];
}
