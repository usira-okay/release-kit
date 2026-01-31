using System.Text.Json.Serialization;

namespace ReleaseKit.Infrastructure.SourceControl.Bitbucket.Models;

/// <summary>
/// Bitbucket 分頁回應模型
/// </summary>
/// <typeparam name="T">分頁資料類型</typeparam>
public sealed record BitbucketPageResponse<T>
{
    /// <summary>
    /// 資料清單
    /// </summary>
    [JsonPropertyName("values")]
    public List<T> Values { get; init; } = new();

    /// <summary>
    /// 下一頁連結
    /// </summary>
    [JsonPropertyName("next")]
    public string? Next { get; init; }

    /// <summary>
    /// 目前頁碼
    /// </summary>
    [JsonPropertyName("page")]
    public int Page { get; init; }

    /// <summary>
    /// 每頁筆數
    /// </summary>
    [JsonPropertyName("pagelen")]
    public int PageLen { get; init; }

    /// <summary>
    /// 總筆數
    /// </summary>
    [JsonPropertyName("size")]
    public int Size { get; init; }
}
