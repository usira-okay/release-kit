using System.ComponentModel.DataAnnotations;

namespace ReleaseKit.Infrastructure.Configuration;

/// <summary>
/// Azure DevOps 配置選項
/// </summary>
public class AzureDevOpsOptions
{
    /// <summary>
    /// Azure DevOps 組織 URL
    /// </summary>
    [Required(ErrorMessage = "AzureDevOps:OrganizationUrl 不可為空")]
    [Url(ErrorMessage = "AzureDevOps:OrganizationUrl 必須是有效的 URL")]
    public string OrganizationUrl { get; init; } = string.Empty;

    /// <summary>
    /// 團隊名稱映射清單
    /// </summary>
    [Required(ErrorMessage = "AzureDevOps:TeamMapping 不可為空")]
    [MinLength(1, ErrorMessage = "AzureDevOps:TeamMapping 至少需要一個項目")]
    public List<TeamMappingOptions> TeamMapping { get; init; } = new();
}
