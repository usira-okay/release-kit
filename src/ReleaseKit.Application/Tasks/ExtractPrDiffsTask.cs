using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ReleaseKit.Application.Common;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Entities;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 從已 Clone 的 Repository 中擷取每個 PR 的 Diff 資訊
/// </summary>
/// <remarks>
/// 從 Redis 讀取 GitLab 與 Bitbucket 的 PR 資料及 Clone 路徑對照表，
/// 對每個 PR 嘗試取得 Branch diff（主要策略），若失敗則透過 Merge commit 取得 diff（備援策略），
/// 最後將結果以 <see cref="PrDiffContext"/> 格式寫入 Redis。
/// </remarks>
public class ExtractPrDiffsTask : ITask
{
    private readonly IRedisService _redisService;
    private readonly IGitService _gitService;
    private readonly ILogger<ExtractPrDiffsTask> _logger;

    /// <summary>
    /// 比對 diff --git a/路徑 的正則表達式
    /// </summary>
    private static readonly Regex DiffFileRegex = new(
        @"^diff --git a/(.+?) b/",
        RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>
    /// 初始化 <see cref="ExtractPrDiffsTask"/> 類別的新執行個體
    /// </summary>
    /// <param name="redisService">Redis 服務</param>
    /// <param name="gitService">Git 操作服務</param>
    /// <param name="logger">日誌記錄器</param>
    public ExtractPrDiffsTask(
        IRedisService redisService,
        IGitService gitService,
        ILogger<ExtractPrDiffsTask> logger)
    {
        _redisService = redisService;
        _gitService = gitService;
        _logger = logger;
    }

    /// <summary>
    /// 執行 PR Diff 擷取任務
    /// </summary>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始擷取 PR Diff 資訊");

        var gitLabJson = await _redisService.HashGetAsync(
            RedisKeys.GitLabHash, RedisKeys.Fields.PullRequestsByUser);
        var bitbucketJson = await _redisService.HashGetAsync(
            RedisKeys.BitbucketHash, RedisKeys.Fields.PullRequestsByUser);
        var clonePathsJson = await _redisService.HashGetAsync(
            RedisKeys.RiskAnalysisHash, RedisKeys.Fields.ClonePaths);

        var gitLabFetch = gitLabJson?.ToTypedObject<FetchResult>();
        var bitbucketFetch = bitbucketJson?.ToTypedObject<FetchResult>();
        var clonePaths = clonePathsJson?.ToTypedObject<Dictionary<string, string>>()
                         ?? new Dictionary<string, string>();

        var allProjects = new List<ProjectResult>();

        if (gitLabFetch?.Results is not null)
            allProjects.AddRange(gitLabFetch.Results);

        if (bitbucketFetch?.Results is not null)
            allProjects.AddRange(bitbucketFetch.Results);

        var diffsByProject = new Dictionary<string, List<PrDiffContext>>();

        foreach (var project in allProjects)
        {
            if (!clonePaths.TryGetValue(project.ProjectPath, out var clonePath))
            {
                _logger.LogWarning("找不到 {ProjectPath} 的 Clone 路徑，跳過", project.ProjectPath);
                continue;
            }

            var projectDiffs = new List<PrDiffContext>();

            foreach (var pr in project.PullRequests)
            {
                var diffContent = await GetDiffContentAsync(clonePath, pr);

                if (diffContent is null)
                    continue;

                var changedFiles = ParseChangedFiles(diffContent);

                projectDiffs.Add(new PrDiffContext
                {
                    Title = pr.Title,
                    Description = pr.Description,
                    SourceBranch = pr.SourceBranch,
                    TargetBranch = pr.TargetBranch,
                    AuthorName = pr.AuthorName,
                    PrUrl = pr.PRUrl,
                    DiffContent = diffContent,
                    ChangedFiles = changedFiles,
                    Platform = project.Platform
                });
            }

            if (projectDiffs.Count > 0)
                diffsByProject[project.ProjectPath] = projectDiffs;
        }

        var json = diffsByProject.ToJson();
        await _redisService.HashSetAsync(RedisKeys.RiskAnalysisHash, RedisKeys.Fields.PrDiffs, json);

        _logger.LogInformation("PR Diff 擷取完成，共處理 {Count} 個專案",
            diffsByProject.Count);
    }

    /// <summary>
    /// 透過 Merge commit 取得 PR 的 diff 內容
    /// </summary>
    private async Task<string?> GetDiffContentAsync(string clonePath, MergeRequestOutput pr)
    {
        var mergeCommitResult = await _gitService.FindMergeCommitAsync(
            clonePath, pr.SourceBranch);

        if (mergeCommitResult.IsFailure)
        {
            _logger.LogWarning("找不到 Merge commit：{Title}（{Source}）",
                pr.Title, pr.SourceBranch);
            return null;
        }

        var commitDiffResult = await _gitService.GetCommitDiffAsync(
            clonePath, mergeCommitResult.Value!);

        if (commitDiffResult.IsSuccess)
            return commitDiffResult.Value;

        _logger.LogWarning("無法取得 PR diff：{Title}（{Source}），Commit diff 擷取失敗",
            pr.Title, pr.SourceBranch);
        return null;
    }

    /// <summary>
    /// 從 diff 內容解析變更的檔案清單
    /// </summary>
    internal static IReadOnlyList<string> ParseChangedFiles(string diffContent)
    {
        var matches = DiffFileRegex.Matches(diffContent);
        return matches
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();
    }
}
