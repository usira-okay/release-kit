using System.Text.Json.Serialization;

namespace ReleaseKit.Infrastructure.AzureDevOps.Models;

/// <summary>
/// Azure DevOps Work Item Relation API 回應模型
/// </summary>
public sealed record AzureDevOpsRelationResponse
{
    /// <summary>
    /// 關係類型（如 "System.LinkTypes.Hierarchy-Reverse" 表示 Parent）
    /// </summary>
    [JsonPropertyName("rel")]
    public string? Rel { get; init; }

    /// <summary>
    /// 關聯的 Work Item URL
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; init; }

    /// <summary>
    /// 關係屬性（選填）
    /// </summary>
    [JsonPropertyName("attributes")]
    public Dictionary<string, object?>? Attributes { get; init; }
}
