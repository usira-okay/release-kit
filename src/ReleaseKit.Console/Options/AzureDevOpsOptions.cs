namespace ReleaseKit.Console.Options;

/// <summary>
/// Azure DevOps 設定選項
/// </summary>
public class AzureDevOpsOptions
{
    /// <summary>
    /// Organization URL (例如: https://dev.azure.com/myorganization)
    /// </summary>
    public string OrganizationUrl { get; set; } = string.Empty;

    /// <summary>
    /// 團隊對應清單
    /// </summary>
    public List<TeamMappingOptions> TeamMapping { get; set; } = new();
}
