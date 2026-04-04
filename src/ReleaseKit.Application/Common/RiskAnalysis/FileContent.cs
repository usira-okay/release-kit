namespace ReleaseKit.Application.Common.RiskAnalysis;

/// <summary>
/// 完整檔案內容（用於 Phase 3 深度分析）
/// </summary>
public sealed record FileContent
{
    /// <summary>檔案路徑</summary>
    public required string FilePath { get; init; }

    /// <summary>檔案完整內容</summary>
    public required string Content { get; init; }

    /// <summary>所屬 Repository 名稱</summary>
    public required string RepositoryName { get; init; }
}
