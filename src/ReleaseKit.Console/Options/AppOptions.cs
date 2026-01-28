namespace ReleaseKit.Console.Options;

/// <summary>
/// 應用程式設定選項
/// </summary>
public class AppOptions
{
    /// <summary>
    /// 擷取模式 (DateTimeRange 或 BranchDiff)
    /// </summary>
    public string? FetchMode { get; set; }

    /// <summary>
    /// 來源分支 (例如: release/yyyyMMdd)
    /// </summary>
    public string? SourceBranch { get; set; }

    /// <summary>
    /// 開始日期時間
    /// </summary>
    public DateTimeOffset? StartDateTime { get; set; }

    /// <summary>
    /// 結束日期時間
    /// </summary>
    public DateTimeOffset? EndDateTime { get; set; }
}
