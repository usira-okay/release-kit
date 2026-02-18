using System.Text.Json.Serialization;

namespace ReleaseKit.Infrastructure.AzureDevOps.Models;

/// <summary>
/// Azure DevOps Work Item 連結集合回應模型
/// </summary>
public sealed record AzureDevOpsLinksResponse
{
    /// <summary>
    /// Work Item 網頁連結
    /// </summary>
    [JsonPropertyName("html")]
    public AzureDevOpsLinkResponse? Html { get; init; }
}
