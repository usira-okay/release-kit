using ReleaseKit.Application.Tasks;

namespace ReleaseKit.Application.Tests.Tasks;

/// <summary>
/// 任務類別單元測試
/// </summary>
public class TasksTests
{
    [Fact]
    public async Task FetchGitLabPullRequestsTask_ExecuteAsync_ShouldThrowNotImplementedException()
    {
        // Arrange
        var task = new FetchGitLabPullRequestsTask();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotImplementedException>(() => task.ExecuteAsync());
        Assert.Contains("拉取 GitLab Pull Request 資訊功能尚未實作", exception.Message);
    }

    [Fact]
    public async Task FetchBitbucketPullRequestsTask_ExecuteAsync_ShouldThrowNotImplementedException()
    {
        // Arrange
        var task = new FetchBitbucketPullRequestsTask();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotImplementedException>(() => task.ExecuteAsync());
        Assert.Contains("拉取 Bitbucket Pull Request 資訊功能尚未實作", exception.Message);
    }

    [Fact]
    public async Task FetchAzureDevOpsWorkItemsTask_ExecuteAsync_ShouldThrowNotImplementedException()
    {
        // Arrange
        var task = new FetchAzureDevOpsWorkItemsTask();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotImplementedException>(() => task.ExecuteAsync());
        Assert.Contains("拉取 Azure DevOps Work Item 資訊功能尚未實作", exception.Message);
    }

    [Fact]
    public async Task UpdateGoogleSheetsTask_ExecuteAsync_ShouldThrowNotImplementedException()
    {
        // Arrange
        var task = new UpdateGoogleSheetsTask();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotImplementedException>(() => task.ExecuteAsync());
        Assert.Contains("更新 Google Sheets 資訊功能尚未實作", exception.Message);
    }
}
