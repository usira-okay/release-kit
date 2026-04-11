namespace ReleaseKit.Infrastructure.SourceControl.Bitbucket.Models;

/// <summary>
/// Bitbucket Merge Commit API 回應模型
/// </summary>
public sealed record BitbucketMergeCommitResponse
{
    /// <summary>
    /// Merge Commit Hash
    /// </summary>
    public string Hash { get; init; } = string.Empty;
}
