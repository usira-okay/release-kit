using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ReleaseKit.Application.Configuration;
using ReleaseKit.Application.Tasks;

namespace ReleaseKit.Application.Tests.Tasks;

/// <summary>
/// 任務類別單元測試
/// </summary>
public class TasksTests
{
    [Fact]
    public async Task FetchGitLabPullRequestsTask_ExecuteAsync_WithEmptyProjects_ShouldCompleteSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        var gitLabOptions = Options.Create(new GitLabOptions { Projects = new List<GitLabProjectOptions>() });
        var fetchModeOptions = Options.Create(new FetchModeOptions());
        
        // Mock logger
        var loggerMock = new Mock<ILogger<FetchGitLabPullRequestsTask>>();
        
        // Mock keyed repository service
        var repositoryMock = new Mock<ReleaseKit.Domain.Abstractions.ISourceControlRepository>();
        
        services.AddSingleton(gitLabOptions);
        services.AddSingleton(fetchModeOptions);
        services.AddSingleton(loggerMock.Object);
        services.AddKeyedSingleton("GitLab", repositoryMock.Object);
        
        var serviceProvider = services.BuildServiceProvider();

        var task = new FetchGitLabPullRequestsTask(
            serviceProvider,
            loggerMock.Object,
            gitLabOptions,
            fetchModeOptions);

        // Act
        await task.ExecuteAsync();

        // Assert - should complete without exception
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("開始執行 GitLab Pull Request 拉取任務")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
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
