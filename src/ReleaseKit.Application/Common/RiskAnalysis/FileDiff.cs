namespace ReleaseKit.Application.Common.RiskAnalysis;

/// <summary>
/// 單一檔案的 diff 資訊
/// </summary>
public sealed record FileDiff
{
    /// <summary>檔案路徑</summary>
    public required string FilePath { get; init; }

    /// <summary>新增行數</summary>
    public required int AddedLines { get; init; }

    /// <summary>刪除行數</summary>
    public required int DeletedLines { get; init; }

    /// <summary>Diff patch 內容</summary>
    public required string DiffContent { get; init; }

    /// <summary>是否為新增檔案</summary>
    public required bool IsNewFile { get; init; }

    /// <summary>是否為刪除檔案</summary>
    public required bool IsDeletedFile { get; init; }
}
