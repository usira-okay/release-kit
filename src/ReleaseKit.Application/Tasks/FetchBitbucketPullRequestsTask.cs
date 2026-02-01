using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 拉取 Bitbucket Pull Request 資訊任務
/// </summary>
public class FetchBitbucketPullRequestsTask : BaseFetchPullRequestsTask<BitbucketOptions, BitbucketProjectOptions>
{
    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="serviceProvider">服務提供者</param>
    /// <param name="logger">日誌記錄器</param>
    /// <param name="bitbucketOptions">Bitbucket 配置選項</param>
    /// <param name="fetchModeOptions">拉取模式配置選項</param>
    public FetchBitbucketPullRequestsTask(
        IServiceProvider serviceProvider,
        ILogger<FetchBitbucketPullRequestsTask> logger,
        IOptions<BitbucketOptions> bitbucketOptions,
        IOptions<FetchModeOptions> fetchModeOptions)
        : base(
            serviceProvider.GetRequiredKeyedService<ISourceControlRepository>("Bitbucket"),
            logger,
            bitbucketOptions.Value,
            fetchModeOptions)
    {
    }

    /// <inheritdoc />
    protected override string PlatformName => "Bitbucket";

    /// <inheritdoc />
    protected override SourceControlPlatform Platform => SourceControlPlatform.Bitbucket;

    /// <inheritdoc />
    protected override IEnumerable<BitbucketProjectOptions> GetProjects() => PlatformOptions.Projects;
}
