namespace ReleaseKit.Console.Options;

/// <summary>
/// 團隊名稱對應設定
/// </summary>
public class TeamMappingOptions
{
    /// <summary>
    /// 原始團隊名稱 (例如 "MoneyLogistic")
    /// </summary>
    public string OriginalTeamName { get; set; } = string.Empty;
    
    /// <summary>
    /// 顯示名稱 (例如 "金流團隊")
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>
    /// 驗證配置是否正確
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OriginalTeamName))
            throw new InvalidOperationException("AzureDevOps:TeamMapping:OriginalTeamName 組態設定不得為空");
            
        if (string.IsNullOrWhiteSpace(DisplayName))
            throw new InvalidOperationException("AzureDevOps:TeamMapping:DisplayName 組態設定不得為空");
    }
}
