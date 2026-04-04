using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Common;
using ReleaseKit.Infrastructure.RiskAnalysis.DiffProviders.Models;

namespace ReleaseKit.Infrastructure.SourceControl.Bitbucket;

/// <summary>
/// Bitbucket Repository 的擴充介面，提供 Diff 相關操作
/// </summary>
public interface IBitbucketRepository : ISourceControlRepository
{
    /// <summary>
    /// 取得指定 PR 的檔案層級變更統計
    /// </summary>
    /// <param name="projectPath">專案路徑（workspace/repo_slug 格式）</param>
    /// <param name="prId">PR 識別碼</param>
    /// <returns>成功時回傳 diffstat 回應；失敗時回傳包含錯誤資訊的 Result</returns>
    Task<Result<BitbucketRiskDiffStatResponse>> GetPullRequestDiffStatAsync(string projectPath, string prId);

    /// <summary>
    /// 取得指定 PR 的 raw unified diff 文字
    /// </summary>
    /// <param name="projectPath">專案路徑（workspace/repo_slug 格式）</param>
    /// <param name="prId">PR 識別碼</param>
    /// <returns>成功時回傳 unified diff 文字；失敗時回傳包含錯誤資訊的 Result</returns>
    Task<Result<string>> GetPullRequestRawDiffAsync(string projectPath, string prId);
}
