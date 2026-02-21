namespace ReleaseKit.Application.Common;

/// <summary>
/// 表示經過 User Story 解析處理的 Work Item 資料
/// </summary>
/// <remarks>
/// 包含轉換後的 User Story 資訊與原始 Work Item 資訊。
/// 成功時所有欄位填入；失敗時僅 WorkItemId、IsSuccess、ErrorMessage、ResolutionStatus 有值。
/// </remarks>
public sealed record UserStoryWorkItemOutput
{
    /// <summary>
    /// 轉換後的 Work Item ID（User Story 的 ID）
    /// </summary>
    public required int WorkItemId { get; init; }

    /// <summary>
    /// 轉換後的標題（失敗時為 null）
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// 轉換後的類型（應為 User Story/Feature/Epic，失敗時為 null）
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// 轉換後的狀態（失敗時為 null）
    /// </summary>
    public string? State { get; init; }

    /// <summary>
    /// 轉換後的 URL（失敗時為 null）
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// 原始團隊名稱（失敗時為 null）
    /// </summary>
    public string? OriginalTeamName { get; init; }

    /// <summary>
    /// 是否成功取得資訊
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// 失敗時的錯誤原因（成功時為 null）
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 解析狀態
    /// </summary>
    public required UserStoryResolutionStatus ResolutionStatus { get; init; }

    /// <summary>
    /// 觸發此 Work Item 查詢的 PR ID
    /// </summary>
    /// <remarks>
    /// 記錄源頭 PR，不寫入 OriginalWorkItem，
    /// 保持 OriginalWorkItem 的原始性。
    /// </remarks>
    public string? PrId { get; init; }

    /// <summary>
    /// PR 所屬專案名稱（ProjectPath split('/') 取最後一段）
    /// </summary>
    /// <remarks>
    /// 記錄源頭 PR 的專案名稱，不寫入 OriginalWorkItem，
    /// 保持 OriginalWorkItem 的原始性。
    /// </remarks>
    public string? ProjectName { get; init; }

    /// <summary>
    /// 原始 Work Item 資訊（若無轉換則為 null）
    /// </summary>
    public WorkItemOutput? OriginalWorkItem { get; init; }
}
