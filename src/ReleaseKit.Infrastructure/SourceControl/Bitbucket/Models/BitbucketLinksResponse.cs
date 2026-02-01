using System.Text.Json.Serialization;

namespace ReleaseKit.Infrastructure.SourceControl.Bitbucket.Models;

/// <summary>
/// Bitbucket 連結集合 API 回應模型
/// </summary>
public sealed record BitbucketLinksResponse
{
    /// <summary>
    /// HTML 連結
    /// </summary>
    [JsonPropertyName("html")]
    public BitbucketLinkResponse Html { get; init; } = new();
}
