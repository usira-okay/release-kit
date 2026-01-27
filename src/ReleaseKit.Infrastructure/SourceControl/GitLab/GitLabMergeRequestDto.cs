using System.Text.Json.Serialization;

namespace ReleaseKit.Infrastructure.SourceControl.GitLab;

/// <summary>
/// GitLab API 回應的 Merge Request DTO
/// </summary>
internal class GitLabMergeRequestDto
{
    /// <summary>
    /// MR 的唯一識別碼
    /// </summary>
    [JsonPropertyName("id")]
    public required long Id { get; init; }
    
    /// <summary>
    /// MR 的內部編號（IID）
    /// </summary>
    [JsonPropertyName("iid")]
    public required int Iid { get; init; }
    
    /// <summary>
    /// MR 標題
    /// </summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }
    
    /// <summary>
    /// MR 描述
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }
    
    /// <summary>
    /// 來源分支名稱
    /// </summary>
    [JsonPropertyName("source_branch")]
    public required string SourceBranch { get; init; }
    
    /// <summary>
    /// 目標分支名稱
    /// </summary>
    [JsonPropertyName("target_branch")]
    public required string TargetBranch { get; init; }
    
    /// <summary>
    /// MR 狀態
    /// </summary>
    [JsonPropertyName("state")]
    public required string State { get; init; }
    
    /// <summary>
    /// 作者資訊
    /// </summary>
    [JsonPropertyName("author")]
    public required GitLabUserDto Author { get; init; }
    
    /// <summary>
    /// 建立時間
    /// </summary>
    [JsonPropertyName("created_at")]
    public required DateTime CreatedAt { get; init; }
    
    /// <summary>
    /// 更新時間
    /// </summary>
    [JsonPropertyName("updated_at")]
    public required DateTime UpdatedAt { get; init; }
    
    /// <summary>
    /// 合併時間（如已合併）
    /// </summary>
    [JsonPropertyName("merged_at")]
    public DateTime? MergedAt { get; init; }
    
    /// <summary>
    /// MR 的網址
    /// </summary>
    [JsonPropertyName("web_url")]
    public required string WebUrl { get; init; }
}

/// <summary>
/// GitLab API 回應的使用者 DTO
/// </summary>
internal class GitLabUserDto
{
    /// <summary>
    /// 使用者 ID
    /// </summary>
    [JsonPropertyName("id")]
    public required long Id { get; init; }
    
    /// <summary>
    /// 使用者名稱
    /// </summary>
    [JsonPropertyName("username")]
    public required string Username { get; init; }
}
