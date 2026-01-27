namespace ReleaseKit.Console.Options;

/// <summary>
/// GitLab 配置選項
/// </summary>
public class GitLabOptions
{
    /// <summary>
    /// 組態區段名稱
    /// </summary>
    public const string SectionName = "GitLab";

    /// <summary>
    /// GitLab API URL
    /// </summary>
    public string ApiUrl { get; set; } = string.Empty;

    /// <summary>
    /// 存取權杖
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// 專案清單
    /// </summary>
    public List<ProjectConfig> Projects { get; set; } = new();
}
