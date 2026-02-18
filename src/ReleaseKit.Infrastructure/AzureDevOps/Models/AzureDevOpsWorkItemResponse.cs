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
    /// Work Item 關聯關係（Parent/Child 等）
    /// </summary>
    [JsonPropertyName("relations")]
    public List<AzureDevOpsRelationResponse>? Relations { get; init; }
}
