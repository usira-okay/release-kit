namespace ReleaseKit.Domain.Abstractions;

/// <summary>
/// Redis 快取服務介面
/// </summary>
public interface IRedisService
{
    /// <summary>
    /// 設定快取值
    /// </summary>
    /// <param name="key">快取鍵值</param>
    /// <param name="value">快取內容</param>
    /// <param name="expiry">過期時間</param>
    /// <param name="cancellationToken">取消權杖</param>
    Task<bool> SetAsync(string key, string value, TimeSpan? expiry = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得快取值
    /// </summary>
    /// <param name="key">快取鍵值</param>
    /// <param name="cancellationToken">取消權杖</param>
    /// <returns>快取內容，若不存在則回傳 null</returns>
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 刪除快取值
    /// </summary>
    /// <param name="key">快取鍵值</param>
    /// <param name="cancellationToken">取消權杖</param>
    Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// 檢查快取鍵值是否存在
    /// </summary>
    /// <param name="key">快取鍵值</param>
    /// <param name="cancellationToken">取消權杖</param>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
}
