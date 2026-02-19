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
    public async Task ExecuteAsync_WithEmptyRedisData_ShouldNotWriteToRedis()
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

        // Assert - Should NOT write to Redis when there's no data
        _redisServiceMock.Verify(x => x.SetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems, It.IsAny<string>(), null), Times.Never);
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

    /// <summary>
    /// T013: 測試透過 1 層 Parent 找到 User Story（FoundViaRecursion）
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithOneLevelParent_ShouldReturnFoundViaRecursion()
    {
        // Arrange - Bug with parent User Story
        var workItemResult = new WorkItemFetchResult
        {
            WorkItems = new List<WorkItemOutput>
            {
                new WorkItemOutput
                {
                    WorkItemId = 11111,
                    Title = "修正登入按鈕顏色",
                    Type = "Bug",
                    State = "Resolved",
                    Url = "https://dev.azure.com/org/proj/_workitems/edit/11111",
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

        // Mock the original Work Item (Bug) with Parent ID 67890
        var bugWorkItem = CreateWorkItemWithParent(11111, "修正登入按鈕顏色", "Bug", "Resolved", 67890);
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(11111))
            .ReturnsAsync(Result<WorkItem>.Success(bugWorkItem));

        // Mock Parent Work Item (User Story) without parent
        var parentWorkItem = CreateWorkItemWithParent(67890, "新增使用者登入功能", "User Story", "Active", null);
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(67890))
            .ReturnsAsync(Result<WorkItem>.Success(parentWorkItem));

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        var result = _capturedRedisJson!.ToTypedObject<UserStoryFetchResult>();
        Assert.NotNull(result);
        Assert.Single(result.WorkItems);
        Assert.Equal(UserStoryResolutionStatus.FoundViaRecursion, result.WorkItems[0].ResolutionStatus);
        Assert.Equal(67890, result.WorkItems[0].WorkItemId);
        Assert.Equal("User Story", result.WorkItems[0].Type);
        var originalWorkItem = result.WorkItems[0].OriginalWorkItem;
        Assert.NotNull(originalWorkItem);
        Assert.Equal(11111, originalWorkItem.WorkItemId);
        Assert.Equal(0, result.AlreadyUserStoryCount);
        Assert.Equal(1, result.FoundViaRecursionCount);
    }

    /// <summary>
    /// T014: 測試透過 2 層 Parent 找到 User Story（遞迴）
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithTwoLevelParents_ShouldReturnFoundViaRecursion()
    {
        // Arrange - Bug -> Task -> User Story
        var workItemResult = new WorkItemFetchResult
        {
            WorkItems = new List<WorkItemOutput>
            {
                new WorkItemOutput
                {
                    WorkItemId = 11111,
                    Title = "修正登入按鈕顏色",
                    Type = "Bug",
                    State = "Resolved",
                    Url = "https://dev.azure.com/org/proj/_workitems/edit/11111",
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

        // Mock original Bug with parent 22222
        var bugWorkItem = CreateWorkItemWithParent(11111, "修正登入按鈕顏色", "Bug", "Resolved", 22222);
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(11111))
            .ReturnsAsync(Result<WorkItem>.Success(bugWorkItem));

        // Mock Parent Level 1 (Task) with parent 67890
        var parentLevel1 = CreateWorkItemWithParent(22222, "實作登入表單", "Task", "Active", 67890);
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(22222))
            .ReturnsAsync(Result<WorkItem>.Success(parentLevel1));

        // Mock Parent Level 2 (User Story)
        var parentLevel2 = CreateWorkItemWithParent(67890, "新增使用者登入功能", "User Story", "Active", null);
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(67890))
            .ReturnsAsync(Result<WorkItem>.Success(parentLevel2));

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        var result = _capturedRedisJson!.ToTypedObject<UserStoryFetchResult>();
        Assert.NotNull(result);
        Assert.Single(result.WorkItems);
        Assert.Equal(UserStoryResolutionStatus.FoundViaRecursion, result.WorkItems[0].ResolutionStatus);
        Assert.Equal(67890, result.WorkItems[0].WorkItemId);
        Assert.Equal("User Story", result.WorkItems[0].Type);
        Assert.Equal(1, result.FoundViaRecursionCount);
    }

    /// <summary>
    /// T015: 測試 Work Item 無 Parent（NotFound）
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithNoParent_ShouldReturnNotFound()
    {
        // Arrange - Bug with no parent
        var workItemResult = new WorkItemFetchResult
        {
            WorkItems = new List<WorkItemOutput>
            {
                new WorkItemOutput
                {
                    WorkItemId = 11111,
                    Title = "獨立 Bug",
                    Type = "Bug",
                    State = "New",
                    Url = "https://dev.azure.com/org/proj/_workitems/edit/11111",
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

        // Mock Work Item with no parent
        var bugWorkItem = CreateWorkItemWithParent(11111, "獨立 Bug", "Bug", "New", null);
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(11111))
            .ReturnsAsync(Result<WorkItem>.Success(bugWorkItem));

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        var result = _capturedRedisJson!.ToTypedObject<UserStoryFetchResult>();
        Assert.NotNull(result);
        Assert.Single(result.WorkItems);
        Assert.Equal(UserStoryResolutionStatus.NotFound, result.WorkItems[0].ResolutionStatus);
        Assert.Equal(1, result.NotFoundCount);
    }

    /// <summary>
    /// T029: 測試原始 Work Item API 呼叫失敗（OriginalFetchFailed）
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithOriginalWorkItemFetchFailed_ShouldReturnOriginalFetchFailed()
    {
        // Arrange
        var workItemResult = new WorkItemFetchResult
        {
            WorkItems = new List<WorkItemOutput>
            {
                new WorkItemOutput
                {
                    WorkItemId = 11111,
                    Title = null,
                    Type = null,
                    State = null,
                    Url = null,
                    OriginalTeamName = null,
                    IsSuccess = false,
                    ErrorMessage = "Work item not found (404)"
                }
            },
            TotalPRsAnalyzed = 1,
            TotalWorkItemsFound = 1,
            SuccessCount = 0,
            FailureCount = 1
        };
        
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsWorkItems))
            .ReturnsAsync(workItemResult.ToJson());

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        var result = _capturedRedisJson!.ToTypedObject<UserStoryFetchResult>();
        Assert.NotNull(result);
        Assert.Single(result.WorkItems);
        Assert.Equal(UserStoryResolutionStatus.OriginalFetchFailed, result.WorkItems[0].ResolutionStatus);
        Assert.False(result.WorkItems[0].IsSuccess);
        Assert.Contains("not found", result.WorkItems[0].ErrorMessage ?? "");
        Assert.Equal(1, result.OriginalFetchFailedCount);
    }

    /// <summary>
    /// T030: 測試 Parent Work Item API 呼叫失敗（NotFound with error）
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithParentWorkItemFetchFailed_ShouldReturnNotFoundWithError()
    {
        // Arrange
        var workItemResult = new WorkItemFetchResult
        {
            WorkItems = new List<WorkItemOutput>
            {
                new WorkItemOutput
                {
                    WorkItemId = 11111,
                    Title = "修正登入按鈕",
                    Type = "Bug",
                    State = "Active",
                    Url = "https://dev.azure.com/org/proj/_workitems/edit/11111",
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

        // Mock Parent API failure
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(It.IsAny<int>()))
            .ReturnsAsync(Result<WorkItem>.Failure(Error.AzureDevOps.WorkItemNotFound(22222)));

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        var result = _capturedRedisJson!.ToTypedObject<UserStoryFetchResult>();
        Assert.NotNull(result);
        Assert.Single(result.WorkItems);
        Assert.Equal(UserStoryResolutionStatus.NotFound, result.WorkItems[0].ResolutionStatus);
        Assert.Contains("22222", result.WorkItems[0].ErrorMessage?.ToLower() ?? "");
    }

    /// <summary>
    /// T038: 測試偵測循環參照（A → B → A）
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithCircularReference_ShouldDetectAndStop()
    {
        // Arrange - Bug 11111 -> Task 22222 -> Bug 11111 (circular)
        var workItemResult = new WorkItemFetchResult
        {
            WorkItems = new List<WorkItemOutput>
            {
                new WorkItemOutput
                {
                    WorkItemId = 11111,
                    Title = "Bug A",
                    Type = "Bug",
                    State = "Active",
                    Url = "https://dev.azure.com/org/proj/_workitems/edit/11111",
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

        // Mock Bug 11111 with parent 22222
        var workItem11111 = CreateWorkItemWithParent(11111, "Bug A", "Bug", "Active", 22222);
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(11111))
            .ReturnsAsync(Result<WorkItem>.Success(workItem11111));

        // Mock circular reference: 22222 -> 11111
        var workItem22222 = CreateWorkItemWithParent(22222, "Task B", "Task", "Active", 11111);
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(22222))
            .ReturnsAsync(Result<WorkItem>.Success(workItem22222));

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        var result = _capturedRedisJson!.ToTypedObject<UserStoryFetchResult>();
        Assert.NotNull(result);
        Assert.Single(result.WorkItems);
        Assert.Equal(UserStoryResolutionStatus.NotFound, result.WorkItems[0].ResolutionStatus);
        Assert.Contains("循環參照", result.WorkItems[0].ErrorMessage ?? "");
    }

    /// <summary>
    /// T039: 測試達到最大遞迴深度時停止（預設 10 層）
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithMaxDepthExceeded_ShouldStopAndReturnNotFound()
    {
        // Arrange - Create 12 levels deep chain
        var workItemResult = new WorkItemFetchResult
        {
            WorkItems = new List<WorkItemOutput>
            {
                new WorkItemOutput
                {
                    WorkItemId = 1,
                    Title = "Deep Bug",
                    Type = "Bug",
                    State = "Active",
                    Url = "https://dev.azure.com/org/proj/_workitems/edit/1",
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

        // Create deep chain: 1 -> 2 -> 3 -> ... -> 12
        for (int i = 1; i <= 12; i++)
        {
            var nextParentId = i < 12 ? i + 1 : (int?)null;
            var workItem = CreateWorkItemWithParent(i, $"Task {i}", "Task", "Active", nextParentId);
            _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(i))
                .ReturnsAsync(Result<WorkItem>.Success(workItem));
        }

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        var result = _capturedRedisJson!.ToTypedObject<UserStoryFetchResult>();
        Assert.NotNull(result);
        Assert.Single(result.WorkItems);
        Assert.Equal(UserStoryResolutionStatus.NotFound, result.WorkItems[0].ResolutionStatus);
        Assert.Contains("最大遞迴深度", result.WorkItems[0].ErrorMessage ?? "");
    }

    /// <summary>
    /// T010: 測試 UserStoryWorkItemOutput.PrId 包含來源 PR，且 OriginalWorkItem.PrId 為 null
    /// </summary>
    /// <remarks>
    /// 驗證 Phase 4 User Story 3 的需求：
    /// - UserStoryWorkItemOutput.PrId 應等於輸入 WorkItemOutput.PrId
    /// - OriginalWorkItem.PrId 應為 null（使用 with-expression 清除）
    /// 涵蓋兩種情境：FoundViaRecursion 與 NotFound
    /// </remarks>
    [Fact]
    public async Task ProcessWorkItemAsync_ShouldSetPrIdInUserStoryWorkItemButNotInOriginalWorkItem()
    {
        // Arrange - 兩個 Work Item：一個會找到 User Story（FoundViaRecursion），一個找不到（NotFound）
        var workItemResult = new WorkItemFetchResult
        {
            WorkItems = new List<WorkItemOutput>
            {
                // Work Item 1: Bug with parent User Story (FoundViaRecursion)
                new WorkItemOutput
                {
                    WorkItemId = 11111,
                    Title = "修正登入按鈕顏色",
                    Type = "Bug",
                    State = "Resolved",
                    Url = "https://dev.azure.com/org/proj/_workitems/edit/11111",
                    OriginalTeamName = "Platform/Web",
                    PrId = "100",
                    IsSuccess = true,
                    ErrorMessage = null
                },
                // Work Item 2: Bug with no parent (NotFound)
                new WorkItemOutput
                {
                    WorkItemId = 22222,
                    Title = "獨立 Bug",
                    Type = "Bug",
                    State = "Active",
                    Url = "https://dev.azure.com/org/proj/_workitems/edit/22222",
                    OriginalTeamName = "Platform/Web",
                    PrId = "200",
                    IsSuccess = true,
                    ErrorMessage = null
                }
            },
            TotalPRsAnalyzed = 2,
            TotalWorkItemsFound = 2,
            SuccessCount = 2,
            FailureCount = 0
        };
        
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsWorkItems))
            .ReturnsAsync(workItemResult.ToJson());

        // Mock Work Item 1 (Bug) with parent 67890 (User Story)
        var bugWorkItem = CreateWorkItemWithParent(11111, "修正登入按鈕顏色", "Bug", "Resolved", 67890);
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(11111))
            .ReturnsAsync(Result<WorkItem>.Success(bugWorkItem));

        // Mock Parent (User Story) without parent
        var parentWorkItem = CreateWorkItemWithParent(67890, "新增使用者登入功能", "User Story", "Active", null);
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(67890))
            .ReturnsAsync(Result<WorkItem>.Success(parentWorkItem));

        // Mock Work Item 2 (Bug) with no parent
        var bugWithoutParent = CreateWorkItemWithParent(22222, "獨立 Bug", "Bug", "Active", null);
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(22222))
            .ReturnsAsync(Result<WorkItem>.Success(bugWithoutParent));

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        var result = _capturedRedisJson!.ToTypedObject<UserStoryFetchResult>();
        Assert.NotNull(result);
        Assert.Equal(2, result.WorkItems.Count);

        // 驗證 Work Item 1 (FoundViaRecursion)
        var userStory1 = result.WorkItems.First(w => w.WorkItemId == 67890);
        Assert.Equal(UserStoryResolutionStatus.FoundViaRecursion, userStory1.ResolutionStatus);
        Assert.Equal("100", userStory1.PrId);
        Assert.NotNull(userStory1.OriginalWorkItem);
        Assert.Null(userStory1.OriginalWorkItem.PrId); // OriginalWorkItem.PrId 應為 null

        // 驗證 Work Item 2 (NotFound)
        var userStory2 = result.WorkItems.First(w => w.WorkItemId == 22222);
        Assert.Equal(UserStoryResolutionStatus.NotFound, userStory2.ResolutionStatus);
        Assert.Equal("200", userStory2.PrId);
        Assert.NotNull(userStory2.OriginalWorkItem);
        Assert.Null(userStory2.OriginalWorkItem.PrId); // OriginalWorkItem.PrId 應為 null
    }

    /// <summary>
    /// 測試多個 Work Item 共享相同 Parent 時，使用快取減少 API 呼叫
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithSharedParent_ShouldUseCacheAndReduceAPICalls()
    {
        // Arrange - 兩個不同的 Bug 有相同的 Parent (User Story 67890)
        var workItemResult = new WorkItemFetchResult
        {
            WorkItems = new List<WorkItemOutput>
            {
                new WorkItemOutput
                {
                    WorkItemId = 11111,
                    Title = "Bug 1",
                    Type = "Bug",
                    State = "Active",
                    Url = "https://dev.azure.com/org/proj/_workitems/edit/11111",
                    OriginalTeamName = "Platform/Web",
                    PrId = "100",
                    IsSuccess = true,
                    ErrorMessage = null
                },
                new WorkItemOutput
                {
                    WorkItemId = 22222,
                    Title = "Bug 2",
                    Type = "Bug",
                    State = "Active",
                    Url = "https://dev.azure.com/org/proj/_workitems/edit/22222",
                    OriginalTeamName = "Platform/Web",
                    PrId = "200",
                    IsSuccess = true,
                    ErrorMessage = null
                }
            },
            TotalPRsAnalyzed = 2,
            TotalWorkItemsFound = 2,
            SuccessCount = 2,
            FailureCount = 0
        };
        
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsWorkItems))
            .ReturnsAsync(workItemResult.ToJson());

        // Mock Bug 11111 with parent 67890
        var bug1 = CreateWorkItemWithParent(11111, "Bug 1", "Bug", "Active", 67890);
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(11111))
            .ReturnsAsync(Result<WorkItem>.Success(bug1));

        // Mock Bug 22222 with parent 67890 (same parent)
        var bug2 = CreateWorkItemWithParent(22222, "Bug 2", "Bug", "Active", 67890);
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(22222))
            .ReturnsAsync(Result<WorkItem>.Success(bug2));

        // Mock shared Parent (User Story 67890)
        var parentWorkItem = CreateWorkItemWithParent(67890, "User Story", "User Story", "Active", null);
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(67890))
            .ReturnsAsync(Result<WorkItem>.Success(parentWorkItem));

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        // 快取生效：每個 Work Item 只呼叫一次（包含共享的 Parent）
        _azureDevOpsRepositoryMock.Verify(x => x.GetWorkItemAsync(11111), Times.Once);
        _azureDevOpsRepositoryMock.Verify(x => x.GetWorkItemAsync(22222), Times.Once);
        _azureDevOpsRepositoryMock.Verify(x => x.GetWorkItemAsync(67890), Times.Once); // 共享的 Parent 只呼叫一次
        
        var result = _capturedRedisJson!.ToTypedObject<UserStoryFetchResult>();
        Assert.NotNull(result);
        Assert.Equal(2, result.WorkItems.Count);
        
        // 兩個 Work Item 都應該解析到相同的 User Story
        Assert.All(result.WorkItems, w =>
        {
            Assert.Equal(67890, w.WorkItemId);
            Assert.Equal(UserStoryResolutionStatus.FoundViaRecursion, w.ResolutionStatus);
        });
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

    private WorkItem CreateWorkItemWithParent(int id, string title, string type, string state, int? parentId)
    {
        return new WorkItem
        {
            WorkItemId = id,
            Title = title,
            Type = type,
            State = state,
            Url = $"https://dev.azure.com/org/proj/_workitems/edit/{id}",
            OriginalTeamName = "Platform/Web",
            ParentId = parentId
        };
    }
}
