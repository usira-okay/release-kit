using Microsoft.Extensions.Logging;
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
/// git diff，並將 <see cref="ProjectDiffResult"/> 寫入 Stage 2 Redis Hash。
/// </remarks>
public class AnalyzePRDiffsTask : ITask
{
    private readonly IGitOperationService _gitService;
    private readonly IRedisService _redisService;
    private readonly ILogger<AnalyzePRDiffsTask> _logger;

    /// <summary>
    /// 初始化 <see cref="AnalyzePRDiffsTask"/> 類別的新執行個體
    /// </summary>
    /// <param name="gitService">Git 操作服務</param>
    /// <param name="redisService">Redis 快取服務</param>
    /// <param name="logger">日誌記錄器</param>
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
    /// 執行 Stage 2：讀取 PR 資料並分析每個 Merge Commit 的 diff
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
    private async Task<Dictionary<string, List<MergeRequest>>> LoadAllMergeRequestsAsync()
    {
        var result = new Dictionary<string, List<MergeRequest>>();

        var gitLabJson = await _redisService.HashGetAsync(RedisKeys.GitLabHash, RedisKeys.Fields.PullRequests);
        if (!string.IsNullOrEmpty(gitLabJson))
        {
            var gitLabPrs = gitLabJson.ToTypedObject<Dictionary<string, List<MergeRequest>>>();
            if (gitLabPrs != null)
            {
                foreach (var kvp in gitLabPrs)
                    result[kvp.Key] = kvp.Value;
            }
        }

        var bbJson = await _redisService.HashGetAsync(RedisKeys.BitbucketHash, RedisKeys.Fields.PullRequests);
        if (!string.IsNullOrEmpty(bbJson))
        {
            var bbPrs = bbJson.ToTypedObject<Dictionary<string, List<MergeRequest>>>();
            if (bbPrs != null)
            {
                foreach (var kvp in bbPrs)
                {
                    if (result.TryGetValue(kvp.Key, out var existing))
                        existing.AddRange(kvp.Value);
                    else
                        result[kvp.Key] = kvp.Value;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 分析單一專案的所有 PR Diff，並將 <see cref="ProjectDiffResult"/> 寫入 Stage 2 Hash
    /// </summary>
    /// <param name="runId">本次執行 ID</param>
    /// <param name="projectPath">專案路徑</param>
    /// <param name="cloneJson">Stage 1 clone 結果 JSON</param>
    /// <param name="mergeRequests">該專案的 MR 清單</param>
    private async Task AnalyzeProjectDiffsAsync(
        string runId,
        string projectPath,
        string cloneJson,
        List<MergeRequest> mergeRequests)
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

        var allDiffs = new List<FileDiff>();

        foreach (var mr in mergeRequests)
        {
            if (string.IsNullOrEmpty(mr.MergeCommitSha))
            {
                _logger.LogWarning("PR {PrId} 無 MergeCommitSha，跳過: {Title}", mr.PrId, mr.Title);
                continue;
            }

            var diffResult = await _gitService.GetCommitDiffAsync(cloneResult.LocalPath, mr.MergeCommitSha);
            if (diffResult.IsSuccess)
            {
                allDiffs.AddRange(diffResult.Value);
            }
            else
            {
                _logger.LogWarning("取得 diff 失敗: {ProjectPath} commit {Sha} - {Error}",
                    projectPath, mr.MergeCommitSha, diffResult.Error!.Message);
            }
        }

        var projectDiffResult = new ProjectDiffResult
        {
            ProjectPath = projectPath,
            FileDiffs = allDiffs
        };

        await _redisService.HashSetAsync(
            RiskAnalysisRedisKeys.Stage2Hash(runId),
            projectPath,
            projectDiffResult.ToJson());

        _logger.LogInformation("專案 {ProjectPath} 分析完成: {DiffCount} 個異動檔案, {MRCount} 個 PR",
            projectPath, allDiffs.Count, mergeRequests.Count);
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
