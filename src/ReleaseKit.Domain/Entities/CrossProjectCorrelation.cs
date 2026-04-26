namespace ReleaseKit.Domain.Entities;

/// <summary>
/// 跨專案交叉比對結果
/// </summary>
public sealed record CrossProjectCorrelation
{
    /// <summary>
    /// 相依性邊清單
    /// </summary>
    public required IReadOnlyList<DependencyEdge> DependencyEdges { get; init; }

    /// <summary>
    /// 經交叉比對後的風險發現清單
    /// </summary>
    public required IReadOnlyList<CorrelatedRiskFinding> CorrelatedFindings { get; init; }

    /// <summary>
    /// 通知對象清單
    /// </summary>
    public required IReadOnlyList<NotificationTarget> NotificationTargets { get; init; }
}
