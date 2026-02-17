using System.Text.Json.Serialization;

namespace ReleaseKit.Infrastructure.AzureDevOps.Models;

/// <summary>
/// Azure DevOps Work Item 關聯回應模型
/// </summary>
/// <remarks>
/// 表示 Work Item 之間的關聯關係，例如 Parent-Child 階層關係。
/// </remarks>
public sealed record AzureDevOpsRelationResponse
{
    /// <summary>
    /// 關聯類型
    /// </summary>
    /// <remarks>
    /// 範例值：
    /// - "System.LinkTypes.Hierarchy-Reverse": 表示此為 Parent 關聯
    /// - "System.LinkTypes.Related": 表示相關的 Work Item
    /// - "Hyperlink": 表示外部連結
    /// </remarks>
    [JsonPropertyName("rel")]
    public string Rel { get; init; } = string.Empty;

    /// <summary>
    /// 關聯目標的 API URL
    /// </summary>
    /// <remarks>
    /// 格式範例：https://dev.azure.com/org/project/_apis/wit/workItems/12345
    /// Parent Work Item ID 可從此 URL 末尾提取
    /// </remarks>
    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    /// <summary>
    /// 關聯的屬性集合
    /// </summary>
    /// <remarks>
    /// 可選欄位，包含關聯的額外屬性資訊
    /// </remarks>
    [JsonPropertyName("attributes")]
    public Dictionary<string, object?>? Attributes { get; init; }
}
