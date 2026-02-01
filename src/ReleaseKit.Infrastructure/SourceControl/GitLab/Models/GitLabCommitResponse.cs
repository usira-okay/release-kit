using System.Text.Json.Serialization;

namespace ReleaseKit.Infrastructure.SourceControl.GitLab.Models;

/// <summary>
/// GitLab Commit API 回應模型
/// </summary>
public sealed record GitLabCommitResponse
{
    /// <summary>
    /// Commit ID（完整 SHA）
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Commit ID（短版 SHA）
    /// </summary>
    [JsonPropertyName("short_id")]
    public string ShortId { get; init; } = string.Empty;

    /// <summary>
    /// Commit 標題
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 作者名稱
    /// </summary>
    [JsonPropertyName("author_name")]
    public string AuthorName { get; init; } = string.Empty;

    /// <summary>
    /// 建立時間
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; }
}
