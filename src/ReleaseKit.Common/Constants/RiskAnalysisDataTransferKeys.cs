namespace ReleaseKit.Common.Constants;

/// <summary>
/// 風險分析專用資料交換鍵值建構器
/// </summary>
public static class RiskAnalysisDataTransferKeys
{
    /// <summary>
    /// 風險分析 Hash 前綴
    /// </summary>
    private const string Prefix = "RiskAnalysis";

    /// <summary>
    /// 取得 Stage 1（Clone）的集合鍵值
    /// </summary>
    public static string Stage1Hash(string runId) => $"{Prefix}:{runId}:Stage1";

    /// <summary>
    /// 取得 Stage 2（PR Diff）的集合鍵值
    /// </summary>
    public static string Stage2Hash(string runId) => $"{Prefix}:{runId}:Stage2";

    /// <summary>
    /// 取得當前 Run ID 的鍵值
    /// </summary>
    public const string CurrentRunIdKey = "RiskAnalysis:CurrentRunId";
}
