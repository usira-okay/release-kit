using System.Text.Json.Serialization;

namespace ReleaseKit.Infrastructure.SourceControl.GitLab;

/// <summary>
/// GitLab API 回應的分支比較 DTO
/// </summary>
internal class GitLabCompareResultDto
{
    /// <summary>
    /// Commit 列表
    /// </summary>
    [JsonPropertyName("commits")]
    public required List<GitLabCommitDto> Commits { get; init; }
}
