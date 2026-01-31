namespace ReleaseKit.Common.Configuration;

/// <summary>
/// 拉取模式列舉
/// </summary>
public enum FetchMode
{
    /// <summary>
    /// 時間區間模式：根據指定的開始與結束時間拉取
    /// </summary>
    DateTimeRange = 0,

    /// <summary>
    /// 分支差異模式：比對兩個分支之間的差異
    /// </summary>
    BranchDiff = 1
}
