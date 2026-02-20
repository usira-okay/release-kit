using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ReleaseKit.Application.Common;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Common.Configuration;

namespace ReleaseKit.Application.Tests.Tasks;

/// <summary>
/// MapUserStoryTeamTask 單元測試
/// </summary>
public class MapUserStoryTeamTaskTests
{
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly Mock<ILogger<MapUserStoryTeamTask>> _loggerMock;
    private string? _capturedJson;

    public MapUserStoryTeamTaskTests()
    {
        _redisServiceMock = new Mock<IRedisService>();
        _loggerMock = new Mock<ILogger<MapUserStoryTeamTask>>();

        _redisServiceMock.Setup(x => x.SetAsync(RedisKeys.AzureDevOpsUserStoryTeamMappedWorkItems, It.IsAny<string>(), null))
            .Callback<string, string, TimeSpan?>((_, json, _) => _capturedJson = json)
            .ReturnsAsync(true);
    }

    /// <summary>
    /// 驗證 OriginalTeamName 會以忽略大小寫的 contains 規則映射至 DisplayName，並寫入新 Redis Key
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldMapTeamNameWithContainsIgnoreCase_AndWriteToNewRedisKey()
    {
        // Arrange
        var fetchResult = new UserStoryFetchResult
        {
            WorkItems = new List<UserStoryWorkItemOutput>
            {
                new()
                {
                    WorkItemId = 1001,
                    Title = "登入功能",
                    Type = "User Story",
                    State = "Active",
                    Url = "https://dev.azure.com/org/proj/_workitems/edit/1001",
                    OriginalTeamName = "Project\\MoneyLogistic",
                    PrId = "PR-1",
                    IsSuccess = true,
                    ErrorMessage = null,
                    ResolutionStatus = UserStoryResolutionStatus.AlreadyUserStoryOrAbove,
                    OriginalWorkItem = null
                },
                new()
                {
                    WorkItemId = 1002,
                    Title = "重構登入 API",
                    Type = "Feature",
                    State = "Active",
                    Url = "https://dev.azure.com/org/proj/_workitems/edit/1002",
                    OriginalTeamName = "project\\dailyresource\\sub",
                    PrId = "PR-2",
                    IsSuccess = true,
                    ErrorMessage = null,
                    ResolutionStatus = UserStoryResolutionStatus.FoundViaRecursion,
                    OriginalWorkItem = new WorkItemOutput
                    {
                        WorkItemId = 2001,
                        Title = "修正登入錯誤碼",
                        Type = "Task",
                        State = "Closed",
                        Url = "https://dev.azure.com/org/proj/_workitems/edit/2001",
                        OriginalTeamName = "PROJECT\\DailyResource\\SUB",
                        PrId = null,
                        IsSuccess = true,
                        ErrorMessage = null
                    }
                }
            },
            TotalWorkItems = 2,
            AlreadyUserStoryCount = 1,
            FoundViaRecursionCount = 1,
            NotFoundCount = 0,
            OriginalFetchFailedCount = 0
        };

        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems))
            .ReturnsAsync(fetchResult.ToJson());

        var options = Options.Create(new AzureDevOpsOptions
        {
            TeamMapping = new List<TeamMappingOptions>
            {
                new() { OriginalTeamName = "MoneyLogistic", DisplayName = "金流團隊" },
                new() { OriginalTeamName = "DailyResource", DisplayName = "日常資源團隊" }
            }
        });

        var task = new MapUserStoryTeamTask(_redisServiceMock.Object, options, _loggerMock.Object);

        // Act
        await task.ExecuteAsync();

        // Assert
        _redisServiceMock.Verify(x => x.SetAsync(RedisKeys.AzureDevOpsUserStoryTeamMappedWorkItems, It.IsAny<string>(), null), Times.Once);
        var mappedResult = _capturedJson!.ToTypedObject<UserStoryFetchResult>();
        Assert.NotNull(mappedResult);

        Assert.Equal("金流團隊", mappedResult!.WorkItems[0].OriginalTeamName);
        Assert.Equal("日常資源團隊", mappedResult.WorkItems[1].OriginalTeamName);
        Assert.Equal("日常資源團隊", mappedResult.WorkItems[1].OriginalWorkItem!.OriginalTeamName);
    }
}
