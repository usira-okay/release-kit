namespace ReleaseKit.Application.Common;

/// <summary>
/// User Story 解析結果輸出
/// </summary>
/// <remarks>
/// 代表單一 Work Item 經過 User Story 解析後的完整結果，
/// 包含原始 Work Item 的所有資訊以及解析出的 User Story 資訊。
/// </remarks>
public sealed record UserStoryResolutionOutput
{
    /// <summary>
    /// 原始 Work Item ID
    /// </summary>
    public required int WorkItemId { get; init; }

    /// <summary>
    /// 原始 Work Item 標題（取得失敗時為 null）
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// 原始 Work Item 類型（取得失敗時為 null）
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// 原始 Work Item 狀態（取得失敗時為 null）
    /// </summary>
    public string? State { get; init; }

    /// <summary>
    /// 原始 Work Item 網址（取得失敗時為 null）
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// 原始 Work Item 團隊名稱（取得失敗時為 null）
    /// </summary>
    public string? OriginalTeamName { get; init; }

    /// <summary>
    /// 原始 Work Item 是否取得成功
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// 原始 Work Item 取得失敗時的錯誤訊息
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// User Story 解析結果狀態
    /// </summary>
    public required UserStoryResolutionStatus ResolutionStatus { get; init; }

    /// <summary>
    /// 找到的 User Story 資訊（僅 AlreadyUserStoryOrAbove 與 FoundViaRecursion 時有值）
    /// </summary>
    public UserStoryInfo? UserStory { get; init; }
}
