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

        /// <summary>風險分析 — PR Diff 資料</summary>
        public const string RiskDiffs = "RiskAnalysis:Diffs";

        /// <summary>風險分析 — Phase 2 初篩結果</summary>
        public const string RiskScreenResults = "RiskAnalysis:ScreenResults";

        /// <summary>風險分析 — Phase 3 深度分析結果</summary>
        public const string RiskDeepResults = "RiskAnalysis:DeepResults";

        /// <summary>風險分析 — Phase 4 跨服務分析結果</summary>
        public const string RiskCrossServiceResults = "RiskAnalysis:CrossServiceResults";
    }
}
