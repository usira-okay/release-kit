namespace ReleaseKit.Domain.Entities;

/// <summary>
/// Merge Request 實體，表示 GitLab 或 Bitbucket 的合併請求
/// </summary>
public class MergeRequest
{
    /// <summary>
    /// MR 的唯一識別碼
    /// </summary>
    public required string Id { get; init; }
    
    /// <summary>
    /// MR 的內部編號（如 GitLab 的 IID）
    /// </summary>
    public required int Number { get; init; }
    
    /// <summary>
    /// MR 標題
    /// </summary>
    public required string Title { get; init; }
    
    /// <summary>
    /// MR 描述
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
    /// MR 狀態（opened, merged, closed）
    /// </summary>
    public required string State { get; init; }
    
    /// <summary>
    /// 作者使用者名稱
    /// </summary>
    public required string Author { get; init; }
    
    /// <summary>
    /// 作者使用者 ID
    /// </summary>
    public required long AuthorId { get; init; }
    
    /// <summary>
    /// 建立時間
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }
    
    /// <summary>
    /// 更新時間
    /// </summary>
    public required DateTimeOffset UpdatedAt { get; init; }
    
    /// <summary>
    /// 合併時間（如已合併）
    /// </summary>
    public DateTimeOffset? MergedAt { get; init; }
    
    /// <summary>
    /// MR 的網址
    /// </summary>
    public required string WebUrl { get; init; }
}
