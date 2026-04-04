using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Common;
using ReleaseKit.Infrastructure.RiskAnalysis.DiffProviders;

namespace ReleaseKit.Infrastructure.SourceControl.GitLab;

/// <summary>
/// GitLab Repository 的擴充介面，提供 Diff 相關操作
/// </summary>
public interface IGitLabRepository : ISourceControlRepository
{
    /// <summary>
    /// 取得指定 MR 的所有檔案變更清單
    /// </summary>
    /// <param name="projectPath">專案路徑（如：mygroup/backend-api）</param>
    /// <param name="prId">MR 識別碼</param>
    /// <returns>成功時回傳 MR 變更清單；失敗時回傳包含錯誤資訊的 Result</returns>
    Task<Result<GitLabMrChangesResponse>> GetMergeRequestChangesAsync(string projectPath, string prId);
}
