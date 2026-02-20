namespace ReleaseKit.Common.Configuration;

/// <summary>
/// Azure DevOps 通用設定選項（用於 Application 層存取 TeamMapping）
/// </summary>
public class AzureDevOpsOptions
{
    /// <summary>
    /// 團隊名稱對映清單
    /// </summary>
    public List<TeamMapping> TeamMapping { get; set; } = new();
}
