using ReleaseKit.Domain.Common;
using ReleaseKit.Domain.Entities;

namespace ReleaseKit.Domain.Abstractions;

/// <summary>
/// Git 操作服務介面
/// </summary>
public interface IGitOperationService
{
    /// <summary>
    /// Clone 或 Pull 遠端倉庫至本地路徑
    /// </summary>
    /// <param name="repoUrl">遠端倉庫 URL（含認證資訊）</param>
    /// <param name="localPath">本地路徑</param>
    /// <param name="cancellationToken">取消標記</param>
    /// <returns>成功時回傳本地路徑；失敗時回傳錯誤</returns>
    Task<Result<string>> CloneOrPullAsync(string repoUrl, string localPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得指定 Commit 的異動統計摘要（檔案清單 + 行數統計，不含完整 diff）
    /// </summary>
    Task<Result<CommitSummary>> GetCommitStatAsync(string repoPath, string commitSha, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得指定 Commit 的完整 diff 內容（unified diff 格式）
    /// </summary>
    Task<Result<string>> GetCommitRawDiffAsync(string repoPath, string commitSha, CancellationToken cancellationToken = default);
}
