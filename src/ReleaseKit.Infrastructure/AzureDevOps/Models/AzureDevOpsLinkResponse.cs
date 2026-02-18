using System.Text.Json.Serialization;

namespace ReleaseKit.Infrastructure.AzureDevOps.Models;

/// <summary>
/// Azure DevOps 單一連結回應模型
/// </summary>
public sealed record AzureDevOpsLinkResponse
{
    /// <summary>
    /// 連結 URL
    /// </summary>
    [JsonPropertyName("href")]
    public string Href { get; init; } = string.Empty;
}
