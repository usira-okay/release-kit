namespace ReleaseKit.Infrastructure.SourceControl.GitLab;

/// <summary>
/// GitLab 組態設定
/// </summary>
public class GitLabSettings
{
    /// <summary>
    /// GitLab 伺服器網域（例如：https://gitlab.com）
    /// </summary>
    public required string Domain { get; init; }
    
    /// <summary>
    /// GitLab 存取權杖（Personal Access Token）
    /// </summary>
    public required string AccessToken { get; init; }
    
    /// <summary>
    /// 預設專案 ID（可選）
    /// </summary>
    public string? DefaultProjectId { get; init; }
}
