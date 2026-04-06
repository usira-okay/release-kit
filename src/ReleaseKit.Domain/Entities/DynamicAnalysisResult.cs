namespace ReleaseKit.Domain.Entities;

/// <summary>
/// 動態分析結果（包含是否繼續分析的 AI 判斷）
/// </summary>
public sealed record DynamicAnalysisResult
{
    /// <summary>本層分析產生的報告</summary>
    public required IReadOnlyList<RiskAnalysisReport> Reports { get; init; }

    /// <summary>AI 判斷是否需要繼續更深層分析</summary>
    public required bool ContinueAnalysis { get; init; }

    /// <summary>繼續分析的理由（繁體中文，供 log 與追蹤）</summary>
    public string? ContinueReason { get; init; }

    /// <summary>本層使用的分析策略描述</summary>
    public required string AnalysisStrategy { get; init; }
}
