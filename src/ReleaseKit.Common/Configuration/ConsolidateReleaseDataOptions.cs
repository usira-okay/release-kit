namespace ReleaseKit.Common.Configuration;

/// <summary>
/// 整合 Release 資料任務的設定選項
/// </summary>
public class ConsolidateReleaseDataOptions
{
    /// <summary>
    /// 團隊名稱對映清單
    /// </summary>
    public List<TeamMappingEntry> TeamMapping { get; init; } = new();
}
