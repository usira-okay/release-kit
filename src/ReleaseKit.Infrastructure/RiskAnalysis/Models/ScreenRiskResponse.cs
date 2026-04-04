namespace ReleaseKit.Infrastructure.RiskAnalysis.Models;

/// <summary>
/// AI 風險初篩回應的反序列化模型
/// </summary>
internal sealed record ScreenRiskResponse
{
    /// <summary>PR 識別碼</summary>
    public string PrId { get; init; } = string.Empty;

    /// <summary>Repository 名稱</summary>
    public string RepositoryName { get; init; } = string.Empty;

    /// <summary>風險等級</summary>
    public string RiskLevel { get; init; } = "None";

    /// <summary>風險類別清單</summary>
    public List<string> RiskCategories { get; init; } = new();

    /// <summary>風險描述</summary>
    public string RiskDescription { get; init; } = string.Empty;

    /// <summary>是否需要深度分析</summary>
    public bool NeedsDeepAnalysis { get; init; }

    /// <summary>受影響的元件</summary>
    public List<string> AffectedComponents { get; init; } = new();

    /// <summary>建議行動</summary>
    public string SuggestedAction { get; init; } = string.Empty;
}
