using ReleaseKit.Domain.Entities;
using ReleaseKit.Infrastructure.AzureDevOps.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ReleaseKit.Infrastructure.AzureDevOps.Mappers;

/// <summary>
/// Azure DevOps Work Item 資料映射器
/// </summary>
public static partial class AzureDevOpsWorkItemMapper
{
    /// <summary>
    /// 用於從 URL 提取 Work Item ID 的正則表達式
    /// </summary>
    [GeneratedRegex(@"workitems/(\d+)$", RegexOptions.Compiled)]
    private static partial Regex WorkItemIdRegex();

    /// <summary>
    /// 將 Azure DevOps API 回應映射到領域實體
    /// </summary>
    /// <param name="response">Azure DevOps API 回應</param>
    /// <returns>WorkItem 領域實體</returns>
    public static WorkItem ToDomain(AzureDevOpsWorkItemResponse response)
    {
        return new WorkItem
        {
            WorkItemId = response.Id,
            Title = GetFieldValue(response.Fields, "System.Title"),
            Type = GetFieldValue(response.Fields, "System.WorkItemType"),
            State = GetFieldValue(response.Fields, "System.State"),
            Url = response.Links?.Html?.Href ?? string.Empty,
            OriginalTeamName = GetFieldValue(response.Fields, "System.AreaPath"),
            ParentId = ExtractParentId(response.Relations)
        };
    }

    /// <summary>
    /// 從 Relations 中提取 Parent Work Item ID
    /// </summary>
    /// <param name="relations">Relations 集合</param>
    /// <returns>Parent Work Item ID，若無則回傳 null</returns>
    private static int? ExtractParentId(List<AzureDevOpsRelationResponse>? relations)
    {
        if (relations is null || relations.Count == 0)
        {
            return null;
        }

        // 尋找 Parent Relation（Hierarchy-Reverse）
        var parentRelation = relations.FirstOrDefault(r => 
            r.Rel == "System.LinkTypes.Hierarchy-Reverse");

        if (parentRelation is null || string.IsNullOrWhiteSpace(parentRelation.Url))
        {
            return null;
        }

        // 從 URL 提取 Parent ID: workitems/{id}
        var match = WorkItemIdRegex().Match(parentRelation.Url);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var parentId))
        {
            return parentId;
        }

        return null;
    }

    /// <summary>
    /// 從欄位字典中安全取得字串值
    /// </summary>
    /// <param name="fields">欄位字典</param>
    /// <param name="fieldName">欄位名稱</param>
    /// <returns>欄位值，若不存在或為 null 則回傳空字串</returns>
    private static string GetFieldValue(Dictionary<string, object?> fields, string fieldName)
    {
        if (!fields.TryGetValue(fieldName, out var value) || value is null)
        {
            return string.Empty;
        }

        // Handle JsonElement (when deserialized from JSON)
        if (value is JsonElement jsonElement)
        {
            return jsonElement.ValueKind == JsonValueKind.String 
                ? jsonElement.GetString() ?? string.Empty 
                : string.Empty;
        }

        return value.ToString() ?? string.Empty;
    }
}
