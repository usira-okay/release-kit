namespace ReleaseKit.Console.Options;

/// <summary>
/// GitLab 設定選項
/// </summary>
public class GitLabOptions
{
    /// <summary>
    /// GitLab API URL
    /// </summary>
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

/// <summary>
/// GitLab 專案設定
/// </summary>
public class GitLabProjectOptions
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
