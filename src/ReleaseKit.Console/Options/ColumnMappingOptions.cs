namespace ReleaseKit.Console.Options;

/// <summary>
/// Google Sheet 欄位對應設定
/// </summary>
public class ColumnMappingOptions
{
    /// <summary>
    /// Repository 名稱欄位
    /// </summary>
    public string RepositoryNameColumn { get; set; } = string.Empty;
    
    /// <summary>
    /// 功能說明欄位
    /// </summary>
    public string FeatureColumn { get; set; } = string.Empty;
    
    /// <summary>
    /// 團隊名稱欄位
    /// </summary>
    public string TeamColumn { get; set; } = string.Empty;
    
    /// <summary>
    /// 作者欄位
    /// </summary>
    public string AuthorsColumn { get; set; } = string.Empty;
    
    /// <summary>
    /// PR/MR URL 欄位
    /// </summary>
    public string PullRequestUrlsColumn { get; set; } = string.Empty;
    
    /// <summary>
    /// 唯一識別碼欄位
    /// </summary>
    public string UniqueKeyColumn { get; set; } = string.Empty;
    
    /// <summary>
    /// 自動同步標記欄位
    /// </summary>
    public string AutoSyncColumn { get; set; } = string.Empty;
    
    /// <summary>
    /// 驗證配置是否正確
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(RepositoryNameColumn))
            throw new InvalidOperationException("GoogleSheet:ColumnMapping:RepositoryNameColumn 組態設定不得為空");
            
        if (string.IsNullOrWhiteSpace(FeatureColumn))
            throw new InvalidOperationException("GoogleSheet:ColumnMapping:FeatureColumn 組態設定不得為空");
            
        if (string.IsNullOrWhiteSpace(TeamColumn))
            throw new InvalidOperationException("GoogleSheet:ColumnMapping:TeamColumn 組態設定不得為空");
            
        if (string.IsNullOrWhiteSpace(AuthorsColumn))
            throw new InvalidOperationException("GoogleSheet:ColumnMapping:AuthorsColumn 組態設定不得為空");
            
        if (string.IsNullOrWhiteSpace(PullRequestUrlsColumn))
            throw new InvalidOperationException("GoogleSheet:ColumnMapping:PullRequestUrlsColumn 組態設定不得為空");
            
        if (string.IsNullOrWhiteSpace(UniqueKeyColumn))
            throw new InvalidOperationException("GoogleSheet:ColumnMapping:UniqueKeyColumn 組態設定不得為空");
            
        if (string.IsNullOrWhiteSpace(AutoSyncColumn))
            throw new InvalidOperationException("GoogleSheet:ColumnMapping:AutoSyncColumn 組態設定不得為空");
    }
}
