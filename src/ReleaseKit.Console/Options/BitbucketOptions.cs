using System.ComponentModel.DataAnnotations;

namespace ReleaseKit.Console.Options;

/// <summary>
/// Bitbucket 設定選項
/// </summary>
public class BitbucketOptions
{
    /// <summary>
    /// Bitbucket API URL
    /// </summary>
    [Url(ErrorMessage = "Bitbucket:ApiUrl 必須是有效的 URL")]
    public string ApiUrl { get; set; } = string.Empty;

    /// <summary>
    /// Bitbucket 電子郵件
    /// </summary>
    [EmailAddress(ErrorMessage = "Bitbucket:Email 必須是有效的電子郵件地址")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Bitbucket 存取權杖
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Bitbucket 專案設定清單
    /// </summary>
    public List<BitbucketProjectOptions> Projects { get; set; } = new();
}
