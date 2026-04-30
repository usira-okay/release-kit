using Microsoft.Extensions.Logging;
using ReleaseKit.Application.Common;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Entities;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// Stage 2：分析 PR/MR Diff
/// </summary>
/// <remarks>
/// 從 Redis 讀取 Stage 1 clone 結果與 PR 資料，對每個成功 clone 的專案執行
/// git diff --shortstat 與 --name-status，並將 <see cref="ProjectDiffResult"/> 寫入 Stage 2 Redis Hash。
/// 僅儲存輕量 metadata（CommitSha、異動檔案清單、行數統計），不儲存完整 diff 內容。
/// </remarks>
public class AnalyzePRDiffsTask : ITask
{
    private readonly IGitOperationService _gitService;
    private readonly IRedisService _redisService;
    private readonly ILogger<AnalyzePRDiffsTask> _logger;

    /// <summary>
    /// 初始化 <see cref="AnalyzePRDiffsTask"/> 類別的新執行個體
    /// </summary>
    public AnalyzePRDiffsTask(
        IGitOperationService gitService,
        IRedisService redisService,
        ILogger<AnalyzePRDiffsTask> logger)
    {
        _gitService = gitService;
        _redisService = redisService;
        _logger = logger;
    }

    /// <summary>
    /// 執行 Stage 2：讀取 PR 資料並收集每個 Merge Commit 的異動摘要
    /// </summary>
    public async Task ExecuteAsync()
    {
        var runId = await _redisService.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey);
        if (string.IsNullOrEmpty(runId))
        {
            _logger.LogWarning("找不到 RunId，請先執行 Stage 1");
            return;
        }

        _logger.LogInformation("開始 Stage 2: 分析 PR Diffs, RunId={RunId}", runId);

        var allMergeRequests = await LoadAllMergeRequestsAsync();

        foreach (var (projectPath, mergeRequests) in allMergeRequests)
        {
            var cloneJson = await _redisService.HashGetAsync(RiskAnalysisRedisKeys.Stage1Hash(runId), projectPath);
            if (string.IsNullOrEmpty(cloneJson))
            {
                _logger.LogWarning("專案 {ProjectPath} 無 Stage 1 clone 記錄，跳過", projectPath);
                continue;
            }

            await AnalyzeProjectDiffsAsync(runId, projectPath, cloneJson, mergeRequests);
        }

        _logger.LogInformation("Stage 2 完成, RunId={RunId}", runId);
    }

    /// <summary>
    /// 從 Redis 載入 GitLab 與 Bitbucket 所有 PR 資料，以專案路徑為 Key 合併
    /// </summary>
    private async Task<Dictionary<string, List<MergeRequestOutput>>> LoadAllMergeRequestsAsync()
    {
        var result = new Dictionary<string, List<MergeRequestOutput>>();

        var gitLabJson = await _redisService.HashGetAsync(RedisKeys.GitLabHash, RedisKeys.Fields.PullRequests);
        if (!string.IsNullOrEmpty(gitLabJson))
        {
            var gitLabFetchResult = gitLabJson.ToTypedObject<FetchResult>();
            MergeFetchResultInto(result, gitLabFetchResult);
        }

        var bbJson = await _redisService.HashGetAsync(RedisKeys.BitbucketHash, RedisKeys.Fields.PullRequests);
        if (!string.IsNullOrEmpty(bbJson))
        {
            var bbFetchResult = bbJson.ToTypedObject<FetchResult>();
            MergeFetchResultInto(result, bbFetchResult);
        }

        return result;
    }

    /// <summary>
    /// 將 FetchResult 中各專案的 PR 資料合併至目標字典
    /// </summary>
    private void MergeFetchResultInto(
        Dictionary<string, List<MergeRequestOutput>> target,
        FetchResult? fetchResult)
    {
        if (fetchResult?.Results == null) return;

        foreach (var projectResult in fetchResult.Results)
        {
            if (!string.IsNullOrWhiteSpace(projectResult.Error))
            {
                _logger.LogWarning("專案 {ProjectPath} 擷取失敗（Error: {Error}），跳過",
                    projectResult.ProjectPath, projectResult.Error);
                continue;
            }

            if (target.TryGetValue(projectResult.ProjectPath, out var existing))
                existing.AddRange(projectResult.PullRequests);
            else
                target[projectResult.ProjectPath] = projectResult.PullRequests.ToList();
        }
    }

    /// <summary>
    /// 分析單一專案的所有 PR，收集每個 Merge Commit 的異動摘要，並寫入 Stage 2 Hash
    /// </summary>
    private async Task AnalyzeProjectDiffsAsync(
        string runId,
        string projectPath,
        string cloneJson,
        List<MergeRequestOutput> mergeRequests)
    {
        var cloneResult = cloneJson.ToTypedObject<CloneStageResult>();
        if (cloneResult?.Status != "Success")
        {
            _logger.LogWarning("專案 {ProjectPath} clone 失敗，跳過 diff 分析", projectPath);
            return;
        }

        if (mergeRequests.Count == 0)
        {
            _logger.LogInformation("專案 {ProjectPath} 無 PR 資料，跳過", projectPath);
            return;
        }

        var commitSummaries = new List<CommitSummary>();

        foreach (var mr in mergeRequests)
        {
            if (string.IsNullOrEmpty(mr.MergeCommitSha))
            {
                _logger.LogWarning("PR {PrId} 無 MergeCommitSha，跳過: {Title}", mr.PrId, mr.Title);
                continue;
            }

            var statResult = await _gitService.GetCommitStatAsync(cloneResult.LocalPath, mr.MergeCommitSha);
            if (statResult.IsSuccess)
            {
                commitSummaries.Add(statResult.Value);
            }
            else
            {
                _logger.LogWarning("取得 commit 統計失敗: {ProjectPath} commit {Sha} - {Error}",
                    projectPath, mr.MergeCommitSha, statResult.Error!.Message);
            }
        }

        var projectDiffResult = new ProjectDiffResult
        {
            ProjectPath = projectPath,
            CommitSummaries = commitSummaries
        };

        await _redisService.HashSetAsync(
            RiskAnalysisRedisKeys.Stage2Hash(runId),
            projectPath,
            projectDiffResult.ToJson());

        _logger.LogInformation("專案 {ProjectPath} 統計完成: {CommitCount} 個 commit, {TotalLines} 行異動",
            projectPath, commitSummaries.Count,
            commitSummaries.Sum(c => c.TotalLinesAdded + c.TotalLinesRemoved));
    }

    /// <summary>
    /// Stage 1 clone 結果的反序列化 DTO
    /// </summary>
    private sealed record CloneStageResult
    {
        /// <summary>本地路徑</summary>
        public string LocalPath { get; init; } = "";

        /// <summary>clone 狀態（"Success" 或 "Failed: ..."）</summary>
        public string Status { get; init; } = "";
    }
}
