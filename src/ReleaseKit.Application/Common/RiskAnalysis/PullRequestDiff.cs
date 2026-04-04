namespace ReleaseKit.Application.Common.RiskAnalysis;

/// <summary>
/// 統一的 PR diff 資料結構
/// </summary>
public sealed record PullRequestDiff
{
    /// <summary>PR 基本資訊</summary>
    public required MergeRequestOutput PullRequest { get; init; }

    /// <summary>所屬 Repository 名稱</summary>
    public required string RepositoryName { get; init; }

    /// <summary>所屬平台 (GitLab/Bitbucket)</summary>
    public required string Platform { get; init; }

    /// <summary>變更的檔案清單</summary>
    public required IReadOnlyList<FileDiff> Files { get; init; }
}
