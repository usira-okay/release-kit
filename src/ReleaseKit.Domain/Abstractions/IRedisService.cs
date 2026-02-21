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
    Task<bool> SetAsync(string key, string value, TimeSpan? expiry = null);

    /// <summary>
    /// 取得快取值
    /// </summary>
    /// <param name="key">快取鍵值</param>
    /// <returns>快取內容，若不存在則回傳 null</returns>
    Task<string?> GetAsync(string key);

    /// <summary>
    /// 刪除快取值
    /// </summary>
    /// <param name="key">快取鍵值</param>
    Task<bool> DeleteAsync(string key);

    /// <summary>
    /// 檢查快取鍵值是否存在
    /// </summary>
    /// <param name="key">快取鍵值</param>
    Task<bool> ExistsAsync(string key);

    /// <summary>
    /// 設定 Hash 欄位值
    /// </summary>
    /// <param name="hashKey">Hash 鍵值</param>
    /// <param name="field">Hash 欄位名稱</param>
    /// <param name="value">欄位內容</param>
    Task<bool> HashSetAsync(string hashKey, string field, string value);

    /// <summary>
    /// 取得 Hash 欄位值
    /// </summary>
    /// <param name="hashKey">Hash 鍵值</param>
    /// <param name="field">Hash 欄位名稱</param>
    /// <returns>欄位內容，若不存在則回傳 null</returns>
    Task<string?> HashGetAsync(string hashKey, string field);

    /// <summary>
    /// 刪除 Hash 欄位
    /// </summary>
    /// <param name="hashKey">Hash 鍵值</param>
    /// <param name="field">Hash 欄位名稱</param>
    Task<bool> HashDeleteAsync(string hashKey, string field);

    /// <summary>
    /// 檢查 Hash 欄位是否存在
    /// </summary>
    /// <param name="hashKey">Hash 鍵值</param>
    /// <param name="field">Hash 欄位名稱</param>
    Task<bool> HashExistsAsync(string hashKey, string field);
}
