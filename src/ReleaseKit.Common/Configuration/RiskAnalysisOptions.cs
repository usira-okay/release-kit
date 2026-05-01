namespace ReleaseKit.Common.Configuration;

/// <summary>
/// 風險分析設定選項
/// </summary>
public class RiskAnalysisOptions
{
    /// <summary>
    /// Clone 到本地的基礎路徑
    /// </summary>
    public string CloneBasePath { get; init; } = "/tmp/release-kit-repos";
}
