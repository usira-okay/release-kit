namespace ReleaseKit.Console.Options;

/// <summary>
/// Redis 設定選項
/// </summary>
public class RedisOptions
{
    /// <summary>
    /// Redis 連線字串
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Redis 實例名稱前綴
    /// </summary>
    public string InstanceName { get; set; } = string.Empty;
}
