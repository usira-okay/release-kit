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

    #region Edge Cases for Grouping Logic (US3)

    [Fact]
    public async Task FetchGitLabReleaseBranchTask_ExecuteAsync_WithMultipleProjectsSameBranch_ShouldGroupTogether()
    {
        // Arrange
        var services = new ServiceCollection();
        var gitLabOptions = Options.Create(new GitLabOptions
        {
            Projects = new List<GitLabProjectOptions>
            {
                new GitLabProjectOptions { ProjectPath = "group/project1", TargetBranch = "main" },
                new GitLabProjectOptions { ProjectPath = "group/project2", TargetBranch = "main" },
                new GitLabProjectOptions { ProjectPath = "group/project3", TargetBranch = "main" }
            }
        });

        var loggerMock = new Mock<ILogger<FetchGitLabReleaseBranchTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        var redisServiceMock = new Mock<IRedisService>();

        // 設定所有專案都有相同的最新 release branch
        var sameBranch = "release/20260210";
        repositoryMock
            .Setup(x => x.GetBranchesAsync(It.IsAny<string>(), "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { sameBranch }.AsReadOnly()));

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

        // Assert - 所有專案應該歸在同一組
        redisServiceMock.Verify(
            x => x.SetAsync(
                It.IsAny<string>(),
                It.Is<string>(json => 
                    json.Contains("release/20260210") && 
                    json.Contains("group/project1") && 
                    json.Contains("group/project2") && 
                    json.Contains("group/project3") &&
                    !json.Contains("NotFound")),
                It.IsAny<TimeSpan?>()),
            Times.Once);
    }

    [Fact]
    public async Task FetchGitLabReleaseBranchTask_ExecuteAsync_WithMixedResults_ShouldGroupCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        var gitLabOptions = Options.Create(new GitLabOptions
        {
            Projects = new List<GitLabProjectOptions>
            {
                new GitLabProjectOptions { ProjectPath = "group/project-old", TargetBranch = "main" },
                new GitLabProjectOptions { ProjectPath = "group/project-new", TargetBranch = "main" },
                new GitLabProjectOptions { ProjectPath = "group/project-none", TargetBranch = "main" },
                new GitLabProjectOptions { ProjectPath = "group/project-error", TargetBranch = "main" }
            }
        });

        var loggerMock = new Mock<ILogger<FetchGitLabReleaseBranchTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        var redisServiceMock = new Mock<IRedisService>();

        // project-old: 有舊版本 release branch
        repositoryMock
            .Setup(x => x.GetBranchesAsync("group/project-old", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "release/20260101" }.AsReadOnly()));

        // project-new: 有新版本 release branch
        repositoryMock
            .Setup(x => x.GetBranchesAsync("group/project-new", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "release/20260210" }.AsReadOnly()));

        // project-none: 沒有 release branch
        repositoryMock
            .Setup(x => x.GetBranchesAsync("group/project-none", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string>().AsReadOnly()));

        // project-error: API 錯誤
        repositoryMock
            .Setup(x => x.GetBranchesAsync("group/project-error", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Failure(Error.SourceControl.ApiError("Error")));

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

        // Assert - 混合情境應該正確分組
        redisServiceMock.Verify(
            x => x.SetAsync(
                It.IsAny<string>(),
                It.Is<string>(json => 
                    json.Contains("release/20260101") && json.Contains("group/project-old") &&
                    json.Contains("release/20260210") && json.Contains("group/project-new") &&
                    json.Contains("NotFound") && json.Contains("group/project-none") && json.Contains("group/project-error")),
                It.IsAny<TimeSpan?>()),
            Times.Once);
    }

    [Fact]
    public async Task FetchGitLabReleaseBranchTask_ExecuteAsync_OutputJson_ShouldMatchExpectedFormat()
    {
        // Arrange
        var services = new ServiceCollection();
        var gitLabOptions = Options.Create(new GitLabOptions
        {
            Projects = new List<GitLabProjectOptions>
            {
                new GitLabProjectOptions { ProjectPath = "group/project1", TargetBranch = "main" },
                new GitLabProjectOptions { ProjectPath = "group/project2", TargetBranch = "main" }
            }
        });

        var loggerMock = new Mock<ILogger<FetchGitLabReleaseBranchTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        var redisServiceMock = new Mock<IRedisService>();

        repositoryMock
            .Setup(x => x.GetBranchesAsync("group/project1", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "release/20260210" }.AsReadOnly()));

        repositoryMock
            .Setup(x => x.GetBranchesAsync("group/project2", "release/", It.IsAny<CancellationToken>()))
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

        // Assert - JSON 結構應該符合預期格式: { "branch": ["proj1", "proj2"], "NotFound": ["proj3"] }
        redisServiceMock.Verify(
            x => x.SetAsync(
                It.IsAny<string>(),
                It.Is<string>(json => 
                    json.StartsWith("{") && json.EndsWith("}") &&  // 應該是 JSON 物件
                    json.Contains("\"release/20260210\"") &&       // 包含分支名稱
                    json.Contains("[") && json.Contains("]") &&    // 包含陣列
                    json.Contains("\"NotFound\"")),                // 包含 NotFound key
                It.IsAny<TimeSpan?>()),
            Times.Once);
    }

    #endregion

    #region Sorting Tests

    [Fact]
    public async Task FetchGitLabReleaseBranchTask_ExecuteAsync_ShouldSortBranchesDescending()
    {
        // Arrange
        var services = new ServiceCollection();
        var gitLabOptions = Options.Create(new GitLabOptions
        {
            Projects = new List<GitLabProjectOptions>
            {
                new GitLabProjectOptions { ProjectPath = "group/project1", TargetBranch = "main" },
                new GitLabProjectOptions { ProjectPath = "group/project2", TargetBranch = "main" },
                new GitLabProjectOptions { ProjectPath = "group/project3", TargetBranch = "main" }
            }
        });

        var loggerMock = new Mock<ILogger<FetchGitLabReleaseBranchTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        var redisServiceMock = new Mock<IRedisService>();

        // 設定不同的 release branch
        repositoryMock
            .Setup(x => x.GetBranchesAsync("group/project1", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "release/20260101" }.AsReadOnly()));
        repositoryMock
            .Setup(x => x.GetBranchesAsync("group/project2", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "release/20260210" }.AsReadOnly()));
        repositoryMock
            .Setup(x => x.GetBranchesAsync("group/project3", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "release/20260115" }.AsReadOnly()));

        string? savedJson = null;
        redisServiceMock.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        redisServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .Callback<string, string, TimeSpan?>((key, json, expiry) => savedJson = json)
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

        // Assert - 驗證排序：release/20260210 > release/20260115 > release/20260101
        Assert.NotNull(savedJson);
        var indexOf20260210 = savedJson.IndexOf("release/20260210", StringComparison.Ordinal);
        var indexOf20260115 = savedJson.IndexOf("release/20260115", StringComparison.Ordinal);
        var indexOf20260101 = savedJson.IndexOf("release/20260101", StringComparison.Ordinal);

        Assert.True(indexOf20260210 < indexOf20260115, "release/20260210 應該在 release/20260115 之前");
        Assert.True(indexOf20260115 < indexOf20260101, "release/20260115 應該在 release/20260101 之前");
    }

    [Fact]
    public async Task FetchGitLabReleaseBranchTask_ExecuteAsync_ShouldPutNotFoundAtEnd()
    {
        // Arrange
        var services = new ServiceCollection();
        var gitLabOptions = Options.Create(new GitLabOptions
        {
            Projects = new List<GitLabProjectOptions>
            {
                new GitLabProjectOptions { ProjectPath = "group/project-with-branch", TargetBranch = "main" },
                new GitLabProjectOptions { ProjectPath = "group/project-no-branch", TargetBranch = "main" }
            }
        });

        var loggerMock = new Mock<ILogger<FetchGitLabReleaseBranchTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        var redisServiceMock = new Mock<IRedisService>();

        // project-with-branch: 有 release branch
        repositoryMock
            .Setup(x => x.GetBranchesAsync("group/project-with-branch", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "release/20260101" }.AsReadOnly()));

        // project-no-branch: 沒有 release branch
        repositoryMock
            .Setup(x => x.GetBranchesAsync("group/project-no-branch", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string>().AsReadOnly()));

        string? savedJson = null;
        redisServiceMock.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        redisServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .Callback<string, string, TimeSpan?>((key, json, expiry) => savedJson = json)
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

        // Assert - 驗證 NotFound 在最後
        Assert.NotNull(savedJson);
        var indexOfRelease = savedJson.IndexOf("release/20260101", StringComparison.Ordinal);
        var indexOfNotFound = savedJson.IndexOf("NotFound", StringComparison.Ordinal);

        Assert.True(indexOfRelease < indexOfNotFound, "release branch 應該在 NotFound 之前");
    }

    [Fact]
    public async Task FetchGitLabReleaseBranchTask_ExecuteAsync_WithMultipleBranchesAndNotFound_ShouldSortCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        var gitLabOptions = Options.Create(new GitLabOptions
        {
            Projects = new List<GitLabProjectOptions>
            {
                new GitLabProjectOptions { ProjectPath = "group/project-old", TargetBranch = "main" },
                new GitLabProjectOptions { ProjectPath = "group/project-new", TargetBranch = "main" },
                new GitLabProjectOptions { ProjectPath = "group/project-mid", TargetBranch = "main" },
                new GitLabProjectOptions { ProjectPath = "group/project-none", TargetBranch = "main" }
            }
        });

        var loggerMock = new Mock<ILogger<FetchGitLabReleaseBranchTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        var redisServiceMock = new Mock<IRedisService>();

        repositoryMock
            .Setup(x => x.GetBranchesAsync("group/project-old", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "release/20250101" }.AsReadOnly()));
        repositoryMock
            .Setup(x => x.GetBranchesAsync("group/project-new", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "release/20260210" }.AsReadOnly()));
        repositoryMock
            .Setup(x => x.GetBranchesAsync("group/project-mid", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "release/20260115" }.AsReadOnly()));
        repositoryMock
            .Setup(x => x.GetBranchesAsync("group/project-none", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string>().AsReadOnly()));

        string? savedJson = null;
        redisServiceMock.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        redisServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .Callback<string, string, TimeSpan?>((key, json, expiry) => savedJson = json)
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

        // Assert - 驗證完整排序：20260210 > 20260115 > 20250101 > NotFound
        Assert.NotNull(savedJson);
        var indexOf20260210 = savedJson.IndexOf("release/20260210", StringComparison.Ordinal);
        var indexOf20260115 = savedJson.IndexOf("release/20260115", StringComparison.Ordinal);
        var indexOf20250101 = savedJson.IndexOf("release/20250101", StringComparison.Ordinal);
        var indexOfNotFound = savedJson.IndexOf("NotFound", StringComparison.Ordinal);

        Assert.True(indexOf20260210 < indexOf20260115, "release/20260210 應該在 release/20260115 之前");
        Assert.True(indexOf20260115 < indexOf20250101, "release/20260115 應該在 release/20250101 之前");
        Assert.True(indexOf20250101 < indexOfNotFound, "release/20250101 應該在 NotFound 之前");
    }

    [Fact]
    public async Task FetchGitLabReleaseBranchTask_ExecuteAsync_WithNonStandardBranches_ShouldSortCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        var gitLabOptions = Options.Create(new GitLabOptions
        {
            Projects = new List<GitLabProjectOptions>
            {
                new GitLabProjectOptions { ProjectPath = "group/project-standard1", TargetBranch = "main" },
                new GitLabProjectOptions { ProjectPath = "group/project-standard2", TargetBranch = "main" },
                new GitLabProjectOptions { ProjectPath = "group/project-custom1", TargetBranch = "main" },
                new GitLabProjectOptions { ProjectPath = "group/project-custom2", TargetBranch = "main" },
                new GitLabProjectOptions { ProjectPath = "group/project-none", TargetBranch = "main" }
            }
        });

        var loggerMock = new Mock<ILogger<FetchGitLabReleaseBranchTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        var redisServiceMock = new Mock<IRedisService>();

        // 標準 release/yyyyMMdd 格式
        repositoryMock
            .Setup(x => x.GetBranchesAsync("group/project-standard1", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "release/20260210" }.AsReadOnly()));
        repositoryMock
            .Setup(x => x.GetBranchesAsync("group/project-standard2", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "release/20260115" }.AsReadOnly()));

        // 非標準格式的 release branch
        repositoryMock
            .Setup(x => x.GetBranchesAsync("group/project-custom1", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "release/v2.0" }.AsReadOnly()));
        repositoryMock
            .Setup(x => x.GetBranchesAsync("group/project-custom2", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "release/hotfix" }.AsReadOnly()));

        // 沒有 release branch
        repositoryMock
            .Setup(x => x.GetBranchesAsync("group/project-none", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string>().AsReadOnly()));

        string? savedJson = null;
        redisServiceMock.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        redisServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .Callback<string, string, TimeSpan?>((key, json, expiry) => savedJson = json)
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

        // Assert - 驗證排序：標準 release/yyyyMMdd 在前面且降冪排序，非標準的在中間，NotFound 最後
        Assert.NotNull(savedJson);
        var indexOf20260210 = savedJson.IndexOf("release/20260210", StringComparison.Ordinal);
        var indexOf20260115 = savedJson.IndexOf("release/20260115", StringComparison.Ordinal);
        var indexOfV2 = savedJson.IndexOf("release/v2.0", StringComparison.Ordinal);
        var indexOfHotfix = savedJson.IndexOf("release/hotfix", StringComparison.Ordinal);
        var indexOfNotFound = savedJson.IndexOf("NotFound", StringComparison.Ordinal);

        // 標準格式的 branch 應該在最前面且降冪排序
        Assert.True(indexOf20260210 < indexOf20260115, "release/20260210 應該在 release/20260115 之前");
        
        // 標準格式應該在非標準格式之前
        Assert.True(indexOf20260115 < indexOfV2, "標準格式應該在非標準格式之前");
        Assert.True(indexOf20260115 < indexOfHotfix, "標準格式應該在非標準格式之前");
        
        // NotFound 應該在最後
        Assert.True(indexOfV2 < indexOfNotFound, "非標準 branch 應該在 NotFound 之前");
        Assert.True(indexOfHotfix < indexOfNotFound, "非標準 branch 應該在 NotFound 之前");
    }

    [Fact]
    public async Task FetchGitLabReleaseBranchTask_ExecuteAsync_WithDifferentCaseReleaseBranches_ShouldSortCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        var gitLabOptions = Options.Create(new GitLabOptions
        {
            Projects = new List<GitLabProjectOptions>
            {
                new GitLabProjectOptions { ProjectPath = "group/project-lower", TargetBranch = "main" },
                new GitLabProjectOptions { ProjectPath = "group/project-upper", TargetBranch = "main" },
                new GitLabProjectOptions { ProjectPath = "group/project-mixed", TargetBranch = "main" },
                new GitLabProjectOptions { ProjectPath = "group/project-none", TargetBranch = "main" }
            }
        });

        var loggerMock = new Mock<ILogger<FetchGitLabReleaseBranchTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        var redisServiceMock = new Mock<IRedisService>();

        // 不同大小寫的 release/yyyyMMdd 格式
        repositoryMock
            .Setup(x => x.GetBranchesAsync("group/project-lower", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "release/20260210" }.AsReadOnly()));
        repositoryMock
            .Setup(x => x.GetBranchesAsync("group/project-upper", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "Release/20260115" }.AsReadOnly()));
        repositoryMock
            .Setup(x => x.GetBranchesAsync("group/project-mixed", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "RELEASE/20260101" }.AsReadOnly()));

        // 沒有 release branch
        repositoryMock
            .Setup(x => x.GetBranchesAsync("group/project-none", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string>().AsReadOnly()));

        string? savedJson = null;
        redisServiceMock.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        redisServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .Callback<string, string, TimeSpan?>((key, json, expiry) => savedJson = json)
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

        // Assert - 驗證不同大小寫的 release branch 都會被正確識別並排序
        Assert.NotNull(savedJson);
        var indexOf20260210 = savedJson.IndexOf("release/20260210", StringComparison.OrdinalIgnoreCase);
        var indexOf20260115 = savedJson.IndexOf("Release/20260115", StringComparison.OrdinalIgnoreCase);
        var indexOf20260101 = savedJson.IndexOf("RELEASE/20260101", StringComparison.OrdinalIgnoreCase);
        var indexOfNotFound = savedJson.IndexOf("NotFound", StringComparison.Ordinal);

        // 所有不同大小寫的標準格式 branch 都應該被識別並排在 NotFound 之前
        Assert.True(indexOf20260210 >= 0, "release/20260210 應該存在");
        Assert.True(indexOf20260115 >= 0, "Release/20260115 應該存在");
        Assert.True(indexOf20260101 >= 0, "RELEASE/20260101 應該存在");
        
        // 標準格式應該降冪排序：20260210 > 20260115 > 20260101
        Assert.True(indexOf20260210 < indexOf20260115, "release/20260210 應該在 Release/20260115 之前");
        Assert.True(indexOf20260115 < indexOf20260101, "Release/20260115 應該在 RELEASE/20260101 之前");
        
        // NotFound 應該在最後
        Assert.True(indexOf20260101 < indexOfNotFound, "所有標準格式 branch 應該在 NotFound 之前");
    }

    #endregion
}
