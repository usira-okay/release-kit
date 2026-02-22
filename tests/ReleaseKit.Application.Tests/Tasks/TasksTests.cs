using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tests.Tasks;

/// <summary>
/// 任務類別單元測試
/// </summary>
public class TasksTests
{
    [Fact]
    public async Task FetchGitLabPullRequestsTask_ExecuteAsync_WithEmptyProjects_ShouldCompleteSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        var gitLabOptions = Options.Create(new GitLabOptions { Projects = new List<GitLabProjectOptions>() });
        var fetchModeOptions = Options.Create(new FetchModeOptions());
        
        // Mock logger
        var loggerMock = new Mock<ILogger<FetchGitLabPullRequestsTask>>();
        
        // Mock keyed repository service
        var repositoryMock = new Mock<ReleaseKit.Domain.Abstractions.ISourceControlRepository>();
        
        // Mock Redis service
        var redisServiceMock = new Mock<ReleaseKit.Domain.Abstractions.IRedisService>();
        redisServiceMock.Setup(x => x.HashExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
        redisServiceMock.Setup(x => x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        
        services.AddSingleton(gitLabOptions);
        services.AddSingleton(fetchModeOptions);
        services.AddSingleton(loggerMock.Object);
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

        // Assert - should complete without exception
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("開始執行 GitLab Pull Request 拉取任務")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task FetchBitbucketPullRequestsTask_ExecuteAsync_WithEmptyProjects_ShouldCompleteSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        var bitbucketOptions = Options.Create(new BitbucketOptions { Projects = new List<BitbucketProjectOptions>() });
        var fetchModeOptions = Options.Create(new FetchModeOptions());
        
        // Mock logger
        var loggerMock = new Mock<ILogger<FetchBitbucketPullRequestsTask>>();
        
        // Mock keyed repository service
        var repositoryMock = new Mock<ReleaseKit.Domain.Abstractions.ISourceControlRepository>();
        
        // Mock Redis service
        var redisServiceMock = new Mock<ReleaseKit.Domain.Abstractions.IRedisService>();
        redisServiceMock.Setup(x => x.HashExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
        redisServiceMock.Setup(x => x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        
        services.AddSingleton(bitbucketOptions);
        services.AddSingleton(fetchModeOptions);
        services.AddSingleton(loggerMock.Object);
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

        // Assert - should complete without exception
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("開始執行 Bitbucket Pull Request 拉取任務")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task FetchGitLabPullRequestsTask_ExecuteAsync_WithDateTimeRangeMode_ShouldFetchAndReturnResults()
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
        
        // Mock logger
        var loggerMock = new Mock<ILogger<FetchGitLabPullRequestsTask>>();
        
        // Mock repository to return sample merge requests
        var repositoryMock = new Mock<ReleaseKit.Domain.Abstractions.ISourceControlRepository>();
        var mockMergeRequests = new List<ReleaseKit.Domain.Entities.MergeRequest>
        {
            new ReleaseKit.Domain.Entities.MergeRequest
            {
                Title = "Test MR 1",
                Description = "Test Description",
                SourceBranch = "feature/test",
                TargetBranch = "main",
                CreatedAt = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero),
                MergedAt = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero),
                State = "merged",
                AuthorUserId = "user1",
                AuthorName = "Test User",
                PrId = "1",
                PRUrl = "https://gitlab.com/test/project/-/merge_requests/1",
                Platform = ReleaseKit.Domain.ValueObjects.SourceControlPlatform.GitLab,
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
            .ReturnsAsync(ReleaseKit.Domain.Common.Result<IReadOnlyList<ReleaseKit.Domain.Entities.MergeRequest>>.Success(mockMergeRequests));
        
        // Mock Redis service
        var redisServiceMock = new Mock<ReleaseKit.Domain.Abstractions.IRedisService>();
        redisServiceMock.Setup(x => x.HashExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
        redisServiceMock.Setup(x => x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        
        services.AddSingleton(gitLabOptions);
        services.AddSingleton(fetchModeOptions);
        services.AddSingleton(loggerMock.Object);
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

        // Assert - verify repository was called with correct parameters
        repositoryMock.Verify(
            x => x.GetMergeRequestsByDateRangeAsync(
                "test/project",
                "main",
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        
        // Verify logging
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("開始執行 GitLab Pull Request 拉取任務")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task FetchGitLabPullRequestsTask_ExecuteAsync_WithBranchDiffMode_ShouldFetchAndReturnResults()
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
                    SourceBranch = "develop",
                    TargetBranch = "main",
                    FetchMode = FetchMode.BranchDiff
                }
            }
        });
        var fetchModeOptions = Options.Create(new FetchModeOptions());
        
        // Mock logger
        var loggerMock = new Mock<ILogger<FetchGitLabPullRequestsTask>>();
        
        // Mock repository to return sample merge requests
        var repositoryMock = new Mock<ReleaseKit.Domain.Abstractions.ISourceControlRepository>();
        var mockMergeRequests = new List<ReleaseKit.Domain.Entities.MergeRequest>
        {
            new ReleaseKit.Domain.Entities.MergeRequest
            {
                Title = "Test MR 2",
                Description = "Test Description",
                SourceBranch = "feature/branch-diff",
                TargetBranch = "main",
                CreatedAt = new DateTimeOffset(2026, 1, 20, 10, 0, 0, TimeSpan.Zero),
                MergedAt = new DateTimeOffset(2026, 1, 20, 12, 0, 0, TimeSpan.Zero),
                State = "merged",
                AuthorUserId = "user2",
                AuthorName = "Another User",
                PrId = "2",
                PRUrl = "https://gitlab.com/test/project/-/merge_requests/2",
                Platform = ReleaseKit.Domain.ValueObjects.SourceControlPlatform.GitLab,
                ProjectPath = "test/project"
            }
        };
        
        repositoryMock
            .Setup(x => x.GetMergeRequestsByBranchDiffAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReleaseKit.Domain.Common.Result<IReadOnlyList<ReleaseKit.Domain.Entities.MergeRequest>>.Success(mockMergeRequests));
        
        // Mock Redis service
        var redisServiceMock = new Mock<ReleaseKit.Domain.Abstractions.IRedisService>();
        redisServiceMock.Setup(x => x.HashExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
        redisServiceMock.Setup(x => x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        
        services.AddSingleton(gitLabOptions);
        services.AddSingleton(fetchModeOptions);
        services.AddSingleton(loggerMock.Object);
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

        // Assert - verify repository was called with correct parameters
        repositoryMock.Verify(
            x => x.GetMergeRequestsByBranchDiffAsync(
                "test/project",
                "develop",
                "main",
                It.IsAny<CancellationToken>()),
            Times.Once);
        
        // Verify logging
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("開始執行 GitLab Pull Request 拉取任務")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task FetchBitbucketPullRequestsTask_ExecuteAsync_WithDateTimeRangeMode_ShouldFetchAndReturnResults()
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
        
        // Mock logger
        var loggerMock = new Mock<ILogger<FetchBitbucketPullRequestsTask>>();
        
        // Mock repository to return sample merge requests
        var repositoryMock = new Mock<ReleaseKit.Domain.Abstractions.ISourceControlRepository>();
        var mockMergeRequests = new List<ReleaseKit.Domain.Entities.MergeRequest>
        {
            new ReleaseKit.Domain.Entities.MergeRequest
            {
                Title = "Test PR 1",
                Description = "Test Description",
                SourceBranch = "feature/test",
                TargetBranch = "main",
                CreatedAt = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero),
                MergedAt = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero),
                State = "merged",
                AuthorUserId = "user1",
                AuthorName = "Test User",
                PrId = "1",
                PRUrl = "https://bitbucket.org/workspace/repo/pull-requests/1",
                Platform = ReleaseKit.Domain.ValueObjects.SourceControlPlatform.Bitbucket,
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
            .ReturnsAsync(ReleaseKit.Domain.Common.Result<IReadOnlyList<ReleaseKit.Domain.Entities.MergeRequest>>.Success(mockMergeRequests));
        
        // Mock Redis service
        var redisServiceMock = new Mock<ReleaseKit.Domain.Abstractions.IRedisService>();
        redisServiceMock.Setup(x => x.HashExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
        redisServiceMock.Setup(x => x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        
        services.AddSingleton(bitbucketOptions);
        services.AddSingleton(fetchModeOptions);
        services.AddSingleton(loggerMock.Object);
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

        // Assert - verify repository was called with correct parameters
        repositoryMock.Verify(
            x => x.GetMergeRequestsByDateRangeAsync(
                "workspace/repo",
                "main",
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        
        // Verify logging
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("開始執行 Bitbucket Pull Request 拉取任務")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task FetchBitbucketPullRequestsTask_ExecuteAsync_WithBranchDiffMode_ShouldFetchAndReturnResults()
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
                    SourceBranch = "develop",
                    TargetBranch = "main",
                    FetchMode = FetchMode.BranchDiff
                }
            }
        });
        var fetchModeOptions = Options.Create(new FetchModeOptions());
        
        // Mock logger
        var loggerMock = new Mock<ILogger<FetchBitbucketPullRequestsTask>>();
        
        // Mock repository to return sample merge requests
        var repositoryMock = new Mock<ReleaseKit.Domain.Abstractions.ISourceControlRepository>();
        var mockMergeRequests = new List<ReleaseKit.Domain.Entities.MergeRequest>
        {
            new ReleaseKit.Domain.Entities.MergeRequest
            {
                Title = "Test PR 2",
                Description = "Test Description",
                SourceBranch = "feature/branch-diff",
                TargetBranch = "main",
                CreatedAt = new DateTimeOffset(2026, 1, 20, 10, 0, 0, TimeSpan.Zero),
                MergedAt = new DateTimeOffset(2026, 1, 20, 12, 0, 0, TimeSpan.Zero),
                State = "merged",
                AuthorUserId = "user2",
                AuthorName = "Another User",
                PrId = "2",
                PRUrl = "https://bitbucket.org/workspace/repo/pull-requests/2",
                Platform = ReleaseKit.Domain.ValueObjects.SourceControlPlatform.Bitbucket,
                ProjectPath = "workspace/repo"
            }
        };
        
        repositoryMock
            .Setup(x => x.GetMergeRequestsByBranchDiffAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReleaseKit.Domain.Common.Result<IReadOnlyList<ReleaseKit.Domain.Entities.MergeRequest>>.Success(mockMergeRequests));
        
        // Mock Redis service
        var redisServiceMock = new Mock<ReleaseKit.Domain.Abstractions.IRedisService>();
        redisServiceMock.Setup(x => x.HashExistsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
        redisServiceMock.Setup(x => x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        
        services.AddSingleton(bitbucketOptions);
        services.AddSingleton(fetchModeOptions);
        services.AddSingleton(loggerMock.Object);
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

        // Assert - verify repository was called with correct parameters
        repositoryMock.Verify(
            x => x.GetMergeRequestsByBranchDiffAsync(
                "workspace/repo",
                "develop",
                "main",
                It.IsAny<CancellationToken>()),
            Times.Once);
        
        // Verify logging
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("開始執行 Bitbucket Pull Request 拉取任務")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateGoogleSheetsTask_ExecuteAsync_ShouldReturnWhenNoRedisData()
    {
        // Arrange
        var redisServiceMock = new Mock<IRedisService>();
        var googleSheetServiceMock = new Mock<IGoogleSheetService>();
        var loggerMock = new Mock<ILogger<UpdateGoogleSheetsTask>>();
        var options = Options.Create(new ReleaseKit.Common.Configuration.GoogleSheetOptions
        {
            SpreadsheetId = "id",
            SheetName = "Sheet",
            ColumnMapping = new ReleaseKit.Common.Configuration.ColumnMappingOptions
            {
                RepositoryNameColumn = "Z", FeatureColumn = "B", TeamColumn = "D",
                AuthorsColumn = "W", PullRequestUrlsColumn = "X", UniqueKeyColumn = "Y", AutoSyncColumn = "F"
            }
        });

        redisServiceMock
            .Setup(x => x.HashGetAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        var task = new UpdateGoogleSheetsTask(
            redisServiceMock.Object,
            googleSheetServiceMock.Object,
            options,
            loggerMock.Object);

        // Act：無資料時應正常返回，不拋出例外
        await task.ExecuteAsync();

        // Assert：不應呼叫 Google Sheet API
        googleSheetServiceMock.Verify(
            x => x.GetSheetDataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }
}
