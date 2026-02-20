namespace ReleaseKit.Application.Common;

/// <summary>
/// 整合記錄 DTO
/// </summary>
public sealed record ConsolidatedReleaseEntry
{
    /// <summary>
    /// PR 標題（取第一筆配對 PR 的標題）
    /// </summary>
    public required string PrTitle { get; init; }

    /// <summary>
    /// Work Item ID
    /// </summary>
    public required int WorkItemId { get; init; }

    /// <summary>
    /// 團隊顯示名稱（經 TeamMapping 轉換後）
    /// </summary>
    public required string TeamDisplayName { get; init; }

    /// <summary>
    /// 作者資訊清單
    /// </summary>
    public required List<ConsolidatedAuthorInfo> Authors { get; init; }

    /// <summary>
    /// PR 資訊清單
    /// </summary>
    public required List<ConsolidatedPrInfo> PullRequests { get; init; }

    /// <summary>
    /// 原始資料
    /// </summary>
    public required ConsolidatedOriginalData OriginalData { get; init; }
}
