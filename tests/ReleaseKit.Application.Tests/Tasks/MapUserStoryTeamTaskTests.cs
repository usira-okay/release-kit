using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ReleaseKit.Application.Common;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tests.Tasks;

/// <summary>
/// MapUserStoryTeamTask 單元測試
/// </summary>
public class MapUserStoryTeamTaskTests
{
    private readonly Mock<ILogger<MapUserStoryTeamTask>> _loggerMock;
    private readonly Mock<IRedisService> _redisServiceMock;
    private string? _capturedRedisJson;

    public MapUserStoryTeamTaskTests()
    {
        _loggerMock = new Mock<ILogger<MapUserStoryTeamTask>>();
        _redisServiceMock = new Mock<IRedisService>();

        // Setup Redis write capture
        _redisServiceMock.Setup(x => x.SetAsync(RedisKeys.AzureDevOpsUserStoryWorkItemsTeamMapped, It.IsAny<string>(), null))
            .Callback<string, string, TimeSpan?>((key, json, ttl) => _capturedRedisJson = json)
            .ReturnsAsync(true);
    }

    /// <summary>
    /// T001: 測試當 Redis 中無資料時，應略過處理
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithNoRedisData_ShouldSkipProcessing()
    {
        // Arrange
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems))
            .ReturnsAsync((string?)null);

        var task = CreateTask(new List<TeamMapping>());

        // Act
        await task.ExecuteAsync();

        // Assert - Should NOT write to Redis when there's no data
        _redisServiceMock.Verify(x => x.SetAsync(RedisKeys.AzureDevOpsUserStoryWorkItemsTeamMapped, It.IsAny<string>(), null), Times.Never);
    }

    /// <summary>
    /// T002: 測試當 Redis 中資料為空清單時，應略過處理
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithEmptyWorkItems_ShouldSkipProcessing()
    {
        // Arrange
        var emptyResult = new UserStoryFetchResult
        {
            WorkItems = new List<UserStoryWorkItemOutput>(),
            TotalWorkItems = 0,
            AlreadyUserStoryCount = 0,
            FoundViaRecursionCount = 0,
            NotFoundCount = 0,
            OriginalFetchFailedCount = 0
        };

        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems))
            .ReturnsAsync(emptyResult.ToJson());

        var task = CreateTask(new List<TeamMapping>());

        // Act
        await task.ExecuteAsync();

        // Assert - Should NOT write to Redis when there's no data
        _redisServiceMock.Verify(x => x.SetAsync(RedisKeys.AzureDevOpsUserStoryWorkItemsTeamMapped, It.IsAny<string>(), null), Times.Never);
    }

    /// <summary>
    /// T003: 測試當 TeamMapping 設定為空時，應略過處理
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithNoTeamMapping_ShouldSkipProcessing()
    {
        // Arrange
        var userStoryResult = new UserStoryFetchResult
        {
            WorkItems = new List<UserStoryWorkItemOutput>
            {
                CreateUserStoryWorkItem(12345, "MoneyLogistic", "User Story 1")
            },
            TotalWorkItems = 1,
            AlreadyUserStoryCount = 1,
            FoundViaRecursionCount = 0,
            NotFoundCount = 0,
            OriginalFetchFailedCount = 0
        };

        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems))
            .ReturnsAsync(userStoryResult.ToJson());

        var task = CreateTask(new List<TeamMapping>()); // Empty team mapping

        // Act
        await task.ExecuteAsync();

        // Assert - Should NOT write to Redis when TeamMapping is empty
        _redisServiceMock.Verify(x => x.SetAsync(RedisKeys.AzureDevOpsUserStoryWorkItemsTeamMapped, It.IsAny<string>(), null), Times.Never);
    }

    /// <summary>
    /// T004: 測試正確的團隊名稱映射（精確匹配）
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithExactTeamNameMatch_ShouldMapToDisplayName()
    {
        // Arrange
        var userStoryResult = new UserStoryFetchResult
        {
            WorkItems = new List<UserStoryWorkItemOutput>
            {
                CreateUserStoryWorkItem(12345, "MoneyLogistic", "User Story 1"),
                CreateUserStoryWorkItem(12346, "DailyResource", "User Story 2")
            },
            TotalWorkItems = 2,
            AlreadyUserStoryCount = 2,
            FoundViaRecursionCount = 0,
            NotFoundCount = 0,
            OriginalFetchFailedCount = 0
        };

        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems))
            .ReturnsAsync(userStoryResult.ToJson());

        var teamMappings = new List<TeamMapping>
        {
            new TeamMapping { OriginalTeamName = "MoneyLogistic", DisplayName = "金流團隊" },
            new TeamMapping { OriginalTeamName = "DailyResource", DisplayName = "日常資源團隊" }
        };

        var task = CreateTask(teamMappings);

        // Act
        await task.ExecuteAsync();

        // Assert
        _redisServiceMock.Verify(x => x.SetAsync(RedisKeys.AzureDevOpsUserStoryWorkItemsTeamMapped, It.IsAny<string>(), null), Times.Once);

        var result = _capturedRedisJson!.ToTypedObject<UserStoryFetchResult>();
        Assert.NotNull(result);
        Assert.Equal(2, result.WorkItems.Count);
        Assert.Equal("金流團隊", result.WorkItems[0].OriginalTeamName);
        Assert.Equal("日常資源團隊", result.WorkItems[1].OriginalTeamName);
    }

    /// <summary>
    /// T005: 測試團隊名稱映射（忽略大小寫的 Contains 匹配）
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithCaseInsensitiveContainsMatch_ShouldMapToDisplayName()
    {
        // Arrange
        var userStoryResult = new UserStoryFetchResult
        {
            WorkItems = new List<UserStoryWorkItemOutput>
            {
                CreateUserStoryWorkItem(12345, "Platform/Web/MoneyLogistic", "User Story 1"),
                CreateUserStoryWorkItem(12346, "Backend/DAILYRESOURCE/API", "User Story 2"),
                CreateUserStoryWorkItem(12347, "moneylogistic-team", "User Story 3")
            },
            TotalWorkItems = 3,
            AlreadyUserStoryCount = 3,
            FoundViaRecursionCount = 0,
            NotFoundCount = 0,
            OriginalFetchFailedCount = 0
        };

        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems))
            .ReturnsAsync(userStoryResult.ToJson());

        var teamMappings = new List<TeamMapping>
        {
            new TeamMapping { OriginalTeamName = "MoneyLogistic", DisplayName = "金流團隊" },
            new TeamMapping { OriginalTeamName = "DailyResource", DisplayName = "日常資源團隊" }
        };

        var task = CreateTask(teamMappings);

        // Act
        await task.ExecuteAsync();

        // Assert
        _redisServiceMock.Verify(x => x.SetAsync(RedisKeys.AzureDevOpsUserStoryWorkItemsTeamMapped, It.IsAny<string>(), null), Times.Once);

        var result = _capturedRedisJson!.ToTypedObject<UserStoryFetchResult>();
        Assert.NotNull(result);
        Assert.Equal(3, result.WorkItems.Count);
        Assert.Equal("金流團隊", result.WorkItems[0].OriginalTeamName);
        Assert.Equal("日常資源團隊", result.WorkItems[1].OriginalTeamName);
        Assert.Equal("金流團隊", result.WorkItems[2].OriginalTeamName);
    }

    /// <summary>
    /// T006: 測試未匹配的團隊名稱保持原樣
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithUnmatchedTeamName_ShouldKeepOriginalName()
    {
        // Arrange
        var userStoryResult = new UserStoryFetchResult
        {
            WorkItems = new List<UserStoryWorkItemOutput>
            {
                CreateUserStoryWorkItem(12345, "MoneyLogistic", "User Story 1"),
                CreateUserStoryWorkItem(12346, "UnknownTeam", "User Story 2"),
                CreateUserStoryWorkItem(12347, "AnotherTeam", "User Story 3")
            },
            TotalWorkItems = 3,
            AlreadyUserStoryCount = 3,
            FoundViaRecursionCount = 0,
            NotFoundCount = 0,
            OriginalFetchFailedCount = 0
        };

        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems))
            .ReturnsAsync(userStoryResult.ToJson());

        var teamMappings = new List<TeamMapping>
        {
            new TeamMapping { OriginalTeamName = "MoneyLogistic", DisplayName = "金流團隊" }
        };

        var task = CreateTask(teamMappings);

        // Act
        await task.ExecuteAsync();

        // Assert
        _redisServiceMock.Verify(x => x.SetAsync(RedisKeys.AzureDevOpsUserStoryWorkItemsTeamMapped, It.IsAny<string>(), null), Times.Once);

        var result = _capturedRedisJson!.ToTypedObject<UserStoryFetchResult>();
        Assert.NotNull(result);
        Assert.Equal(3, result.WorkItems.Count);
        Assert.Equal("金流團隊", result.WorkItems[0].OriginalTeamName);
        Assert.Equal("UnknownTeam", result.WorkItems[1].OriginalTeamName); // 保持原樣
        Assert.Equal("AnotherTeam", result.WorkItems[2].OriginalTeamName); // 保持原樣
    }

    /// <summary>
    /// T007: 測試空的 OriginalTeamName 保持原樣
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithNullOrEmptyTeamName_ShouldKeepOriginal()
    {
        // Arrange
        var userStoryResult = new UserStoryFetchResult
        {
            WorkItems = new List<UserStoryWorkItemOutput>
            {
                CreateUserStoryWorkItem(12345, null, "User Story 1"),
                CreateUserStoryWorkItem(12346, "", "User Story 2"),
                CreateUserStoryWorkItem(12347, "MoneyLogistic", "User Story 3")
            },
            TotalWorkItems = 3,
            AlreadyUserStoryCount = 3,
            FoundViaRecursionCount = 0,
            NotFoundCount = 0,
            OriginalFetchFailedCount = 0
        };

        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems))
            .ReturnsAsync(userStoryResult.ToJson());

        var teamMappings = new List<TeamMapping>
        {
            new TeamMapping { OriginalTeamName = "MoneyLogistic", DisplayName = "金流團隊" }
        };

        var task = CreateTask(teamMappings);

        // Act
        await task.ExecuteAsync();

        // Assert
        _redisServiceMock.Verify(x => x.SetAsync(RedisKeys.AzureDevOpsUserStoryWorkItemsTeamMapped, It.IsAny<string>(), null), Times.Once);

        var result = _capturedRedisJson!.ToTypedObject<UserStoryFetchResult>();
        Assert.NotNull(result);
        Assert.Equal(3, result.WorkItems.Count);
        Assert.Null(result.WorkItems[0].OriginalTeamName); // 保持 null
        Assert.Equal("", result.WorkItems[1].OriginalTeamName); // 保持空字串
        Assert.Equal("金流團隊", result.WorkItems[2].OriginalTeamName); // 已映射
    }

    /// <summary>
    /// T008: 測試統計資訊保持不變
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldPreserveStatistics()
    {
        // Arrange
        var userStoryResult = new UserStoryFetchResult
        {
            WorkItems = new List<UserStoryWorkItemOutput>
            {
                CreateUserStoryWorkItem(12345, "MoneyLogistic", "User Story 1")
            },
            TotalWorkItems = 10,
            AlreadyUserStoryCount = 5,
            FoundViaRecursionCount = 3,
            NotFoundCount = 1,
            OriginalFetchFailedCount = 1
        };

        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems))
            .ReturnsAsync(userStoryResult.ToJson());

        var teamMappings = new List<TeamMapping>
        {
            new TeamMapping { OriginalTeamName = "MoneyLogistic", DisplayName = "金流團隊" }
        };

        var task = CreateTask(teamMappings);

        // Act
        await task.ExecuteAsync();

        // Assert
        var result = _capturedRedisJson!.ToTypedObject<UserStoryFetchResult>();
        Assert.NotNull(result);
        Assert.Equal(10, result.TotalWorkItems);
        Assert.Equal(5, result.AlreadyUserStoryCount);
        Assert.Equal(3, result.FoundViaRecursionCount);
        Assert.Equal(1, result.NotFoundCount);
        Assert.Equal(1, result.OriginalFetchFailedCount);
    }

    private MapUserStoryTeamTask CreateTask(List<TeamMapping> teamMappings)
    {
        var teamMappingOptions = Options.Create(new TeamMappingOptions
        {
            Mappings = teamMappings
        });

        return new MapUserStoryTeamTask(_redisServiceMock.Object, _loggerMock.Object, teamMappingOptions);
    }

    private UserStoryWorkItemOutput CreateUserStoryWorkItem(int workItemId, string? originalTeamName, string title)
    {
        return new UserStoryWorkItemOutput
        {
            WorkItemId = workItemId,
            Title = title,
            Type = "User Story",
            State = "Active",
            Url = $"https://dev.azure.com/org/proj/_workitems/edit/{workItemId}",
            OriginalTeamName = originalTeamName,
            IsSuccess = true,
            ErrorMessage = null,
            ResolutionStatus = UserStoryResolutionStatus.AlreadyUserStoryOrAbove,
            PrId = null,
            OriginalWorkItem = null
        };
    }
}
