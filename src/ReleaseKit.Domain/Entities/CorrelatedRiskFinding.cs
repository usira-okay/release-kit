using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Entities;

/// <summary>
/// 經交叉比對後的風險發現（包含受影響專案的確認）
/// </summary>
public sealed record CorrelatedRiskFinding
{
    /// <summary>
    /// 原始風險發現
    /// </summary>
    public required RiskFinding OriginalFinding { get; init; }

    /// <summary>
    /// 經確認的受影響專案清單
    /// </summary>
    public required IReadOnlyList<string> ConfirmedAffectedProjects { get; init; }

    /// <summary>
    /// 最終風險等級（可能因交叉比對而調整）
    /// </summary>
    public required RiskLevel FinalRiskLevel { get; init; }
}
