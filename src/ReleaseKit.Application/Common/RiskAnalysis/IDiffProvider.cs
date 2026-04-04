using ReleaseKit.Domain.Common;

namespace ReleaseKit.Application.Common.RiskAnalysis;

/// <summary>
/// 取得 PR diff 資料的抽象介面
/// </summary>
public interface IDiffProvider
{
    /// <summary>
    /// 取得指定 PR 的 diff 資料
    /// </summary>
    /// <param name="projectPath">專案路徑（如 group/project-name）</param>
    /// <param name="prId">PR/MR 識別碼</param>
    /// <returns>PR diff 結果，包含變更檔案清單</returns>
    Task<Result<PullRequestDiff>> GetDiffAsync(string projectPath, string prId);
}
