namespace ReleaseKit.Application.Common;

/// <summary>
/// 整合結果中的 PR 資訊
/// </summary>
public sealed record ConsolidatedPrInfo
{
    /// <summary>
    /// PR 網址
    /// </summary>
    public required string Url { get; init; }
}
