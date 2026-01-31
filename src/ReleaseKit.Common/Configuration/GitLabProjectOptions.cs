namespace ReleaseKit.Common.Configuration;

/// <summary>
/// GitLab 專案配置選項
/// </summary>
public class GitLabProjectOptions
{
    /// <summary>
    /// 專案路徑（如 "group/project"）
    /// </summary>
    public string ProjectPath { get; init; } = string.Empty;

    /// <summary>
    /// 目標分支名稱
    /// </summary>
    public string TargetBranch { get; init; } = string.Empty;

    /// <summary>
    /// 拉取模式：DateTimeRange 或 BranchDiff（可選，若未提供則使用全域設定）
    /// </summary>
    public FetchMode? FetchMode { get; init; }

    /// <summary>
    /// 來源分支名稱（BranchDiff 模式時必填）
    /// </summary>
    public string? SourceBranch { get; init; }

    /// <summary>
    /// 開始時間（DateTimeRange 模式時必填）
    /// </summary>
    public DateTimeOffset? StartDateTime { get; init; }

    /// <summary>
    /// 結束時間（DateTimeRange 模式時必填）
    /// </summary>
    public DateTimeOffset? EndDateTime { get; init; }
}
