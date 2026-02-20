namespace ReleaseKit.Common.Configuration;

/// <summary>
/// 團隊名稱映射配置選項
/// </summary>
public class TeamMappingOptions
{
    /// <summary>
    /// 團隊映射清單
    /// </summary>
    public List<TeamMapping> Mappings { get; set; } = new();
}
