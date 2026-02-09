namespace ReleaseKit.Common.Constants;

/// <summary>
/// Redis 鍵值常數
/// </summary>
public static class RedisKeys
{
    /// <summary>
    /// GitLab Pull Request 資料的 Redis Key
    /// </summary>
    public const string GitLabPullRequests = "GitLab:PullRequests";

    /// <summary>
    /// Bitbucket Pull Request 資料的 Redis Key
    /// </summary>
    public const string BitbucketPullRequests = "Bitbucket:PullRequests";
}
