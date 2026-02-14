using System.Text.Json.Serialization;

namespace ReleaseKit.Infrastructure.AzureDevOps.Models;

/// <summary>
/// Azure DevOps Work Item 關聯回應模型
/// </summary>
public sealed record AzureDevOpsRelationResponse
{
    /// <summary>
    /// 關聯類型（如 System.LinkTypes.Hierarchy-Reverse 表示 parent）
    /// </summary>
    [JsonPropertyName("rel")]
    public string Rel { get; init; } = string.Empty;

    /// <summary>
    /// 關聯目標的 API URL
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;
}
