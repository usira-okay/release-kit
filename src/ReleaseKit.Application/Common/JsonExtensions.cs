using System.Text.Json;

namespace ReleaseKit.Application.Common;

/// <summary>
/// JSON 序列化與反序列化擴充方法
/// </summary>
public static class JsonExtensions
{
    /// <summary>
    /// 預設 JSON 序列化選項（使用 camelCase 命名策略，帶縮排）
    /// </summary>
    private static readonly JsonSerializerOptions DefaultIndentedOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// 將物件序列化為 JSON 字串
    /// </summary>
    /// <typeparam name="T">來源型別</typeparam>
    /// <param name="obj">要序列化的物件</param>
    /// <param name="indented">是否使用縮排格式（預設為 false）</param>
    /// <returns>JSON 字串</returns>
    public static string SerializeToJson<T>(this T obj, bool indented = false)
    {
        if (indented)
        {
            return JsonSerializer.Serialize(obj, DefaultIndentedOptions);
        }

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        return JsonSerializer.Serialize(obj, options);
    }
}
