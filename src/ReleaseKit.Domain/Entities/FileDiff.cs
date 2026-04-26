using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Entities;

/// <summary>
/// 表示單一檔案的差異資訊
/// </summary>
public sealed record FileDiff
{
    /// <summary>
    /// 檔案路徑（相對於 repo 根目錄）
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// 變更類型
    /// </summary>
    public required ChangeType ChangeType { get; init; }

    /// <summary>
    /// Diff 內容（unified diff 格式）
    /// </summary>
    public required string DiffContent { get; init; }

    /// <summary>
    /// 對應的 Commit SHA
    /// </summary>
    public required string CommitSha { get; init; }
}
