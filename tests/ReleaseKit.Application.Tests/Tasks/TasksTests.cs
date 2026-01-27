using Microsoft.Extensions.Logging;
using Moq;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Application.Tests.Tasks;

/// <summary>
/// 任務類別單元測試
/// </summary>
public class TasksTests
{
    [Fact]
    public async Task FetchGitLabPullRequestsTask_ExecuteAsync_ShouldCallRepository()
    {
        // Arrange
        var mockGitLabRepository = new Mock<IGitLabRepository>();
        var mockNow = new Mock<INow>();
        var mockLogger = new Mock<ILogger<FetchGitLabPullRequestsTask>>();
        mockNow.Setup(x => x.UtcNow).Returns(new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.Zero));
        
        var gitLabSettings = new GitLabSettings
        {
            Domain = "https://gitlab.com",
            AccessToken = "test-token",
            Projects = new List<GitLabProjectSettings>
            {
                new GitLabProjectSettings
                {
                    ProjectId = "test/project",
                    TargetBranch = "main"
                }
            }
        };
        
        mockGitLabRepository
            .Setup(x => x.FetchMergeRequestsAsync(It.IsAny<IGitLabFetchRequest>()))
            .ReturnsAsync(new List<Domain.Entities.MergeRequest>());

        var task = new FetchGitLabPullRequestsTask(
            mockGitLabRepository.Object, 
            mockNow.Object, 
            gitLabSettings,
            mockLogger.Object);

        // Act
        await task.ExecuteAsync();

        // Assert
        mockGitLabRepository.Verify(x => x.FetchMergeRequestsAsync(
            It.IsAny<IGitLabFetchRequest>()), Times.Exactly(2));
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
