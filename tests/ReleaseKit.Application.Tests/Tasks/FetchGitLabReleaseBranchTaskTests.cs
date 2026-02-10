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
/// FetchGitLabReleaseBranchTask 單元測試
/// </summary>
public class FetchGitLabReleaseBranchTaskTests
{
    [Fact]
    public async Task FetchGitLabReleaseBranchTask_ExecuteAsync_WithEmptyProjects_ShouldCompleteSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        var gitLabOptions = Options.Create(new GitLabOptions { Projects = new List<GitLabProjectOptions>() });

        // Mock logger
        var loggerMock = new Mock<ILogger<FetchGitLabReleaseBranchTask>>();

        // Mock keyed repository service
        var repositoryMock = new Mock<ISourceControlRepository>();

        // Mock Redis service
        var redisServiceMock = new Mock<IRedisService>();
        redisServiceMock.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        redisServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>())).ReturnsAsync(true);

        services.AddKeyedSingleton("GitLab", repositoryMock.Object);

        var serviceProvider = services.BuildServiceProvider();

        var task = new FetchGitLabReleaseBranchTask(
            serviceProvider,
            loggerMock.Object,
            redisServiceMock.Object,
            gitLabOptions);

        // Act
        await task.ExecuteAsync();

        // Assert - should complete without exception
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("開始執行 GitLab Release Branch 拉取任務")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task FetchGitLabReleaseBranchTask_ExecuteAsync_WithProjectsHavingReleaseBranches_ShouldGroupByBranchName()
    {
        // Arrange
        var services = new ServiceCollection();
        var gitLabOptions = Options.Create(new GitLabOptions
        {
            Projects = new List<GitLabProjectOptions>
            {
                new GitLabProjectOptions
                {
                    ProjectPath = "group/project1",
                    TargetBranch = "main"
                },
                new GitLabProjectOptions
                {
                    ProjectPath = "group/project2",
                    TargetBranch = "main"
                }
            }
        });

        var loggerMock = new Mock<ILogger<FetchGitLabReleaseBranchTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        var redisServiceMock = new Mock<IRedisService>();

        // 設定 project1 有 release branch
        repositoryMock
            .Setup(x => x.GetBranchesAsync("group/project1", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "release/20260201" }.AsReadOnly()));

        // 設定 project2 有 release branch
        repositoryMock
            .Setup(x => x.GetBranchesAsync("group/project2", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "release/20260201" }.AsReadOnly()));

        redisServiceMock.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        redisServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>())).ReturnsAsync(true);

        services.AddKeyedSingleton("GitLab", repositoryMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var task = new FetchGitLabReleaseBranchTask(
            serviceProvider,
            loggerMock.Object,
            redisServiceMock.Object,
            gitLabOptions);

        // Act
        await task.ExecuteAsync();

        // Assert
        redisServiceMock.Verify(
            x => x.SetAsync(
                It.IsAny<string>(),
                It.Is<string>(json => json.Contains("release/20260201") && json.Contains("group/project1") && json.Contains("group/project2")),
                It.IsAny<TimeSpan?>()),
            Times.Once);
    }

    [Fact]
    public async Task FetchGitLabReleaseBranchTask_ExecuteAsync_WithProjectsWithoutReleaseBranches_ShouldAddToNotFound()
    {
        // Arrange
        var services = new ServiceCollection();
        var gitLabOptions = Options.Create(new GitLabOptions
        {
            Projects = new List<GitLabProjectOptions>
            {
                new GitLabProjectOptions
                {
                    ProjectPath = "group/project-no-release",
                    TargetBranch = "main"
                }
            }
        });

        var loggerMock = new Mock<ILogger<FetchGitLabReleaseBranchTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        var redisServiceMock = new Mock<IRedisService>();

        // 設定專案沒有 release branch（回傳空清單）
        repositoryMock
            .Setup(x => x.GetBranchesAsync("group/project-no-release", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string>().AsReadOnly()));

        redisServiceMock.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        redisServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>())).ReturnsAsync(true);

        services.AddKeyedSingleton("GitLab", repositoryMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var task = new FetchGitLabReleaseBranchTask(
            serviceProvider,
            loggerMock.Object,
            redisServiceMock.Object,
            gitLabOptions);

        // Act
        await task.ExecuteAsync();

        // Assert
        redisServiceMock.Verify(
            x => x.SetAsync(
                It.IsAny<string>(),
                It.Is<string>(json => json.Contains("NotFound") && json.Contains("group/project-no-release")),
                It.IsAny<TimeSpan?>()),
            Times.Once);
    }

    [Fact]
    public async Task FetchGitLabReleaseBranchTask_ExecuteAsync_WithMultipleReleaseBranches_ShouldPickLatest()
    {
        // Arrange
        var services = new ServiceCollection();
        var gitLabOptions = Options.Create(new GitLabOptions
        {
            Projects = new List<GitLabProjectOptions>
            {
                new GitLabProjectOptions
                {
                    ProjectPath = "group/project-multi",
                    TargetBranch = "main"
                }
            }
        });

        var loggerMock = new Mock<ILogger<FetchGitLabReleaseBranchTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        var redisServiceMock = new Mock<IRedisService>();

        // 設定專案有多個 release branch，應取最新的（字母排序最大）
        repositoryMock
            .Setup(x => x.GetBranchesAsync("group/project-multi", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string>
            {
                "release/20260101",
                "release/20260115",
                "release/20260210"  // 最新
            }.AsReadOnly()));

        redisServiceMock.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        redisServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>())).ReturnsAsync(true);

        services.AddKeyedSingleton("GitLab", repositoryMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var task = new FetchGitLabReleaseBranchTask(
            serviceProvider,
            loggerMock.Object,
            redisServiceMock.Object,
            gitLabOptions);

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
    public async Task FetchGitLabReleaseBranchTask_ExecuteAsync_WithGetBranchesFailure_ShouldAddToNotFound()
    {
        // Arrange
        var services = new ServiceCollection();
        var gitLabOptions = Options.Create(new GitLabOptions
        {
            Projects = new List<GitLabProjectOptions>
            {
                new GitLabProjectOptions
                {
                    ProjectPath = "group/project-error",
                    TargetBranch = "main"
                }
            }
        });

        var loggerMock = new Mock<ILogger<FetchGitLabReleaseBranchTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        var redisServiceMock = new Mock<IRedisService>();

        // 設定 GetBranchesAsync 失敗
        repositoryMock
            .Setup(x => x.GetBranchesAsync("group/project-error", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Failure(Error.SourceControl.ApiError("Failed to get branches")));

        redisServiceMock.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        redisServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>())).ReturnsAsync(true);

        services.AddKeyedSingleton("GitLab", repositoryMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var task = new FetchGitLabReleaseBranchTask(
            serviceProvider,
            loggerMock.Object,
            redisServiceMock.Object,
            gitLabOptions);

        // Act
        await task.ExecuteAsync();

        // Assert - 失敗的專案應該歸入 NotFound
        redisServiceMock.Verify(
            x => x.SetAsync(
                It.IsAny<string>(),
                It.Is<string>(json => json.Contains("NotFound") && json.Contains("group/project-error")),
                It.IsAny<TimeSpan?>()),
            Times.Once);
    }
}
