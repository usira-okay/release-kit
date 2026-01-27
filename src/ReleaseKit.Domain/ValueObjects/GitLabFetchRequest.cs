namespace ReleaseKit.Domain.ValueObjects;

/// <summary>
/// GitLab 拉取請求介面
/// </summary>
public interface IGitLabFetchRequest
{
    /// <summary>
    /// 專案 ID
    /// </summary>
    string ProjectId { get; }
    
    /// <summary>
    /// 拉取模式
    /// </summary>
    GitLabFetchMode FetchMode { get; }
    
    /// <summary>
    /// 驗證請求參數
    /// </summary>
    void Validate();
}

/// <summary>
/// 時間區間拉取請求
/// </summary>
public class DateTimeRangeFetchRequest : IGitLabFetchRequest
{
    public required string ProjectId { get; init; }
    public GitLabFetchMode FetchMode => GitLabFetchMode.DateTimeRange;
    public required DateTimeOffset StartDateTime { get; init; }
    public required DateTimeOffset EndDateTime { get; init; }
    public string? State { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProjectId))
            throw new ArgumentException("專案 ID 不得為空", nameof(ProjectId));
        
        if (StartDateTime > EndDateTime)
            throw new ArgumentException("開始時間不得大於結束時間");
    }
}

/// <summary>
/// 分支差異拉取請求
/// </summary>
public class BranchDiffFetchRequest : IGitLabFetchRequest
{
    public required string ProjectId { get; init; }
    public GitLabFetchMode FetchMode => GitLabFetchMode.BranchDiff;
    public required string SourceBranch { get; init; }
    public required string TargetBranch { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProjectId))
            throw new ArgumentException("專案 ID 不得為空", nameof(ProjectId));
        
        if (string.IsNullOrWhiteSpace(SourceBranch))
            throw new ArgumentException("來源分支不得為空", nameof(SourceBranch));
        
        if (string.IsNullOrWhiteSpace(TargetBranch))
            throw new ArgumentException("目標分支不得為空", nameof(TargetBranch));
    }
}

/// <summary>
/// GitLab 拉取請求工廠
/// </summary>
public static class GitLabFetchRequestFactory
{
    /// <summary>
    /// 建立時間區間拉取請求
    /// </summary>
    public static IGitLabFetchRequest CreateDateTimeRangeRequest(
        string projectId,
        DateTimeOffset startDateTime,
        DateTimeOffset endDateTime,
        string? state = null)
    {
        var request = new DateTimeRangeFetchRequest
        {
            ProjectId = projectId,
            StartDateTime = startDateTime,
            EndDateTime = endDateTime,
            State = state
        };
        
        request.Validate();
        return request;
    }
    
    /// <summary>
    /// 建立分支差異拉取請求
    /// </summary>
    public static IGitLabFetchRequest CreateBranchDiffRequest(
        string projectId,
        string sourceBranch,
        string targetBranch)
    {
        var request = new BranchDiffFetchRequest
        {
            ProjectId = projectId,
            SourceBranch = sourceBranch,
            TargetBranch = targetBranch
        };
        
        request.Validate();
        return request;
    }
}
