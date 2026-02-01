namespace ReleaseKit.Common.Configuration;

/// <summary>
/// GitLab 配置選項
/// </summary>
public class GitLabOptions
{
    /// <summary>
    /// GitLab API 基礎 URL
    /// </summary>
    public string ApiUrl { get; init; } = string.Empty;

    /// <summary>
    /// GitLab Personal Access Token
    /// </summary>
    public string AccessToken { get; init; } = string.Empty;

    /// <summary>
    /// 專案清單
    /// </summary>
    public List<GitLabProjectOptions> Projects { get; init; } = new();
}
