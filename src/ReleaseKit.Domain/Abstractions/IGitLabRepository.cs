using ReleaseKit.Domain.Entities;

namespace ReleaseKit.Domain.Abstractions;

/// <summary>
/// GitLab Repository 抽象介面，負責與 GitLab API 互動
/// </summary>
public interface IGitLabRepository
{
    /// <summary>
    /// 根據時間區間拉取 Merge Request 資訊
    /// </summary>
    /// <param name="projectId">GitLab 專案 ID</param>
    /// <param name="startTime">開始時間（UTC）</param>
    /// <param name="endTime">結束時間（UTC）</param>
    /// <param name="state">MR 狀態篩選（可選，如 "merged", "opened", "closed"）</param>
    /// <returns>Merge Request 列表</returns>
    Task<IReadOnlyList<MergeRequest>> FetchMergeRequestsByTimeRangeAsync(
        string projectId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        string? state = null);
    
    /// <summary>
    /// 比較兩個分支之間的 commit 差異，並取得相關的 Merge Request
    /// </summary>
    /// <param name="projectId">GitLab 專案 ID</param>
    /// <param name="sourceBranch">來源分支</param>
    /// <param name="targetBranch">目標分支</param>
    /// <returns>相關的 Merge Request 列表</returns>
    Task<IReadOnlyList<MergeRequest>> FetchMergeRequestsByBranchComparisonAsync(
        string projectId,
        string sourceBranch,
        string targetBranch);
}
