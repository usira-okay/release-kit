namespace ReleaseKit.Infrastructure.Configuration;

/// <summary>
/// Azure DevOps 配置選項
/// </summary>
public class AzureDevOpsOptions
{
    /// <summary>
    /// Azure DevOps 組織 URL
    /// </summary>
    public string OrganizationUrl { get; init; } = string.Empty;

    /// <summary>
    /// Personal Access Token
    /// </summary>
    public string PersonalAccessToken { get; init; } = string.Empty;

    /// <summary>
    /// 團隊名稱映射清單
    /// </summary>
    public List<TeamMappingOptions> TeamMapping { get; init; } = new();
}
