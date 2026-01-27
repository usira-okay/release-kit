namespace ReleaseKit.Infrastructure.SourceControl.GitLab;

/// <summary>
/// GitLab API 回應的 Merge Request DTO
/// </summary>
internal class GitLabMergeRequestDto
{
    /// <summary>
    /// MR 的唯一識別碼
    /// </summary>
    public required long Id { get; init; }
    
    /// <summary>
    /// MR 的內部編號（IID）
    /// </summary>
    public required int Iid { get; init; }
    
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
    public required string Source_Branch { get; init; }
    
    /// <summary>
    /// 目標分支名稱
    /// </summary>
    public required string Target_Branch { get; init; }
    
    /// <summary>
    /// MR 狀態
    /// </summary>
    public required string State { get; init; }
    
    /// <summary>
    /// 作者資訊
    /// </summary>
    public required GitLabUserDto Author { get; init; }
    
    /// <summary>
    /// 建立時間
    /// </summary>
    public required DateTime Created_At { get; init; }
    
    /// <summary>
    /// 更新時間
    /// </summary>
    public required DateTime Updated_At { get; init; }
    
    /// <summary>
    /// 合併時間（如已合併）
    /// </summary>
    public DateTime? Merged_At { get; init; }
    
    /// <summary>
    /// MR 的網址
    /// </summary>
    public required string Web_Url { get; init; }
}

/// <summary>
/// GitLab API 回應的使用者 DTO
/// </summary>
internal class GitLabUserDto
{
    /// <summary>
    /// 使用者名稱
    /// </summary>
    public required string Username { get; init; }
}
