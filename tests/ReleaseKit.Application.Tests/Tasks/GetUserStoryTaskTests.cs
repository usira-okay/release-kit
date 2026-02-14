using Microsoft.Extensions.Logging;
using Moq;
using ReleaseKit.Application.Common;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Common;
using ReleaseKit.Domain.Entities;
using Xunit;

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

    /// <summary>
    /// 測試 1: 已是 User Story 的 WorkItem → 直接保留，不查詢 API
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithUserStoryWorkItem_ShouldKeepDirectlyWithoutApiCall()
    {
        // Arrange
        var userStory = new WorkItemOutput
        {
            WorkItemId = 100,
            Title = "用戶故事",
            Type = "User Story",
            State = "Active",
            Url = "https://dev.azure.com/org/project/_workitems/edit/100",
            OriginalTeamName = "MyTeam",
            IsSuccess = true,
            ErrorMessage = null,
            SourcePullRequestId = 1,
            SourceProjectName = "group/project",
            SourcePRUrl = "https://example.com/pr/1"
        };

        var workItemFetchResult = new WorkItemFetchResult
        {
            WorkItems = new List<WorkItemOutput> { userStory },
            TotalPRsAnalyzed = 1,
            TotalWorkItemsFound = 1,
            SuccessCount = 1,
            FailureCount = 0
        };

        SetupRedis(workItemFetchResult);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _azureDevOpsRepositoryMock.Verify(x => x.GetWorkItemAsync(It.IsAny<int>()), Times.Never);
        VerifyRedisWrite(result =>
        {
            Assert.Single(result.UserStories);
            Assert.Equal(100, result.UserStories[0].WorkItemId);
            Assert.Equal("User Story", result.UserStories[0].Type);
            Assert.True(result.UserStories[0].IsSuccess);
            Assert.Equal(1, result.TotalWorkItemsProcessed);
            Assert.Equal(1, result.AlreadyUserStoryCount);
            Assert.Equal(0, result.ResolvedCount);
            Assert.Equal(0, result.KeptOriginalCount);
        });
    }

    /// <summary>
    /// 測試 2: 已是 Feature 的 WorkItem → 直接保留
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithFeatureWorkItem_ShouldKeepDirectly()
    {
        // Arrange
        var feature = new WorkItemOutput
        {
            WorkItemId = 200,
            Title = "功能",
            Type = "Feature",
            State = "Active",
            Url = "https://dev.azure.com/org/project/_workitems/edit/200",
            OriginalTeamName = "MyTeam",
            IsSuccess = true,
            ErrorMessage = null,
            SourcePullRequestId = 1,
            SourceProjectName = "group/project",
            SourcePRUrl = "https://example.com/pr/1"
        };

        var workItemFetchResult = new WorkItemFetchResult
        {
            WorkItems = new List<WorkItemOutput> { feature },
            TotalPRsAnalyzed = 1,
            TotalWorkItemsFound = 1,
            SuccessCount = 1,
            FailureCount = 0
        };

        SetupRedis(workItemFetchResult);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _azureDevOpsRepositoryMock.Verify(x => x.GetWorkItemAsync(It.IsAny<int>()), Times.Never);
        VerifyRedisWrite(result =>
        {
            Assert.Single(result.UserStories);
            Assert.Equal(200, result.UserStories[0].WorkItemId);
            Assert.Equal("Feature", result.UserStories[0].Type);
            Assert.Equal(1, result.AlreadyUserStoryCount);
        });
    }

    /// <summary>
    /// 測試 3: 已是 Epic 的 WorkItem → 直接保留
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithEpicWorkItem_ShouldKeepDirectly()
    {
        // Arrange
        var epic = new WorkItemOutput
        {
            WorkItemId = 300,
            Title = "史詩",
            Type = "Epic",
            State = "Active",
            Url = "https://dev.azure.com/org/project/_workitems/edit/300",
            OriginalTeamName = "MyTeam",
            IsSuccess = true,
            ErrorMessage = null,
            SourcePullRequestId = 1,
            SourceProjectName = "group/project",
            SourcePRUrl = "https://example.com/pr/1"
        };

        var workItemFetchResult = new WorkItemFetchResult
        {
            WorkItems = new List<WorkItemOutput> { epic },
            TotalPRsAnalyzed = 1,
            TotalWorkItemsFound = 1,
            SuccessCount = 1,
            FailureCount = 0
        };

        SetupRedis(workItemFetchResult);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _azureDevOpsRepositoryMock.Verify(x => x.GetWorkItemAsync(It.IsAny<int>()), Times.Never);
        VerifyRedisWrite(result =>
        {
            Assert.Single(result.UserStories);
            Assert.Equal(300, result.UserStories[0].WorkItemId);
            Assert.Equal("Epic", result.UserStories[0].Type);
            Assert.Equal(1, result.AlreadyUserStoryCount);
        });
    }

    /// <summary>
    /// 測試 4: Task 的 parent 為 User Story → 查詢一次 API，解析至 parent，記錄 OriginalWorkItemId
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithTaskHavingUserStoryParent_ShouldResolveToParent()
    {
        // Arrange
        var task = new WorkItemOutput
        {
            WorkItemId = 400,
            Title = "工作項目",
            Type = "Task",
            State = "Active",
            Url = "https://dev.azure.com/org/project/_workitems/edit/400",
            OriginalTeamName = "MyTeam",
            IsSuccess = true,
            ErrorMessage = null,
            SourcePullRequestId = 1,
            SourceProjectName = "group/project",
            SourcePRUrl = "https://example.com/pr/1"
        };

        var workItemFetchResult = new WorkItemFetchResult
        {
            WorkItems = new List<WorkItemOutput> { task },
            TotalPRsAnalyzed = 1,
            TotalWorkItemsFound = 1,
            SuccessCount = 1,
            FailureCount = 0
        };

        var parentUserStory = new WorkItem
        {
            WorkItemId = 500,
            Title = "父層用戶故事",
            Type = "User Story",
            State = "Active",
            Url = "https://dev.azure.com/org/project/_workitems/edit/500",
            OriginalTeamName = "MyTeam",
            ParentWorkItemId = null
        };

        var originalTaskWorkItem = new WorkItem
        {
            WorkItemId = 400,
            Title = "工作項目",
            Type = "Task",
            State = "Active",
            Url = "https://dev.azure.com/org/project/_workitems/edit/400",
            OriginalTeamName = "MyTeam",
            ParentWorkItemId = 500
        };

        SetupRedis(workItemFetchResult);
        _azureDevOpsRepositoryMock
            .Setup(x => x.GetWorkItemAsync(400))
            .ReturnsAsync(Result<WorkItem>.Success(originalTaskWorkItem));
        _azureDevOpsRepositoryMock
            .Setup(x => x.GetWorkItemAsync(500))
            .ReturnsAsync(Result<WorkItem>.Success(parentUserStory));

        var createTaskInstance = CreateTask();

        // Act
        await createTaskInstance.ExecuteAsync();

        // Assert
        _azureDevOpsRepositoryMock.Verify(x => x.GetWorkItemAsync(400), Times.Once);
        _azureDevOpsRepositoryMock.Verify(x => x.GetWorkItemAsync(500), Times.Once);
        VerifyRedisWrite(result =>
        {
            Assert.Single(result.UserStories);
            var userStoryOutput = result.UserStories[0];
            Assert.Equal(500, userStoryOutput.WorkItemId);
            Assert.Equal(400, userStoryOutput.OriginalWorkItemId);
            Assert.Equal("User Story", userStoryOutput.Type);
            Assert.True(userStoryOutput.IsSuccess);
            Assert.Equal(1, result.ResolvedCount);
            Assert.Equal(0, result.KeptOriginalCount);
        });
    }

    /// <summary>
    /// 測試 5: Bug 的祖父為 User Story（二層遞迴）→ 查詢兩次 API，解析至祖父
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithBugHavingTwoLevelHierarchy_ShouldResolveToGrandparent()
    {
        // Arrange
        var bug = new WorkItemOutput
        {
            WorkItemId = 1000,
            Title = "缺陷",
            Type = "Bug",
            State = "Active",
            Url = "https://dev.azure.com/org/project/_workitems/edit/1000",
            OriginalTeamName = "MyTeam",
            IsSuccess = true,
            ErrorMessage = null,
            SourcePullRequestId = 1,
            SourceProjectName = "group/project",
            SourcePRUrl = "https://example.com/pr/1"
        };

        var workItemFetchResult = new WorkItemFetchResult
        {
            WorkItems = new List<WorkItemOutput> { bug },
            TotalPRsAnalyzed = 1,
            TotalWorkItemsFound = 1,
            SuccessCount = 1,
            FailureCount = 0
        };

        var bugWorkItem = new WorkItem
        {
            WorkItemId = 1000,
            Title = "缺陷",
            Type = "Bug",
            State = "Active",
            Url = "https://dev.azure.com/org/project/_workitems/edit/1000",
            OriginalTeamName = "MyTeam",
            ParentWorkItemId = 1001
        };

        var taskWorkItem = new WorkItem
        {
            WorkItemId = 1001,
            Title = "工作項目",
            Type = "Task",
            State = "Active",
            Url = "https://dev.azure.com/org/project/_workitems/edit/1001",
            OriginalTeamName = "MyTeam",
            ParentWorkItemId = 1002
        };

        var userStoryWorkItem = new WorkItem
        {
            WorkItemId = 1002,
            Title = "祖父用戶故事",
            Type = "User Story",
            State = "Active",
            Url = "https://dev.azure.com/org/project/_workitems/edit/1002",
            OriginalTeamName = "MyTeam",
            ParentWorkItemId = null
        };

        SetupRedis(workItemFetchResult);
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(1000)).ReturnsAsync(Result<WorkItem>.Success(bugWorkItem));
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(1001)).ReturnsAsync(Result<WorkItem>.Success(taskWorkItem));
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(1002)).ReturnsAsync(Result<WorkItem>.Success(userStoryWorkItem));

        var createTaskInstance = CreateTask();

        // Act
        await createTaskInstance.ExecuteAsync();

        // Assert
        _azureDevOpsRepositoryMock.Verify(x => x.GetWorkItemAsync(1000), Times.Once);
        _azureDevOpsRepositoryMock.Verify(x => x.GetWorkItemAsync(1001), Times.Once);
        _azureDevOpsRepositoryMock.Verify(x => x.GetWorkItemAsync(1002), Times.Once);
        VerifyRedisWrite(result =>
        {
            Assert.Single(result.UserStories);
            var userStoryOutput = result.UserStories[0];
            Assert.Equal(1002, userStoryOutput.WorkItemId);
            Assert.Equal(1000, userStoryOutput.OriginalWorkItemId);
            Assert.Equal("User Story", userStoryOutput.Type);
            Assert.True(userStoryOutput.IsSuccess);
            Assert.Equal(1, result.ResolvedCount);
        });
    }

    /// <summary>
    /// 測試 6: 整條 parent 鏈無高層級類型 → 保留原始 WorkItem 資料
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithNoHigherLevelTypeInChain_ShouldKeepOriginal()
    {
        // Arrange
        var task = new WorkItemOutput
        {
            WorkItemId = 2000,
            Title = "工作項目",
            Type = "Task",
            State = "Active",
            Url = "https://dev.azure.com/org/project/_workitems/edit/2000",
            OriginalTeamName = "MyTeam",
            IsSuccess = true,
            ErrorMessage = null,
            SourcePullRequestId = 1,
            SourceProjectName = "group/project",
            SourcePRUrl = "https://example.com/pr/1"
        };

        var workItemFetchResult = new WorkItemFetchResult
        {
            WorkItems = new List<WorkItemOutput> { task },
            TotalPRsAnalyzed = 1,
            TotalWorkItemsFound = 1,
            SuccessCount = 1,
            FailureCount = 0
        };

        var taskWorkItem = new WorkItem
        {
            WorkItemId = 2000,
            Title = "工作項目",
            Type = "Task",
            State = "Active",
            Url = "https://dev.azure.com/org/project/_workitems/edit/2000",
            OriginalTeamName = "MyTeam",
            ParentWorkItemId = 2001
        };

        var parentTaskWorkItem = new WorkItem
        {
            WorkItemId = 2001,
            Title = "父層工作項目",
            Type = "Task",
            State = "Active",
            Url = "https://dev.azure.com/org/project/_workitems/edit/2001",
            OriginalTeamName = "MyTeam",
            ParentWorkItemId = null
        };

        SetupRedis(workItemFetchResult);
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(2000)).ReturnsAsync(Result<WorkItem>.Success(taskWorkItem));
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(2001)).ReturnsAsync(Result<WorkItem>.Success(parentTaskWorkItem));

        var createTaskInstance = CreateTask();

        // Act
        await createTaskInstance.ExecuteAsync();

        // Assert
        VerifyRedisWrite(result =>
        {
            Assert.Single(result.UserStories);
            var userStoryOutput = result.UserStories[0];
            Assert.Equal(2000, userStoryOutput.WorkItemId);
            Assert.Equal(2000, userStoryOutput.OriginalWorkItemId);
            Assert.Equal("Task", userStoryOutput.Type);
            Assert.True(userStoryOutput.IsSuccess);
            Assert.Equal(0, result.ResolvedCount);
            Assert.Equal(1, result.KeptOriginalCount);
        });
    }

    /// <summary>
    /// 測試 7: 原始抓取失敗（IsSuccess=false）→ 保留失敗記錄
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithFailedWorkItem_ShouldKeepFailureRecord()
    {
        // Arrange
        var failedWorkItem = new WorkItemOutput
        {
            WorkItemId = 3000,
            Title = null,
            Type = null,
            State = null,
            Url = null,
            OriginalTeamName = null,
            IsSuccess = false,
            ErrorMessage = "找不到 Work Item",
            SourcePullRequestId = 1,
            SourceProjectName = "group/project",
            SourcePRUrl = "https://example.com/pr/1"
        };

        var workItemFetchResult = new WorkItemFetchResult
        {
            WorkItems = new List<WorkItemOutput> { failedWorkItem },
            TotalPRsAnalyzed = 1,
            TotalWorkItemsFound = 1,
            SuccessCount = 0,
            FailureCount = 1
        };

        SetupRedis(workItemFetchResult);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _azureDevOpsRepositoryMock.Verify(x => x.GetWorkItemAsync(It.IsAny<int>()), Times.Never);
        VerifyRedisWrite(result =>
        {
            Assert.Single(result.UserStories);
            var userStoryOutput = result.UserStories[0];
            Assert.Equal(3000, userStoryOutput.WorkItemId);
            Assert.False(userStoryOutput.IsSuccess);
            Assert.Equal("找不到 Work Item", userStoryOutput.ErrorMessage);
            Assert.Equal(1, result.KeptOriginalCount);
        });
    }

    /// <summary>
    /// 測試 8: 結果正確寫入 Redis key `AzureDevOps:UserStories`
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldWriteResultsToCorrectRedisKey()
    {
        // Arrange
        var userStory = new WorkItemOutput
        {
            WorkItemId = 4000,
            Title = "用戶故事",
            Type = "User Story",
            State = "Active",
            Url = "https://dev.azure.com/org/project/_workitems/edit/4000",
            OriginalTeamName = "MyTeam",
            IsSuccess = true,
            ErrorMessage = null,
            SourcePullRequestId = 1,
            SourceProjectName = "group/project",
            SourcePRUrl = "https://example.com/pr/1"
        };

        var workItemFetchResult = new WorkItemFetchResult
        {
            WorkItems = new List<WorkItemOutput> { userStory },
            TotalPRsAnalyzed = 1,
            TotalWorkItemsFound = 1,
            SuccessCount = 1,
            FailureCount = 0
        };

        SetupRedis(workItemFetchResult);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _redisServiceMock.Verify(
            x => x.SetAsync(RedisKeys.AzureDevOpsUserStories, It.IsAny<string>(), null),
            Times.Once);
    }

    /// <summary>
    /// 測試 9: 遞迴深度超過 10 層 → 保留原始資料
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithRecursionDepthExceeding10_ShouldKeepOriginal()
    {
        // Arrange
        var deepWorkItem = new WorkItemOutput
        {
            WorkItemId = 5000,
            Title = "深層工作項目",
            Type = "Task",
            State = "Active",
            Url = "https://dev.azure.com/org/project/_workitems/edit/5000",
            OriginalTeamName = "MyTeam",
            IsSuccess = true,
            ErrorMessage = null,
            SourcePullRequestId = 1,
            SourceProjectName = "group/project",
            SourcePRUrl = "https://example.com/pr/1"
        };

        var workItemFetchResult = new WorkItemFetchResult
        {
            WorkItems = new List<WorkItemOutput> { deepWorkItem },
            TotalPRsAnalyzed = 1,
            TotalWorkItemsFound = 1,
            SuccessCount = 1,
            FailureCount = 0
        };

        // 建立一個 11 層的 parent 鏈
        var currentId = 5000;
        var nextId = 5001;
        var workItems = new Dictionary<int, WorkItem>();

        var firstItem = new WorkItem
        {
            WorkItemId = currentId,
            Title = "深層工作項目",
            Type = "Task",
            State = "Active",
            Url = $"https://dev.azure.com/org/project/_workitems/edit/{currentId}",
            OriginalTeamName = "MyTeam",
            ParentWorkItemId = nextId
        };
        workItems[currentId] = firstItem;

        // 建立層級 2-11
        for (int i = 1; i < 11; i++)
        {
            currentId = nextId;
            nextId = 5000 + i + 1;
            var workItem = new WorkItem
            {
                WorkItemId = currentId,
                Title = $"層級 {i}",
                Type = "Task",
                State = "Active",
                Url = $"https://dev.azure.com/org/project/_workitems/edit/{currentId}",
                OriginalTeamName = "MyTeam",
                ParentWorkItemId = i < 10 ? nextId : null
            };
            workItems[currentId] = workItem;
        }

        SetupRedis(workItemFetchResult);

        // 設定所有 API 呼叫
        foreach (var (id, workItem) in workItems)
        {
            _azureDevOpsRepositoryMock
                .Setup(x => x.GetWorkItemAsync(id))
                .ReturnsAsync(Result<WorkItem>.Success(workItem));
        }

        var createTaskInstance = CreateTask();

        // Act
        await createTaskInstance.ExecuteAsync();

        // Assert
        VerifyRedisWrite(result =>
        {
            Assert.Single(result.UserStories);
            var userStoryOutput = result.UserStories[0];
            Assert.Equal(5000, userStoryOutput.WorkItemId);
            Assert.True(userStoryOutput.IsSuccess);
            Assert.Equal(1, result.KeptOriginalCount);
        });
    }

    /// <summary>
    /// 測試 10: 同一 WorkItem 出現在多筆 PR → 各自獨立解析
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithSameWorkItemInMultiplePRs_ShouldResolveIndependently()
    {
        // Arrange
        var task1 = new WorkItemOutput
        {
            WorkItemId = 6000,
            Title = "工作項目",
            Type = "Task",
            State = "Active",
            Url = "https://dev.azure.com/org/project/_workitems/edit/6000",
            OriginalTeamName = "MyTeam",
            IsSuccess = true,
            ErrorMessage = null,
            SourcePullRequestId = 1,
            SourceProjectName = "group/project1",
            SourcePRUrl = "https://example.com/pr/1"
        };

        var task2 = new WorkItemOutput
        {
            WorkItemId = 6000,
            Title = "工作項目",
            Type = "Task",
            State = "Active",
            Url = "https://dev.azure.com/org/project/_workitems/edit/6000",
            OriginalTeamName = "MyTeam",
            IsSuccess = true,
            ErrorMessage = null,
            SourcePullRequestId = 2,
            SourceProjectName = "group/project2",
            SourcePRUrl = "https://example.com/pr/2"
        };

        var workItemFetchResult = new WorkItemFetchResult
        {
            WorkItems = new List<WorkItemOutput> { task1, task2 },
            TotalPRsAnalyzed = 2,
            TotalWorkItemsFound = 1,
            SuccessCount = 2,
            FailureCount = 0
        };

        var taskWorkItem = new WorkItem
        {
            WorkItemId = 6000,
            Title = "工作項目",
            Type = "Task",
            State = "Active",
            Url = "https://dev.azure.com/org/project/_workitems/edit/6000",
            OriginalTeamName = "MyTeam",
            ParentWorkItemId = 6001
        };

        var userStoryWorkItem = new WorkItem
        {
            WorkItemId = 6001,
            Title = "用戶故事",
            Type = "User Story",
            State = "Active",
            Url = "https://dev.azure.com/org/project/_workitems/edit/6001",
            OriginalTeamName = "MyTeam",
            ParentWorkItemId = null
        };

        SetupRedis(workItemFetchResult);
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(6000)).ReturnsAsync(Result<WorkItem>.Success(taskWorkItem));
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(6001)).ReturnsAsync(Result<WorkItem>.Success(userStoryWorkItem));

        var createTaskInstance = CreateTask();

        // Act
        await createTaskInstance.ExecuteAsync();

        // Assert
        // 儘管同一個 WorkItem，但有兩筆不同的 PR 記錄，應該生成兩筆 output
        // 但由於快取，API 僅調用一次
        _azureDevOpsRepositoryMock.Verify(x => x.GetWorkItemAsync(6000), Times.Once);
        _azureDevOpsRepositoryMock.Verify(x => x.GetWorkItemAsync(6001), Times.Once);
        VerifyRedisWrite(result =>
        {
            Assert.Equal(2, result.UserStories.Count);
            Assert.All(result.UserStories, us => Assert.Equal(6001, us.WorkItemId));
        });
    }

    /// <summary>
    /// 測試 11: 遞迴查詢中 API 失敗 → 保留原始 WorkItem 資料
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithApiFailureDuringRecursion_ShouldKeepOriginal()
    {
        // Arrange
        var task = new WorkItemOutput
        {
            WorkItemId = 7000,
            Title = "工作項目",
            Type = "Task",
            State = "Active",
            Url = "https://dev.azure.com/org/project/_workitems/edit/7000",
            OriginalTeamName = "MyTeam",
            IsSuccess = true,
            ErrorMessage = null,
            SourcePullRequestId = 1,
            SourceProjectName = "group/project",
            SourcePRUrl = "https://example.com/pr/1"
        };

        var workItemFetchResult = new WorkItemFetchResult
        {
            WorkItems = new List<WorkItemOutput> { task },
            TotalPRsAnalyzed = 1,
            TotalWorkItemsFound = 1,
            SuccessCount = 1,
            FailureCount = 0
        };

        var taskWorkItem = new WorkItem
        {
            WorkItemId = 7000,
            Title = "工作項目",
            Type = "Task",
            State = "Active",
            Url = "https://dev.azure.com/org/project/_workitems/edit/7000",
            OriginalTeamName = "MyTeam",
            ParentWorkItemId = 7001
        };

        SetupRedis(workItemFetchResult);
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(7000)).ReturnsAsync(Result<WorkItem>.Success(taskWorkItem));
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(7001)).ReturnsAsync(Result<WorkItem>.Failure(Error.AzureDevOps.WorkItemNotFound(7001)));

        var createTaskInstance = CreateTask();

        // Act
        await createTaskInstance.ExecuteAsync();

        // Assert
        VerifyRedisWrite(result =>
        {
            Assert.Single(result.UserStories);
            var userStoryOutput = result.UserStories[0];
            Assert.Equal(7000, userStoryOutput.WorkItemId);
            Assert.Equal(7000, userStoryOutput.OriginalWorkItemId);
            Assert.Equal("Task", userStoryOutput.Type);
            Assert.False(userStoryOutput.IsSuccess);
            Assert.NotNull(userStoryOutput.ErrorMessage);
        });
    }

    /// <summary>
    /// 測試 12: 重複 Work Item ID → Dictionary 快取，API 僅查詢一次
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithDuplicateWorkItemIds_ShouldCacheAndQueryOnce()
    {
        // Arrange
        var task1 = new WorkItemOutput
        {
            WorkItemId = 8000,
            Title = "工作項目 1",
            Type = "Task",
            State = "Active",
            Url = "https://dev.azure.com/org/project/_workitems/edit/8000",
            OriginalTeamName = "MyTeam",
            IsSuccess = true,
            ErrorMessage = null,
            SourcePullRequestId = 1,
            SourceProjectName = "group/project1",
            SourcePRUrl = "https://example.com/pr/1"
        };

        var task2 = new WorkItemOutput
        {
            WorkItemId = 8000,
            Title = "工作項目 2",
            Type = "Task",
            State = "Active",
            Url = "https://dev.azure.com/org/project/_workitems/edit/8000",
            OriginalTeamName = "MyTeam",
            IsSuccess = true,
            ErrorMessage = null,
            SourcePullRequestId = 2,
            SourceProjectName = "group/project2",
            SourcePRUrl = "https://example.com/pr/2"
        };

        var workItemFetchResult = new WorkItemFetchResult
        {
            WorkItems = new List<WorkItemOutput> { task1, task2 },
            TotalPRsAnalyzed = 2,
            TotalWorkItemsFound = 1,
            SuccessCount = 2,
            FailureCount = 0
        };

        var taskWorkItem = new WorkItem
        {
            WorkItemId = 8000,
            Title = "工作項目",
            Type = "Task",
            State = "Active",
            Url = "https://dev.azure.com/org/project/_workitems/edit/8000",
            OriginalTeamName = "MyTeam",
            ParentWorkItemId = 8001
        };

        var userStoryWorkItem = new WorkItem
        {
            WorkItemId = 8001,
            Title = "用戶故事",
            Type = "User Story",
            State = "Active",
            Url = "https://dev.azure.com/org/project/_workitems/edit/8001",
            OriginalTeamName = "MyTeam",
            ParentWorkItemId = null
        };

        SetupRedis(workItemFetchResult);
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(8000)).ReturnsAsync(Result<WorkItem>.Success(taskWorkItem));
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(8001)).ReturnsAsync(Result<WorkItem>.Success(userStoryWorkItem));

        var createTaskInstance = CreateTask();

        // Act
        await createTaskInstance.ExecuteAsync();

        // Assert
        _azureDevOpsRepositoryMock.Verify(x => x.GetWorkItemAsync(8000), Times.Once);
        _azureDevOpsRepositoryMock.Verify(x => x.GetWorkItemAsync(8001), Times.Once);
    }

    /// <summary>
    /// 測試 13: 統計數字驗證：TotalWorkItemsProcessed == AlreadyUserStoryCount + ResolvedCount + KeptOriginalCount
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_StatisticsShouldBeConsistent()
    {
        // Arrange
        var userStory = new WorkItemOutput
        {
            WorkItemId = 9000,
            Title = "用戶故事",
            Type = "User Story",
            State = "Active",
            Url = "https://dev.azure.com/org/project/_workitems/edit/9000",
            OriginalTeamName = "MyTeam",
            IsSuccess = true,
            ErrorMessage = null,
            SourcePullRequestId = 1,
            SourceProjectName = "group/project1",
            SourcePRUrl = "https://example.com/pr/1"
        };

        var task = new WorkItemOutput
        {
            WorkItemId = 9001,
            Title = "工作項目",
            Type = "Task",
            State = "Active",
            Url = "https://dev.azure.com/org/project/_workitems/edit/9001",
            OriginalTeamName = "MyTeam",
            IsSuccess = true,
            ErrorMessage = null,
            SourcePullRequestId = 2,
            SourceProjectName = "group/project2",
            SourcePRUrl = "https://example.com/pr/2"
        };

        var failedWorkItem = new WorkItemOutput
        {
            WorkItemId = 9002,
            Title = null,
            Type = null,
            State = null,
            Url = null,
            OriginalTeamName = null,
            IsSuccess = false,
            ErrorMessage = "找不到",
            SourcePullRequestId = 3,
            SourceProjectName = "group/project3",
            SourcePRUrl = "https://example.com/pr/3"
        };

        var workItemFetchResult = new WorkItemFetchResult
        {
            WorkItems = new List<WorkItemOutput> { userStory, task, failedWorkItem },
            TotalPRsAnalyzed = 3,
            TotalWorkItemsFound = 3,
            SuccessCount = 2,
            FailureCount = 1
        };

        var taskWorkItem = new WorkItem
        {
            WorkItemId = 9001,
            Title = "工作項目",
            Type = "Task",
            State = "Active",
            Url = "https://dev.azure.com/org/project/_workitems/edit/9001",
            OriginalTeamName = "MyTeam",
            ParentWorkItemId = 9010
        };

        var userStoryWorkItem = new WorkItem
        {
            WorkItemId = 9010,
            Title = "用戶故事",
            Type = "User Story",
            State = "Active",
            Url = "https://dev.azure.com/org/project/_workitems/edit/9010",
            OriginalTeamName = "MyTeam",
            ParentWorkItemId = null
        };

        SetupRedis(workItemFetchResult);
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(9001)).ReturnsAsync(Result<WorkItem>.Success(taskWorkItem));
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(9010)).ReturnsAsync(Result<WorkItem>.Success(userStoryWorkItem));

        var createTaskInstance = CreateTask();

        // Act
        await createTaskInstance.ExecuteAsync();

        // Assert
        VerifyRedisWrite(result =>
        {
            // TotalWorkItemsProcessed = AlreadyUserStoryCount + ResolvedCount + KeptOriginalCount
            Assert.Equal(3, result.TotalWorkItemsProcessed);
            Assert.Equal(1, result.AlreadyUserStoryCount);
            Assert.Equal(1, result.ResolvedCount);
            // 失敗的記錄也被計為 KeptOriginalCount
            Assert.Equal(1, result.KeptOriginalCount);
            Assert.Equal(3, result.AlreadyUserStoryCount + result.ResolvedCount + result.KeptOriginalCount);
        });
    }

    // Helper methods
    private GetUserStoryTask CreateTask()
    {
        return new GetUserStoryTask(
            _loggerMock.Object,
            _redisServiceMock.Object,
            _azureDevOpsRepositoryMock.Object);
    }

    private void SetupRedis(WorkItemFetchResult workItemFetchResult)
    {
        _redisServiceMock
            .Setup(x => x.GetAsync(RedisKeys.AzureDevOpsWorkItems))
            .ReturnsAsync(workItemFetchResult.ToJson());

        string? capturedJson = null;
        _redisServiceMock
            .Setup(x => x.SetAsync(RedisKeys.AzureDevOpsUserStories, It.IsAny<string>(), null))
            .Callback<string, string, TimeSpan?>((key, json, ttl) => capturedJson = json)
            .ReturnsAsync(true);
    }

    private void VerifyRedisWrite(Action<UserStoryFetchResult> assert)
    {
        _redisServiceMock.Verify(
            x => x.SetAsync(RedisKeys.AzureDevOpsUserStories, It.IsAny<string>(), null),
            Times.Once);

        var json = _redisServiceMock.Invocations
            .Where(i => i.Method.Name == nameof(IRedisService.SetAsync)
                && i.Arguments[0] as string == RedisKeys.AzureDevOpsUserStories)
            .Select(i => i.Arguments[1] as string)
            .FirstOrDefault();

        Assert.NotNull(json);
        var result = json.ToTypedObject<UserStoryFetchResult>();
        Assert.NotNull(result);
        assert(result);
    }
}
