namespace ReleaseKit.Domain.ValueObjects;

/// <summary>
/// GitLab 專案組態
/// </summary>
public class GitLabProjectSettings
{
    /// <summary>
    /// 專案 ID（格式：namespace/project）
    /// </summary>
    public required string ProjectId { get; init; }
    
    /// <summary>
    /// 目標分支
    /// </summary>
    public required string TargetBranch { get; init; }
}

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
    /// 專案列表
    /// </summary>
    public List<GitLabProjectSettings> Projects { get; init; } = new();
}
