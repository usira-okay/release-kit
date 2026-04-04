namespace ReleaseKit.Infrastructure.RiskAnalysis;

/// <summary>
/// 執行外部程序的抽象介面
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// 執行指定的命令並回傳結果
    /// </summary>
    /// <param name="fileName">執行檔名稱</param>
    /// <param name="arguments">命令參數</param>
    /// <param name="workingDirectory">工作目錄（可為空）</param>
    /// <returns>執行結果，包含 exit code、stdout、stderr</returns>
    Task<ProcessRunResult> RunAsync(string fileName, string arguments, string? workingDirectory = null);
}
