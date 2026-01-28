namespace ReleaseKit.Infrastructure.Configuration;

/// <summary>
/// 拉取模式列舉
/// </summary>
public enum FetchMode
{
    /// <summary>
    /// 未指定（使用預設值或繼承上層設定）
    /// </summary>
    None = 0,

    /// <summary>
    /// 時間區間模式：根據指定的開始與結束時間拉取
    /// </summary>
    DateTimeRange = 1,

    /// <summary>
    /// 分支差異模式：比對兩個分支之間的差異
    /// </summary>
    BranchDiff = 2
}
