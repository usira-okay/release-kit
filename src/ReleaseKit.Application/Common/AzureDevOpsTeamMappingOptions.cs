namespace ReleaseKit.Application.Common;

/// <summary>
/// consolidate-release-data 任務使用的 Azure DevOps Team Mapping 設定
/// </summary>
public sealed class AzureDevOpsTeamMappingOptions
{
    /// <summary>
    /// 團隊名稱對映清單
    /// </summary>
    public List<TeamMappingOption> TeamMapping { get; init; } = new();
}

/// <summary>
/// 團隊名稱對映設定
/// </summary>
public sealed class TeamMappingOption
{
    /// <summary>
    /// 原始團隊名稱
    /// </summary>
    public string OriginalTeamName { get; init; } = string.Empty;

    /// <summary>
    /// 顯示名稱
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;
}
