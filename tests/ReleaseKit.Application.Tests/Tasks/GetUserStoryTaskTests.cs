using Microsoft.Extensions.Logging;
using Moq;
using ReleaseKit.Application.Common;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Common;
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
    private string? _capturedRedisJson;

    public GetUserStoryTaskTests()
    {
        _loggerMock = new Mock<ILogger<GetUserStoryTask>>();
        _redisServiceMock = new Mock<IRedisService>();
        _azureDevOpsRepositoryMock = new Mock<IAzureDevOpsRepository>();
        
        // Setup Redis write capture
        _redisServiceMock.Setup(x => x.SetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems, It.IsAny<string>(), null))
            .Callback<string, string, TimeSpan?>((key, json, ttl) => _capturedRedisJson = json)
            .ReturnsAsync(true);
    }

    /// <summary>
    /// T011: 測試從 Redis 讀取 Work Item 資料（空資料情境）
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithEmptyRedisData_ShouldReturnEmptyResult()
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
        
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsWorkItems))
            .ReturnsAsync(emptyResult.ToJson());

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _redisServiceMock.Verify(x => x.SetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems, It.IsAny<string>(), null), Times.Once);
        
        var result = _capturedRedisJson!.ToTypedObject<UserStoryFetchResult>();
        Assert.NotNull(result);
        Assert.Empty(result.WorkItems);
        Assert.Equal(0, result.TotalWorkItems);
    }

    /// <summary>
    /// T012: 測試原始 Work Item 已是 User Story 層級（AlreadyUserStoryOrAbove）
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithUserStoryWorkItem_ShouldReturnAlreadyUserStoryOrAbove()
    {
        // Arrange
        var workItemResult = new WorkItemFetchResult
        {
            WorkItems = new List<WorkItemOutput>
            {
                new WorkItemOutput
                {
                    WorkItemId = 12345,
                    Title = "新增使用者登入功能",
                    Type = "User Story",
                    State = "Active",
                    Url = "https://dev.azure.com/org/proj/_workitems/edit/12345",
                    OriginalTeamName = "Platform/Web",
                    IsSuccess = true,
                    ErrorMessage = null
                }
            },
            TotalPRsAnalyzed = 1,
            TotalWorkItemsFound = 1,
            SuccessCount = 1,
            FailureCount = 0
        };
        
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsWorkItems))
            .ReturnsAsync(workItemResult.ToJson());

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _redisServiceMock.Verify(x => x.SetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems, It.IsAny<string>(), null), Times.Once);
        
        var result = _capturedRedisJson!.ToTypedObject<UserStoryFetchResult>();
        Assert.NotNull(result);
        Assert.Single(result.WorkItems);
        Assert.Equal(UserStoryResolutionStatus.AlreadyUserStoryOrAbove, result.WorkItems[0].ResolutionStatus);
        Assert.Equal(12345, result.WorkItems[0].WorkItemId);
        Assert.Equal("User Story", result.WorkItems[0].Type);
        Assert.Equal(1, result.AlreadyUserStoryCount);
        Assert.Equal(0, result.FoundViaRecursionCount);
    }

    // Helper methods
    private GetUserStoryTask CreateTask()
    {
        return new GetUserStoryTask(
            _azureDevOpsRepositoryMock.Object,
            _redisServiceMock.Object,
            _loggerMock.Object);
    }

    private WorkItem CreateWorkItem(int id, string? title, string? type, string? state)
    {
        return new WorkItem
        {
            WorkItemId = id,
            Title = title ?? "",
            Type = type ?? "",
            State = state ?? "",
            Url = $"https://dev.azure.com/org/proj/_workitems/edit/{id}",
            OriginalTeamName = "Platform/Web"
        };
    }
}
