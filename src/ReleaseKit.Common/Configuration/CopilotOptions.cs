namespace ReleaseKit.Common.Configuration;

/// <summary>
/// GitHub Copilot SDK 組態選項
/// </summary>
public class CopilotOptions
{
    /// <summary>
    /// 使用的模型名稱（如 gpt-4.1、claude-sonnet-4.5 等）
    /// </summary>
    public string Model { get; init; } = "gpt-4.1";

    /// <summary>
    /// SendAndWaitAsync 的逾時時間（秒），預設 600 秒（10 分鐘）
    /// </summary>
    public int TimeoutSeconds { get; init; } = 600;

    /// <summary>
    /// GitHub Personal Access Token，用於 Copilot SDK 驗證身份。
    /// 若未設定，SDK 將嘗試使用本機已登入的 GitHub 帳號。
    /// </summary>
    public string GitHubToken { get; init; } = string.Empty;
}
