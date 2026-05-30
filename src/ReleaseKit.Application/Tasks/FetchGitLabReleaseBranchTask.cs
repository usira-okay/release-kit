using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 取得 GitLab 各專案最新 Release Branch 任務
/// </summary>
public class FetchGitLabReleaseBranchTask : BaseFetchReleaseBranchTask<GitLabOptions, GitLabProjectOptions>
{
    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="serviceProvider">服務提供者</param>
    /// <param name="logger">日誌記錄器</param>
    /// <param name="dataTransferService">資料傳遞服務</param>
    /// <param name="gitLabOptions">GitLab 配置選項</param>
    public FetchGitLabReleaseBranchTask(
        IServiceProvider serviceProvider,
        ILogger<FetchGitLabReleaseBranchTask> logger,
        IDataTransferService dataTransferService,
        IOptions<GitLabOptions> gitLabOptions)
        : base(
            serviceProvider.GetRequiredKeyedService<ISourceControlRepository>("GitLab"),
            logger,
            dataTransferService,
            gitLabOptions.Value)
    {
    }

    /// <inheritdoc />
    protected override string PlatformName => "GitLab";

    /// <inheritdoc />
    protected override string DataTransferGroupKey => DataTransferKeys.GitLabHash;

    /// <inheritdoc />
    protected override string DataTransferGroupField => DataTransferKeys.Fields.ReleaseBranches;

    /// <inheritdoc />
    protected override IEnumerable<GitLabProjectOptions> GetProjects() => PlatformOptions.Projects;
}
