using ReleaseKit.Domain.Entities;
using ReleaseKit.Infrastructure.AzureDevOps.Models;
using System.Text.Json;

namespace ReleaseKit.Infrastructure.AzureDevOps.Mappers;

/// <summary>
/// Azure DevOps Work Item 資料映射器
/// </summary>
public static class AzureDevOpsWorkItemMapper
{
    /// <summary>
    /// 從 Azure DevOps Work Item 回應中提取父層 Work Item ID
    /// </summary>
    /// <param name="response">Azure DevOps API 回應</param>
    /// <returns>父層 Work Item ID，若無父層則回傳 null</returns>
    /// <remarks>
    /// 從 relations 陣列中尋找關聯類型為 "System.LinkTypes.Hierarchy-Reverse" 的項目，
    /// 並從其 URL 的最後一段解析出父層 Work Item ID。
    /// 若 URL 格式異常或無法解析，則回傳 null。
    /// </remarks>
    public static int? ExtractParentWorkItemId(AzureDevOpsWorkItemResponse response)
    {
        if (response.Relations == null || response.Relations.Count == 0)
        {
            return null;
        }

        var hierarchyReverseRelation = response.Relations
            .FirstOrDefault(r => r.Rel == "System.LinkTypes.Hierarchy-Reverse");

        if (hierarchyReverseRelation == null)
        {
            return null;
        }

        // URL 格式: https://dev.azure.com/{organization}/_apis/wit/workItems/{id}
        var urlSegments = hierarchyReverseRelation.Url.Split('/');
        var lastSegment = urlSegments.LastOrDefault();

        if (string.IsNullOrWhiteSpace(lastSegment))
        {
            return null;
        }

        if (int.TryParse(lastSegment, out var parentId))
        {
            return parentId;
        }

        return null;
    }

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
            ParentWorkItemId = ExtractParentWorkItemId(response)
        };
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
