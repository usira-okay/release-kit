namespace ReleaseKit.Common.Constants;

/// <summary>
/// 風險分析專用 Redis Key 建構器
/// </summary>
public static class RiskAnalysisRedisKeys
{
    /// <summary>
    /// 風險分析 Hash 前綴
    /// </summary>
    private const string Prefix = "RiskAnalysis";

    /// <summary>
    /// 取得 Stage 1（Clone）的 Redis Hash Key
    /// </summary>
    public static string Stage1Hash(string runId) => $"{Prefix}:{runId}:Stage1";

    /// <summary>
    /// 取得 Stage 2（PR Diff）的 Redis Hash Key
    /// </summary>
    public static string Stage2Hash(string runId) => $"{Prefix}:{runId}:Stage2";

    /// <summary>
    /// 取得 Stage 3（靜態分析）的 Redis Hash Key
    /// </summary>
    public static string Stage3Hash(string runId) => $"{Prefix}:{runId}:Stage3";

    /// <summary>
    /// 取得 Stage 4（Copilot 分析）的 Redis Hash Key
    /// </summary>
    public static string Stage4Hash(string runId) => $"{Prefix}:{runId}:Stage4";

    /// <summary>
    /// 取得 Stage 5（交叉比對）的 Redis Hash Key
    /// </summary>
    public static string Stage5Hash(string runId) => $"{Prefix}:{runId}:Stage5";

    /// <summary>
    /// 取得 Stage 6（報告）的 Redis Hash Key
    /// </summary>
    public static string Stage6Hash(string runId) => $"{Prefix}:{runId}:Stage6";

    /// <summary>
    /// 取得當前 Run ID 的 Redis Key
    /// </summary>
    public const string CurrentRunIdKey = "RiskAnalysis:CurrentRunId";

    /// <summary>
    /// Stage 5 交叉比對結果的欄位名稱
    /// </summary>
    public const string CorrelationField = "Correlation";

    /// <summary>
    /// Stage 6 報告結果的欄位名稱
    /// </summary>
    public const string ReportField = "Report";
}
