namespace ReleaseKit.Console.Options;

/// <summary>
/// 拉取模式
/// </summary>
public enum FetchMode
{
    /// <summary>
    /// 根據時間區間拉取 PR/MR
    /// </summary>
    DateTimeRange,
    
    /// <summary>
    /// 根據分支差異拉取 PR/MR
    /// </summary>
    BranchDiff
}
