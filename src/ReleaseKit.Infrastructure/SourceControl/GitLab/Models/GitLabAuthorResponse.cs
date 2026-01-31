using System.Text.Json.Serialization;

namespace ReleaseKit.Infrastructure.SourceControl.GitLab.Models;

/// <summary>
/// GitLab 作者 API 回應模型
/// </summary>
public sealed record GitLabAuthorResponse
{
    /// <summary>
    /// 作者 ID
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; init; }

    /// <summary>
    /// 作者使用者名稱
    /// </summary>
    [JsonPropertyName("username")]
    public string Username { get; init; } = string.Empty;
}
