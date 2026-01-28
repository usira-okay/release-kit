namespace ReleaseKit.Infrastructure.Configuration;

/// <summary>
/// Google Sheets 欄位映射配置選項
/// </summary>
public class ColumnMappingOptions
{
    /// <summary>
    /// Repository 名稱欄位（如 "Z"）
    /// </summary>
    public string RepositoryNameColumn { get; init; } = string.Empty;

    /// <summary>
    /// Feature 欄位（如 "B"）
    /// </summary>
    public string FeatureColumn { get; init; } = string.Empty;

    /// <summary>
    /// 團隊欄位（如 "D"）
    /// </summary>
    public string TeamColumn { get; init; } = string.Empty;

    /// <summary>
    /// 作者欄位（如 "W"）
    /// </summary>
    public string AuthorsColumn { get; init; } = string.Empty;

    /// <summary>
    /// PR URL 欄位（如 "X"）
    /// </summary>
    public string PullRequestUrlsColumn { get; init; } = string.Empty;

    /// <summary>
    /// 唯一鍵欄位（如 "Y"）
    /// </summary>
    public string UniqueKeyColumn { get; init; } = string.Empty;

    /// <summary>
    /// 自動同步欄位（如 "F"）
    /// </summary>
    public string AutoSyncColumn { get; init; } = string.Empty;
}
