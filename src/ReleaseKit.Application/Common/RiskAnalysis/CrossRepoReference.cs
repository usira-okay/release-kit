namespace ReleaseKit.Application.Common.RiskAnalysis;

/// <summary>
/// 跨 Repository 的程式碼引用
/// </summary>
public sealed record CrossRepoReference
{
    /// <summary>引用所在的 Repository 名稱</summary>
    public required string RepositoryName { get; init; }

    /// <summary>引用所在的檔案路徑</summary>
    public required string FilePath { get; init; }

    /// <summary>引用的程式碼片段</summary>
    public required string CodeSnippet { get; init; }
}
