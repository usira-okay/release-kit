using System.ComponentModel.DataAnnotations;

namespace ReleaseKit.Console.Options;

/// <summary>
/// GitLab 設定選項
/// </summary>
public class GitLabOptions
{
    /// <summary>
    /// GitLab API URL
    /// </summary>
    [Url(ErrorMessage = "GitLab:ApiUrl 必須是有效的 URL")]
    public string ApiUrl { get; set; } = string.Empty;

    /// <summary>
    /// GitLab 存取權杖
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// GitLab 專案設定清單
    /// </summary>
    public List<GitLabProjectOptions> Projects { get; set; } = new();
}
