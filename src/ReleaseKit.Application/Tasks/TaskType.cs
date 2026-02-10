namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 任務類型列舉
/// </summary>
public enum TaskType
{
    /// <summary>
    /// 拉取 GitLab Pull Request 資訊
    /// </summary>
    FetchGitLabPullRequests,
    
    /// <summary>
    /// 拉取 Bitbucket Pull Request 資訊
    /// </summary>
    FetchBitbucketPullRequests,
    
    /// <summary>
    /// 拉取 Azure DevOps Work Item 資訊
    /// </summary>
    FetchAzureDevOpsWorkItems,
    
    /// <summary>
    /// 更新 Google Sheets 資訊
    /// </summary>
    UpdateGoogleSheets,
    
    /// <summary>
    /// 取得 GitLab 各專案最新 Release Branch
    /// </summary>
    FetchGitLabReleaseBranch,
    
    /// <summary>
    /// 取得 Bitbucket 各專案最新 Release Branch
    /// </summary>
    FetchBitbucketReleaseBranch
}
