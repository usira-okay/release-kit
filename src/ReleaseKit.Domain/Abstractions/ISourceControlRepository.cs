using ReleaseKit.Domain.Common;
using ReleaseKit.Domain.Entities;

namespace ReleaseKit.Domain.Abstractions;

/// <summary>
/// 原始碼控制平台的 Repository 介面
/// </summary>
public interface ISourceControlRepository
{
    /// <summary>
    /// 依時間區間取得已合併的 Merge Requests
    /// </summary>
    /// <param name="projectPath">專案路徑（如：mygroup/backend-api）</param>
    /// <param name="targetBranch">目標分支名稱</param>
    /// <param name="startDateTime">開始時間 (UTC)</param>
    /// <param name="endDateTime">結束時間 (UTC)</param>
    /// <param name="cancellationToken">取消標記</param>
    /// <returns>Merge Request 清單</returns>
    Task<Result<IReadOnlyList<MergeRequest>>> GetMergeRequestsByDateRangeAsync(
        string projectPath,
        string targetBranch,
        DateTimeOffset startDateTime,
        DateTimeOffset endDateTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 依分支差異取得關聯的 Merge Requests
    /// </summary>
    /// <param name="projectPath">專案路徑（如：mygroup/backend-api）</param>
    /// <param name="sourceBranch">來源分支名稱</param>
    /// <param name="targetBranch">目標分支名稱</param>
    /// <param name="cancellationToken">取消標記</param>
    /// <returns>Merge Request 清單</returns>
    Task<Result<IReadOnlyList<MergeRequest>>> GetMergeRequestsByBranchDiffAsync(
        string projectPath,
        string sourceBranch,
        string targetBranch,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得專案的分支清單
    /// </summary>
    /// <param name="projectPath">專案路徑（如：mygroup/backend-api）</param>
    /// <param name="pattern">分支名稱篩選模式（如：release/*）</param>
    /// <param name="cancellationToken">取消標記</param>
    /// <returns>分支名稱清單</returns>
    Task<Result<IReadOnlyList<string>>> GetBranchesAsync(
        string projectPath,
        string? pattern = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 依 Commit SHA 取得關聯的 Merge Request
    /// </summary>
    /// <param name="projectPath">專案路徑（如：mygroup/backend-api）</param>
    /// <param name="commitSha">Commit SHA</param>
    /// <param name="cancellationToken">取消標記</param>
    /// <returns>Merge Request 清單</returns>
    Task<Result<IReadOnlyList<MergeRequest>>> GetMergeRequestsByCommitAsync(
        string projectPath,
        string commitSha,
        CancellationToken cancellationToken = default);
}
