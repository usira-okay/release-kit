namespace ReleaseKit.Console.Options;

/// <summary>
/// 專案配置
/// </summary>
public class ProjectConfig
{
    /// <summary>
    /// 專案路徑，格式為 "group/project"
    /// </summary>
    public string ProjectPath { get; set; } = string.Empty;

    /// <summary>
    /// 目標分支
    /// </summary>
    public string TargetBranch { get; set; } = string.Empty;
}
