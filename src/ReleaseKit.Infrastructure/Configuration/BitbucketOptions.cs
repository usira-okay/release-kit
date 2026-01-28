using System.ComponentModel.DataAnnotations;

namespace ReleaseKit.Infrastructure.Configuration;

/// <summary>
/// Bitbucket 配置選項
/// </summary>
public class BitbucketOptions
{
    /// <summary>
    /// Bitbucket API 基礎 URL
    /// </summary>
    [Required(ErrorMessage = "Bitbucket:ApiUrl 不可為空")]
    [Url(ErrorMessage = "Bitbucket:ApiUrl 必須是有效的 URL")]
    public string ApiUrl { get; init; } = string.Empty;

    /// <summary>
    /// Bitbucket 帳戶 Email
    /// </summary>
    [Required(ErrorMessage = "Bitbucket:Email 不可為空")]
    [EmailAddress(ErrorMessage = "Bitbucket:Email 必須是有效的 Email 地址")]
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Bitbucket App Password
    /// </summary>
    [Required(ErrorMessage = "Bitbucket:AccessToken 不可為空")]
    public string AccessToken { get; init; } = string.Empty;

    /// <summary>
    /// 專案清單
    /// </summary>
    [Required(ErrorMessage = "Bitbucket:Projects 不可為空")]
    [MinLength(1, ErrorMessage = "Bitbucket:Projects 至少需要一個項目")]
    public List<BitbucketProjectOptions> Projects { get; init; } = new();
}
