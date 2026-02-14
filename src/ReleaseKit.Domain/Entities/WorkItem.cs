namespace ReleaseKit.Domain.Entities;

/// <summary>
/// 表示一個 Azure DevOps Work Item
/// </summary>
/// <remarks>
/// Work Item 可能是 Bug、Task、User Story 等類型的工作項目。
/// 所有屬性均為必填（required），確保資料完整性。
/// 此實體為不可變（immutable）record 類型，遵循 DDD 實體原則。
/// </remarks>
public sealed record WorkItem
{
    /// <summary>
    /// Work Item 唯一識別碼
    /// </summary>
    /// <remarks>
    /// Azure DevOps API 回傳的 id 欄位。
    /// </remarks>
    public required int WorkItemId { get; init; }

    /// <summary>
    /// 工作項目標題
    /// </summary>
    /// <remarks>
    /// 對應 Azure DevOps API 回應中的 fields["System.Title"] 欄位。
    /// </remarks>
    public required string Title { get; init; }

    /// <summary>
    /// 工作項目類型（Bug/Task/User Story 等）
    /// </summary>
    /// <remarks>
    /// 對應 Azure DevOps API 回應中的 fields["System.WorkItemType"] 欄位。
    /// </remarks>
    public required string Type { get; init; }

    /// <summary>
    /// 工作項目狀態（New/Active/Resolved/Closed 等）
    /// </summary>
    /// <remarks>
    /// 對應 Azure DevOps API 回應中的 fields["System.State"] 欄位。
    /// </remarks>
    public required string State { get; init; }

    /// <summary>
    /// Work Item 網頁連結
    /// </summary>
    /// <remarks>
    /// 對應 Azure DevOps API 回應中的 _links.html.href 欄位。
    /// 提供可直接存取 Work Item 詳細頁面的完整 URL。
    /// </remarks>
    public required string Url { get; init; }

    /// <summary>
    /// 原始區域路徑（Team 名稱）
    /// </summary>
    /// <remarks>
    /// 對應 Azure DevOps API 回應中的 fields["System.AreaPath"] 欄位。
    /// 本階段直接儲存原始值，不做 TeamMapping 轉換。
    /// </remarks>
    public required string OriginalTeamName { get; init; }

    /// <summary>
    /// 父層 Work Item ID
    /// </summary>
    /// <remarks>
    /// 從 Azure DevOps API 回應中的 relations 陣列解析。
    /// 對應關聯類型為 "System.LinkTypes.Hierarchy-Reverse" 的 Work Item ID。
    /// 若無父層 Work Item，則為 null。
    /// </remarks>
    public int? ParentWorkItemId { get; init; }
}
