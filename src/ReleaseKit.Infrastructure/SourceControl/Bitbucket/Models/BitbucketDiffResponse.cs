using System.Text.Json.Serialization;

namespace ReleaseKit.Infrastructure.SourceControl.Bitbucket.Models;

/// <summary>
/// Bitbucket Diff/Compare API 回應模型 (使用 diffstat endpoint)
/// 因為 Bitbucket 沒有直接的 compare endpoint，使用 diffstat 來取得變更清單
/// </summary>
public sealed record BitbucketDiffResponse
{
    /// <summary>
    /// 變更的檔案清單
    /// </summary>
    [JsonPropertyName("values")]
    public List<BitbucketDiffStatResponse> Values { get; init; } = new();

    /// <summary>
    /// 下一頁 URL
    /// </summary>
    [JsonPropertyName("next")]
    public string? Next { get; init; }
}

/// <summary>
/// Bitbucket DiffStat 項目
/// </summary>
public sealed record BitbucketDiffStatResponse
{
    /// <summary>
    /// 檔案類型
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// 檔案狀態 (modified, added, removed)
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// 行數變更統計
    /// </summary>
    [JsonPropertyName("lines_added")]
    public int LinesAdded { get; init; }

    /// <summary>
    /// 行數移除統計
    /// </summary>
    [JsonPropertyName("lines_removed")]
    public int LinesRemoved { get; init; }
}
