namespace ReleaseKit.Infrastructure.RiskAnalysis;

/// <summary>
/// 程序執行結果
/// </summary>
public sealed record ProcessRunResult
{
    /// <summary>
    /// 程序結束碼
    /// </summary>
    public required int ExitCode { get; init; }

    /// <summary>
    /// 標準輸出內容
    /// </summary>
    public required string StandardOutput { get; init; }

    /// <summary>
    /// 標準錯誤輸出內容
    /// </summary>
    public required string StandardError { get; init; }
}
