namespace ReleaseKit.Application.Common;

/// <summary>
/// User Story 解析輸出 DTO
/// </summary>
/// <remarks>
/// 表示單一 Work Item 解析至 User Story 層級的結果。
/// 若原始 Work Item 為 Task/Bug，會解析至其 parent User Story。
/// </remarks>
public sealed record UserStoryOutput
{
    /// <summary>
    /// 解析後的 Work Item 識別碼（User Story/Feature/Epic 的 ID）
    /// </summary>
    public required int WorkItemId { get; init; }

    /// <summary>
    /// 原始 Work Item 識別碼
    /// </summary>
    /// <remarks>
    /// 若原始 Work Item 已是 User Story 以上，則與 WorkItemId 相同。
    /// </remarks>
    public required int OriginalWorkItemId { get; init; }

    /// <summary>
    /// 標題（失敗時為 null）
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// 類型（User Story/Feature/Epic 等，失敗時為 null）
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// 狀態（失敗時為 null）
    /// </summary>
    public string? State { get; init; }

    /// <summary>
    /// Work Item 網頁連結（失敗時為 null）
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// 原始區域路徑（失敗時為 null）
    /// </summary>
    public string? OriginalTeamName { get; init; }

    /// <summary>
    /// 是否成功解析
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// 失敗時的錯誤原因
    /// </summary>
    public string? ErrorMessage { get; init; }
}
