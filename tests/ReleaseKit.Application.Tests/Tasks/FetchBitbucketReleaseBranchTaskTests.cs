using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Common;

namespace ReleaseKit.Application.Tests.Tasks;

/// <summary>
/// FetchBitbucketReleaseBranchTask 單元測試
/// </summary>
public class FetchBitbucketReleaseBranchTaskTests
{
    [Fact]
    public async Task FetchBitbucketReleaseBranchTask_ExecuteAsync_WithEmptyProjects_ShouldCompleteSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        var bitbucketOptions = Options.Create(new BitbucketOptions { Projects = new List<BitbucketProjectOptions>() });

        // Mock logger
        var loggerMock = new Mock<ILogger<FetchBitbucketReleaseBranchTask>>();

        // Mock keyed repository service
        var repositoryMock = new Mock<ISourceControlRepository>();

        // Mock Redis service
        var redisServiceMock = new Mock<IRedisService>();
        redisServiceMock.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        redisServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>())).ReturnsAsync(true);

        services.AddKeyedSingleton("Bitbucket", repositoryMock.Object);

        var serviceProvider = services.BuildServiceProvider();

        var task = new FetchBitbucketReleaseBranchTask(
            serviceProvider,
            loggerMock.Object,
            redisServiceMock.Object,
            bitbucketOptions);

        // Act
        await task.ExecuteAsync();

        // Assert - should complete without exception
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("開始執行 Bitbucket Release Branch 拉取任務")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task FetchBitbucketReleaseBranchTask_ExecuteAsync_WithProjectsHavingReleaseBranches_ShouldGroupByBranchName()
    {
        // Arrange
        var services = new ServiceCollection();
        var bitbucketOptions = Options.Create(new BitbucketOptions
        {
            Projects = new List<BitbucketProjectOptions>
            {
                new BitbucketProjectOptions
                {
                    ProjectPath = "workspace/project1",
                    TargetBranch = "main"
                },
                new BitbucketProjectOptions
                {
                    ProjectPath = "workspace/project2",
                    TargetBranch = "main"
                }
            }
        });

        var loggerMock = new Mock<ILogger<FetchBitbucketReleaseBranchTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        var redisServiceMock = new Mock<IRedisService>();

        // 設定 project1 有 release branch
        repositoryMock
            .Setup(x => x.GetBranchesAsync("workspace/project1", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "release/20260201" }.AsReadOnly()));

        // 設定 project2 有 release branch
        repositoryMock
            .Setup(x => x.GetBranchesAsync("workspace/project2", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "release/20260201" }.AsReadOnly()));

        redisServiceMock.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        redisServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>())).ReturnsAsync(true);

        services.AddKeyedSingleton("Bitbucket", repositoryMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var task = new FetchBitbucketReleaseBranchTask(
            serviceProvider,
            loggerMock.Object,
            redisServiceMock.Object,
            bitbucketOptions);

        // Act
        await task.ExecuteAsync();

        // Assert
        redisServiceMock.Verify(
            x => x.SetAsync(
                It.IsAny<string>(),
                It.Is<string>(json => json.Contains("release/20260201") && json.Contains("workspace/project1") && json.Contains("workspace/project2")),
                It.IsAny<TimeSpan?>()),
            Times.Once);
    }

    [Fact]
    public async Task FetchBitbucketReleaseBranchTask_ExecuteAsync_WithProjectsWithoutReleaseBranches_ShouldAddToNotFound()
    {
        // Arrange
        var services = new ServiceCollection();
        var bitbucketOptions = Options.Create(new BitbucketOptions
        {
            Projects = new List<BitbucketProjectOptions>
            {
                new BitbucketProjectOptions
                {
                    ProjectPath = "workspace/project-no-release",
                    TargetBranch = "main"
                }
            }
        });

        var loggerMock = new Mock<ILogger<FetchBitbucketReleaseBranchTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        var redisServiceMock = new Mock<IRedisService>();

        // 設定專案沒有 release branch（回傳空清單）
        repositoryMock
            .Setup(x => x.GetBranchesAsync("workspace/project-no-release", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string>().AsReadOnly()));

        redisServiceMock.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        redisServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>())).ReturnsAsync(true);

        services.AddKeyedSingleton("Bitbucket", repositoryMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var task = new FetchBitbucketReleaseBranchTask(
            serviceProvider,
            loggerMock.Object,
            redisServiceMock.Object,
            bitbucketOptions);

        // Act
        await task.ExecuteAsync();

        // Assert
        redisServiceMock.Verify(
            x => x.SetAsync(
                It.IsAny<string>(),
                It.Is<string>(json => json.Contains("NotFound") && json.Contains("workspace/project-no-release")),
                It.IsAny<TimeSpan?>()),
            Times.Once);
    }

    [Fact]
    public async Task FetchBitbucketReleaseBranchTask_ExecuteAsync_WithMultipleReleaseBranches_ShouldPickLatest()
    {
        // Arrange
        var services = new ServiceCollection();
        var bitbucketOptions = Options.Create(new BitbucketOptions
        {
            Projects = new List<BitbucketProjectOptions>
            {
                new BitbucketProjectOptions
                {
                    ProjectPath = "workspace/project-multi",
                    TargetBranch = "main"
                }
            }
        });

        var loggerMock = new Mock<ILogger<FetchBitbucketReleaseBranchTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        var redisServiceMock = new Mock<IRedisService>();

        // 設定專案有多個 release branch，應取最新的（字母排序最大）
        repositoryMock
            .Setup(x => x.GetBranchesAsync("workspace/project-multi", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string>
            {
                "release/20260101",
                "release/20260115",
                "release/20260210"  // 最新
            }.AsReadOnly()));

        redisServiceMock.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        redisServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>())).ReturnsAsync(true);

        services.AddKeyedSingleton("Bitbucket", repositoryMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var task = new FetchBitbucketReleaseBranchTask(
            serviceProvider,
            loggerMock.Object,
            redisServiceMock.Object,
            bitbucketOptions);

        // Act
        await task.ExecuteAsync();

        // Assert - 應該只有最新的 release/20260210
        redisServiceMock.Verify(
            x => x.SetAsync(
                It.IsAny<string>(),
                It.Is<string>(json => json.Contains("release/20260210") && !json.Contains("release/20260101") && !json.Contains("release/20260115")),
                It.IsAny<TimeSpan?>()),
            Times.Once);
    }

    [Fact]
    public async Task FetchBitbucketReleaseBranchTask_ExecuteAsync_WithGetBranchesFailure_ShouldAddToNotFound()
    {
        // Arrange
        var services = new ServiceCollection();
        var bitbucketOptions = Options.Create(new BitbucketOptions
        {
            Projects = new List<BitbucketProjectOptions>
            {
                new BitbucketProjectOptions
                {
                    ProjectPath = "workspace/project-error",
                    TargetBranch = "main"
                }
            }
        });

        var loggerMock = new Mock<ILogger<FetchBitbucketReleaseBranchTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        var redisServiceMock = new Mock<IRedisService>();

        // 設定 GetBranchesAsync 失敗
        repositoryMock
            .Setup(x => x.GetBranchesAsync("workspace/project-error", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Failure(Error.SourceControl.ApiError("Failed to get branches")));

        redisServiceMock.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        redisServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>())).ReturnsAsync(true);

        services.AddKeyedSingleton("Bitbucket", repositoryMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var task = new FetchBitbucketReleaseBranchTask(
            serviceProvider,
            loggerMock.Object,
            redisServiceMock.Object,
            bitbucketOptions);

        // Act
        await task.ExecuteAsync();

        // Assert - 失敗的專案應該歸入 NotFound
        redisServiceMock.Verify(
            x => x.SetAsync(
                It.IsAny<string>(),
                It.Is<string>(json => json.Contains("NotFound") && json.Contains("workspace/project-error")),
                It.IsAny<TimeSpan?>()),
            Times.Once);
    }

    #region Edge Cases for Grouping Logic (US3)

    [Fact]
    public async Task FetchBitbucketReleaseBranchTask_ExecuteAsync_WithMultipleProjectsSameBranch_ShouldGroupTogether()
    {
        // Arrange
        var services = new ServiceCollection();
        var bitbucketOptions = Options.Create(new BitbucketOptions
        {
            Projects = new List<BitbucketProjectOptions>
            {
                new BitbucketProjectOptions { ProjectPath = "workspace/project1", TargetBranch = "main" },
                new BitbucketProjectOptions { ProjectPath = "workspace/project2", TargetBranch = "main" },
                new BitbucketProjectOptions { ProjectPath = "workspace/project3", TargetBranch = "main" }
            }
        });

        var loggerMock = new Mock<ILogger<FetchBitbucketReleaseBranchTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        var redisServiceMock = new Mock<IRedisService>();

        // 設定所有專案都有相同的最新 release branch
        var sameBranch = "release/20260210";
        repositoryMock
            .Setup(x => x.GetBranchesAsync(It.IsAny<string>(), "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { sameBranch }.AsReadOnly()));

        redisServiceMock.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        redisServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>())).ReturnsAsync(true);

        services.AddKeyedSingleton("Bitbucket", repositoryMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var task = new FetchBitbucketReleaseBranchTask(
            serviceProvider,
            loggerMock.Object,
            redisServiceMock.Object,
            bitbucketOptions);

        // Act
        await task.ExecuteAsync();

        // Assert - 所有專案應該歸在同一組
        redisServiceMock.Verify(
            x => x.SetAsync(
                It.IsAny<string>(),
                It.Is<string>(json => 
                    json.Contains("release/20260210") && 
                    json.Contains("workspace/project1") && 
                    json.Contains("workspace/project2") && 
                    json.Contains("workspace/project3") &&
                    !json.Contains("NotFound")),
                It.IsAny<TimeSpan?>()),
            Times.Once);
    }

    [Fact]
    public async Task FetchBitbucketReleaseBranchTask_ExecuteAsync_WithMixedResults_ShouldGroupCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        var bitbucketOptions = Options.Create(new BitbucketOptions
        {
            Projects = new List<BitbucketProjectOptions>
            {
                new BitbucketProjectOptions { ProjectPath = "workspace/project-old", TargetBranch = "main" },
                new BitbucketProjectOptions { ProjectPath = "workspace/project-new", TargetBranch = "main" },
                new BitbucketProjectOptions { ProjectPath = "workspace/project-none", TargetBranch = "main" },
                new BitbucketProjectOptions { ProjectPath = "workspace/project-error", TargetBranch = "main" }
            }
        });

        var loggerMock = new Mock<ILogger<FetchBitbucketReleaseBranchTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        var redisServiceMock = new Mock<IRedisService>();

        // project-old: 有舊版本 release branch
        repositoryMock
            .Setup(x => x.GetBranchesAsync("workspace/project-old", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "release/20260101" }.AsReadOnly()));

        // project-new: 有新版本 release branch
        repositoryMock
            .Setup(x => x.GetBranchesAsync("workspace/project-new", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "release/20260210" }.AsReadOnly()));

        // project-none: 沒有 release branch
        repositoryMock
            .Setup(x => x.GetBranchesAsync("workspace/project-none", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string>().AsReadOnly()));

        // project-error: API 錯誤
        repositoryMock
            .Setup(x => x.GetBranchesAsync("workspace/project-error", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Failure(Error.SourceControl.ApiError("Error")));

        redisServiceMock.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        redisServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>())).ReturnsAsync(true);

        services.AddKeyedSingleton("Bitbucket", repositoryMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var task = new FetchBitbucketReleaseBranchTask(
            serviceProvider,
            loggerMock.Object,
            redisServiceMock.Object,
            bitbucketOptions);

        // Act
        await task.ExecuteAsync();

        // Assert - 混合情境應該正確分組
        redisServiceMock.Verify(
            x => x.SetAsync(
                It.IsAny<string>(),
                It.Is<string>(json => 
                    json.Contains("release/20260101") && json.Contains("workspace/project-old") &&
                    json.Contains("release/20260210") && json.Contains("workspace/project-new") &&
                    json.Contains("NotFound") && json.Contains("workspace/project-none") && json.Contains("workspace/project-error")),
                It.IsAny<TimeSpan?>()),
            Times.Once);
    }

    #endregion
}
