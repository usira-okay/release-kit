namespace ReleaseKit.Domain.Abstractions;

/// <summary>
/// 指令間資料傳遞服務介面
/// </summary>
public interface IDataTransferService
{
    /// <summary>
    /// 設定指定 Key 的值
    /// </summary>
    /// <param name="key">鍵值</param>
    /// <param name="value">內容</param>
    /// <param name="expiry">過期時間（選用；FileSystem 實作忽略此參數）</param>
    Task<bool> SetAsync(string key, string value, TimeSpan? expiry = null);

    /// <summary>
    /// 取得指定 Key 的值
    /// </summary>
    /// <param name="key">鍵值</param>
    /// <returns>內容，若不存在則回傳 null</returns>
    Task<string?> GetAsync(string key);

    /// <summary>
    /// 刪除指定 Key
    /// </summary>
    /// <param name="key">鍵值</param>
    Task<bool> DeleteAsync(string key);

    /// <summary>
    /// 檢查指定 Key 是否存在
    /// </summary>
    /// <param name="key">鍵值</param>
    Task<bool> ExistsAsync(string key);

    /// <summary>
    /// 設定群組中指定欄位的值
    /// </summary>
    /// <param name="groupKey">群組鍵值</param>
    /// <param name="field">欄位名稱</param>
    /// <param name="value">欄位內容</param>
    Task<bool> GroupSetAsync(string groupKey, string field, string value);

    /// <summary>
    /// 取得群組中指定欄位的值
    /// </summary>
    /// <param name="groupKey">群組鍵值</param>
    /// <param name="field">欄位名稱</param>
    /// <returns>欄位內容，若不存在則回傳 null</returns>
    Task<string?> GroupGetAsync(string groupKey, string field);

    /// <summary>
    /// 刪除群組中指定欄位
    /// </summary>
    /// <param name="groupKey">群組鍵值</param>
    /// <param name="field">欄位名稱</param>
    Task<bool> GroupDeleteAsync(string groupKey, string field);

    /// <summary>
    /// 檢查群組中指定欄位是否存在
    /// </summary>
    /// <param name="groupKey">群組鍵值</param>
    /// <param name="field">欄位名稱</param>
    Task<bool> GroupExistsAsync(string groupKey, string field);

    /// <summary>
    /// 取得群組中所有欄位與值
    /// </summary>
    /// <param name="groupKey">群組鍵值</param>
    /// <returns>欄位名稱與內容的字典，若群組不存在則回傳空字典</returns>
    Task<IReadOnlyDictionary<string, string>> GroupGetAllAsync(string groupKey);
}
