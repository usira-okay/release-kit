namespace ReleaseKit.Infrastructure.Configuration;

/// <summary>
/// Azure DevOps 團隊映射配置選項
/// </summary>
public class TeamMappingOptions
{
    /// <summary>
    /// 原始團隊名稱（英文）
    /// </summary>
    public string OriginalTeamName { get; init; } = string.Empty;

    /// <summary>
    /// 顯示名稱（中文或其他語言）
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;
}
