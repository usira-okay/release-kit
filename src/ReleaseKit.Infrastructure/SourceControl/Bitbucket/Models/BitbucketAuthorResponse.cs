using System.Text.Json.Serialization;

namespace ReleaseKit.Infrastructure.SourceControl.Bitbucket.Models;

/// <summary>
/// Bitbucket 作者 API 回應模型
/// </summary>
public sealed record BitbucketAuthorResponse
{
    /// <summary>
    /// 作者 UUID
    /// </summary>
    [JsonPropertyName("uuid")]
    public string Uuid { get; init; } = string.Empty;

    /// <summary>
    /// 作者顯示名稱
    /// </summary>
    [JsonPropertyName("display_name")]
    public string DisplayName { get; init; } = string.Empty;
}
