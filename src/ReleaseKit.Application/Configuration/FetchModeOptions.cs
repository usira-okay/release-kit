namespace ReleaseKit.Application.Configuration;

/// <summary>
/// 拉取模式配置選項
/// </summary>
public class FetchModeOptions
{
    /// <summary>
    /// 拉取模式：DateTimeRange（時間區間）或 BranchDiff（分支差異）
    /// </summary>
    public FetchMode FetchMode { get; init; } = FetchMode.DateTimeRange;

    /// <summary>
    /// 目標分支名稱（全域預設值，可被專案層級設定覆蓋）
    /// </summary>
    public string? TargetBranch { get; init; }

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
