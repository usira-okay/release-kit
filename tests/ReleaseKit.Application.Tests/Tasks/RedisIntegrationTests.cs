using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Common;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Application.Tests.Tasks;

/// <summary>
/// Redis 整合測試
/// </summary>
public class RedisIntegrationTests
{
    [Fact]
    public async Task FetchGitLabPullRequestsTask_ShouldClearOldRedisData_WhenDataExists()
    {
        // Arrange
        var services = new ServiceCollection();
        var gitLabOptions = Options.Create(new GitLabOptions 
        { 
            Projects = new List<GitLabProjectOptions>()
        });
        var fetchModeOptions = Options.Create(new FetchModeOptions());
        
        var loggerMock = new Mock<ILogger<FetchGitLabPullRequestsTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        
        // Mock Redis service - 設定有舊資料存在
        var redisServiceMock = new Mock<IRedisService>();
        redisServiceMock.Setup(x => x.ExistsAsync(RedisKeys.GitLabPullRequests))
            .ReturnsAsync(true);
        redisServiceMock.Setup(x => x.DeleteAsync(RedisKeys.GitLabPullRequests))
            .ReturnsAsync(true);
        redisServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(true);
        
        services.AddKeyedSingleton("GitLab", repositoryMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var task = new FetchGitLabPullRequestsTask(
            serviceProvider,
            loggerMock.Object,
            redisServiceMock.Object,
            gitLabOptions,
            fetchModeOptions);

        // Act
        await task.ExecuteAsync();

        // Assert - 驗證檢查並刪除舊資料
        redisServiceMock.Verify(
            x => x.ExistsAsync(RedisKeys.GitLabPullRequests),
            Times.Once);
        redisServiceMock.Verify(
            x => x.DeleteAsync(RedisKeys.GitLabPullRequests),
            Times.Once);
    }

    [Fact]
    public async Task FetchGitLabPullRequestsTask_ShouldNotDeleteRedisData_WhenNoDataExists()
    {
        // Arrange
        var services = new ServiceCollection();
        var gitLabOptions = Options.Create(new GitLabOptions 
        { 
            Projects = new List<GitLabProjectOptions>()
        });
        var fetchModeOptions = Options.Create(new FetchModeOptions());
        
        var loggerMock = new Mock<ILogger<FetchGitLabPullRequestsTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        
        // Mock Redis service - 設定沒有舊資料
        var redisServiceMock = new Mock<IRedisService>();
        redisServiceMock.Setup(x => x.ExistsAsync(RedisKeys.GitLabPullRequests))
            .ReturnsAsync(false);
        redisServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(true);
        
        services.AddKeyedSingleton("GitLab", repositoryMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var task = new FetchGitLabPullRequestsTask(
            serviceProvider,
            loggerMock.Object,
            redisServiceMock.Object,
            gitLabOptions,
            fetchModeOptions);

        // Act
        await task.ExecuteAsync();

        // Assert - 驗證沒有嘗試刪除資料
        redisServiceMock.Verify(
            x => x.ExistsAsync(RedisKeys.GitLabPullRequests),
            Times.Once);
        redisServiceMock.Verify(
            x => x.DeleteAsync(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task FetchGitLabPullRequestsTask_ShouldSaveDataToRedis_AfterFetch()
    {
        // Arrange
        var services = new ServiceCollection();
        var gitLabOptions = Options.Create(new GitLabOptions 
        { 
            Projects = new List<GitLabProjectOptions>
            {
                new GitLabProjectOptions
                {
                    ProjectPath = "test/project",
                    TargetBranch = "main",
                    FetchMode = FetchMode.DateTimeRange,
                    StartDateTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    EndDateTime = new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.Zero)
                }
            }
        });
        var fetchModeOptions = Options.Create(new FetchModeOptions());
        
        var loggerMock = new Mock<ILogger<FetchGitLabPullRequestsTask>>();
        
        // Mock repository
        var repositoryMock = new Mock<ISourceControlRepository>();
        var mockMergeRequests = new List<MergeRequest>
        {
            new MergeRequest
            {
                PullRequestId = 1,
                Title = "Test MR",
                Description = "Test",
                SourceBranch = "feature/test",
                TargetBranch = "main",
                CreatedAt = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero),
                MergedAt = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero),
                State = "merged",
                AuthorUserId = "user1",
                AuthorName = "Test User",
                PRUrl = "https://gitlab.com/test/project/-/merge_requests/1",
                Platform = SourceControlPlatform.GitLab,
                ProjectPath = "test/project"
            }
        };
        
        repositoryMock
            .Setup(x => x.GetMergeRequestsByDateRangeAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<MergeRequest>>.Success(mockMergeRequests));
        
        // Mock Redis service
        var redisServiceMock = new Mock<IRedisService>();
        redisServiceMock.Setup(x => x.ExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(false);
        redisServiceMock.Setup(x => x.SetAsync(RedisKeys.GitLabPullRequests, It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(true);
        
        services.AddKeyedSingleton("GitLab", repositoryMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var task = new FetchGitLabPullRequestsTask(
            serviceProvider,
            loggerMock.Object,
            redisServiceMock.Object,
            gitLabOptions,
            fetchModeOptions);

        // Act
        await task.ExecuteAsync();

        // Assert - 驗證資料已存入 Redis
        redisServiceMock.Verify(
            x => x.SetAsync(
                RedisKeys.GitLabPullRequests, 
                It.Is<string>(json => json.Contains("Test MR")), 
                null),
            Times.Once);
    }

    [Fact]
    public async Task FetchBitbucketPullRequestsTask_ShouldClearOldRedisData_WhenDataExists()
    {
        // Arrange
        var services = new ServiceCollection();
        var bitbucketOptions = Options.Create(new BitbucketOptions 
        { 
            Projects = new List<BitbucketProjectOptions>()
        });
        var fetchModeOptions = Options.Create(new FetchModeOptions());
        
        var loggerMock = new Mock<ILogger<FetchBitbucketPullRequestsTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        
        // Mock Redis service - 設定有舊資料存在
        var redisServiceMock = new Mock<IRedisService>();
        redisServiceMock.Setup(x => x.ExistsAsync(RedisKeys.BitbucketPullRequests))
            .ReturnsAsync(true);
        redisServiceMock.Setup(x => x.DeleteAsync(RedisKeys.BitbucketPullRequests))
            .ReturnsAsync(true);
        redisServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(true);
        
        services.AddKeyedSingleton("Bitbucket", repositoryMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var task = new FetchBitbucketPullRequestsTask(
            serviceProvider,
            loggerMock.Object,
            redisServiceMock.Object,
            bitbucketOptions,
            fetchModeOptions);

        // Act
        await task.ExecuteAsync();

        // Assert - 驗證檢查並刪除舊資料
        redisServiceMock.Verify(
            x => x.ExistsAsync(RedisKeys.BitbucketPullRequests),
            Times.Once);
        redisServiceMock.Verify(
            x => x.DeleteAsync(RedisKeys.BitbucketPullRequests),
            Times.Once);
    }

    [Fact]
    public async Task FetchBitbucketPullRequestsTask_ShouldSaveDataToRedis_AfterFetch()
    {
        // Arrange
        var services = new ServiceCollection();
        var bitbucketOptions = Options.Create(new BitbucketOptions 
        { 
            Projects = new List<BitbucketProjectOptions>
            {
                new BitbucketProjectOptions
                {
                    ProjectPath = "workspace/repo",
                    TargetBranch = "main",
                    FetchMode = FetchMode.DateTimeRange,
                    StartDateTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    EndDateTime = new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.Zero)
                }
            }
        });
        var fetchModeOptions = Options.Create(new FetchModeOptions());
        
        var loggerMock = new Mock<ILogger<FetchBitbucketPullRequestsTask>>();
        
        // Mock repository
        var repositoryMock = new Mock<ISourceControlRepository>();
        var mockMergeRequests = new List<MergeRequest>
        {
            new MergeRequest
            {
                PullRequestId = 1,
                Title = "Test PR",
                Description = "Test",
                SourceBranch = "feature/test",
                TargetBranch = "main",
                CreatedAt = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero),
                MergedAt = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero),
                State = "merged",
                AuthorUserId = "user1",
                AuthorName = "Test User",
                PRUrl = "https://bitbucket.org/workspace/repo/pull-requests/1",
                Platform = SourceControlPlatform.Bitbucket,
                ProjectPath = "workspace/repo"
            }
        };
        
        repositoryMock
            .Setup(x => x.GetMergeRequestsByDateRangeAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<MergeRequest>>.Success(mockMergeRequests));
        
        // Mock Redis service
        var redisServiceMock = new Mock<IRedisService>();
        redisServiceMock.Setup(x => x.ExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(false);
        redisServiceMock.Setup(x => x.SetAsync(RedisKeys.BitbucketPullRequests, It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(true);
        
        services.AddKeyedSingleton("Bitbucket", repositoryMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var task = new FetchBitbucketPullRequestsTask(
            serviceProvider,
            loggerMock.Object,
            redisServiceMock.Object,
            bitbucketOptions,
            fetchModeOptions);

        // Act
        await task.ExecuteAsync();

        // Assert - 驗證資料已存入 Redis
        redisServiceMock.Verify(
            x => x.SetAsync(
                RedisKeys.BitbucketPullRequests, 
                It.Is<string>(json => json.Contains("Test PR")), 
                null),
            Times.Once);
    }

    [Fact]
    public async Task FetchGitLabPullRequestsTask_ShouldUseCorrectRedisKey()
    {
        // Arrange
        var services = new ServiceCollection();
        var gitLabOptions = Options.Create(new GitLabOptions { Projects = new List<GitLabProjectOptions>() });
        var fetchModeOptions = Options.Create(new FetchModeOptions());
        
        var loggerMock = new Mock<ILogger<FetchGitLabPullRequestsTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        var redisServiceMock = new Mock<IRedisService>();
        
        redisServiceMock.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        redisServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>())).ReturnsAsync(true);
        
        services.AddKeyedSingleton("GitLab", repositoryMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var task = new FetchGitLabPullRequestsTask(
            serviceProvider,
            loggerMock.Object,
            redisServiceMock.Object,
            gitLabOptions,
            fetchModeOptions);

        // Act
        await task.ExecuteAsync();

        // Assert - 驗證使用正確的 Redis Key
        redisServiceMock.Verify(x => x.ExistsAsync(RedisKeys.GitLabPullRequests), Times.Once);
        redisServiceMock.Verify(x => x.SetAsync(RedisKeys.GitLabPullRequests, It.IsAny<string>(), null), Times.Once);
        
        // 確保沒有使用 Bitbucket 的 Key
        redisServiceMock.Verify(x => x.ExistsAsync(RedisKeys.BitbucketPullRequests), Times.Never);
        redisServiceMock.Verify(x => x.SetAsync(RedisKeys.BitbucketPullRequests, It.IsAny<string>(), It.IsAny<TimeSpan?>()), Times.Never);
    }

    [Fact]
    public async Task FetchBitbucketPullRequestsTask_ShouldUseCorrectRedisKey()
    {
        // Arrange
        var services = new ServiceCollection();
        var bitbucketOptions = Options.Create(new BitbucketOptions { Projects = new List<BitbucketProjectOptions>() });
        var fetchModeOptions = Options.Create(new FetchModeOptions());
        
        var loggerMock = new Mock<ILogger<FetchBitbucketPullRequestsTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        var redisServiceMock = new Mock<IRedisService>();
        
        redisServiceMock.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        redisServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>())).ReturnsAsync(true);
        
        services.AddKeyedSingleton("Bitbucket", repositoryMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var task = new FetchBitbucketPullRequestsTask(
            serviceProvider,
            loggerMock.Object,
            redisServiceMock.Object,
            bitbucketOptions,
            fetchModeOptions);

        // Act
        await task.ExecuteAsync();

        // Assert - 驗證使用正確的 Redis Key
        redisServiceMock.Verify(x => x.ExistsAsync(RedisKeys.BitbucketPullRequests), Times.Once);
        redisServiceMock.Verify(x => x.SetAsync(RedisKeys.BitbucketPullRequests, It.IsAny<string>(), null), Times.Once);
        
        // 確保沒有使用 GitLab 的 Key
        redisServiceMock.Verify(x => x.ExistsAsync(RedisKeys.GitLabPullRequests), Times.Never);
        redisServiceMock.Verify(x => x.SetAsync(RedisKeys.GitLabPullRequests, It.IsAny<string>(), It.IsAny<TimeSpan?>()), Times.Never);
    }
}
