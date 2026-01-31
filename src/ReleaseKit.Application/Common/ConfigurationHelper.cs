using ReleaseKit.Application.Configuration;

namespace ReleaseKit.Application.Common;

/// <summary>
/// 設定繼承與合併輔助類別
/// </summary>
/// <remarks>
/// 提供專案層級設定覆蓋根層級設定的邏輯。
/// 專案層級的非 null/空白 值會覆蓋根層級的值。
/// </remarks>
public static class ConfigurationHelper
{
    /// <summary>
    /// 合併 GitLab 專案設定選項
    /// </summary>
    /// <param name="rootFetchMode">根層級擷取模式設定</param>
    /// <param name="projectOptions">專案層級設定</param>
    /// <returns>合併後的專案設定，專案層級設定優先</returns>
    public static GitLabProjectOptions MergeGitLabProjectOptions(
        FetchModeOptions rootFetchMode,
        GitLabProjectOptions projectOptions)
    {
        return new GitLabProjectOptions
        {
            ProjectPath = projectOptions.ProjectPath,
            FetchMode = projectOptions.FetchMode ?? rootFetchMode.FetchMode,
            TargetBranch = string.IsNullOrWhiteSpace(projectOptions.TargetBranch)
                ? rootFetchMode.TargetBranch ?? string.Empty
                : projectOptions.TargetBranch,
            SourceBranch = string.IsNullOrWhiteSpace(projectOptions.SourceBranch)
                ? rootFetchMode.SourceBranch
                : projectOptions.SourceBranch,
            StartDateTime = projectOptions.StartDateTime ?? rootFetchMode.StartDateTime,
            EndDateTime = projectOptions.EndDateTime ?? rootFetchMode.EndDateTime
        };
    }

    /// <summary>
    /// 合併 Bitbucket 專案設定選項
    /// </summary>
    /// <param name="rootFetchMode">根層級擷取模式設定</param>
    /// <param name="projectOptions">專案層級設定</param>
    /// <returns>合併後的專案設定，專案層級設定優先</returns>
    public static BitbucketProjectOptions MergeBitbucketProjectOptions(
        FetchModeOptions rootFetchMode,
        BitbucketProjectOptions projectOptions)
    {
        return new BitbucketProjectOptions
        {
            ProjectPath = projectOptions.ProjectPath,
            FetchMode = projectOptions.FetchMode ?? rootFetchMode.FetchMode,
            TargetBranch = string.IsNullOrWhiteSpace(projectOptions.TargetBranch)
                ? rootFetchMode.TargetBranch ?? string.Empty
                : projectOptions.TargetBranch,
            SourceBranch = string.IsNullOrWhiteSpace(projectOptions.SourceBranch)
                ? rootFetchMode.SourceBranch
                : projectOptions.SourceBranch,
            StartDateTime = projectOptions.StartDateTime ?? rootFetchMode.StartDateTime,
            EndDateTime = projectOptions.EndDateTime ?? rootFetchMode.EndDateTime
        };
    }
}
