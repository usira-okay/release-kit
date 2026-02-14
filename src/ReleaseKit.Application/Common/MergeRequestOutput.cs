namespace ReleaseKit.Application.Common;

/// <summary>
/// MR/PR 輸出模型
/// </summary>
/// <remarks>
/// 用於最終 JSON 輸出的 MR/PR 資訊，統一 GitLab 與 Bitbucket 的欄位格式。
/// </remarks>
public sealed record MergeRequestOutput
{
    /// <summary>
    /// PR/MR 識別碼
    /// </summary>
    public int PullRequestId { get; init; }

    /// <summary>
    /// PR/MR 標題
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// PR/MR 描述
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 來源分支名稱
    /// </summary>
    public string SourceBranch { get; init; } = string.Empty;

    /// <summary>
    /// 目標分支名稱
    /// </summary>
    public string TargetBranch { get; init; } = string.Empty;

    /// <summary>
    /// PR/MR 建立時間（UTC）
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// PR/MR 合併時間（UTC）
    /// </summary>
    /// <remarks>
    /// 若 PR/MR 尚未合併，此值為 null。
    /// </remarks>
    public DateTimeOffset? MergedAt { get; init; }

    /// <summary>
    /// PR/MR 狀態（通常為 merged）
    /// </summary>
    public string State { get; init; } = string.Empty;

    /// <summary>
    /// 作者使用者 ID
    /// </summary>
    public string AuthorUserId { get; init; } = string.Empty;

    /// <summary>
    /// 作者名稱
    /// </summary>
    public string AuthorName { get; init; } = string.Empty;

    /// <summary>
    /// PR/MR 網址
    /// </summary>
    public string PRUrl { get; init; } = string.Empty;
}
