using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Infrastructure.SourceControl.GitLab.Models;

namespace ReleaseKit.Infrastructure.SourceControl.GitLab;

/// <summary>
/// GitLab Merge Request 資料映射器
/// </summary>
public static class GitLabMergeRequestMapper
{
    /// <summary>
    /// 將 GitLab API 回應映射到領域實體
    /// </summary>
    /// <param name="response">GitLab API 回應</param>
    /// <param name="projectPath">專案路徑</param>
    /// <returns>MergeRequest 領域實體</returns>
    public static MergeRequest ToDomain(GitLabMergeRequestResponse response, string projectPath)
    {
        return new MergeRequest
        {
            Title = response.Title,
            Description = response.Description,
            SourceBranch = response.SourceBranch,
            TargetBranch = response.TargetBranch,
            CreatedAt = response.CreatedAt,
            MergedAt = response.MergedAt ?? DateTimeOffset.MinValue,
            State = response.State,
            AuthorUserId = response.Author.Id.ToString(),
            AuthorName = response.Author.Username,
            PRUrl = response.WebUrl,
            Platform = SourceControlPlatform.GitLab,
            ProjectPath = projectPath
        };
    }
}
