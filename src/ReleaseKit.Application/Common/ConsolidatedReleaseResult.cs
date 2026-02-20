namespace ReleaseKit.Application.Common;

/// <summary>
/// 整合 Release 資料的最終結果
/// </summary>
public sealed record ConsolidatedReleaseResult
{
    /// <summary>
    /// 依專案分組的整合結果
    /// </summary>
    public required List<ConsolidatedProjectGroup> Projects { get; init; }
}
