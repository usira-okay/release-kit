namespace ReleaseKit.Common.Configuration;

/// <summary>
/// Azure DevOps 團隊名稱對應設定選項
/// </summary>
public class AzureDevOpsTeamMappingOptions
{
    /// <summary>
    /// 團隊名稱對應清單
    /// </summary>
    public List<TeamMapping> TeamMapping { get; init; } = new();
}
