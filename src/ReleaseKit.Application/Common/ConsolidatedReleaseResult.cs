namespace ReleaseKit.Application.Common;

/// <summary>
/// 整合 Release 資料的最終結果
/// </summary>
public sealed record ConsolidatedReleaseResult
{
    /// <summary>
    /// 依專案名稱為 Key 的整合結果字典（每個 Key 為 ProjectName，Value 為已排序的整合記錄清單）
    /// </summary>
    public required Dictionary<string, List<ConsolidatedReleaseEntry>> Projects { get; init; }
}
