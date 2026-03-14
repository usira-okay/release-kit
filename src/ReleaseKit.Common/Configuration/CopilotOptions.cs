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
}
