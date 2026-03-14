namespace ReleaseKit.Application.Common;

/// <summary>
/// 增強標題後的 Release 資料結果
/// </summary>
/// <remarks>
/// 結構與 <see cref="ConsolidatedReleaseResult"/> 對應，
/// 每個專案下的記錄改為包含增強標題的 <see cref="EnhancedTitleEntry"/>。
/// </remarks>
public sealed record EnhancedTitleResult
{
    /// <summary>
    /// 依專案名稱為 Key 的增強標題結果字典
    /// </summary>
    public required Dictionary<string, List<EnhancedTitleEntry>> Projects { get; init; }
}
