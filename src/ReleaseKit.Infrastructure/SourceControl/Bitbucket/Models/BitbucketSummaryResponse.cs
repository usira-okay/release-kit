using System.Text.Json.Serialization;

namespace ReleaseKit.Infrastructure.SourceControl.Bitbucket.Models;

/// <summary>
/// Bitbucket Summary API 回應模型
/// </summary>
public sealed record BitbucketSummaryResponse
{
    /// <summary>
    /// 原始文字內容
    /// </summary>
    [JsonPropertyName("raw")]
    public string Raw { get; init; } = string.Empty;
}
