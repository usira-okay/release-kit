using System.Text.Json.Serialization;

namespace ReleaseKit.Infrastructure.AzureDevOps.Models;

/// <summary>
/// Azure DevOps Work Item API 回應模型
/// </summary>
public sealed record AzureDevOpsWorkItemResponse
{
    /// <summary>
    /// Work Item ID
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; init; }

    /// <summary>
    /// Work Item 欄位集合
    /// </summary>
    /// <remarks>
    /// 包含 System.Title、System.WorkItemType、System.State、System.AreaPath 等欄位。
    /// 使用 Dictionary 處理動態鍵值結構。
    /// </remarks>
    [JsonPropertyName("fields")]
    public Dictionary<string, object?> Fields { get; init; } = new();

    /// <summary>
    /// Work Item 相關連結
    /// </summary>
    [JsonPropertyName("_links")]
    public AzureDevOpsLinksResponse? Links { get; init; }

    /// <summary>
    /// Work Item 關聯清單
    /// </summary>
    /// <remarks>
    /// 包含 Parent-Child 階層關係、相關 Work Item、外部連結等。
    /// 需要在 API 請求中加入 $expand=all 參數才會回傳此欄位。
    /// </remarks>
    [JsonPropertyName("relations")]
    public List<AzureDevOpsRelationResponse>? Relations { get; init; }
}

/// <summary>
/// Azure DevOps Work Item 連結集合回應模型
/// </summary>
public sealed record AzureDevOpsLinksResponse
{
    /// <summary>
    /// Work Item 網頁連結
    /// </summary>
    [JsonPropertyName("html")]
    public AzureDevOpsLinkResponse? Html { get; init; }
}

/// <summary>
/// Azure DevOps 單一連結回應模型
/// </summary>
public sealed record AzureDevOpsLinkResponse
{
    /// <summary>
    /// 連結 URL
    /// </summary>
    [JsonPropertyName("href")]
    public string Href { get; init; } = string.Empty;
}
