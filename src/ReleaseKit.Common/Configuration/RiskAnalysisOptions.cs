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

    /// <summary>
    /// 啟用的分析情境清單
    /// </summary>
    public List<string> AnalysisScenarios { get; init; } = new()
    {
        "ApiContractBreak",
        "DatabaseSchemaChange",
        "MessageQueueFormat",
        "ConfigEnvChange",
        "DataSemanticChange"
    };

    /// <summary>
    /// 分析情境清單（字串格式，可由設定檔覆寫，預設包含全部 5 種情境）
    /// </summary>
    public List<string> Scenarios { get; init; } = new()
    {
        "ApiContractBreak",
        "DatabaseSchemaChange",
        "MessageQueueFormat",
        "ConfigEnvChange",
        "DataSemanticChange"
    };

    /// <summary>
    /// 風險報告輸出路徑（預設為當前目錄）
    /// </summary>
    public string ReportOutputPath { get; init; } = ".";
}
