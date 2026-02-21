using ReleaseKit.Common.Configuration;

namespace ReleaseKit.Application.Common;

/// <summary>
/// 整合 Release 資料任務的配置選項
/// </summary>
public class ConsolidateReleaseDataOptions
{
    /// <summary>
    /// 團隊名稱映射清單
    /// </summary>
    public List<TeamMappingOptions> TeamMapping { get; init; } = new();
}
