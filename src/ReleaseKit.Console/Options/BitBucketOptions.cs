namespace ReleaseKit.Console.Options;

/// <summary>
/// BitBucket 配置選項
/// </summary>
public class BitBucketOptions
{
    /// <summary>
    /// 組態區段名稱
    /// </summary>
    public const string SectionName = "BitBucket";

    /// <summary>
    /// BitBucket API URL
    /// </summary>
    public string ApiUrl { get; set; } = string.Empty;

    /// <summary>
    /// 電子郵件
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// 存取權杖
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// 專案清單
    /// </summary>
    public List<ProjectConfig> Projects { get; set; } = new();
}
