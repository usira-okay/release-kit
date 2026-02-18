namespace ReleaseKit.Application.Common;

/// <summary>
/// Work Item User Story 解析結果彙整 DTO
/// </summary>
/// <remarks>
/// 包含所有解析結果與統計資訊，用於序列化為 JSON 輸出至 Redis。
/// </remarks>
public sealed record UserStoryFetchResult
{
    /// <summary>
    /// 所有解析結果清單
    /// </summary>
    public required List<UserStoryWorkItemOutput> WorkItems { get; init; }

    /// <summary>
    /// 原始 Work Item 總數
    /// </summary>
    public required int TotalWorkItems { get; init; }

    /// <summary>
    /// 原本就是 User Story 層級的數量
    /// </summary>
    public required int AlreadyUserStoryCount { get; init; }

    /// <summary>
    /// 透過遞迴找到的數量
    /// </summary>
    public required int FoundViaRecursionCount { get; init; }

    /// <summary>
    /// 無法找到 User Story 的數量
    /// </summary>
    public required int NotFoundCount { get; init; }

    /// <summary>
    /// 原始 Work Item 無法取得的數量
    /// </summary>
    public required int OriginalFetchFailedCount { get; init; }
}
