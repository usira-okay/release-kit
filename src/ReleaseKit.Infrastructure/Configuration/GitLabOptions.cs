using System.ComponentModel.DataAnnotations;

namespace ReleaseKit.Infrastructure.Configuration;

/// <summary>
/// GitLab 配置選項
/// </summary>
public class GitLabOptions
{
    /// <summary>
    /// GitLab API 基礎 URL
    /// </summary>
    [Required(ErrorMessage = "GitLab:ApiUrl 不可為空")]
    [Url(ErrorMessage = "GitLab:ApiUrl 必須是有效的 URL")]
    public string ApiUrl { get; init; } = string.Empty;

    /// <summary>
    /// GitLab Personal Access Token
    /// </summary>
    [Required(ErrorMessage = "GitLab:AccessToken 不可為空")]
    public string AccessToken { get; init; } = string.Empty;

    /// <summary>
    /// 專案清單
    /// </summary>
    [Required(ErrorMessage = "GitLab:Projects 不可為空")]
    [MinLength(1, ErrorMessage = "GitLab:Projects 至少需要一個項目")]
    public List<GitLabProjectOptions> Projects { get; init; } = new();
}
