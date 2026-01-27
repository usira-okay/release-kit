using System.Text.Json.Serialization;

namespace ReleaseKit.Infrastructure.SourceControl.GitLab;

/// <summary>
/// GitLab API 回應的 Commit DTO
/// </summary>
internal class GitLabCommitDto
{
    /// <summary>
    /// Commit SHA
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }
    
    /// <summary>
    /// Commit 標題
    /// </summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }
    
    /// <summary>
    /// Commit 建立時間
    /// </summary>
    [JsonPropertyName("created_at")]
    public required DateTime CreatedAt { get; init; }
}
