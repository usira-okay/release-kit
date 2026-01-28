namespace ReleaseKit.Console.Options;

/// <summary>
/// 使用者對應
/// </summary>
public class UserMapping
{
    /// <summary>
    /// GitLab 使用者 ID
    /// </summary>
    public string GitLabUserId { get; set; } = string.Empty;

    /// <summary>
    /// Bitbucket 使用者 ID
    /// </summary>
    public string BitbucketUserId { get; set; } = string.Empty;

    /// <summary>
    /// 顯示名稱
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
}
