namespace ReleaseKit.Application.Common;

/// <summary>
/// User Story 解析結果 DTO
/// </summary>
/// <remarks>
/// 表示單一 Work Item 解析至 User Story/Feature/Epic 層級後的結果。
/// </remarks>
public sealed record UserStoryOutput
{
    /// <summary>
    /// 解析後的 Work Item ID（User Story/Feature/Epic）
    /// </summary>
    /// <remarks>
    /// 若成功向上解析，此為高層級 Work Item 的 ID；
    /// 若保留原始資料，此為原始 Work Item 的 ID。
    /// </remarks>
    public required int WorkItemId { get; init; }

    /// <summary>
    /// 原始 Work Item ID
    /// </summary>
    /// <remarks>
    /// 記錄最初從 PR 解析出的 Work Item ID（可能是 Task/Bug）。
    /// </remarks>
    public required int OriginalWorkItemId { get; init; }

    /// <summary>
    /// 標題
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// 類型（User Story/Feature/Epic/Task/Bug 等）
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// 狀態（New/Active/Resolved/Closed 等）
    /// </summary>
    public string? State { get; init; }

    /// <summary>
    /// Work Item 網頁連結
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// 區域路徑（Team 名稱）
    /// </summary>
    public string? OriginalTeamName { get; init; }

    /// <summary>
    /// 是否成功解析
    /// </summary>
    /// <remarks>
    /// false 表示原始 Work Item 抓取失敗或遞迴查詢過程中發生錯誤。
    /// </remarks>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// 錯誤訊息（成功時為 null）
    /// </summary>
    public string? ErrorMessage { get; init; }
}
