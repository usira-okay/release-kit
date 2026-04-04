namespace ReleaseKit.Domain.Abstractions;

/// <summary>
/// Phase 3 深度分析的輸入資料
/// </summary>
public sealed record DeepAnalyzeInput
{
    /// <summary>PR 識別碼</summary>
    public required string PrId { get; init; }

    /// <summary>Repository 名稱</summary>
    public required string RepositoryName { get; init; }

    /// <summary>初篩結果摘要</summary>
    public required string InitialRiskSummary { get; init; }

    /// <summary>完整的分析上下文（程式碼內容）</summary>
    public required string FullContext { get; init; }
}
