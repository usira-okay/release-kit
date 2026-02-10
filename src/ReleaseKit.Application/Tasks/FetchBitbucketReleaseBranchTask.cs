using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 取得 Bitbucket 各專案最新 Release Branch 任務
/// </summary>
public class FetchBitbucketReleaseBranchTask : BaseFetchReleaseBranchTask<BitbucketOptions, BitbucketProjectOptions>
{
    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="serviceProvider">服務提供者</param>
    /// <param name="logger">日誌記錄器</param>
    /// <param name="redisService">Redis 快取服務</param>
    /// <param name="bitbucketOptions">Bitbucket 配置選項</param>
    public FetchBitbucketReleaseBranchTask(
        IServiceProvider serviceProvider,
        ILogger<FetchBitbucketReleaseBranchTask> logger,
        IRedisService redisService,
        IOptions<BitbucketOptions> bitbucketOptions)
        : base(
            serviceProvider.GetRequiredKeyedService<ISourceControlRepository>("Bitbucket"),
            logger,
            redisService,
            bitbucketOptions.Value)
    {
    }

    /// <inheritdoc />
    protected override string PlatformName => "Bitbucket";

    /// <inheritdoc />
    protected override string RedisKey => RedisKeys.BitbucketReleaseBranches;

    /// <inheritdoc />
    protected override IEnumerable<BitbucketProjectOptions> GetProjects() => PlatformOptions.Projects;
}
