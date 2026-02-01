using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace ReleaseKit.Common.Extensions;

/// <summary>
/// JSON 序列化與反序列化擴充方法
/// </summary>
public static class JsonExtensions
{
    /// <summary>
    /// 預設 JSON 序列化選項
    /// - 使用 camelCase 命名策略
    /// - 不縮排輸出
    /// - 支援完整 Unicode 字元（包含中文）
    /// - 支援字串枚舉轉換
    /// </summary>
    private static readonly JsonSerializerOptions DefaultSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    /// 預設 JSON 反序列化選項
    /// - 忽略大小寫
    /// - 支援完整 Unicode 字元（包含中文）
    /// - 支援字串枚舉轉換
    /// </summary>
    private static readonly JsonSerializerOptions DefaultDeserializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    /// 將 JSON 字串反序列化為指定型別
    /// </summary>
    /// <typeparam name="T">目標型別</typeparam>
    /// <param name="json">JSON 字串</param>
    /// <param name="options">JSON 序列化選項（選填，預設使用忽略大小寫的選項）</param>
    /// <returns>反序列化後的物件，若失敗則返回 null</returns>
    public static T? ToTypedObject<T>(this string json, JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Deserialize<T>(json, options ?? DefaultDeserializerOptions);
    }

    /// <summary>
    /// 將物件序列化為 JSON 字串（不縮排）
    /// </summary>
    /// <typeparam name="T">來源型別</typeparam>
    /// <param name="obj">要序列化的物件</param>
    /// <returns>JSON 字串</returns>
    public static string ToJson<T>(this T obj)
    {
        return JsonSerializer.Serialize(obj, DefaultSerializerOptions);
    }

    /// <summary>
    /// 將物件序列化為 JSON 字串（使用自訂選項）
    /// </summary>
    /// <typeparam name="T">來源型別</typeparam>
    /// <param name="obj">要序列化的物件</param>
    /// <param name="options">JSON 序列化選項</param>
    /// <returns>JSON 字串</returns>
    public static string ToJson<T>(this T obj, JsonSerializerOptions options)
    {
        return JsonSerializer.Serialize(obj, options);
    }
}
