namespace ReleaseKit.Application.Common;

/// <summary>
/// Work Item 輸出 DTO
/// </summary>
/// <remarks>
/// 表示單一 Work Item 的查詢結果，包含成功/失敗狀態。
/// 成功時所有欄位填入；失敗時僅 WorkItemId 與 ErrorMessage 有值。
/// </remarks>
public sealed record WorkItemOutput
{
    /// <summary>
    /// Work Item 識別碼
    /// </summary>
    public required int WorkItemId { get; init; }

    /// <summary>
    /// 標題（失敗時為 null）
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// 類型（Bug/Task/User Story 等，失敗時為 null）
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// 狀態（New/Active/Resolved/Closed 等，失敗時為 null）
    /// </summary>
    public string? State { get; init; }

    /// <summary>
    /// Work Item 網頁連結（失敗時為 null）
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// 原始區域路徑（Team 名稱，失敗時為 null）
    /// </summary>
    public string? OriginalTeamName { get; init; }

    /// <summary>
    /// 是否成功取得 Work Item 資訊
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// 失敗時的錯誤原因（成功時為 null）
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 來源 PR 識別碼
    /// </summary>
    /// <remarks>
    /// 表示此 Work Item 是從哪個 PR 被識別的（PR/MR 的 ID）。
    /// 同一個 Work Item 若出現在多筆 PR 中，會產生多筆 WorkItemOutput 記錄。
    /// </remarks>
    public int? SourcePullRequestId { get; init; }

    /// <summary>
    /// 來源 PR 所屬專案名稱
    /// </summary>
    /// <remarks>
    /// 格式：group/project（GitLab）或 workspace/repo（Bitbucket）。
    /// 用於識別此 Work Item 來自哪個專案的 PR。
    /// </remarks>
    public string? SourceProjectName { get; init; }

    /// <summary>
    /// 來源 PR 網址
    /// </summary>
    /// <remarks>
    /// 提供可直接存取原始 PR/MR 的完整 URL。
    /// </remarks>
    public string? SourcePRUrl { get; init; }
}
