namespace ReleaseKit.Application.Common;

/// <summary>
/// Work Item 查詢結果彙整 DTO
/// </summary>
/// <remarks>
/// 包含所有 Work Item 查詢結果與統計資訊，用於序列化為 JSON 輸出至 Redis。
/// </remarks>
public sealed record WorkItemFetchResult
{
    /// <summary>
    /// 所有 Work Item 查詢結果清單
    /// </summary>
    public required List<WorkItemOutput> WorkItems { get; init; }

    /// <summary>
    /// 分析的 PR 總數
    /// </summary>
    /// <remarks>
    /// 包含 GitLab 與 Bitbucket 的 PR 總數量。
    /// </remarks>
    public required int TotalPRsAnalyzed { get; init; }

    /// <summary>
    /// 解析出的 Work Item ID 總數
    /// </summary>
    /// <remarks>
    /// 從所有 PR 解析出的 VSTS Work Item ID 總數，包含同一 ID 被多個 PR 參照的情況。
    /// 此數量等同於最終 WorkItems 清單的長度。
    /// </remarks>
    public required int TotalWorkItemsFound { get; init; }

    /// <summary>
    /// 成功取得資訊的 Work Item 數量
    /// </summary>
    public required int SuccessCount { get; init; }

    /// <summary>
    /// 取得失敗的 Work Item 數量
    /// </summary>
    public required int FailureCount { get; init; }
}
