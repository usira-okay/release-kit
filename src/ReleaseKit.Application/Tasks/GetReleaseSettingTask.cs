using Microsoft.Extensions.Logging;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Helpers;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 產生 Release Setting 設定任務
/// </summary>
/// <remarks>
/// 從 Redis 讀取前置指令產生的 release branch 資訊，
/// 依規則產生 GitLab 與 Bitbucket 的專案設定，並寫入 Redis。
/// </remarks>
public class GetReleaseSettingTask : ITask
{
    private readonly IRedisService _redisService;
    private readonly INow _now;
    private readonly ILogger<GetReleaseSettingTask> _logger;

    /// <summary>
    /// 超過此月數的 release branch 視為過期，退回 DateTimeRange 模式
    /// </summary>
    private const int ExpiredMonths = 3;

    /// <summary>
    /// GitLab 平台預設的目標分支
    /// </summary>
    private const string GitLabDefaultTargetBranch = "master";

    /// <summary>
    /// Bitbucket 平台預設的目標分支
    /// </summary>
    private const string BitbucketDefaultTargetBranch = "develop";

    /// <summary>
    /// 前置資料中無 release branch 的專案分組鍵值
    /// </summary>
    private const string NotFoundKey = "NotFound";

    public GetReleaseSettingTask(
        IRedisService redisService,
        INow now,
        ILogger<GetReleaseSettingTask> logger)
    {
        _redisService = redisService ?? throw new ArgumentNullException(nameof(redisService));
        _now = now ?? throw new ArgumentNullException(nameof(now));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 執行產生 Release Setting 設定任務
    /// </summary>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始執行 Release Setting 產生任務");

        // 讀取 GitLab release branch 資料
        var gitLabBranchData = await ReadReleaseBranchDataAsync(
            RedisKeys.GitLabHash, RedisKeys.Fields.ReleaseBranches, "GitLab");

        // 讀取 Bitbucket release branch 資料
        var bitbucketBranchData = await ReadReleaseBranchDataAsync(
            RedisKeys.BitbucketHash, RedisKeys.Fields.ReleaseBranches, "Bitbucket");

        // 產生設定
        var output = new ReleaseSettingOutput
        {
            GitLab = GeneratePlatformSetting(gitLabBranchData, GitLabDefaultTargetBranch, "GitLab"),
            Bitbucket = GeneratePlatformSetting(bitbucketBranchData, BitbucketDefaultTargetBranch, "Bitbucket")
        };

        // 序列化並輸出
        var json = output.ToJson();
        Console.WriteLine(json);

        // 清除舊資料並寫入 Redis
        if (await _redisService.ExistsAsync(RedisKeys.ReleaseSetting))
        {
            await _redisService.DeleteAsync(RedisKeys.ReleaseSetting);
            _logger.LogInformation("已清除 Redis 中的舊 Release Setting 資料");
        }

        await _redisService.SetAsync(RedisKeys.ReleaseSetting, json);
        _logger.LogInformation("Release Setting 已寫入 Redis，Key: {Key}", RedisKeys.ReleaseSetting);

        _logger.LogInformation(
            "Release Setting 產生完成，GitLab 專案數: {GitLabCount}，Bitbucket 專案數: {BitbucketCount}",
            output.GitLab.Projects.Count,
            output.Bitbucket.Projects.Count);
    }

    /// <summary>
    /// 從 Redis 讀取 release branch 資料
    /// </summary>
    private async Task<Dictionary<string, List<string>>?> ReadReleaseBranchDataAsync(
        string hashKey, string field, string platformName)
    {
        var json = await _redisService.HashGetAsync(hashKey, field);
        if (string.IsNullOrWhiteSpace(json))
        {
            _logger.LogInformation("{Platform} 無前置 release branch 資料，將產生空設定", platformName);
            return null;
        }

        var data = json.ToTypedObject<Dictionary<string, List<string>>>();
        _logger.LogInformation("{Platform} 讀取到 {Count} 個 release branch 分組", platformName, data?.Count ?? 0);
        return data;
    }

    /// <summary>
    /// 依 release branch 資料產生平台設定
    /// </summary>
    private PlatformSettingOutput GeneratePlatformSetting(
        Dictionary<string, List<string>>? branchData,
        string defaultTargetBranch,
        string platformName)
    {
        if (branchData == null || branchData.Count == 0)
        {
            return new PlatformSettingOutput { Projects = new List<ProjectSettingOutput>() };
        }

        var projects = new List<ProjectSettingOutput>();
        var cutoffDate = _now.UtcNow.AddMonths(-ExpiredMonths);

        foreach (var (branchName, projectPaths) in branchData)
        {
            var (fetchMode, sourceBranch) = DetermineFetchMode(branchName, cutoffDate);

            foreach (var projectPath in projectPaths)
            {
                var project = new ProjectSettingOutput
                {
                    ProjectPath = projectPath,
                    TargetBranch = defaultTargetBranch,
                    FetchMode = fetchMode,
                    SourceBranch = sourceBranch,
                    StartDateTime = null,
                    EndDateTime = null
                };

                projects.Add(project);

                _logger.LogInformation(
                    "{Platform} 專案 {ProjectPath}: FetchMode={FetchMode}, SourceBranch={SourceBranch}, TargetBranch={TargetBranch}",
                    platformName,
                    projectPath,
                    fetchMode,
                    sourceBranch ?? "(null)",
                    defaultTargetBranch);
            }
        }

        return new PlatformSettingOutput { Projects = projects };
    }

    /// <summary>
    /// 依 release branch 名稱判斷 FetchMode
    /// </summary>
    private static (FetchMode fetchMode, string? sourceBranch) DetermineFetchMode(
        string branchName, DateTimeOffset cutoffDate)
    {
        // 規則 1：NotFound 專案
        if (branchName == NotFoundKey)
        {
            return (FetchMode.DateTimeRange, null);
        }

        // 規則 2：格式不符合 release/yyyyMMdd
        if (!ReleaseBranchHelper.IsReleaseBranch(branchName))
        {
            return (FetchMode.DateTimeRange, null);
        }

        // 規則 3：日期超過 3 個月
        var branchDate = ReleaseBranchHelper.ParseReleaseBranchDate(branchName);
        if (branchDate.HasValue && branchDate.Value < cutoffDate)
        {
            return (FetchMode.DateTimeRange, null);
        }

        // 規則 4：其餘使用 BranchDiff
        return (FetchMode.BranchDiff, branchName);
    }
}
