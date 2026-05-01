using ReleaseKit.Common.Configuration;

namespace ReleaseKit.Common.Git;

/// <summary>
/// 建構 Git Clone URL 的工具類別
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
    {
        var uri = new Uri(options.ApiUrl);
        var encodedToken = Uri.EscapeDataString(options.AccessToken);
        var basePath = uri.AbsolutePath.TrimEnd('/');

        if (basePath.EndsWith("/api/v4", StringComparison.OrdinalIgnoreCase))
        {
            basePath = basePath[..^"/api/v4".Length];
        }

        basePath = basePath.TrimEnd('/');
        return $"{uri.Scheme}://oauth2:{encodedToken}@{uri.Authority}{basePath}/{projectPath}.git";
    }

    /// <summary>
    /// 建構 Bitbucket Clone URL（使用 Username:AccessToken 內嵌認證）
    /// </summary>
    /// <param name="options">Bitbucket 配置選項</param>
    /// <param name="projectPath">專案路徑</param>
    /// <returns>Bitbucket Clone URL</returns>
    public static string BuildBitbucketCloneUrl(BitbucketOptions options, string projectPath)
    {
        if (string.IsNullOrWhiteSpace(options.Username))
        {
            throw new InvalidOperationException("缺少必要的組態鍵: Bitbucket:Username");
        }

        var encodedUsername = Uri.EscapeDataString(options.Username);
        var encodedAccessToken = Uri.EscapeDataString(options.AccessToken);
        return $"https://{encodedUsername}:{encodedAccessToken}@bitbucket.org/{projectPath}.git";
    }
}
