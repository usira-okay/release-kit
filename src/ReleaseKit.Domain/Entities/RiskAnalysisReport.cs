using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Entities;

/// <summary>
/// 風險分析報告
/// </summary>
public sealed record RiskAnalysisReport
{
    /// <summary>報告的分析階段金鑰</summary>
    public required AnalysisPassKey PassKey { get; init; }

    /// <summary>來源專案名稱（Pass 1 時使用）</summary>
    public string? ProjectName { get; init; }

    /// <summary>風險類別（Pass 2 時使用）</summary>
    public RiskCategory? Category { get; init; }

    /// <summary>識別到的風險項目</summary>
    public required IReadOnlyList<RiskItem> RiskItems { get; init; }

    /// <summary>分析摘要（繁體中文）</summary>
    public required string Summary { get; init; }

    /// <summary>分析時間戳</summary>
    public required DateTimeOffset AnalyzedAt { get; init; }
}
