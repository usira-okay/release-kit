namespace ReleaseKit.Domain.Abstractions;

/// <summary>
/// 時間服務介面，提供取得當前時間的功能
/// </summary>
public interface INow
{
    /// <summary>
    /// 取得當前 UTC 時間
    /// </summary>
    /// <returns>當前 UTC 時間的 DateTimeOffset</returns>
    DateTimeOffset UtcNow { get; }
}
