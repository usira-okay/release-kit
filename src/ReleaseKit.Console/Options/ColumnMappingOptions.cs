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
    /// 功能欄位
    /// </summary>
    public string FeatureColumn { get; set; } = string.Empty;

    /// <summary>
    /// 團隊欄位
    /// </summary>
    public string TeamColumn { get; set; } = string.Empty;

    /// <summary>
    /// 作者欄位
    /// </summary>
    public string AuthorsColumn { get; set; } = string.Empty;

    /// <summary>
    /// Pull Request URL 欄位
    /// </summary>
    public string PullRequestUrlsColumn { get; set; } = string.Empty;

    /// <summary>
    /// 唯一鍵欄位
    /// </summary>
    public string UniqueKeyColumn { get; set; } = string.Empty;

    /// <summary>
    /// 自動同步欄位
    /// </summary>
    public string AutoSyncColumn { get; set; } = string.Empty;
}
