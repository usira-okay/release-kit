namespace ReleaseKit.Application.Common;

/// <summary>
/// 增強標題後的單筆 Release 記錄
/// </summary>
/// <remarks>
/// 包含 AI 增強後的標題與原始整合資料。
/// </remarks>
public sealed record EnhancedTitleEntry
{
    /// <summary>
    /// AI 增強後的標題
    /// </summary>
    public required string EnhancedTitle { get; init; }

    /// <summary>
    /// 原始整合記錄
    /// </summary>
    public required ConsolidatedReleaseEntry OriginalEntry { get; init; }
}
