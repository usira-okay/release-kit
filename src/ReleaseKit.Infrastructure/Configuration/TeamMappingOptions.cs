// 此類別已遷移至 ReleaseKit.Common.Configuration.TeamMappingOptions
// 保留此檔案做為命名空間相容，避免外部參照中斷
using CommonTeamMappingOptions = ReleaseKit.Common.Configuration.TeamMappingOptions;

namespace ReleaseKit.Infrastructure.Configuration;

/// <summary>
/// Azure DevOps 團隊映射配置選項（已遷移至 Common 層）
/// </summary>
[System.Obsolete("請改用 ReleaseKit.Common.Configuration.TeamMappingOptions")]
public class TeamMappingOptions : CommonTeamMappingOptions;
