using ReleaseKit.Domain.Common;
using ReleaseKit.Domain.Entities;

namespace ReleaseKit.Domain.Abstractions;

/// <summary>
/// Azure DevOps Repository 介面
/// </summary>
/// <remarks>
/// 定義與 Azure DevOps REST API 互動的標準介面。
/// 用於取得 Work Item 詳細資訊。
/// 所有方法皆使用 Result Pattern 回傳結果，避免例外處理。
/// </remarks>
public interface IAzureDevOpsRepository
{
    /// <summary>
    /// 取得單一 Work Item 詳細資訊
    /// </summary>
    /// <param name="workItemId">Work Item 唯一識別碼</param>
    /// <returns>成功時回傳 WorkItem 實體；失敗時回傳包含錯誤資訊的 Result</returns>
    /// <remarks>
    /// 呼叫 Azure DevOps REST API GET _apis/wit/workitems/{id}?$expand=all&amp;api-version=7.0。
    /// 失敗情況包含：Work Item 不存在（404）、驗證失敗（401）、其他 API 錯誤。
    /// </remarks>
    Task<Result<WorkItem>> GetWorkItemAsync(int workItemId);
}
