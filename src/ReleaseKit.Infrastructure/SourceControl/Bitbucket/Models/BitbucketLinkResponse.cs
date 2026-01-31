using System.Text.Json.Serialization;

namespace ReleaseKit.Infrastructure.SourceControl.Bitbucket.Models;

/// <summary>
/// Bitbucket 連結 API 回應模型
/// </summary>
public sealed record BitbucketLinkResponse
{
    /// <summary>
    /// 連結網址
    /// </summary>
    [JsonPropertyName("href")]
    public string Href { get; init; } = string.Empty;
}
