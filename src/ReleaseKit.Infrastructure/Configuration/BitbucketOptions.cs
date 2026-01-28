namespace ReleaseKit.Infrastructure.Configuration;

/// <summary>
/// Bitbucket 配置選項
/// </summary>
public class BitbucketOptions
{
    /// <summary>
    /// Bitbucket API 基礎 URL
    /// </summary>
    public string ApiUrl { get; init; } = string.Empty;

    /// <summary>
    /// Bitbucket 帳戶 Email
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Bitbucket App Password
    /// </summary>
    public string AccessToken { get; init; } = string.Empty;

    /// <summary>
    /// 專案清單
    /// </summary>
    public List<BitbucketProjectOptions> Projects { get; init; } = new();
}
