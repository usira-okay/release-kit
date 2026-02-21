namespace ReleaseKit.Application.Common;

/// <summary>
/// 整合結果中的單筆 Release 記錄
/// </summary>
public sealed record ConsolidatedReleaseEntry
{
    /// <summary>
    /// User Story 標題
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Work Item URL
    /// </summary>
    public required string WorkItemUrl { get; init; }

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
