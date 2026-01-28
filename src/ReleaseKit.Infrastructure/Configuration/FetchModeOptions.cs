namespace ReleaseKit.Infrastructure.Configuration;

/// <summary>
/// 拉取模式配置選項
/// </summary>
public class FetchModeOptions
{
    /// <summary>
    /// 拉取模式：DateTimeRange（時間區間）或 BranchDiff（分支差異）
    /// </summary>
    public string FetchMode { get; init; } = string.Empty;

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
