namespace ReleaseKit.Domain.Abstractions;

/// <summary>
/// Phase 2 風險初篩的輸入資料
/// </summary>
public sealed record ScreenRiskInput
{
    /// <summary>PR 識別碼</summary>
    public required string PrId { get; init; }

    /// <summary>PR 標題</summary>
    public required string PrTitle { get; init; }

    /// <summary>PR 連結</summary>
    public required string PrUrl { get; init; }

    /// <summary>Repository 名稱</summary>
    public required string RepositoryName { get; init; }

    /// <summary>變更檔案的 diff 摘要</summary>
    public required string DiffSummary { get; init; }
}
