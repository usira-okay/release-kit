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

    /// <summary>最大平行分析數量</summary>
    public int MaxConcurrentAnalysis { get; init; } = 3;

    /// <summary>每次 shell 指令輸出字元數上限</summary>
    public int MaxOutputCharacters { get; init; } = 50000;

    /// <summary>每次 shell 指令超時（秒）</summary>
    public int CommandTimeoutSeconds { get; init; } = 30;

    /// <summary>報告輸出路徑</summary>
    public required string ReportOutputPath { get; init; }
}
