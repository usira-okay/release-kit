using System.Text.Json.Serialization;

namespace ReleaseKit.Infrastructure.AzureDevOps.Models;

/// <summary>
/// Azure DevOps Work Item 關聯回應模型
/// </summary>
/// <remarks>
/// 表示 Work Item 與其他 Work Item 之間的關聯（如父子階層關係）。
/// 根據憲法原則 IX，外部 API 契約模型允許使用 JsonPropertyName 屬性。
/// </remarks>
public sealed record AzureDevOpsRelationResponse
{
    /// <summary>
    /// 關聯類型
    /// </summary>
    /// <remarks>
    /// 例如："System.LinkTypes.Hierarchy-Reverse" 表示此 Work Item 的父層。
    /// </remarks>
    [JsonPropertyName("rel")]
    public string Rel { get; init; } = string.Empty;

    /// <summary>
    /// 關聯目標的 API URL
    /// </summary>
    /// <remarks>
    /// Azure DevOps API 回傳的 Work Item URL，格式如：
    /// https://dev.azure.com/{organization}/_apis/wit/workItems/{id}
    /// </remarks>
    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;
}
