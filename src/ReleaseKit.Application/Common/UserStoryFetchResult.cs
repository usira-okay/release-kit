namespace ReleaseKit.Application.Common;

/// <summary>
/// User Story 抓取結果彙整 DTO
/// </summary>
/// <remarks>
/// 彙總所有 Work Item 解析至 User Story 層級的結果與統計數據。
/// </remarks>
public sealed record UserStoryFetchResult
{
    /// <summary>
    /// User Story 解析結果清單
    /// </summary>
    public required List<UserStoryOutput> UserStories { get; init; }

    /// <summary>
    /// 處理的 Work Item 總數
    /// </summary>
    /// <remarks>
    /// 等於輸入的 WorkItemOutput 數量（包含成功與失敗）。
    /// </remarks>
    public required int TotalWorkItemsProcessed { get; init; }

    /// <summary>
    /// 已是高層級類型的數量
    /// </summary>
    /// <remarks>
    /// 輸入時已經是 User Story/Feature/Epic，無需向上查詢的數量。
    /// </remarks>
    public required int AlreadyUserStoryCount { get; init; }

    /// <summary>
    /// 成功向上解析的數量
    /// </summary>
    /// <remarks>
    /// 從 Task/Bug 成功遞迴查詢到 User Story/Feature/Epic 的數量。
    /// </remarks>
    public required int ResolvedCount { get; init; }

    /// <summary>
    /// 保留原始資料的數量
    /// </summary>
    /// <remarks>
    /// 無法向上解析或原始抓取失敗，保留原始 Work Item 資料的數量。
    /// </remarks>
    public required int KeptOriginalCount { get; init; }
}
