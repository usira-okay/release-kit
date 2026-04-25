using ReleaseKit.Common.Configuration;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 單一專案的 Release 設定輸出
/// </summary>
public record ProjectSettingOutput
{
    /// <summary>
    /// 專案路徑（如 "group/project" 或 "workspace/repo"）
    /// </summary>
    public string ProjectPath { get; init; } = string.Empty;

    /// <summary>
    /// 目標分支名稱
    /// </summary>
    public string TargetBranch { get; init; } = string.Empty;

    /// <summary>
    /// 拉取模式
    /// </summary>
    public FetchMode FetchMode { get; init; }

    /// <summary>
    /// 來源分支名稱（BranchDiff 模式時使用）
    /// </summary>
    public string? SourceBranch { get; init; }

    /// <summary>
    /// 開始時間（DateTimeRange 模式時使用）
    /// </summary>
    public DateTimeOffset? StartDateTime { get; init; }

    /// <summary>
    /// 結束時間（DateTimeRange 模式時使用）
    /// </summary>
    public DateTimeOffset? EndDateTime { get; init; }
}
