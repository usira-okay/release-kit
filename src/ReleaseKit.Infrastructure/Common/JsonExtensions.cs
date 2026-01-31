using System.Text.Json;

namespace ReleaseKit.Infrastructure.Common;

/// <summary>
/// JSON 序列化與反序列化擴充方法
/// </summary>
public static class JsonExtensions
{
    /// <summary>
    /// 預設 JSON 序列化選項（使用 camelCase 命名策略）
    /// </summary>
    private static readonly JsonSerializerOptions DefaultSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// 預設 JSON 序列化選項（帶縮排，用於輸出）
    /// </summary>
    private static readonly JsonSerializerOptions IndentedSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// 將 JSON 字串反序列化為指定型別
    /// </summary>
    /// <typeparam name="T">目標型別</typeparam>
    /// <param name="json">JSON 字串</param>
    /// <param name="options">JSON 序列化選項（選填）</param>
    /// <returns>反序列化後的物件，若失敗則返回 null</returns>
    public static T? DeserializeFromJson<T>(this string json, JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Deserialize<T>(json, options);
    }

    /// <summary>
    /// 將物件序列化為 JSON 字串
    /// </summary>
    /// <typeparam name="T">來源型別</typeparam>
    /// <param name="obj">要序列化的物件</param>
    /// <param name="indented">是否使用縮排格式（預設為 false）</param>
    /// <returns>JSON 字串</returns>
    public static string SerializeToJson<T>(this T obj, bool indented = false)
    {
        var options = indented ? IndentedSerializerOptions : DefaultSerializerOptions;
        return JsonSerializer.Serialize(obj, options);
    }

    /// <summary>
    /// 將物件序列化為 JSON 字串（使用自訂選項）
    /// </summary>
    /// <typeparam name="T">來源型別</typeparam>
    /// <param name="obj">要序列化的物件</param>
    /// <param name="options">JSON 序列化選項</param>
    /// <returns>JSON 字串</returns>
    public static string SerializeToJson<T>(this T obj, JsonSerializerOptions options)
    {
        return JsonSerializer.Serialize(obj, options);
    }
}
