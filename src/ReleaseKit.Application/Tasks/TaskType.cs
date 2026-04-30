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
    FetchBitbucketReleaseBranch,
    
    /// <summary>
    /// 過濾 GitLab Pull Request 依使用者
    /// </summary>
    FilterGitLabPullRequestsByUser,
    
    /// <summary>
    /// 過濾 Bitbucket Pull Request 依使用者
    /// </summary>
    FilterBitbucketPullRequestsByUser,
    
    /// <summary>
    /// 取得 User Story 層級的 Work Item
    /// </summary>
    GetUserStory,

    /// <summary>
    /// 整合 Release 資料
    /// </summary>
    ConsolidateReleaseData,

    /// <summary>
    /// 使用 AI 增強 Release 標題
    /// </summary>
    EnhanceTitles,

    /// <summary>
    /// 產生 Release Setting 設定
    /// </summary>
    GetReleaseSetting,

    /// <summary>
    /// Clone/Pull 所有專案 Repo
    /// </summary>
    CloneRepositories,

    /// <summary>
    /// 分析 PR Diff 資訊
    /// </summary>
    AnalyzePRDiffs,

    /// <summary>
    /// Copilot SDK 風險分析
    /// </summary>
    CopilotRiskAnalysis,

    /// <summary>
    /// 跨專案交叉比對
    /// </summary>
    CrossProjectCorrelation,

    /// <summary>
    /// 產生風險報告
    /// </summary>
    GenerateRiskReport
}
