using ReleaseKit.Domain.Common;
using ReleaseKit.Domain.Entities;

namespace ReleaseKit.Domain.Abstractions;

/// <summary>
/// 原始碼控制平台的 Repository 介面
/// </summary>
/// <remarks>
/// 定義與原始碼控制平台（如 GitLab、Bitbucket）互動的標準介面。
/// 支援依時間區間、分支差異等多種模式擷取已合併的 Pull Request / Merge Request 資訊。
/// 所有方法皆使用 Result Pattern 回傳結果，避免例外處理。
/// </remarks>
public interface ISourceControlRepository
{
    /// <summary>
    /// 依時間區間取得已合併的 Merge Requests
    /// </summary>
    /// <param name="projectPath">專案路徑（如：mygroup/backend-api 或 workspace/repo_slug）</param>
    /// <param name="targetBranch">目標分支名稱（如：main、master、production）</param>
    /// <param name="startDateTime">開始時間 (UTC)，包含此時間點</param>
    /// <param name="endDateTime">結束時間 (UTC)，包含此時間點</param>
    /// <param name="cancellationToken">取消標記</param>
    /// <returns>成功時回傳已合併的 Merge Request 清單；失敗時回傳包含錯誤資訊的 Result</returns>
    /// <remarks>
    /// 僅回傳在指定時間區間內合併到目標分支的 PR/MR。
    /// 時間篩選以 merged_at (GitLab) 或 closed_on (Bitbucket) 為準。
    /// </remarks>
    Task<Result<IReadOnlyList<MergeRequest>>> GetMergeRequestsByDateRangeAsync(
        string projectPath,
        string targetBranch,
        DateTimeOffset startDateTime,
        DateTimeOffset endDateTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 依分支差異取得關聯的 Merge Requests
    /// </summary>
    /// <param name="projectPath">專案路徑（如：mygroup/backend-api 或 workspace/repo_slug）</param>
    /// <param name="sourceBranch">來源分支名稱（如：release/20240101）</param>
    /// <param name="targetBranch">目標分支名稱（如：main、develop）</param>
    /// <param name="cancellationToken">取消標記</param>
    /// <returns>成功時回傳與分支差異關聯的 Merge Request 清單；失敗時回傳包含錯誤資訊的 Result</returns>
    /// <remarks>
    /// 比較兩個分支的差異，取得所有相關的 commits，再查詢每個 commit 對應的 PR/MR。
    /// 回傳的 PR/MR 清單會自動去重複。
    /// </remarks>
    Task<Result<IReadOnlyList<MergeRequest>>> GetMergeRequestsByBranchDiffAsync(
        string projectPath,
        string sourceBranch,
        string targetBranch,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得專案的分支清單
    /// </summary>
    /// <param name="projectPath">專案路徑（如：mygroup/backend-api 或 workspace/repo_slug）</param>
    /// <param name="pattern">分支名稱篩選模式（如：release/*），null 表示取得所有分支</param>
    /// <param name="cancellationToken">取消標記</param>
    /// <returns>成功時回傳分支名稱清單；失敗時回傳包含錯誤資訊的 Result</returns>
    /// <remarks>
    /// 用於取得符合特定命名模式的分支，常用於 BranchDiff 模式中判斷分支順序。
    /// 篩選模式支援萬用字元（*）。
    /// </remarks>
    Task<Result<IReadOnlyList<string>>> GetBranchesAsync(
        string projectPath,
        string? pattern = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 依 Commit SHA 取得關聯的 Merge Request
    /// </summary>
    /// <param name="projectPath">專案路徑（如：mygroup/backend-api 或 workspace/repo_slug）</param>
    /// <param name="commitSha">Commit SHA（完整或縮短的 SHA 值）</param>
    /// <param name="cancellationToken">取消標記</param>
    /// <returns>成功時回傳包含此 commit 的 Merge Request 清單；失敗時回傳包含錯誤資訊的 Result</returns>
    /// <remarks>
    /// 查詢包含指定 commit 的所有 PR/MR。
    /// 一個 commit 可能對應多個 PR/MR（例如：cherry-pick 或 backport 場景）。
    /// </remarks>
    Task<Result<IReadOnlyList<MergeRequest>>> GetMergeRequestsByCommitAsync(
        string projectPath,
        string commitSha,
        CancellationToken cancellationToken = default);
}
