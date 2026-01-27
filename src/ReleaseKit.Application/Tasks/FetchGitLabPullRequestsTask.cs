using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 拉取 GitLab Pull Request 資訊任務
/// </summary>
public class FetchGitLabPullRequestsTask : ITask
{
    /// <summary>
    /// 執行拉取 GitLab Pull Request 資訊任務
    /// </summary>
    public Task ExecuteAsync()
    {
        throw new NotImplementedException("拉取 GitLab Pull Request 資訊功能尚未實作");
    }
}
