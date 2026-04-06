namespace ReleaseKit.Common.Configuration;

/// <summary>
/// 風險分析組態
/// </summary>
public sealed class RiskAnalysisOptions
{
    /// <summary>Clone 的基底路徑</summary>
    public required string CloneBasePath { get; init; }

    /// <summary>最大平行 Clone 數量</summary>
    public int MaxConcurrentClones { get; init; } = 5;

    /// <summary>每次 AI 呼叫的最大 Token 數</summary>
    public int MaxTokensPerAiCall { get; init; } = 100000;

    /// <summary>動態分析最大層數（硬上限 10）</summary>
    public int MaxAnalysisPasses { get; init; } = 10;

    /// <summary>報告輸出路徑</summary>
    public required string ReportOutputPath { get; init; }
}
