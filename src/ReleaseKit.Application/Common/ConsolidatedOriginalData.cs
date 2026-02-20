namespace ReleaseKit.Application.Common;

/// <summary>
/// 原始資料 DTO
/// </summary>
public sealed record ConsolidatedOriginalData
{
    /// <summary>
    /// 原始 Work Item 資料
    /// </summary>
    public required UserStoryWorkItemOutput WorkItem { get; init; }

    /// <summary>
    /// 原始 PR 資料清單
    /// </summary>
    public required List<MergeRequestOutput> PullRequests { get; init; }
}
