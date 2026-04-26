using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Git;

namespace ReleaseKit.Infrastructure.Git;

/// <summary>
/// 建構 Git Clone URL 的工具類別（委派至 ReleaseKit.Common.Git.CloneUrlBuilder）
/// </summary>
public static class CloneUrlBuilder
{
    /// <summary>
    /// 建構 GitLab Clone URL（移除 /api/v4 後，使用 oauth2:{PAT} 內嵌認證）
    /// </summary>
    /// <param name="options">GitLab 配置選項</param>
    /// <param name="projectPath">專案路徑</param>
    /// <returns>包含 PAT 認證的 GitLab Clone URL</returns>
    public static string BuildGitLabCloneUrl(GitLabOptions options, string projectPath)
        => Common.Git.CloneUrlBuilder.BuildGitLabCloneUrl(options, projectPath);

    /// <summary>
    /// 建構 Bitbucket Clone URL（使用 Username:AccessToken 內嵌認證）
    /// </summary>
    /// <param name="options">Bitbucket 配置選項</param>
    /// <param name="projectPath">專案路徑</param>
    /// <returns>Bitbucket Clone URL</returns>
    public static string BuildBitbucketCloneUrl(BitbucketOptions options, string projectPath)
        => Common.Git.CloneUrlBuilder.BuildBitbucketCloneUrl(options, projectPath);
}
