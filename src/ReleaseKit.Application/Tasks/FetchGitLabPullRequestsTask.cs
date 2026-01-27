using Microsoft.Extensions.Logging;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 拉取 GitLab Pull Request 資訊任務
/// </summary>
public class FetchGitLabPullRequestsTask : ITask
{
    private readonly IGitLabRepository _gitLabRepository;
    private readonly INow _now;
    private readonly GitLabSettings _gitLabSettings;
    private readonly ILogger<FetchGitLabPullRequestsTask> _logger;

    public FetchGitLabPullRequestsTask(
        IGitLabRepository gitLabRepository,
        INow now,
        GitLabSettings gitLabSettings,
        ILogger<FetchGitLabPullRequestsTask> logger)
    {
        _gitLabRepository = gitLabRepository ?? throw new ArgumentNullException(nameof(gitLabRepository));
        _now = now ?? throw new ArgumentNullException(nameof(now));
        _gitLabSettings = gitLabSettings ?? throw new ArgumentNullException(nameof(gitLabSettings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 執行拉取 GitLab Pull Request 資訊任務
    /// </summary>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始執行拉取 GitLab Pull Request 資訊任務");

        if (_gitLabSettings.Projects.Count == 0)
        {
            _logger.LogWarning("未設定任何 GitLab 專案，跳過任務執行");
            return;
        }

        foreach (var project in _gitLabSettings.Projects)
        {
            _logger.LogInformation("處理專案: {ProjectId}", project.ProjectId);

            // 情境 1: 使用工廠方法建立時間區間拉取請求
            var endTime = _now.UtcNow;
            var startTime = endTime.AddDays(-7);
            
            var dateTimeRangeRequest = GitLabFetchRequestFactory.CreateDateTimeRangeRequest(
                project.ProjectId,
                startTime,
                endTime,
                "merged");
            
            _logger.LogInformation(
                "情境 1: 拉取時間區間內的 MR，專案: {ProjectId}, 時間區間: {StartTime} ~ {EndTime}",
                project.ProjectId, startTime, endTime);
            
            var mergeRequestsByTime = await _gitLabRepository.FetchMergeRequestsAsync(dateTimeRangeRequest);
            
            _logger.LogInformation("拉取到 {Count} 筆 MR 資料（時間區間）", mergeRequestsByTime.Count);
            
            foreach (var mr in mergeRequestsByTime)
            {
                _logger.LogInformation(
                    "MR #{Number}: {Title} ({State}) by {Author} (ID: {AuthorId}) - {Url}",
                    mr.Number, mr.Title, mr.State, mr.Author, mr.AuthorId, mr.WebUrl);
            }

            // 情境 2: 使用工廠方法建立分支差異拉取請求
            const string sourceBranch = "develop";
            
            var branchDiffRequest = GitLabFetchRequestFactory.CreateBranchDiffRequest(
                project.ProjectId,
                sourceBranch,
                project.TargetBranch);
            
            _logger.LogInformation(
                "情境 2: 比較分支差異並拉取相關 MR，專案: {ProjectId}, 來源: {SourceBranch}, 目標: {TargetBranch}",
                project.ProjectId, sourceBranch, project.TargetBranch);
            
            var mergeRequestsByBranch = await _gitLabRepository.FetchMergeRequestsAsync(branchDiffRequest);
            
            _logger.LogInformation("拉取到 {Count} 筆 MR 資料（分支比較）", mergeRequestsByBranch.Count);
            
            foreach (var mr in mergeRequestsByBranch)
            {
                _logger.LogInformation(
                    "MR #{Number}: {Title} ({State}) by {Author} (ID: {AuthorId}) - {Url}",
                    mr.Number, mr.Title, mr.State, mr.Author, mr.AuthorId, mr.WebUrl);
            }
        }

        _logger.LogInformation("拉取 GitLab Pull Request 資訊任務執行完成");
    }
}
