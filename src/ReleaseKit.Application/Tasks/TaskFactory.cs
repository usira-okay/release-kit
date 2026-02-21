using Microsoft.Extensions.DependencyInjection;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 任務工廠，使用工廠模式建立任務實例
/// </summary>
public class TaskFactory
{
    private readonly IServiceProvider _serviceProvider;

    public TaskFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// 根據任務類型建立任務實例
    /// </summary>
    /// <param name="taskType">任務類型</param>
    /// <returns>任務實例</returns>
    /// <exception cref="ArgumentException">當任務類型無效時拋出</exception>
    public ITask CreateTask(TaskType taskType)
    {
        return taskType switch
        {
            TaskType.FetchGitLabPullRequests => _serviceProvider.GetRequiredService<FetchGitLabPullRequestsTask>(),
            TaskType.FetchBitbucketPullRequests => _serviceProvider.GetRequiredService<FetchBitbucketPullRequestsTask>(),
            TaskType.FetchAzureDevOpsWorkItems => _serviceProvider.GetRequiredService<FetchAzureDevOpsWorkItemsTask>(),
            TaskType.UpdateGoogleSheets => _serviceProvider.GetRequiredService<UpdateGoogleSheetsTask>(),
            TaskType.FetchGitLabReleaseBranch => _serviceProvider.GetRequiredService<FetchGitLabReleaseBranchTask>(),
            TaskType.FetchBitbucketReleaseBranch => _serviceProvider.GetRequiredService<FetchBitbucketReleaseBranchTask>(),
            TaskType.FilterGitLabPullRequestsByUser => _serviceProvider.GetRequiredService<FilterGitLabPullRequestsByUserTask>(),
            TaskType.FilterBitbucketPullRequestsByUser => _serviceProvider.GetRequiredService<FilterBitbucketPullRequestsByUserTask>(),
            TaskType.GetUserStory => _serviceProvider.GetRequiredService<GetUserStoryTask>(),
            TaskType.ConsolidateReleaseData => _serviceProvider.GetRequiredService<ConsolidateReleaseDataTask>(),
            _ => throw new ArgumentException($"不支援的任務類型: {taskType}", nameof(taskType))
        };
    }
}
