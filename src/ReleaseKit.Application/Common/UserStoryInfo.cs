namespace ReleaseKit.Application.Common;

/// <summary>
/// User Story 資訊
/// </summary>
/// <remarks>
/// 代表透過解析找到的 User Story（或更高層級如 Feature、Epic）的基本資訊。
/// </remarks>
public sealed record UserStoryInfo
{
    /// <summary>
    /// User Story 的 Work Item ID
    /// </summary>
    public required int WorkItemId { get; init; }

    /// <summary>
    /// User Story 標題
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Work Item 類型（User Story / Feature / Epic）
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Work Item 狀態
    /// </summary>
    public required string State { get; init; }

    /// <summary>
    /// Work Item 網址
    /// </summary>
    public required string Url { get; init; }
}
