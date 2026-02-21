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
        redisServiceMock.Setup(x => x.HashExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
        redisServiceMock.Setup(x => x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

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

        redisServiceMock.Setup(x => x.HashExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
        redisServiceMock.Setup(x => x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

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
            x => x.HashSetAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<string>(json => json.Contains("release/20260201") && json.Contains("workspace/project1") && json.Contains("workspace/project2"))),
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

        redisServiceMock.Setup(x => x.HashExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
        redisServiceMock.Setup(x => x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

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
            x => x.HashSetAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<string>(json => json.Contains("NotFound") && json.Contains("workspace/project-no-release"))),
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

        redisServiceMock.Setup(x => x.HashExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
        redisServiceMock.Setup(x => x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

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
            x => x.HashSetAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<string>(json => json.Contains("release/20260210") && !json.Contains("release/20260101") && !json.Contains("release/20260115"))),
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

        redisServiceMock.Setup(x => x.HashExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
        redisServiceMock.Setup(x => x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

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
            x => x.HashSetAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<string>(json => json.Contains("NotFound") && json.Contains("workspace/project-error"))),
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

        redisServiceMock.Setup(x => x.HashExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
        redisServiceMock.Setup(x => x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

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
            x => x.HashSetAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<string>(json => 
                    json.Contains("release/20260210") && 
                    json.Contains("workspace/project1") && 
                    json.Contains("workspace/project2") && 
                    json.Contains("workspace/project3") &&
                    !json.Contains("NotFound"))),
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

        redisServiceMock.Setup(x => x.HashExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
        redisServiceMock.Setup(x => x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

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
            x => x.HashSetAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<string>(json => 
                    json.Contains("release/20260101") && json.Contains("workspace/project-old") &&
                    json.Contains("release/20260210") && json.Contains("workspace/project-new") &&
                    json.Contains("NotFound") && json.Contains("workspace/project-none") && json.Contains("workspace/project-error"))),
            Times.Once);
    }

    #endregion

    #region Sorting Tests

    [Fact]
    public async Task FetchBitbucketReleaseBranchTask_ExecuteAsync_ShouldSortBranchesDescending()
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

        // 設定不同的 release branch
        repositoryMock
            .Setup(x => x.GetBranchesAsync("workspace/project1", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "release/20260101" }.AsReadOnly()));
        repositoryMock
            .Setup(x => x.GetBranchesAsync("workspace/project2", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "release/20260210" }.AsReadOnly()));
        repositoryMock
            .Setup(x => x.GetBranchesAsync("workspace/project3", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "release/20260115" }.AsReadOnly()));

        string? savedJson = null;
        redisServiceMock.Setup(x => x.HashExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
        redisServiceMock.Setup(x => x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((hashKey, field, json) => savedJson = json)
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

        // Assert - 驗證排序：release/20260210 > release/20260115 > release/20260101
        Assert.NotNull(savedJson);
        var indexOf20260210 = savedJson.IndexOf("release/20260210", StringComparison.Ordinal);
        var indexOf20260115 = savedJson.IndexOf("release/20260115", StringComparison.Ordinal);
        var indexOf20260101 = savedJson.IndexOf("release/20260101", StringComparison.Ordinal);

        Assert.True(indexOf20260210 < indexOf20260115, "release/20260210 應該在 release/20260115 之前");
        Assert.True(indexOf20260115 < indexOf20260101, "release/20260115 應該在 release/20260101 之前");
    }

    [Fact]
    public async Task FetchBitbucketReleaseBranchTask_ExecuteAsync_ShouldPutNotFoundAtEnd()
    {
        // Arrange
        var services = new ServiceCollection();
        var bitbucketOptions = Options.Create(new BitbucketOptions
        {
            Projects = new List<BitbucketProjectOptions>
            {
                new BitbucketProjectOptions { ProjectPath = "workspace/project-with-branch", TargetBranch = "main" },
                new BitbucketProjectOptions { ProjectPath = "workspace/project-no-branch", TargetBranch = "main" }
            }
        });

        var loggerMock = new Mock<ILogger<FetchBitbucketReleaseBranchTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        var redisServiceMock = new Mock<IRedisService>();

        // project-with-branch: 有 release branch
        repositoryMock
            .Setup(x => x.GetBranchesAsync("workspace/project-with-branch", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "release/20260101" }.AsReadOnly()));

        // project-no-branch: 沒有 release branch
        repositoryMock
            .Setup(x => x.GetBranchesAsync("workspace/project-no-branch", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string>().AsReadOnly()));

        string? savedJson = null;
        redisServiceMock.Setup(x => x.HashExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
        redisServiceMock.Setup(x => x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((hashKey, field, json) => savedJson = json)
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

        // Assert - 驗證 NotFound 在最後
        Assert.NotNull(savedJson);
        var indexOfRelease = savedJson.IndexOf("release/20260101", StringComparison.Ordinal);
        var indexOfNotFound = savedJson.IndexOf("NotFound", StringComparison.Ordinal);

        Assert.True(indexOfRelease < indexOfNotFound, "release branch 應該在 NotFound 之前");
    }

    [Fact]
    public async Task FetchBitbucketReleaseBranchTask_ExecuteAsync_WithMultipleBranchesAndNotFound_ShouldSortCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        var bitbucketOptions = Options.Create(new BitbucketOptions
        {
            Projects = new List<BitbucketProjectOptions>
            {
                new BitbucketProjectOptions { ProjectPath = "workspace/project-old", TargetBranch = "main" },
                new BitbucketProjectOptions { ProjectPath = "workspace/project-new", TargetBranch = "main" },
                new BitbucketProjectOptions { ProjectPath = "workspace/project-mid", TargetBranch = "main" },
                new BitbucketProjectOptions { ProjectPath = "workspace/project-none", TargetBranch = "main" }
            }
        });

        var loggerMock = new Mock<ILogger<FetchBitbucketReleaseBranchTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        var redisServiceMock = new Mock<IRedisService>();

        repositoryMock
            .Setup(x => x.GetBranchesAsync("workspace/project-old", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "release/20250101" }.AsReadOnly()));
        repositoryMock
            .Setup(x => x.GetBranchesAsync("workspace/project-new", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "release/20260210" }.AsReadOnly()));
        repositoryMock
            .Setup(x => x.GetBranchesAsync("workspace/project-mid", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "release/20260115" }.AsReadOnly()));
        repositoryMock
            .Setup(x => x.GetBranchesAsync("workspace/project-none", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string>().AsReadOnly()));

        string? savedJson = null;
        redisServiceMock.Setup(x => x.HashExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
        redisServiceMock.Setup(x => x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((hashKey, field, json) => savedJson = json)
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
    public async Task FetchBitbucketReleaseBranchTask_ExecuteAsync_WithNonStandardBranches_ShouldSortCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        var bitbucketOptions = Options.Create(new BitbucketOptions
        {
            Projects = new List<BitbucketProjectOptions>
            {
                new BitbucketProjectOptions { ProjectPath = "workspace/project-standard1", TargetBranch = "main" },
                new BitbucketProjectOptions { ProjectPath = "workspace/project-standard2", TargetBranch = "main" },
                new BitbucketProjectOptions { ProjectPath = "workspace/project-custom1", TargetBranch = "main" },
                new BitbucketProjectOptions { ProjectPath = "workspace/project-custom2", TargetBranch = "main" },
                new BitbucketProjectOptions { ProjectPath = "workspace/project-none", TargetBranch = "main" }
            }
        });

        var loggerMock = new Mock<ILogger<FetchBitbucketReleaseBranchTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        var redisServiceMock = new Mock<IRedisService>();

        // 標準 release/yyyyMMdd 格式
        repositoryMock
            .Setup(x => x.GetBranchesAsync("workspace/project-standard1", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "release/20260210" }.AsReadOnly()));
        repositoryMock
            .Setup(x => x.GetBranchesAsync("workspace/project-standard2", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "release/20260115" }.AsReadOnly()));

        // 非標準格式的 release branch
        repositoryMock
            .Setup(x => x.GetBranchesAsync("workspace/project-custom1", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "release/v2.0" }.AsReadOnly()));
        repositoryMock
            .Setup(x => x.GetBranchesAsync("workspace/project-custom2", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "release/hotfix" }.AsReadOnly()));

        // 沒有 release branch
        repositoryMock
            .Setup(x => x.GetBranchesAsync("workspace/project-none", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string>().AsReadOnly()));

        string? savedJson = null;
        redisServiceMock.Setup(x => x.HashExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
        redisServiceMock.Setup(x => x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((hashKey, field, json) => savedJson = json)
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
    public async Task FetchBitbucketReleaseBranchTask_ExecuteAsync_WithDifferentCaseReleaseBranches_ShouldSortCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        var bitbucketOptions = Options.Create(new BitbucketOptions
        {
            Projects = new List<BitbucketProjectOptions>
            {
                new BitbucketProjectOptions { ProjectPath = "workspace/project-lower", TargetBranch = "main" },
                new BitbucketProjectOptions { ProjectPath = "workspace/project-upper", TargetBranch = "main" },
                new BitbucketProjectOptions { ProjectPath = "workspace/project-mixed", TargetBranch = "main" },
                new BitbucketProjectOptions { ProjectPath = "workspace/project-none", TargetBranch = "main" }
            }
        });

        var loggerMock = new Mock<ILogger<FetchBitbucketReleaseBranchTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        var redisServiceMock = new Mock<IRedisService>();

        // 不同大小寫的 release/yyyyMMdd 格式
        repositoryMock
            .Setup(x => x.GetBranchesAsync("workspace/project-lower", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "release/20260210" }.AsReadOnly()));
        repositoryMock
            .Setup(x => x.GetBranchesAsync("workspace/project-upper", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "Release/20260115" }.AsReadOnly()));
        repositoryMock
            .Setup(x => x.GetBranchesAsync("workspace/project-mixed", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "RELEASE/20260101" }.AsReadOnly()));

        // 沒有 release branch
        repositoryMock
            .Setup(x => x.GetBranchesAsync("workspace/project-none", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string>().AsReadOnly()));

        string? savedJson = null;
        redisServiceMock.Setup(x => x.HashExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
        redisServiceMock.Setup(x => x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((hashKey, field, json) => savedJson = json)
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
