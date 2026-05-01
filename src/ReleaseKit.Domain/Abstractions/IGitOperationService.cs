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

    /// <summary>
    /// 使用 git grep 搜尋程式碼庫中符合模式的內容
    /// </summary>
    /// <param name="repoPath">本地 repo 路徑</param>
    /// <param name="pattern">搜尋模式（正規表示式）</param>
    /// <param name="fileGlob">檔案 glob 篩選（如 "*.cs"），null 表示搜尋所有檔案</param>
    /// <param name="cancellationToken">取消標記</param>
    /// <returns>搜尋結果（每行格式：檔案:行號:內容）</returns>
    Task<Result<string>> SearchPatternAsync(
        string repoPath,
        string pattern,
        string? fileGlob = null,
        CancellationToken cancellationToken = default);
}
