namespace ReleaseKit.Common.Constants;

/// <summary>
/// Redis 鍵值常數
/// </summary>
public static class RedisKeys
{
    /// <summary>
    /// GitLab 資料的 Redis Hash 鍵值
    /// </summary>
    public const string GitLabHash = "GitLab";

    /// <summary>
    /// Bitbucket 資料的 Redis Hash 鍵值
    /// </summary>
    public const string BitbucketHash = "Bitbucket";

    /// <summary>
    /// Azure DevOps 資料的 Redis Hash 鍵值
    /// </summary>
    public const string AzureDevOpsHash = "AzureDevOps";

    /// <summary>
    /// 整合後的 Release 資料的 Redis Hash 鍵值
    /// </summary>
    public const string ReleaseDataHash = "ReleaseData";

    /// <summary>
    /// 風險分析資料的 Redis Hash 鍵值
    /// </summary>
    public const string RiskAnalysisHash = "RiskAnalysis";

    /// <summary>
    /// Redis Hash 欄位名稱常數
    /// </summary>
    public static class Fields
    {
        /// <summary>
        /// Pull Request 資料的欄位名稱
        /// </summary>
        public const string PullRequests = "PullRequests";

        /// <summary>
        /// 過濾後（依使用者）的 Pull Request 資料欄位名稱
        /// </summary>
        public const string PullRequestsByUser = "PullRequests:ByUser";

        /// <summary>
        /// Release Branch 資料的欄位名稱
        /// </summary>
        public const string ReleaseBranches = "ReleaseBranches";

        /// <summary>
        /// Work Items 資料的欄位名稱
        /// </summary>
        public const string WorkItems = "WorkItems";

        /// <summary>
        /// User Story 層級 Work Items 資料的欄位名稱
        /// </summary>
        public const string WorkItemsUserStories = "WorkItems:UserStories";

        /// <summary>
        /// 整合後的 Release 資料欄位名稱
        /// </summary>
        public const string Consolidated = "Consolidated";

        /// <summary>
        /// 增強標題後的 Release 資料欄位名稱
        /// </summary>
        public const string EnhancedTitles = "EnhancedTitles";

        /// <summary>
        /// Clone 路徑對照資料欄位名稱
        /// </summary>
        public const string ClonePaths = "ClonePaths";

        /// <summary>
        /// 中間分析結果欄位前綴（格式：Intermediate:{sequence}）
        /// </summary>
        public const string IntermediatePrefix = "Intermediate:";

        /// <summary>
        /// 分析上下文欄位前綴（格式：AnalysisContext:{sequence}）
        /// </summary>
        public const string AnalysisContextPrefix = "AnalysisContext:";

        /// <summary>
        /// 最終風險分析報告欄位名稱
        /// </summary>
        public const string FinalReport = "FinalReport";
    }
}
