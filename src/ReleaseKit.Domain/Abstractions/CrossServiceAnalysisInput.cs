using ReleaseKit.Domain.Entities;

namespace ReleaseKit.Domain.Abstractions;

/// <summary>
/// Phase 4 跨服務分析的輸入資料
/// </summary>
public sealed record CrossServiceAnalysisInput
{
    /// <summary>所有 PR 的風險分析結果摘要</summary>
    public required IReadOnlyList<PullRequestRisk> AllRisks { get; init; }

    /// <summary>服務相依性資訊</summary>
    public required string ServiceDependencyContext { get; init; }
}
