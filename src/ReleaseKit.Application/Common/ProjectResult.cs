namespace ReleaseKit.Application.Common;

/// <summary>
/// 單一專案的擷取結果
/// </summary>
/// <remarks>
/// 代表某個專案的 PR 資訊擷取結果，包含專案路徑、平台、PR 清單與可能的錯誤訊息。
/// </remarks>
public sealed record ProjectResult
{
    /// <summary>
    /// 專案路徑（如 group/project-name 或 workspace/repo_slug）
    /// </summary>
    public string ProjectPath { get; init; } = string.Empty;

    /// <summary>
    /// 來源平台（GitLab 或 Bitbucket）
    /// </summary>
    public string Platform { get; init; } = string.Empty;

    /// <summary>
    /// 該專案擷取到的 PR/MR 清單
    /// </summary>
    public List<MergeRequestOutput> PullRequests { get; init; } = new();

    /// <summary>
    /// 擷取失敗時的錯誤訊息（成功時為 null）
    /// </summary>
    public string? Error { get; init; }
}
