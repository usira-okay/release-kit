namespace ReleaseKit.Console.Options;

/// <summary>
/// Azure DevOps 設定選項
/// </summary>
public class AzureDevOpsOptions
{
    /// <summary>
    /// Azure DevOps 組織 URL (例如 "https://dev.azure.com/myorganization")
    /// </summary>
    public string OrganizationUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// 團隊名稱對應清單
    /// </summary>
    public List<TeamMappingOptions> TeamMapping { get; set; } = new();
    
    /// <summary>
    /// 驗證配置是否正確
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OrganizationUrl))
            throw new InvalidOperationException("AzureDevOps:OrganizationUrl 組態設定不得為空");
        
        if (!Uri.TryCreate(OrganizationUrl, UriKind.Absolute, out _))
            throw new InvalidOperationException("AzureDevOps:OrganizationUrl 必須為有效的 URL 格式");
        
        foreach (var mapping in TeamMapping)
        {
            mapping.Validate();
        }
    }
}
