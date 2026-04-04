namespace ReleaseKit.Infrastructure.RiskAnalysis.Models;

/// <summary>
/// AI 跨服務風險分析回應的反序列化模型
/// </summary>
internal sealed record CrossServiceRiskResponse
{
    /// <summary>來源服務名稱</summary>
    public string SourceService { get; init; } = string.Empty;

    /// <summary>受影響的服務清單</summary>
    public List<string> AffectedServices { get; init; } = new();

    /// <summary>風險等級</summary>
    public string RiskLevel { get; init; } = "None";

    /// <summary>影響描述</summary>
    public string ImpactDescription { get; init; } = string.Empty;

    /// <summary>建議行動</summary>
    public string SuggestedAction { get; init; } = string.Empty;

    /// <summary>相關的 PR ID 清單</summary>
    public List<string> RelatedPrIds { get; init; } = new();
}
