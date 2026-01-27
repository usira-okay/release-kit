using Moq;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Domain.Abstractions;

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
        mockNow.Setup(x => x.UtcNow).Returns(new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.Zero));
        
        mockGitLabRepository
            .Setup(x => x.FetchMergeRequestsByTimeRangeAsync(
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<string>()))
            .ReturnsAsync(new List<Domain.Entities.MergeRequest>());
        
        mockGitLabRepository
            .Setup(x => x.FetchMergeRequestsByBranchComparisonAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(new List<Domain.Entities.MergeRequest>());

        var task = new FetchGitLabPullRequestsTask(mockGitLabRepository.Object, mockNow.Object);

        // Act
        await task.ExecuteAsync();

        // Assert
        mockGitLabRepository.Verify(x => x.FetchMergeRequestsByTimeRangeAsync(
            It.IsAny<string>(),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<string>()), Times.Once);
        
        mockGitLabRepository.Verify(x => x.FetchMergeRequestsByBranchComparisonAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Once);
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
