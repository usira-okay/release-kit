namespace ReleaseKit.Domain.ValueObjects;

/// <summary>
/// GitLab 拉取模式列舉
/// </summary>
public enum GitLabFetchMode
{
    /// <summary>
    /// 時間區間查詢
    /// </summary>
    DateTimeRange,
    
    /// <summary>
    /// 分支差異比較
    /// </summary>
    BranchDiff
}
