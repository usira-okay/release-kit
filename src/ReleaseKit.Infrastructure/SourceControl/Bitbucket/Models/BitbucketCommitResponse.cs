using System.Text.Json.Serialization;

namespace ReleaseKit.Infrastructure.SourceControl.Bitbucket.Models;

/// <summary>
/// Bitbucket Commit API 回應模型
/// </summary>
public sealed record BitbucketCommitResponse
{
    /// <summary>
    /// Commit Hash
    /// </summary>
    [JsonPropertyName("hash")]
    public string Hash { get; init; } = string.Empty;

    /// <summary>
    /// Commit 類型 (通常是 "commit")
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// Commit 訊息
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 建立日期
    /// </summary>
    [JsonPropertyName("date")]
    public DateTimeOffset Date { get; init; }

    /// <summary>
    /// 作者資訊
    /// </summary>
    [JsonPropertyName("author")]
    public BitbucketCommitAuthorResponse? Author { get; init; }
}

/// <summary>
/// Bitbucket Commit 作者資訊
/// </summary>
public sealed record BitbucketCommitAuthorResponse
{
    /// <summary>
    /// 作者原始資訊 (包含 name 和 email)
    /// </summary>
    [JsonPropertyName("raw")]
    public string Raw { get; init; } = string.Empty;

    /// <summary>
    /// 作者使用者資訊
    /// </summary>
    [JsonPropertyName("user")]
    public BitbucketAuthorResponse? User { get; init; }
}
