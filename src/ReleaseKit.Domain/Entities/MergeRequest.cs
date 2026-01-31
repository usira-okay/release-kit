using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Entities;

/// <summary>
/// 表示一個已合併的 Pull Request / Merge Request
/// </summary>
public sealed record MergeRequest
{
    /// <summary>
    /// PR/MR 標題
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// PR/MR 描述
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 來源分支名稱
    /// </summary>
    public required string SourceBranch { get; init; }

    /// <summary>
    /// 目標分支名稱
    /// </summary>
    public required string TargetBranch { get; init; }

    /// <summary>
    /// 建立時間 (UTC)
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// 合併時間 (UTC)
    /// </summary>
    public required DateTimeOffset MergedAt { get; init; }

    /// <summary>
    /// 狀態（通常為 "merged"）
    /// </summary>
    public required string State { get; init; }

    /// <summary>
    /// 作者 ID
    /// </summary>
    public required string AuthorUserId { get; init; }

    /// <summary>
    /// 作者名稱
    /// </summary>
    public required string AuthorName { get; init; }

    /// <summary>
    /// PR/MR 網址
    /// </summary>
    public required string PRUrl { get; init; }

    /// <summary>
    /// 來源平台
    /// </summary>
    public required SourceControlPlatform Platform { get; init; }

    /// <summary>
    /// 專案路徑（如：mygroup/backend-api）
    /// </summary>
    public required string ProjectPath { get; init; }
}
