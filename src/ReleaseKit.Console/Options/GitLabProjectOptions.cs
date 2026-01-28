namespace ReleaseKit.Console.Options;

/// <summary>
/// GitLab 專案設定
/// </summary>
public class GitLabProjectOptions
{
    /// <summary>
    /// 專案路徑 (例如: mygroup/backend-api)
    /// </summary>
    public string ProjectPath { get; set; } = string.Empty;

    /// <summary>
    /// 目標分支
    /// </summary>
    public string TargetBranch { get; set; } = string.Empty;
    
    /// <summary>
    /// 拉取模式 (可選，若未設定則使用全域設定)
    /// </summary>
    public FetchMode? FetchMode { get; set; }
    
    /// <summary>
    /// 來源分支 (可選，僅在 FetchMode 為 BranchDiff 時使用)
    /// </summary>
    public string? SourceBranch { get; set; }
    
    /// <summary>
    /// 開始時間 (可選，僅在 FetchMode 為 DateTimeRange 時使用)
    /// </summary>
    public DateTimeOffset? StartDateTime { get; set; }
    
    /// <summary>
    /// 結束時間 (可選，僅在 FetchMode 為 DateTimeRange 時使用)
    /// </summary>
    public DateTimeOffset? EndDateTime { get; set; }
    
    /// <summary>
    /// 驗證配置是否正確
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProjectPath))
            throw new InvalidOperationException("GitLab:Projects:ProjectPath 組態設定不得為空");
            
        if (string.IsNullOrWhiteSpace(TargetBranch))
            throw new InvalidOperationException("GitLab:Projects:TargetBranch 組態設定不得為空");
        
        // 若有設定 FetchMode，則驗證對應欄位
        if (FetchMode.HasValue)
        {
            if (FetchMode.Value == Options.FetchMode.BranchDiff && string.IsNullOrWhiteSpace(SourceBranch))
                throw new InvalidOperationException("當 FetchMode 為 BranchDiff 時，SourceBranch 不得為空");
            
            if (FetchMode.Value == Options.FetchMode.DateTimeRange)
            {
                if (!StartDateTime.HasValue)
                    throw new InvalidOperationException("當 FetchMode 為 DateTimeRange 時，StartDateTime 不得為空");
                    
                if (!EndDateTime.HasValue)
                    throw new InvalidOperationException("當 FetchMode 為 DateTimeRange 時，EndDateTime 不得為空");
                
                if (StartDateTime.Value >= EndDateTime.Value)
                    throw new InvalidOperationException("StartDateTime 必須早於 EndDateTime");
            }
        }
    }
}
