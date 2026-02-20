namespace ReleaseKit.Common.Configuration;

/// <summary>
/// 團隊名稱映射
/// </summary>
public class TeamMapping
{
    /// <summary>
    /// 原始團隊名稱（英文）
    /// </summary>
    public string OriginalTeamName { get; set; } = string.Empty;

    /// <summary>
    /// 顯示名稱（中文或其他語言）
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
}
