namespace ReleaseKit.Console.Options;

/// <summary>
/// GitLab 設定
/// </summary>
public class GitLabOptions
{
    /// <summary>
    /// GitLab API URL（例如：https://gitlab.com/api/v4）
    /// </summary>
    public string ApiUrl { get; set; } = string.Empty;

    /// <summary>
    /// 存取權杖
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// 專案清單
    /// </summary>
    public List<GitLabProjectOptions> Projects { get; set; } = new();
}
