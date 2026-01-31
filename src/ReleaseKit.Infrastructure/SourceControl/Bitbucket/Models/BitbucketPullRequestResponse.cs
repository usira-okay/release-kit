using System.Text.Json.Serialization;

namespace ReleaseKit.Infrastructure.SourceControl.Bitbucket.Models;

/// <summary>
/// Bitbucket Pull Request API 回應模型
/// </summary>
public sealed record BitbucketPullRequestResponse
{
    /// <summary>
    /// PR ID
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; init; }

    /// <summary>
    /// PR 標題
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// PR 描述
    /// </summary>
    [JsonPropertyName("summary")]
    public BitbucketSummaryResponse? Summary { get; init; }

    /// <summary>
    /// 來源分支資訊
    /// </summary>
    [JsonPropertyName("source")]
    public BitbucketBranchRefResponse Source { get; init; } = new();

    /// <summary>
    /// 目標分支資訊
    /// </summary>
    [JsonPropertyName("destination")]
    public BitbucketBranchRefResponse Destination { get; init; } = new();

    /// <summary>
    /// PR 狀態
    /// </summary>
    [JsonPropertyName("state")]
    public string State { get; init; } = string.Empty;

    /// <summary>
    /// 建立時間
    /// </summary>
    [JsonPropertyName("created_on")]
    public DateTimeOffset CreatedOn { get; init; }

    /// <summary>
    /// 關閉時間（合併時間）
    /// </summary>
    [JsonPropertyName("closed_on")]
    public DateTimeOffset? ClosedOn { get; init; }

    /// <summary>
    /// 作者資訊
    /// </summary>
    [JsonPropertyName("author")]
    public BitbucketAuthorResponse Author { get; init; } = new();

    /// <summary>
    /// 連結資訊
    /// </summary>
    [JsonPropertyName("links")]
    public BitbucketLinksResponse Links { get; init; } = new();
}
