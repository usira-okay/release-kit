using ReleaseKit.Domain.Common;

namespace ReleaseKit.Application.Common.RiskAnalysis;

/// <summary>
/// Clone Repository 的抽象介面
/// </summary>
public interface IRepositoryCloner
{
    /// <summary>
    /// Clone 或更新指定的 Repository
    /// </summary>
    /// <param name="cloneUrl">Repository 的 clone URL</param>
    /// <param name="targetPath">目標目錄路徑</param>
    /// <returns>Clone 結果，成功時回傳本地路徑</returns>
    Task<Result<string>> CloneAsync(string cloneUrl, string targetPath);

    /// <summary>
    /// 切換至指定分支
    /// </summary>
    /// <param name="localPath">本地 Repository 路徑</param>
    /// <param name="branch">目標分支名稱</param>
    /// <returns>切換結果</returns>
    Task<Result<string>> CheckoutAsync(string localPath, string branch);

    /// <summary>
    /// 清理已 clone 的 Repository 目錄
    /// </summary>
    /// <param name="localPath">本地路徑</param>
    Task CleanupAsync(string localPath);
}
