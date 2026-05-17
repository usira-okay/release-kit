namespace ReleaseKit.Domain.Abstractions;

/// <summary>
/// 指令間資料交換服務介面
/// </summary>
public interface IDataTransferService
{
    /// <summary>
    /// 設定鍵值資料
    /// </summary>
    /// <param name="key">資料鍵值</param>
    /// <param name="value">資料內容</param>
    /// <param name="expiry">過期時間</param>
    Task<bool> SetValueAsync(string key, string value, TimeSpan? expiry = null);

    /// <summary>
    /// 取得鍵值資料
    /// </summary>
    /// <param name="key">資料鍵值</param>
    /// <returns>資料內容，若不存在則回傳 null</returns>
    Task<string?> GetValueAsync(string key);

    /// <summary>
    /// 刪除鍵值資料
    /// </summary>
    /// <param name="key">資料鍵值</param>
    Task<bool> DeleteValueAsync(string key);

    /// <summary>
    /// 檢查鍵值是否存在
    /// </summary>
    /// <param name="key">資料鍵值</param>
    Task<bool> ExistsValueAsync(string key);

    /// <summary>
    /// 設定集合欄位值
    /// </summary>
    /// <param name="hashKey">集合鍵值</param>
    /// <param name="field">集合欄位名稱</param>
    /// <param name="value">欄位內容</param>
    Task<bool> SetFieldAsync(string hashKey, string field, string value);

    /// <summary>
    /// 取得集合欄位值
    /// </summary>
    /// <param name="hashKey">集合鍵值</param>
    /// <param name="field">集合欄位名稱</param>
    /// <returns>欄位內容，若不存在則回傳 null</returns>
    Task<string?> GetFieldAsync(string hashKey, string field);

    /// <summary>
    /// 刪除集合欄位
    /// </summary>
    /// <param name="hashKey">集合鍵值</param>
    /// <param name="field">集合欄位名稱</param>
    Task<bool> DeleteFieldAsync(string hashKey, string field);

    /// <summary>
    /// 檢查集合欄位是否存在
    /// </summary>
    /// <param name="hashKey">集合鍵值</param>
    /// <param name="field">集合欄位名稱</param>
    Task<bool> FieldExistsAsync(string hashKey, string field);

    /// <summary>
    /// 取得集合所有欄位與值
    /// </summary>
    /// <param name="hashKey">集合鍵值</param>
    /// <returns>欄位名稱與內容的字典，若不存在則回傳空字典</returns>
    Task<IReadOnlyDictionary<string, string>> GetAllFieldsAsync(string hashKey);
}
