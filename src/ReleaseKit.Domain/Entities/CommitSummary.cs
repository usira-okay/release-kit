using System.Collections.Generic;

namespace ReleaseKit.Domain.Entities;

/// <summary>
/// 表示單一 Commit 的異動摘要（輕量 metadata，不含完整 diff 內容）
/// </summary>
public sealed record CommitSummary
{
    /// <summary>
    /// Commit SHA
    /// </summary>
    public required string CommitSha { get; init; }

    /// <summary>
    /// 異動檔案清單（含路徑與變更類型，不含 diff 內容）
    /// </summary>
    public required IReadOnlyList<FileDiff> ChangedFiles { get; init; }

    /// <summary>
    /// 異動檔案總數
    /// </summary>
    public required int TotalFilesChanged { get; init; }

    /// <summary>
    /// 新增行數
    /// </summary>
    public required int TotalLinesAdded { get; init; }

    /// <summary>
    /// 刪除行數
    /// </summary>
    public required int TotalLinesRemoved { get; init; }
}
