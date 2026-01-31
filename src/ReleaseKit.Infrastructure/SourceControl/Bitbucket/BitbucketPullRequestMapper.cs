using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Infrastructure.SourceControl.Bitbucket.Models;

namespace ReleaseKit.Infrastructure.SourceControl.Bitbucket;

/// <summary>
/// Bitbucket Pull Request 資料映射器
/// </summary>
public static class BitbucketPullRequestMapper
{
    /// <summary>
    /// 將 Bitbucket API 回應映射到領域實體
    /// </summary>
    /// <param name="response">Bitbucket API 回應</param>
    /// <param name="projectPath">專案路徑</param>
    /// <returns>MergeRequest 領域實體</returns>
    public static MergeRequest ToDomain(BitbucketPullRequestResponse response, string projectPath)
    {
        return new MergeRequest
        {
            Title = response.Title,
            Description = response.Summary?.Raw,
            SourceBranch = response.Source.Branch.Name,
            TargetBranch = response.Destination.Branch.Name,
            CreatedAt = response.CreatedOn,
            MergedAt = response.ClosedOn ?? DateTimeOffset.MinValue,
            State = response.State,
            AuthorUserId = response.Author.Uuid,
            AuthorName = response.Author.DisplayName,
            PRUrl = response.Links.Html.Href,
            Platform = SourceControlPlatform.Bitbucket,
            ProjectPath = projectPath
        };
    }
}
