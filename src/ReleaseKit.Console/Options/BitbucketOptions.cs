namespace ReleaseKit.Console.Options;

/// <summary>
/// Bitbucket 設定
/// </summary>
public class BitbucketOptions
{
    private List<BitbucketProjectOptions> _projects = new();

    /// <summary>
    /// Bitbucket API URL（例如：https://api.bitbucket.org/2.0）
    /// </summary>
    public string ApiUrl { get; set; } = string.Empty;

    /// <summary>
    /// 電子郵件
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// 存取權杖
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// 專案清單
    /// </summary>
    public List<BitbucketProjectOptions> Projects
    {
        get => _projects;
        set => _projects = value ?? new();
    }
}
