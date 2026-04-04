using ReleaseKit.Domain.Entities;

namespace ReleaseKit.Application.Common.RiskAnalysis;

/// <summary>
/// 深度分析的上下文資料（Phase 3 使用）
/// </summary>
public sealed record DeepAnalysisContext
{
    /// <summary>初篩結果</summary>
    public required PullRequestRisk InitialRisk { get; init; }

    /// <summary>PR 的 diff 資料</summary>
    public required PullRequestDiff Diff { get; init; }

    /// <summary>變更檔案的完整內容</summary>
    public required IReadOnlyList<FileContent> FullFileContents { get; init; }

    /// <summary>相關的 interface 和 base class 內容</summary>
    public required IReadOnlyList<FileContent> RelatedFiles { get; init; }

    /// <summary>其他 Repos 中引用此程式碼的地方</summary>
    public required IReadOnlyList<CrossRepoReference> CrossRepoReferences { get; init; }
}
