using Microsoft.Extensions.Logging;
using Moq;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tests.Tasks;

/// <summary>
/// GetReleaseSettingTask 單元測試
/// </summary>
public class GetReleaseSettingTaskTests
{
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly Mock<INow> _nowMock;
    private readonly Mock<ILogger<GetReleaseSettingTask>> _loggerMock;
    private string? _capturedJson;

    public GetReleaseSettingTaskTests()
    {
        _redisServiceMock = new Mock<IRedisService>();
        _nowMock = new Mock<INow>();
        _loggerMock = new Mock<ILogger<GetReleaseSettingTask>>();

        // 預設時間：2025-04-25 UTC
        _nowMock.Setup(x => x.UtcNow).Returns(new DateTimeOffset(2025, 4, 25, 0, 0, 0, TimeSpan.Zero));

        // 預設 Redis 行為
        _redisServiceMock.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _redisServiceMock
            .Setup(x => x.SetAsync(RedisKeys.ReleaseSetting, It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .Callback<string, string, TimeSpan?>((_, json, _) => _capturedJson = json)
            .ReturnsAsync(true);
    }

    private GetReleaseSettingTask CreateTask()
    {
        return new GetReleaseSettingTask(
            _redisServiceMock.Object,
            _nowMock.Object,
            _loggerMock.Object);
    }

    /// <summary>
    /// 從捕獲的 JSON 反序列化出 ReleaseSettingOutput
    /// </summary>
    private ReleaseSettingOutput GetCapturedResult()
    {
        Assert.NotNull(_capturedJson);
        var result = _capturedJson!.ToTypedObject<ReleaseSettingOutput>();
        Assert.NotNull(result);
        return result!;
    }

    [Fact]
    public async Task ExecuteAsync_當兩個平台都無前置資料_應產生空設定並寫入Redis()
    {
        // Arrange
        _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.GitLabHash, RedisKeys.Fields.ReleaseBranches))
            .ReturnsAsync((string?)null);
        _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.BitbucketHash, RedisKeys.Fields.ReleaseBranches))
            .ReturnsAsync((string?)null);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        var result = GetCapturedResult();
        Assert.Empty(result.GitLab.Projects);
        Assert.Empty(result.Bitbucket.Projects);
        _redisServiceMock.Verify(
            x => x.SetAsync(RedisKeys.ReleaseSetting, It.IsAny<string>(), It.IsAny<TimeSpan?>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_當GitLab有有效ReleaseBranch_應產生BranchDiff設定()
    {
        // Arrange
        var branchData = new Dictionary<string, List<string>>
        {
            { "release/20250401", new List<string> { "group/project-a", "group/project-b" } }
        };

        _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.GitLabHash, RedisKeys.Fields.ReleaseBranches))
            .ReturnsAsync(branchData.ToJson());
        _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.BitbucketHash, RedisKeys.Fields.ReleaseBranches))
            .ReturnsAsync((string?)null);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        var result = GetCapturedResult();
        Assert.Equal(2, result.GitLab.Projects.Count);
        Assert.Equal("group/project-a", result.GitLab.Projects[0].ProjectPath);
        Assert.Equal("master", result.GitLab.Projects[0].TargetBranch);
        Assert.Equal(FetchMode.BranchDiff, result.GitLab.Projects[0].FetchMode);
        Assert.Equal("release/20250401", result.GitLab.Projects[0].SourceBranch);
        Assert.Equal("group/project-b", result.GitLab.Projects[1].ProjectPath);
        Assert.Equal(FetchMode.BranchDiff, result.GitLab.Projects[1].FetchMode);
        Assert.Empty(result.Bitbucket.Projects);
    }

    [Fact]
    public async Task ExecuteAsync_當Bitbucket有有效ReleaseBranch_TargetBranch應為develop()
    {
        // Arrange
        var branchData = new Dictionary<string, List<string>>
        {
            { "release/20250401", new List<string> { "workspace/repo-a" } }
        };

        _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.GitLabHash, RedisKeys.Fields.ReleaseBranches))
            .ReturnsAsync((string?)null);
        _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.BitbucketHash, RedisKeys.Fields.ReleaseBranches))
            .ReturnsAsync(branchData.ToJson());

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        var result = GetCapturedResult();
        Assert.Single(result.Bitbucket.Projects);
        Assert.Equal("develop", result.Bitbucket.Projects[0].TargetBranch);
        Assert.Equal(FetchMode.BranchDiff, result.Bitbucket.Projects[0].FetchMode);
        Assert.Equal("release/20250401", result.Bitbucket.Projects[0].SourceBranch);
        Assert.Empty(result.GitLab.Projects);
    }

    [Fact]
    public async Task ExecuteAsync_當有NotFound專案_應使用DateTimeRange模式()
    {
        // Arrange
        var branchData = new Dictionary<string, List<string>>
        {
            { "release/20250401", new List<string> { "group/project-a" } },
            { "NotFound", new List<string> { "group/project-b" } }
        };

        _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.GitLabHash, RedisKeys.Fields.ReleaseBranches))
            .ReturnsAsync(branchData.ToJson());
        _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.BitbucketHash, RedisKeys.Fields.ReleaseBranches))
            .ReturnsAsync((string?)null);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        var result = GetCapturedResult();
        var notFoundProject = result.GitLab.Projects.First(p => p.ProjectPath == "group/project-b");
        Assert.Equal(FetchMode.DateTimeRange, notFoundProject.FetchMode);
        Assert.Null(notFoundProject.SourceBranch);
        Assert.Null(notFoundProject.StartDateTime);
        Assert.Null(notFoundProject.EndDateTime);
    }

    [Fact]
    public async Task ExecuteAsync_當ReleaseBranch格式不符合yyyyMMdd_應使用DateTimeRange模式()
    {
        // Arrange
        var branchData = new Dictionary<string, List<string>>
        {
            { "release/hotfix-123", new List<string> { "group/project-a" } }
        };

        _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.GitLabHash, RedisKeys.Fields.ReleaseBranches))
            .ReturnsAsync(branchData.ToJson());
        _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.BitbucketHash, RedisKeys.Fields.ReleaseBranches))
            .ReturnsAsync((string?)null);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        var result = GetCapturedResult();
        Assert.Single(result.GitLab.Projects);
        Assert.Equal(FetchMode.DateTimeRange, result.GitLab.Projects[0].FetchMode);
        Assert.Null(result.GitLab.Projects[0].SourceBranch);
    }

    [Fact]
    public async Task ExecuteAsync_當ReleaseBranch日期超過三個月_應使用DateTimeRange模式()
    {
        // Arrange — 目前時間 2025-04-25，release/20250101 距離超過 3 個月
        var branchData = new Dictionary<string, List<string>>
        {
            { "release/20250101", new List<string> { "group/project-a" } }
        };

        _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.GitLabHash, RedisKeys.Fields.ReleaseBranches))
            .ReturnsAsync(branchData.ToJson());
        _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.BitbucketHash, RedisKeys.Fields.ReleaseBranches))
            .ReturnsAsync((string?)null);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        var result = GetCapturedResult();
        Assert.Single(result.GitLab.Projects);
        Assert.Equal(FetchMode.DateTimeRange, result.GitLab.Projects[0].FetchMode);
        Assert.Null(result.GitLab.Projects[0].SourceBranch);
    }

    [Fact]
    public async Task ExecuteAsync_當ReleaseBranch日期剛好三個月_應使用BranchDiff模式()
    {
        // Arrange — 目前時間 2025-04-25，cutoff = 2025-01-25
        // release/20250125 剛好等於 cutoff，不應退回 DateTimeRange
        _nowMock.Setup(x => x.UtcNow).Returns(new DateTimeOffset(2025, 4, 25, 0, 0, 0, TimeSpan.Zero));

        var branchData = new Dictionary<string, List<string>>
        {
            { "release/20250125", new List<string> { "group/project-a" } }
        };

        _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.GitLabHash, RedisKeys.Fields.ReleaseBranches))
            .ReturnsAsync(branchData.ToJson());
        _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.BitbucketHash, RedisKeys.Fields.ReleaseBranches))
            .ReturnsAsync((string?)null);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        var result = GetCapturedResult();
        Assert.Single(result.GitLab.Projects);
        Assert.Equal(FetchMode.BranchDiff, result.GitLab.Projects[0].FetchMode);
        Assert.Equal("release/20250125", result.GitLab.Projects[0].SourceBranch);
    }

    [Fact]
    public async Task ExecuteAsync_當ReleaseBranch日期超過三個月一天_應使用DateTimeRange模式()
    {
        // Arrange — 目前時間 2025-04-25，cutoff = 2025-01-25
        // release/20250124 比 cutoff 早一天，應退回 DateTimeRange
        _nowMock.Setup(x => x.UtcNow).Returns(new DateTimeOffset(2025, 4, 25, 0, 0, 0, TimeSpan.Zero));

        var branchData = new Dictionary<string, List<string>>
        {
            { "release/20250124", new List<string> { "group/project-a" } }
        };

        _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.GitLabHash, RedisKeys.Fields.ReleaseBranches))
            .ReturnsAsync(branchData.ToJson());
        _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.BitbucketHash, RedisKeys.Fields.ReleaseBranches))
            .ReturnsAsync((string?)null);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        var result = GetCapturedResult();
        Assert.Single(result.GitLab.Projects);
        Assert.Equal(FetchMode.DateTimeRange, result.GitLab.Projects[0].FetchMode);
        Assert.Null(result.GitLab.Projects[0].SourceBranch);
    }

    [Fact]
    public async Task ExecuteAsync_當ReleaseBranch格式符合但日期不存在_應使用DateTimeRange模式()
    {
        // Arrange — release/20250230（2 月 30 日不存在）格式符合正規表示式但日期無效
        var branchData = new Dictionary<string, List<string>>
        {
            { "release/20250230", new List<string> { "group/project-a" } }
        };

        _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.GitLabHash, RedisKeys.Fields.ReleaseBranches))
            .ReturnsAsync(branchData.ToJson());
        _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.BitbucketHash, RedisKeys.Fields.ReleaseBranches))
            .ReturnsAsync((string?)null);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        var result = GetCapturedResult();
        Assert.Single(result.GitLab.Projects);
        Assert.Equal(FetchMode.DateTimeRange, result.GitLab.Projects[0].FetchMode);
        Assert.Null(result.GitLab.Projects[0].SourceBranch);
    }

    [Fact]
    public async Task ExecuteAsync_當現在時間非午夜且ReleaseBranch日期剛好三個月_應使用BranchDiff模式()
    {
        // Arrange — 目前時間 2025-04-25 12:34:56 UTC（非午夜），cutoff 日期 = 2025-01-25
        // release/20250125 剛好等於 cutoff 日期，不應受時間成分影響而誤判為過期
        _nowMock.Setup(x => x.UtcNow).Returns(new DateTimeOffset(2025, 4, 25, 12, 34, 56, TimeSpan.Zero));

        var branchData = new Dictionary<string, List<string>>
        {
            { "release/20250125", new List<string> { "group/project-a" } }
        };

        _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.GitLabHash, RedisKeys.Fields.ReleaseBranches))
            .ReturnsAsync(branchData.ToJson());
        _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.BitbucketHash, RedisKeys.Fields.ReleaseBranches))
            .ReturnsAsync((string?)null);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        var result = GetCapturedResult();
        Assert.Single(result.GitLab.Projects);
        Assert.Equal(FetchMode.BranchDiff, result.GitLab.Projects[0].FetchMode);
        Assert.Equal("release/20250125", result.GitLab.Projects[0].SourceBranch);
    }

    [Fact]
    public async Task ExecuteAsync_當兩個平台都有資料_應正確產生完整設定()
    {
        // Arrange
        var gitLabData = new Dictionary<string, List<string>>
        {
            { "release/20250401", new List<string> { "group/project-a" } },
            { "release/20250315", new List<string> { "group/project-b" } },
            { "NotFound", new List<string> { "group/project-c" } }
        };
        var bitbucketData = new Dictionary<string, List<string>>
        {
            { "release/20250401", new List<string> { "workspace/repo-a" } },
            { "NotFound", new List<string> { "workspace/repo-b" } }
        };

        _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.GitLabHash, RedisKeys.Fields.ReleaseBranches))
            .ReturnsAsync(gitLabData.ToJson());
        _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.BitbucketHash, RedisKeys.Fields.ReleaseBranches))
            .ReturnsAsync(bitbucketData.ToJson());

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        var result = GetCapturedResult();

        // GitLab: 3 projects
        Assert.Equal(3, result.GitLab.Projects.Count);
        var glA = result.GitLab.Projects.First(p => p.ProjectPath == "group/project-a");
        var glB = result.GitLab.Projects.First(p => p.ProjectPath == "group/project-b");
        var glC = result.GitLab.Projects.First(p => p.ProjectPath == "group/project-c");

        Assert.Equal(FetchMode.BranchDiff, glA.FetchMode);
        Assert.Equal("release/20250401", glA.SourceBranch);
        Assert.Equal("master", glA.TargetBranch);

        Assert.Equal(FetchMode.BranchDiff, glB.FetchMode);
        Assert.Equal("release/20250315", glB.SourceBranch);
        Assert.Equal("master", glB.TargetBranch);

        Assert.Equal(FetchMode.DateTimeRange, glC.FetchMode);
        Assert.Null(glC.SourceBranch);
        Assert.Equal("master", glC.TargetBranch);

        // Bitbucket: 2 projects
        Assert.Equal(2, result.Bitbucket.Projects.Count);
        var bbA = result.Bitbucket.Projects.First(p => p.ProjectPath == "workspace/repo-a");
        var bbB = result.Bitbucket.Projects.First(p => p.ProjectPath == "workspace/repo-b");

        Assert.Equal(FetchMode.BranchDiff, bbA.FetchMode);
        Assert.Equal("release/20250401", bbA.SourceBranch);
        Assert.Equal("develop", bbA.TargetBranch);

        Assert.Equal(FetchMode.DateTimeRange, bbB.FetchMode);
        Assert.Null(bbB.SourceBranch);
        Assert.Equal("develop", bbB.TargetBranch);
    }
}
