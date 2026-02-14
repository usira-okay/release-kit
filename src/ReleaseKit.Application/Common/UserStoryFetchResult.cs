namespace ReleaseKit.Application.Common;

/// <summary>
/// User Story 解析結果彙整 DTO
/// </summary>
public sealed record UserStoryFetchResult
{
    /// <summary>
    /// 所有 User Story 解析結果清單
    /// </summary>
    public required List<UserStoryOutput> UserStories { get; init; }

    /// <summary>
    /// 處理的 Work Item 總數
    /// </summary>
    public required int TotalWorkItemsProcessed { get; init; }

    /// <summary>
    /// 已是 User Story 以上類型的數量
    /// </summary>
    public required int AlreadyUserStoryCount { get; init; }

    /// <summary>
    /// 成功解析至 User Story 的數量
    /// </summary>
    public required int ResolvedCount { get; init; }

    /// <summary>
    /// 保留原始資料的數量（無法解析或原始失敗）
    /// </summary>
    public required int KeptOriginalCount { get; init; }
}
