namespace ReleaseKit.Domain.Entities;

/// <summary>
/// 風險報告完整資料模型
/// </summary>
public sealed record RiskReport
{
    /// <summary>
    /// 執行 ID（yyyyMMddHHmmss 格式）
    /// </summary>
    public required string RunId { get; init; }

    /// <summary>
    /// 執行時間
    /// </summary>
    public required DateTimeOffset ExecutedAt { get; init; }

    /// <summary>
    /// 跨專案交叉比對結果
    /// </summary>
    public required CrossProjectCorrelation Correlation { get; init; }

    /// <summary>
    /// 各專案的風險分析結果
    /// </summary>
    public required IReadOnlyList<ProjectRiskAnalysis> ProjectAnalyses { get; init; }

    /// <summary>
    /// Markdown 報告內容
    /// </summary>
    public required string MarkdownContent { get; init; }
}
