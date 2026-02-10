using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Common;

namespace ReleaseKit.Application.Tests.Tasks;

/// <summary>
/// Release Branch Redis 整合測試
/// </summary>
public class ReleaseBranchRedisIntegrationTests
{
    #region GitLab Tests

    [Fact]
    public async Task FetchGitLabReleaseBranchTask_ShouldClearOldRedisData_WhenDataExists()
    {
        // Arrange
        var services = new ServiceCollection();
        var gitLabOptions = Options.Create(new GitLabOptions 
        { 
            Projects = new List<GitLabProjectOptions>()
        });
        
        var loggerMock = new Mock<ILogger<FetchGitLabReleaseBranchTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        
        // Mock Redis service - 設定有舊資料存在
        var redisServiceMock = new Mock<IRedisService>();
        redisServiceMock.Setup(x => x.ExistsAsync(RedisKeys.GitLabReleaseBranches))
            .ReturnsAsync(true);
        redisServiceMock.Setup(x => x.DeleteAsync(RedisKeys.GitLabReleaseBranches))
            .ReturnsAsync(true);
        redisServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(true);
        
        services.AddKeyedSingleton("GitLab", repositoryMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var task = new FetchGitLabReleaseBranchTask(
            serviceProvider,
            loggerMock.Object,
            redisServiceMock.Object,
            gitLabOptions);

        // Act
        await task.ExecuteAsync();

        // Assert - 驗證檢查並刪除舊資料
        redisServiceMock.Verify(
            x => x.ExistsAsync(RedisKeys.GitLabReleaseBranches),
            Times.Once);
        redisServiceMock.Verify(
            x => x.DeleteAsync(RedisKeys.GitLabReleaseBranches),
            Times.Once);
    }

    [Fact]
    public async Task FetchGitLabReleaseBranchTask_ShouldNotDeleteRedisData_WhenNoDataExists()
    {
        // Arrange
        var services = new ServiceCollection();
        var gitLabOptions = Options.Create(new GitLabOptions 
        { 
            Projects = new List<GitLabProjectOptions>()
        });
        
        var loggerMock = new Mock<ILogger<FetchGitLabReleaseBranchTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        
        // Mock Redis service - 設定沒有舊資料
        var redisServiceMock = new Mock<IRedisService>();
        redisServiceMock.Setup(x => x.ExistsAsync(RedisKeys.GitLabReleaseBranches))
            .ReturnsAsync(false);
        redisServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(true);
        
        services.AddKeyedSingleton("GitLab", repositoryMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var task = new FetchGitLabReleaseBranchTask(
            serviceProvider,
            loggerMock.Object,
            redisServiceMock.Object,
            gitLabOptions);

        // Act
        await task.ExecuteAsync();

        // Assert - 驗證沒有嘗試刪除資料
        redisServiceMock.Verify(
            x => x.ExistsAsync(RedisKeys.GitLabReleaseBranches),
            Times.Once);
        redisServiceMock.Verify(
            x => x.DeleteAsync(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task FetchGitLabReleaseBranchTask_ShouldSaveDataToRedis_AfterFetch()
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
                    TargetBranch = "main"
                }
            }
        });
        
        var loggerMock = new Mock<ILogger<FetchGitLabReleaseBranchTask>>();
        
        // Mock repository - 回傳一個 release branch
        var repositoryMock = new Mock<ISourceControlRepository>();
        repositoryMock
            .Setup(x => x.GetBranchesAsync("test/project", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "release/20260210" }.AsReadOnly()));
        
