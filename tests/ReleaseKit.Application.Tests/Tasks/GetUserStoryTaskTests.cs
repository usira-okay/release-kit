using Microsoft.Extensions.Logging;
using Moq;
using ReleaseKit.Application.Common;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Entities;

namespace ReleaseKit.Application.Tests.Tasks;

/// <summary>
/// GetUserStoryTask 單元測試
/// </summary>
public class GetUserStoryTaskTests
{
    private readonly Mock<ILogger<GetUserStoryTask>> _loggerMock;
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly Mock<IAzureDevOpsRepository> _azureDevOpsRepositoryMock;

    public GetUserStoryTaskTests()
    {
        _loggerMock = new Mock<ILogger<GetUserStoryTask>>();
        _redisServiceMock = new Mock<IRedisService>();
        _azureDevOpsRepositoryMock = new Mock<IAzureDevOpsRepository>();
    }

    [Fact]
    public async Task ExecuteAsync_WithUserStoryWorkItem_ShouldMarkAsAlreadyUserStoryOrAbove()
    {
        // Arrange
        var workItemFetchResult = new WorkItemFetchResult
        {
            WorkItems = new List<WorkItemOutput>
            {
                new WorkItemOutput
                {
                    WorkItemId = 12345,
                    Title = "實作使用者登入功能",
                    Type = "User Story",
                    State = "Active",
                    Url = "https://dev.azure.com/org/project/_workitems/edit/12345",
                    OriginalTeamName = "TeamA",
                    IsSuccess = true,
                    ErrorMessage = null
                }
            },
            TotalPRsAnalyzed = 1,
            TotalWorkItemsFound = 1,
            SuccessCount = 1,
            FailureCount = 0
        };

        _redisServiceMock
            .Setup(r => r.GetAsync(RedisKeys.AzureDevOpsWorkItems))
            .ReturnsAsync(workItemFetchResult.ToJson());

        var task = new GetUserStoryTask(
            _loggerMock.Object,
            _redisServiceMock.Object,
            _azureDevOpsRepositoryMock.Object);

        // Act
        await task.ExecuteAsync();

        // Assert
        _redisServiceMock.Verify(r => r.SetAsync(
            RedisKeys.AzureDevOpsUserStories,
            It.Is<string>(json =>
                json.Contains("\"resolutionStatus\":\"alreadyUserStoryOrAbove\"") ||
                json.Contains("\"resolutionStatus\":0")),
            null), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithTaskHavingUserStoryParent_ShouldMarkAsFoundViaRecursion()
    {
        // Arrange
        var workItemFetchResult = new WorkItemFetchResult
        {
            WorkItems = new List<WorkItemOutput>
            {
                new WorkItemOutput
                {
                    WorkItemId = 67890,
                    Title = "修復登入頁面 CSS 問題",
                    Type = "Bug",
                    State = "Active",
                    Url = "https://dev.azure.com/org/project/_workitems/edit/67890",
                    OriginalTeamName = "TeamA",
                    IsSuccess = true,
                    ErrorMessage = null
                }
            },
            TotalPRsAnalyzed = 1,
            TotalWorkItemsFound = 1,
            SuccessCount = 1,
            FailureCount = 0
        };

        _redisServiceMock
            .Setup(r => r.GetAsync(RedisKeys.AzureDevOpsWorkItems))
            .ReturnsAsync(workItemFetchResult.ToJson());

        // Mock the Bug work item (with Parent ID)
        var bugWorkItem = new WorkItem
        {
            WorkItemId = 67890,
            Title = "修復登入頁面 CSS 問題",
            Type = "Bug",
            State = "Active",
            Url = "https://dev.azure.com/org/project/_workitems/edit/67890",
            OriginalTeamName = "TeamA",
            ParentWorkItemId = 12345  // Has a parent User Story
        };

        // Mock the parent User Story response
        var parentWorkItem = new WorkItem
        {
            WorkItemId = 12345,
            Title = "實作使用者登入功能",
            Type = "User Story",
            State = "Active",
            Url = "https://dev.azure.com/org/project/_workitems/edit/12345",
            OriginalTeamName = "TeamA",
            ParentWorkItemId = null
        };

        _azureDevOpsRepositoryMock
            .Setup(r => r.GetWorkItemAsync(67890))
            .ReturnsAsync(Domain.Common.Result<WorkItem>.Success(bugWorkItem));

        _azureDevOpsRepositoryMock
            .Setup(r => r.GetWorkItemAsync(12345))
            .ReturnsAsync(Domain.Common.Result<WorkItem>.Success(parentWorkItem));

        var task = new GetUserStoryTask(
            _loggerMock.Object,
            _redisServiceMock.Object,
            _azureDevOpsRepositoryMock.Object);

        // Act
        await task.ExecuteAsync();

        // Assert
        _redisServiceMock.Verify(r => r.SetAsync(
            RedisKeys.AzureDevOpsUserStories,
            It.Is<string>(json =>
                json.Contains("\"resolutionStatus\":\"foundViaRecursion\"") ||
                json.Contains("\"resolutionStatus\":1")),
            null), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldWriteResultToCorrectRedisKey()
    {
        // Arrange
        var workItemFetchResult = new WorkItemFetchResult
        {
            WorkItems = new List<WorkItemOutput>(),
            TotalPRsAnalyzed = 0,
            TotalWorkItemsFound = 0,
            SuccessCount = 0,
            FailureCount = 0
        };

        _redisServiceMock
            .Setup(r => r.GetAsync(RedisKeys.AzureDevOpsWorkItems))
            .ReturnsAsync(workItemFetchResult.ToJson());

        var task = new GetUserStoryTask(
            _loggerMock.Object,
            _redisServiceMock.Object,
            _azureDevOpsRepositoryMock.Object);

        // Act
        await task.ExecuteAsync();

        // Assert
        _redisServiceMock.Verify(r => r.SetAsync(
            RedisKeys.AzureDevOpsUserStories,
            It.IsAny<string>(),
            null), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithFailedWorkItem_ShouldMarkAsOriginalFetchFailed()
    {
        // Arrange
        var workItemFetchResult = new WorkItemFetchResult
        {
            WorkItems = new List<WorkItemOutput>
            {
                new WorkItemOutput
                {
                    WorkItemId = 99999,
                    Title = null,
                    Type = null,
                    State = null,
                    Url = null,
                    OriginalTeamName = null,
                    IsSuccess = false,
                    ErrorMessage = "Work Item not found (404)"
                }
            },
            TotalPRsAnalyzed = 1,
            TotalWorkItemsFound = 1,
            SuccessCount = 0,
            FailureCount = 1
        };

        _redisServiceMock
            .Setup(r => r.GetAsync(RedisKeys.AzureDevOpsWorkItems))
            .ReturnsAsync(workItemFetchResult.ToJson());

        var task = new GetUserStoryTask(
            _loggerMock.Object,
            _redisServiceMock.Object,
            _azureDevOpsRepositoryMock.Object);

        // Act
        await task.ExecuteAsync();

        // Assert
        _redisServiceMock.Verify(r => r.SetAsync(
            RedisKeys.AzureDevOpsUserStories,
            It.Is<string>(json =>
                json.Contains("\"resolutionStatus\":\"originalFetchFailed\"") ||
                json.Contains("\"resolutionStatus\":3")),
            null), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithTaskHavingNoParent_ShouldMarkAsNotFound()
    {
        // Arrange
        var workItemFetchResult = new WorkItemFetchResult
        {
            WorkItems = new List<WorkItemOutput>
            {
                new WorkItemOutput
                {
                    WorkItemId = 88888,
                    Title = "Orphan Task",
                    Type = "Task",
                    State = "Active",
                    Url = "https://dev.azure.com/org/project/_workitems/edit/88888",
                    OriginalTeamName = "TeamA",
                    IsSuccess = true,
                    ErrorMessage = null
                }
            },
            TotalPRsAnalyzed = 1,
            TotalWorkItemsFound = 1,
            SuccessCount = 1,
            FailureCount = 0
        };

        _redisServiceMock
            .Setup(r => r.GetAsync(RedisKeys.AzureDevOpsWorkItems))
            .ReturnsAsync(workItemFetchResult.ToJson());

        // Mock Task with no parent
        var orphanTask = new WorkItem
        {
            WorkItemId = 88888,
            Title = "Orphan Task",
            Type = "Task",
            State = "Active",
            Url = "https://dev.azure.com/org/project/_workitems/edit/88888",
            OriginalTeamName = "TeamA",
            ParentWorkItemId = null  // No parent
        };

        _azureDevOpsRepositoryMock
            .Setup(r => r.GetWorkItemAsync(88888))
            .ReturnsAsync(Domain.Common.Result<WorkItem>.Success(orphanTask));

        var task = new GetUserStoryTask(
            _loggerMock.Object,
            _redisServiceMock.Object,
            _azureDevOpsRepositoryMock.Object);

        // Act
        await task.ExecuteAsync();

        // Assert
        _redisServiceMock.Verify(r => r.SetAsync(
            RedisKeys.AzureDevOpsUserStories,
            It.Is<string>(json =>
                json.Contains("\"resolutionStatus\":\"notFound\"") ||
                json.Contains("\"resolutionStatus\":2")),
            null), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyRedisData_ShouldWriteEmptyResult()
    {
        // Arrange
        var emptyResult = new WorkItemFetchResult
        {
            WorkItems = new List<WorkItemOutput>(),
            TotalPRsAnalyzed = 0,
            TotalWorkItemsFound = 0,
            SuccessCount = 0,
            FailureCount = 0
        };

        _redisServiceMock
            .Setup(r => r.GetAsync(RedisKeys.AzureDevOpsWorkItems))
            .ReturnsAsync(emptyResult.ToJson());

        var task = new GetUserStoryTask(
            _loggerMock.Object,
            _redisServiceMock.Object,
            _azureDevOpsRepositoryMock.Object);

        // Act
        await task.ExecuteAsync();

        // Assert
        _redisServiceMock.Verify(r => r.SetAsync(
            RedisKeys.AzureDevOpsUserStories,
            It.Is<string>(json =>
                json.Contains("\"totalCount\":0")),
            null), Times.Once);
    }
}
