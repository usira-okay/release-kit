namespace ReleaseKit.Application.Common;

/// <summary>
/// User Story 解析彙總結果
/// </summary>
/// <remarks>
/// 彙總所有 Work Item 的 User Story 解析結果，包含統計資訊。
/// </remarks>
public sealed record UserStoryResolutionResult
{
    /// <summary>
    /// 所有 Work Item 的解析結果清單
    /// </summary>
    public required List<UserStoryResolutionOutput> Items { get; init; }

    /// <summary>
    /// 總 Work Item 數量
    /// </summary>
    public required int TotalCount { get; init; }

    /// <summary>
    /// 原始即為 User Story 以上層級的數量
    /// </summary>
    public required int AlreadyUserStoryCount { get; init; }

    /// <summary>
    /// 透過遞迴找到 User Story 的數量
    /// </summary>
    public required int FoundViaRecursionCount { get; init; }

    /// <summary>
    /// 無法找到 User Story 的數量
    /// </summary>
    public required int NotFoundCount { get; init; }

    /// <summary>
    /// 原始取得失敗的數量
    /// </summary>
    public required int OriginalFetchFailedCount { get; init; }
}
