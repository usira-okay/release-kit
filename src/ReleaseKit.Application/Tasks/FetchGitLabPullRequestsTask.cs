using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 拉取 GitLab Pull Request 資訊任務
/// </summary>
public class FetchGitLabPullRequestsTask : ITask
{
    private readonly IGitLabRepository _gitLabRepository;
    private readonly INow _now;

    public FetchGitLabPullRequestsTask(
        IGitLabRepository gitLabRepository,
        INow now)
    {
        _gitLabRepository = gitLabRepository ?? throw new ArgumentNullException(nameof(gitLabRepository));
        _now = now ?? throw new ArgumentNullException(nameof(now));
    }

    /// <summary>
    /// 執行拉取 GitLab Pull Request 資訊任務
    /// 
    /// 注意：此為示範實作，實際使用時應透過命令列參數或組態檔指定專案 ID、時間區間等參數
    /// </summary>
    public async Task ExecuteAsync()
    {
        // 範例：拉取最近 7 天的 Merge Request
        // 實際應用中，這些參數應由外層（Console 層）注入
        const string exampleProjectId = "example/project";
        var endTime = _now.UtcNow;
        var startTime = endTime.AddDays(-7);
        
        // 情境 1: 拉取時間區間內的 MR
        var mergeRequestsByTime = await _gitLabRepository.FetchMergeRequestsByTimeRangeAsync(
            exampleProjectId,
            startTime,
            endTime,
            "merged");
        
        // 情境 2: 比較分支差異並拉取相關 MR
        const string sourceBranch = "develop";
        const string targetBranch = "main";
        
        var mergeRequestsByBranch = await _gitLabRepository.FetchMergeRequestsByBranchComparisonAsync(
            exampleProjectId,
            sourceBranch,
            targetBranch);

        // 此處可以進行後續處理，例如儲存至資料庫或輸出至檔案
        // 實際實作應根據業務需求調整
    }
}
