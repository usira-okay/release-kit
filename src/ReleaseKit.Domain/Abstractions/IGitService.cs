using ReleaseKit.Domain.Common;

namespace ReleaseKit.Domain.Abstractions;

/// <summary>
/// Git 操作服務介面
/// </summary>
public interface IGitService
{
    /// <summary>完整 Clone 指定 repository（含 fetch --all）</summary>
    Task<Result<string>> CloneRepositoryAsync(
        string repoUrl,
        string targetPath,
        CancellationToken cancellationToken = default);

    /// <summary>取得指定兩個 branch 之間的 diff（git diff baseBranch...headBranch）</summary>
    Task<Result<string>> GetBranchDiffAsync(
        string repoPath,
        string baseBranch,
        string headBranch,
        CancellationToken cancellationToken = default);

    /// <summary>透過 merge commit 訊息搜尋 merge commit SHA（分支刪除時的 fallback）</summary>
    Task<Result<string>> FindMergeCommitAsync(
        string repoPath,
        string branchName,
        CancellationToken cancellationToken = default);

    /// <summary>取得指定 commit 的 diff</summary>
    Task<Result<string>> GetCommitDiffAsync(
        string repoPath,
        string commitSha,
        CancellationToken cancellationToken = default);

    /// <summary>取得 repository 的遠端 URL</summary>
    Task<Result<string>> GetRemoteUrlAsync(
        string repoPath,
        CancellationToken cancellationToken = default);
}
