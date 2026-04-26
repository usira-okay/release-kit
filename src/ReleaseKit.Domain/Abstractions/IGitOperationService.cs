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
    /// 取得指定 commit 的異動檔案與 diff 內容
    /// </summary>
    /// <param name="repoPath">本地 repo 路徑</param>
    /// <param name="commitSha">Commit SHA</param>
    /// <param name="cancellationToken">取消標記</param>
    /// <returns>成功時回傳 FileDiff 清單；失敗時回傳錯誤</returns>
    Task<Result<IReadOnlyList<FileDiff>>> GetCommitDiffAsync(string repoPath, string commitSha, CancellationToken cancellationToken = default);
}
