namespace ReleaseKit.Console.Options;

/// <summary>
/// Bitbucket 設定選項
/// </summary>
public class BitbucketOptions
{
    /// <summary>
    /// Bitbucket API URL
    /// </summary>
    public string ApiUrl { get; set; } = string.Empty;

    /// <summary>
    /// Bitbucket 電子郵件
    /// </summary>
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

/// <summary>
/// Bitbucket 專案設定
/// </summary>
public class BitbucketProjectOptions
{
    /// <summary>
    /// 專案路徑 (例如: mygroup/backend-api)
    /// </summary>
    public string ProjectPath { get; set; } = string.Empty;

    /// <summary>
    /// 目標分支
    /// </summary>
    public string TargetBranch { get; set; } = string.Empty;
}
