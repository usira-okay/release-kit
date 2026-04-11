using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Entities;

/// <summary>
/// 風險分析報告
/// </summary>
public sealed record RiskAnalysisReport
{
    /// <summary>專案排序序號</summary>
    public required int Sequence { get; init; }

    /// <summary>來源專案名稱</summary>
    public string? ProjectName { get; init; }

    /// <summary>識別到的風險項目</summary>
    public required IReadOnlyList<RiskItem> RiskItems { get; init; }

    /// <summary>分析摘要（繁體中文）</summary>
    public required string Summary { get; init; }

    /// <summary>分析時間戳</summary>
    public required DateTimeOffset AnalyzedAt { get; init; }

    /// <summary>分析過程記錄（Copilot 執行了哪些指令與原因）</summary>
    public string? AnalysisLog { get; init; }
}
