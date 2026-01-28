using System.ComponentModel.DataAnnotations;

namespace ReleaseKit.Infrastructure.Configuration;

/// <summary>
/// Google Sheets 欄位映射配置選項
/// </summary>
public class ColumnMappingOptions
{
    /// <summary>
    /// Repository 名稱欄位（如 "Z"）
    /// </summary>
    [Required(ErrorMessage = "RepositoryNameColumn 不可為空")]
    [RegularExpression("^[A-Z]+$", ErrorMessage = "欄位名稱必須是大寫英文字母")]
    public string RepositoryNameColumn { get; init; } = string.Empty;

    /// <summary>
    /// Feature 欄位（如 "B"）
    /// </summary>
    [Required(ErrorMessage = "FeatureColumn 不可為空")]
    [RegularExpression("^[A-Z]+$", ErrorMessage = "欄位名稱必須是大寫英文字母")]
    public string FeatureColumn { get; init; } = string.Empty;

    /// <summary>
    /// 團隊欄位（如 "D"）
    /// </summary>
    [Required(ErrorMessage = "TeamColumn 不可為空")]
    [RegularExpression("^[A-Z]+$", ErrorMessage = "欄位名稱必須是大寫英文字母")]
    public string TeamColumn { get; init; } = string.Empty;

    /// <summary>
    /// 作者欄位（如 "W"）
    /// </summary>
    [Required(ErrorMessage = "AuthorsColumn 不可為空")]
    [RegularExpression("^[A-Z]+$", ErrorMessage = "欄位名稱必須是大寫英文字母")]
    public string AuthorsColumn { get; init; } = string.Empty;

    /// <summary>
    /// PR URL 欄位（如 "X"）
    /// </summary>
    [Required(ErrorMessage = "PullRequestUrlsColumn 不可為空")]
    [RegularExpression("^[A-Z]+$", ErrorMessage = "欄位名稱必須是大寫英文字母")]
    public string PullRequestUrlsColumn { get; init; } = string.Empty;

    /// <summary>
    /// 唯一鍵欄位（如 "Y"）
    /// </summary>
    [Required(ErrorMessage = "UniqueKeyColumn 不可為空")]
    [RegularExpression("^[A-Z]+$", ErrorMessage = "欄位名稱必須是大寫英文字母")]
    public string UniqueKeyColumn { get; init; } = string.Empty;

    /// <summary>
    /// 自動同步欄位（如 "F"）
    /// </summary>
    [Required(ErrorMessage = "AutoSyncColumn 不可為空")]
    [RegularExpression("^[A-Z]+$", ErrorMessage = "欄位名稱必須是大寫英文字母")]
    public string AutoSyncColumn { get; init; } = string.Empty;
}