        // Mock Redis service
        var redisServiceMock = new Mock<IRedisService>();
        redisServiceMock.Setup(x => x.ExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(false);
        redisServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(true);
        
        services.AddKeyedSingleton("GitLab", repositoryMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var task = new FetchGitLabReleaseBranchTask(
            serviceProvider,
            loggerMock.Object,
            redisServiceMock.Object,
            gitLabOptions);

        // Act
        await task.ExecuteAsync();

        // Assert - 驗證儲存到 Redis
        redisServiceMock.Verify(
            x => x.SetAsync(
                RedisKeys.GitLabReleaseBranches,
                It.Is<string>(json => json.Contains("release/20260210") && json.Contains("test/project")),
                It.IsAny<TimeSpan?>()),
            Times.Once);
    }

    [Fact]
    public async Task FetchGitLabReleaseBranchTask_ShouldUseCorrectRedisKey()
    {
        // Arrange
        var services = new ServiceCollection();
        var gitLabOptions = Options.Create(new GitLabOptions 
        { 
            Projects = new List<GitLabProjectOptions>()
        });
        
        var loggerMock = new Mock<ILogger<FetchGitLabReleaseBranchTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        var redisServiceMock = new Mock<IRedisService>();
        
        redisServiceMock.Setup(x => x.ExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(false);
        redisServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(true);
        
        services.AddKeyedSingleton("GitLab", repositoryMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var task = new FetchGitLabReleaseBranchTask(
            serviceProvider,
            loggerMock.Object,
            redisServiceMock.Object,
            gitLabOptions);

        // Act
        await task.ExecuteAsync();

        // Assert - 驗證使用正確的 Redis Key
        redisServiceMock.Verify(
            x => x.ExistsAsync(RedisKeys.GitLabReleaseBranches),
            Times.Once);
        redisServiceMock.Verify(
            x => x.SetAsync(RedisKeys.GitLabReleaseBranches, It.IsAny<string>(), It.IsAny<TimeSpan?>()),
            Times.Once);
        
        // 驗證不應該使用 Bitbucket 的 Key
        redisServiceMock.Verify(
            x => x.ExistsAsync(RedisKeys.BitbucketReleaseBranches),
            Times.Never);
        redisServiceMock.Verify(
            x => x.SetAsync(RedisKeys.BitbucketReleaseBranches, It.IsAny<string>(), It.IsAny<TimeSpan?>()),
            Times.Never);
    }

    #endregion

    #region Bitbucket Tests

    [Fact]
    public async Task FetchBitbucketReleaseBranchTask_ShouldClearOldRedisData_WhenDataExists()
    {
        // Arrange
        var services = new ServiceCollection();
        var bitbucketOptions = Options.Create(new BitbucketOptions 
        { 
            Projects = new List<BitbucketProjectOptions>()
        });
        
        var loggerMock = new Mock<ILogger<FetchBitbucketReleaseBranchTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        
        // Mock Redis service - 設定有舊資料存在
        var redisServiceMock = new Mock<IRedisService>();
        redisServiceMock.Setup(x => x.ExistsAsync(RedisKeys.BitbucketReleaseBranches))
            .ReturnsAsync(true);
        redisServiceMock.Setup(x => x.DeleteAsync(RedisKeys.BitbucketReleaseBranches))
            .ReturnsAsync(true);
        redisServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(true);
        
        services.AddKeyedSingleton("Bitbucket", repositoryMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var task = new FetchBitbucketReleaseBranchTask(
            serviceProvider,
            loggerMock.Object,
            redisServiceMock.Object,
            bitbucketOptions);

        // Act
        await task.ExecuteAsync();

        // Assert - 驗證檢查並刪除舊資料
        redisServiceMock.Verify(
            x => x.ExistsAsync(RedisKeys.BitbucketReleaseBranches),
            Times.Once);
        redisServiceMock.Verify(
            x => x.DeleteAsync(RedisKeys.BitbucketReleaseBranches),
            Times.Once);
    }

    [Fact]
    public async Task FetchBitbucketReleaseBranchTask_ShouldNotDeleteRedisData_WhenNoDataExists()
    {
        // Arrange
        var services = new ServiceCollection();
        var bitbucketOptions = Options.Create(new BitbucketOptions 
        { 
            Projects = new List<BitbucketProjectOptions>()
        });
        
        var loggerMock = new Mock<ILogger<FetchBitbucketReleaseBranchTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        
        // Mock Redis service - 設定沒有舊資料
        var redisServiceMock = new Mock<IRedisService>();
        redisServiceMock.Setup(x => x.ExistsAsync(RedisKeys.BitbucketReleaseBranches))
            .ReturnsAsync(false);
        redisServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(true);
        
        services.AddKeyedSingleton("Bitbucket", repositoryMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var task = new FetchBitbucketReleaseBranchTask(
            serviceProvider,
            loggerMock.Object,
            redisServiceMock.Object,
            bitbucketOptions);

        // Act
        await task.ExecuteAsync();

        // Assert - 驗證沒有嘗試刪除資料
        redisServiceMock.Verify(
            x => x.ExistsAsync(RedisKeys.BitbucketReleaseBranches),
            Times.Once);
        redisServiceMock.Verify(
            x => x.DeleteAsync(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task FetchBitbucketReleaseBranchTask_ShouldSaveDataToRedis_AfterFetch()
    {
        // Arrange
        var services = new ServiceCollection();
        var bitbucketOptions = Options.Create(new BitbucketOptions 
        { 
            Projects = new List<BitbucketProjectOptions>
            {
                new BitbucketProjectOptions
                {
                    ProjectPath = "test/project",
                    TargetBranch = "main"
                }
            }
        });
        
        var loggerMock = new Mock<ILogger<FetchBitbucketReleaseBranchTask>>();
        
        // Mock repository - 回傳一個 release branch
        var repositoryMock = new Mock<ISourceControlRepository>();
        repositoryMock
            .Setup(x => x.GetBranchesAsync("test/project", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "release/20260210" }.AsReadOnly()));
        
        // Mock Redis service
        var redisServiceMock = new Mock<IRedisService>();
        redisServiceMock.Setup(x => x.ExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(false);
        redisServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(true);
        
        services.AddKeyedSingleton("Bitbucket", repositoryMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var task = new FetchBitbucketReleaseBranchTask(
            serviceProvider,
            loggerMock.Object,
            redisServiceMock.Object,
            bitbucketOptions);

        // Act
        await task.ExecuteAsync();

        // Assert - 驗證儲存到 Redis
        redisServiceMock.Verify(
            x => x.SetAsync(
                RedisKeys.BitbucketReleaseBranches,
                It.Is<string>(json => json.Contains("release/20260210") && json.Contains("test/project")),
                It.IsAny<TimeSpan?>()),
            Times.Once);
    }

    [Fact]
    public async Task FetchBitbucketReleaseBranchTask_ShouldUseCorrectRedisKey()
    {
        // Arrange
        var services = new ServiceCollection();
        var bitbucketOptions = Options.Create(new BitbucketOptions 
        { 
            Projects = new List<BitbucketProjectOptions>()
        });
        
        var loggerMock = new Mock<ILogger<FetchBitbucketReleaseBranchTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        var redisServiceMock = new Mock<IRedisService>();
        
        redisServiceMock.Setup(x => x.ExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(false);
        redisServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(true);
        
        services.AddKeyedSingleton("Bitbucket", repositoryMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var task = new FetchBitbucketReleaseBranchTask(
            serviceProvider,
            loggerMock.Object,
            redisServiceMock.Object,
            bitbucketOptions);

        // Act
        await task.ExecuteAsync();

        // Assert - 驗證使用正確的 Redis Key
        redisServiceMock.Verify(
            x => x.ExistsAsync(RedisKeys.BitbucketReleaseBranches),
            Times.Once);
        redisServiceMock.Verify(
            x => x.SetAsync(RedisKeys.BitbucketReleaseBranches, It.IsAny<string>(), It.IsAny<TimeSpan?>()),
            Times.Once);
        
        // 驗證不應該使用 GitLab 的 Key
        redisServiceMock.Verify(
            x => x.ExistsAsync(RedisKeys.GitLabReleaseBranches),
            Times.Never);
        redisServiceMock.Verify(
            x => x.SetAsync(RedisKeys.GitLabReleaseBranches, It.IsAny<string>(), It.IsAny<TimeSpan?>()),
            Times.Never);
    }

    #endregion
}
