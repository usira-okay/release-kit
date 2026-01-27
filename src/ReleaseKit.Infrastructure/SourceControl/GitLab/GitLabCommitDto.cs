namespace ReleaseKit.Infrastructure.SourceControl.GitLab;

/// <summary>
/// GitLab API 回應的 Commit DTO
/// </summary>
internal class GitLabCommitDto
{
    /// <summary>
    /// Commit SHA
    /// </summary>
    public required string Id { get; init; }
    
    /// <summary>
    /// Commit 標題
    /// </summary>
    public required string Title { get; init; }
    
    /// <summary>
    /// Commit 建立時間
    /// </summary>
    public required DateTime Created_At { get; init; }
}
