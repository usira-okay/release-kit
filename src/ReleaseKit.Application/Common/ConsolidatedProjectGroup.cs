namespace ReleaseKit.Application.Common;

/// <summary>
/// 依專案分組的整合結果
/// </summary>
public sealed record ConsolidatedProjectGroup
{
    /// <summary>
    /// 專案名稱
    /// </summary>
    public required string ProjectName { get; init; }

    /// <summary>
    /// 專案內的整合資料清單
    /// </summary>
    public required List<ConsolidatedReleaseEntry> Entries { get; init; }
}
