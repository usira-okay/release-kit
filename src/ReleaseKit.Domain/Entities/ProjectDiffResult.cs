namespace ReleaseKit.Domain.Entities;

/// <summary>
/// 表示單一專案的所有差異結果
/// </summary>
public sealed record ProjectDiffResult
{
    /// <summary>
    /// 專案路徑
    /// </summary>
    public required string ProjectPath { get; init; }

    /// <summary>
    /// 所有異動檔案的差異清單
    /// </summary>
    public required IReadOnlyList<FileDiff> FileDiffs { get; init; }
}
