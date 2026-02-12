using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 過濾 Bitbucket Pull Request 依使用者任務
/// </summary>
/// <remarks>
/// 從 Redis Key `Bitbucket:PullRequests` 讀取資料，依 UserMapping 的 BitbucketUserId 過濾，
/// 將結果寫入 Redis Key `Bitbucket:PullRequests:ByUser`。
/// </remarks>
public class FilterBitbucketPullRequestsByUserTask : BaseFilterPullRequestsByUserTask
{
    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="logger">日誌記錄器</param>
    /// <param name="redisService">Redis 服務</param>
    /// <param name="userMappingOptions">使用者對應設定</param>
    public FilterBitbucketPullRequestsByUserTask(
        ILogger<FilterBitbucketPullRequestsByUserTask> logger,
        IRedisService redisService,
        IOptions<UserMappingOptions> userMappingOptions)
        : base(
            logger,
            redisService,
            ExtractBitbucketUserIds(userMappingOptions.Value),
            ExtractBitbucketUserIdToDisplayName(userMappingOptions.Value))
    {
    }

    /// <inheritdoc />
    protected override string SourceRedisKey => RedisKeys.BitbucketPullRequests;

    /// <inheritdoc />
    protected override string TargetRedisKey => RedisKeys.BitbucketPullRequestsByUser;

    /// <inheritdoc />
    protected override string PlatformName => "Bitbucket";

    /// <summary>
    /// 從 UserMappingOptions 中提取 Bitbucket 使用者 ID 清單
    /// </summary>
    /// <param name="options">使用者對應設定</param>
    /// <returns>非空的 Bitbucket 使用者 ID 清單</returns>
    private static IReadOnlyList<string> ExtractBitbucketUserIds(UserMappingOptions options)
    {
        return options.Mappings
            .Select(m => m.BitbucketUserId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToList();
    }

    /// <summary>
    /// 從 UserMappingOptions 中提取 Bitbucket 使用者 ID 與 DisplayName 的對應字典
    /// </summary>
    /// <param name="options">使用者對應設定</param>
    /// <returns>Bitbucket 使用者 ID 與 DisplayName 的對應字典</returns>
    private static IReadOnlyDictionary<string, string> ExtractBitbucketUserIdToDisplayName(UserMappingOptions options)
    {
        return options.Mappings
            .Where(m => !string.IsNullOrWhiteSpace(m.BitbucketUserId) && !string.IsNullOrWhiteSpace(m.DisplayName))
            .ToDictionary(m => m.BitbucketUserId, m => m.DisplayName);
    }
}
