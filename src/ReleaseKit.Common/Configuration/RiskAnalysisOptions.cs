namespace ReleaseKit.Common.Configuration;

/// <summary>
/// 風險分析功能的設定選項
/// </summary>
public class RiskAnalysisOptions
{
    /// <summary>
    /// Clone Repos 的工作目錄路徑
    /// </summary>
    public string CloneBasePath { get; init; } = "/tmp/release-risk-repos";

    /// <summary>
    /// 分析完成後是否清理 Clone 的 Repos
    /// </summary>
    public bool CleanupAfterAnalysis { get; init; } = false;

    /// <summary>
    /// 單一 PR Diff 超過此大小（bytes）時，單獨一批送 AI 分析
    /// </summary>
    public int MaxDiffSizeBytes { get; init; } = 51200;

    /// <summary>
    /// Phase 2 每批最多 PR 數量
    /// </summary>
    public int BatchSize { get; init; } = 10;

    /// <summary>
    /// 達到此風險等級才進入 Phase 3 深度分析
    /// </summary>
    public string RiskThresholdForDeepAnalysis { get; init; } = "Medium";

    /// <summary>
    /// Markdown 風險報告輸出路徑
    /// </summary>
    public string ReportOutputPath { get; init; } = "./reports";
}
