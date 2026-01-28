namespace ReleaseKit.Console.Options;

/// <summary>
/// 團隊對應設定
/// </summary>
public class TeamMappingOptions
{
    /// <summary>
    /// 原始團隊名稱
    /// </summary>
    public string OriginalTeamName { get; set; } = string.Empty;

    /// <summary>
    /// 顯示名稱
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
}
