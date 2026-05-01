namespace ReleaseKit.Infrastructure.SourceControl.Bitbucket.Models;

/// <summary>
/// Bitbucket 合併 Commit 資訊
/// </summary>
public sealed record BitbucketMergeCommitResponse
{
    /// <summary>
    /// Commit Hash
    /// </summary>
    public string Hash { get; init; } = string.Empty;
}
