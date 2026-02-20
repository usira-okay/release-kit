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

    /// <summary>
    /// GitLab Release Branch 資料的 Redis Key
    /// </summary>
    public const string GitLabReleaseBranches = "GitLab:ReleaseBranches";

    /// <summary>
    /// Bitbucket Release Branch 資料的 Redis Key
    /// </summary>
    public const string BitbucketReleaseBranches = "Bitbucket:ReleaseBranches";

    /// <summary>
    /// 過濾後的 GitLab Pull Request 資料（依使用者）的 Redis Key
    /// </summary>
    public const string GitLabPullRequestsByUser = "GitLab:PullRequests:ByUser";

    /// <summary>
    /// 過濾後的 Bitbucket Pull Request 資料（依使用者）的 Redis Key
    /// </summary>
    public const string BitbucketPullRequestsByUser = "Bitbucket:PullRequests:ByUser";

    /// <summary>
    /// Azure DevOps Work Items 資料的 Redis Key
    /// </summary>
    public const string AzureDevOpsWorkItems = "AzureDevOps:WorkItems";

    /// <summary>
    /// Azure DevOps User Story 層級 Work Items 資料的 Redis Key
    /// </summary>
    public const string AzureDevOpsUserStoryWorkItems = "AzureDevOps:WorkItems:UserStories";
}
