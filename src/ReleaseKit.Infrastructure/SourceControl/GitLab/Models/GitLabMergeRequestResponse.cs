using System.Text.Json.Serialization;

namespace ReleaseKit.Infrastructure.SourceControl.GitLab.Models;

/// <summary>
/// GitLab Merge Request API 回應模型
/// </summary>
public sealed record GitLabMergeRequestResponse
{
    /// <summary>
    /// MR ID
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; init; }

    /// <summary>
    /// MR IID（專案內的 ID）
    /// </summary>
    [JsonPropertyName("iid")]
    public int Iid { get; init; }

    /// <summary>
    /// MR 標題
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// MR 描述
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// 來源分支名稱
    /// </summary>
    [JsonPropertyName("source_branch")]
    public string SourceBranch { get; init; } = string.Empty;

    /// <summary>
    /// 目標分支名稱
    /// </summary>
    [JsonPropertyName("target_branch")]
    public string TargetBranch { get; init; } = string.Empty;

    /// <summary>
    /// MR 狀態
    /// </summary>
    [JsonPropertyName("state")]
    public string State { get; init; } = string.Empty;

    /// <summary>
    /// 建立時間
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// 合併時間
    /// </summary>
    [JsonPropertyName("merged_at")]
    public DateTimeOffset? MergedAt { get; init; }

    /// <summary>
    /// MR 網址
    /// </summary>
    [JsonPropertyName("web_url")]
    public string WebUrl { get; init; } = string.Empty;

    /// <summary>
    /// 作者資訊
    /// </summary>
    [JsonPropertyName("author")]
    public GitLabAuthorResponse Author { get; init; } = new();
}
