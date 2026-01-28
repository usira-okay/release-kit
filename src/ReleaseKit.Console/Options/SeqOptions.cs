namespace ReleaseKit.Console.Options;

/// <summary>
/// Seq 日誌伺服器設定選項
/// </summary>
public class SeqOptions
{
    /// <summary>
    /// Seq 伺服器 URL
    /// </summary>
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>
    /// Seq API 金鑰
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
}
