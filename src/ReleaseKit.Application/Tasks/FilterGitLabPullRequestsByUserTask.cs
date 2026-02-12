using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 過濾 GitLab Pull Request 依使用者任務
/// </summary>
/// <remarks>
/// 從 Redis Key `GitLab:PullRequests` 讀取資料，依 UserMapping 的 GitLabUserId 過濾，
/// 將結果寫入 Redis Key `GitLab:PullRequests:ByUser`。
/// </remarks>
public class FilterGitLabPullRequestsByUserTask : BaseFilterPullRequestsByUserTask
{
    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="logger">日誌記錄器</param>
    /// <param name="redisService">Redis 服務</param>
    /// <param name="userMappingOptions">使用者對應設定</param>
    public FilterGitLabPullRequestsByUserTask(
        ILogger<FilterGitLabPullRequestsByUserTask> logger,
        IRedisService redisService,
        IOptions<UserMappingOptions> userMappingOptions)
        : base(
            logger,
            redisService,
            ExtractGitLabUserIds(userMappingOptions.Value))
    {
    }

    /// <inheritdoc />
    protected override string SourceRedisKey => RedisKeys.GitLabPullRequests;

    /// <inheritdoc />
    protected override string TargetRedisKey => RedisKeys.GitLabPullRequestsByUser;

    /// <inheritdoc />
    protected override string PlatformName => "GitLab";

    /// <summary>
    /// 從 UserMappingOptions 中提取 GitLab 使用者 ID 清單
    /// </summary>
    /// <param name="options">使用者對應設定</param>
    /// <returns>非空的 GitLab 使用者 ID 清單</returns>
    private static IReadOnlyList<string> ExtractGitLabUserIds(UserMappingOptions options)
    {
        return options.Mappings
            .Select(m => m.GitLabUserId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToList();
    }
}
