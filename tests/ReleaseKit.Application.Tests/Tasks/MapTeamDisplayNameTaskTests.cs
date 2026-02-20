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
/// MapTeamDisplayNameTask 單元測試
/// </summary>
public class MapTeamDisplayNameTaskTests
{
    private readonly Mock<ILogger<MapTeamDisplayNameTask>> _loggerMock;
    private readonly Mock<IRedisService> _redisServiceMock;
    private string? _capturedRedisJson;
    private string? _capturedRedisKey;

    public MapTeamDisplayNameTaskTests()
    {
        _loggerMock = new Mock<ILogger<MapTeamDisplayNameTask>>();
        _redisServiceMock = new Mock<IRedisService>();

        _redisServiceMock
            .Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), null))
            .Callback<string, string, TimeSpan?>((key, json, ttl) =>
            {
                _capturedRedisKey = key;
                _capturedRedisJson = json;
            })
            .ReturnsAsync(true);
    }

    /// <summary>
    /// 當 Redis 中沒有資料時，不應寫入 Redis
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithNullRedisData_ShouldNotWriteToRedis()
    {
        // Arrange
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems))
            .ReturnsAsync((string?)null);

        var task = CreateTask(new List<TeamMapping>());

        // Act
        await task.ExecuteAsync();

        // Assert
        _redisServiceMock.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), null), Times.Never);
    }

    /// <summary>
    /// 當 Redis 中有空的 WorkItems 清單時，不應寫入 Redis
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithEmptyWorkItems_ShouldNotWriteToRedis()
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

        // Assert
        _redisServiceMock.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), null), Times.Never);
    }

    /// <summary>
    /// 當 OriginalTeamName 以 contains（忽略大小寫）匹配到 TeamMapping 時，應替換為 DisplayName
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithMatchingTeamName_ShouldReplaceWithDisplayName()
    {
        // Arrange
        var fetchResult = new UserStoryFetchResult
        {
            WorkItems = new List<UserStoryWorkItemOutput>
            {
                new UserStoryWorkItemOutput
                {
                    WorkItemId = 1,
                    Title = "測試功能",
                    Type = "User Story",
                    State = "Active",
                    Url = "https://dev.azure.com/org/proj/_workitems/edit/1",
                    OriginalTeamName = "MyOrg\\MoneyLogistic\\Team1",
                    IsSuccess = true,
                    ResolutionStatus = UserStoryResolutionStatus.AlreadyUserStoryOrAbove
                }
            },
            TotalWorkItems = 1,
            AlreadyUserStoryCount = 1,
            FoundViaRecursionCount = 0,
            NotFoundCount = 0,
            OriginalFetchFailedCount = 0
        };

        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems))
            .ReturnsAsync(fetchResult.ToJson());

        var teamMappings = new List<TeamMapping>
        {
            new TeamMapping { OriginalTeamName = "MoneyLogistic", DisplayName = "金流團隊" }
        };

        var task = CreateTask(teamMappings);

        // Act
        await task.ExecuteAsync();

        // Assert
        Assert.Equal(RedisKeys.AzureDevOpsUserStoryWorkItemsTeamMapped, _capturedRedisKey);
        var result = _capturedRedisJson!.ToTypedObject<UserStoryFetchResult>();
        Assert.NotNull(result);
        Assert.Single(result.WorkItems);
        Assert.Equal("金流團隊", result.WorkItems[0].OriginalTeamName);
    }

    /// <summary>
    /// 匹配應忽略大小寫
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithCaseInsensitiveMatch_ShouldReplaceWithDisplayName()
    {
        // Arrange
        var fetchResult = new UserStoryFetchResult
        {
            WorkItems = new List<UserStoryWorkItemOutput>
            {
                new UserStoryWorkItemOutput
                {
                    WorkItemId = 1,
                    Title = "測試功能",
                    Type = "User Story",
                    State = "Active",
                    Url = "https://dev.azure.com/org/proj/_workitems/edit/1",
                    OriginalTeamName = "MyOrg\\MONEYLOGISTIC\\Team1",
                    IsSuccess = true,
                    ResolutionStatus = UserStoryResolutionStatus.AlreadyUserStoryOrAbove
                }
            },
            TotalWorkItems = 1,
            AlreadyUserStoryCount = 1,
            FoundViaRecursionCount = 0,
            NotFoundCount = 0,
            OriginalFetchFailedCount = 0
        };

        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems))
            .ReturnsAsync(fetchResult.ToJson());

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
        Assert.Equal("金流團隊", result.WorkItems[0].OriginalTeamName);
    }

    /// <summary>
    /// 當 OriginalTeamName 無匹配時，應保留原始值
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithNoMatchingTeamName_ShouldKeepOriginalValue()
    {
        // Arrange
        var fetchResult = new UserStoryFetchResult
        {
            WorkItems = new List<UserStoryWorkItemOutput>
            {
                new UserStoryWorkItemOutput
                {
                    WorkItemId = 1,
                    Title = "測試功能",
                    Type = "User Story",
                    State = "Active",
                    Url = "https://dev.azure.com/org/proj/_workitems/edit/1",
                    OriginalTeamName = "SomeOtherTeam\\Group1",
                    IsSuccess = true,
                    ResolutionStatus = UserStoryResolutionStatus.AlreadyUserStoryOrAbove
                }
            },
            TotalWorkItems = 1,
            AlreadyUserStoryCount = 1,
            FoundViaRecursionCount = 0,
            NotFoundCount = 0,
            OriginalFetchFailedCount = 0
        };

        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems))
            .ReturnsAsync(fetchResult.ToJson());

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
        Assert.Equal("SomeOtherTeam\\Group1", result.WorkItems[0].OriginalTeamName);
    }

    /// <summary>
    /// 當 OriginalTeamName 為 null 時，應保持 null
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithNullTeamName_ShouldKeepNull()
    {
        // Arrange
        var fetchResult = new UserStoryFetchResult
        {
            WorkItems = new List<UserStoryWorkItemOutput>
            {
                new UserStoryWorkItemOutput
                {
                    WorkItemId = 1,
                    Title = null,
                    Type = null,
                    State = null,
                    Url = null,
                    OriginalTeamName = null,
                    IsSuccess = false,
                    ErrorMessage = "API error",
                    ResolutionStatus = UserStoryResolutionStatus.OriginalFetchFailed
                }
            },
            TotalWorkItems = 1,
            AlreadyUserStoryCount = 0,
            FoundViaRecursionCount = 0,
            NotFoundCount = 0,
            OriginalFetchFailedCount = 1
        };

        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems))
            .ReturnsAsync(fetchResult.ToJson());

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
        Assert.Null(result.WorkItems[0].OriginalTeamName);
    }

    /// <summary>
    /// 結果應存入新的 Redis Key（AzureDevOps:WorkItems:UserStories:TeamMapped）
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldWriteToNewRedisKey()
    {
        // Arrange
        var fetchResult = new UserStoryFetchResult
        {
            WorkItems = new List<UserStoryWorkItemOutput>
            {
                new UserStoryWorkItemOutput
                {
                    WorkItemId = 1,
                    Title = "功能",
                    Type = "User Story",
                    State = "Active",
                    Url = "https://dev.azure.com/org/proj/_workitems/edit/1",
                    OriginalTeamName = "Team/MoneyLogistic",
                    IsSuccess = true,
                    ResolutionStatus = UserStoryResolutionStatus.AlreadyUserStoryOrAbove
                }
            },
            TotalWorkItems = 1,
            AlreadyUserStoryCount = 1,
            FoundViaRecursionCount = 0,
            NotFoundCount = 0,
            OriginalFetchFailedCount = 0
        };

        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems))
            .ReturnsAsync(fetchResult.ToJson());

        var task = CreateTask(new List<TeamMapping>());

        // Act
        await task.ExecuteAsync();

        // Assert
        Assert.Equal(RedisKeys.AzureDevOpsUserStoryWorkItemsTeamMapped, _capturedRedisKey);
        _redisServiceMock.Verify(x => x.SetAsync(RedisKeys.AzureDevOpsUserStoryWorkItemsTeamMapped, It.IsAny<string>(), null), Times.Once);
        _redisServiceMock.Verify(x => x.SetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems, It.IsAny<string>(), null), Times.Never);
    }

    /// <summary>
    /// OriginalWorkItem 的 OriginalTeamName 也應套用 TeamMapping 對應
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithOriginalWorkItem_ShouldAlsoMapOriginalWorkItemTeamName()
    {
        // Arrange
        var fetchResult = new UserStoryFetchResult
        {
            WorkItems = new List<UserStoryWorkItemOutput>
            {
                new UserStoryWorkItemOutput
                {
                    WorkItemId = 99,
                    Title = "User Story",
                    Type = "User Story",
                    State = "Active",
                    Url = "https://dev.azure.com/org/proj/_workitems/edit/99",
                    OriginalTeamName = "Org\\MoneyLogistic\\Sub",
                    IsSuccess = true,
                    ResolutionStatus = UserStoryResolutionStatus.FoundViaRecursion,
                    OriginalWorkItem = new WorkItemOutput
                    {
                        WorkItemId = 11,
                        Title = "Bug",
                        Type = "Bug",
                        State = "Resolved",
                        Url = "https://dev.azure.com/org/proj/_workitems/edit/11",
                        OriginalTeamName = "Org\\MoneyLogistic\\Sub",
                        IsSuccess = true
                    }
                }
            },
            TotalWorkItems = 1,
            AlreadyUserStoryCount = 0,
            FoundViaRecursionCount = 1,
            NotFoundCount = 0,
            OriginalFetchFailedCount = 0
        };

        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems))
            .ReturnsAsync(fetchResult.ToJson());

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
        Assert.Single(result.WorkItems);
        Assert.Equal("金流團隊", result.WorkItems[0].OriginalTeamName);
        Assert.NotNull(result.WorkItems[0].OriginalWorkItem);
        Assert.Equal("金流團隊", result.WorkItems[0].OriginalWorkItem!.OriginalTeamName);
    }

    // Helper
    private MapTeamDisplayNameTask CreateTask(List<TeamMapping> teamMappings)
    {
        var options = Options.Create(new AzureDevOpsTeamMappingOptions
        {
            TeamMapping = teamMappings
        });

        return new MapTeamDisplayNameTask(
            _redisServiceMock.Object,
            options,
            _loggerMock.Object);
    }
}
