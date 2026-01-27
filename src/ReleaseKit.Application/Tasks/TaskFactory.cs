using Microsoft.Extensions.DependencyInjection;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 任務類型列舉
/// </summary>
public enum TaskType
{
    /// <summary>
    /// 拉取 GitLab Pull Request 資訊
    /// </summary>
    FetchGitLabPullRequests,
    
    /// <summary>
    /// 拉取 Bitbucket Pull Request 資訊
    /// </summary>
    FetchBitbucketPullRequests,
    
    /// <summary>
    /// 拉取 Azure DevOps Work Item 資訊
    /// </summary>
    FetchAzureDevOpsWorkItems,
    
    /// <summary>
    /// 更新 Google Sheets 資訊
    /// </summary>
    UpdateGoogleSheets
}

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
            _ => throw new ArgumentException($"不支援的任務類型: {taskType}", nameof(taskType))
        };
    }
}
