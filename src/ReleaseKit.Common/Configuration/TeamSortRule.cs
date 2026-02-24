namespace ReleaseKit.Common.Configuration;

/// <summary>
/// 團隊排序規則
/// </summary>
public class TeamSortRule
{
    /// <summary>
    /// 團隊顯示名稱（與 Google Sheet 中的 Team 欄位值對應）
    /// </summary>
    public string TeamDisplayName { get; init; } = string.Empty;

    /// <summary>
    /// 排序順序（數字越小排越前面）
    /// </summary>
    public int Sort { get; init; }
}
