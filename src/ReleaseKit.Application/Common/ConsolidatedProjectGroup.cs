namespace ReleaseKit.Application.Common;

/// <summary>
/// 整合結果中的專案分組
/// </summary>
public sealed record ConsolidatedProjectGroup
{
    /// <summary>
    /// 專案名稱（ProjectPath split('/') 後取最後一段）
    /// </summary>
    public required string ProjectName { get; init; }

    /// <summary>
    /// 該專案下的整合記錄清單（已排序：依 TeamDisplayName 升冪，再依 WorkItemId 升冪）
    /// </summary>
    public required List<ConsolidatedReleaseEntry> Entries { get; init; }
}
