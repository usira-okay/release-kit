using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Common;
using ReleaseKit.Domain.Entities;

namespace ReleaseKit.Application.Tests.Tasks;

/// <summary>
/// Release Branch 動態 Target Branch 調整邏輯的整合測試
/// </summary>
public sealed class ReleaseBranchTargetAdjustmentTests
{
    private Mock<ISourceControlRepository> CreateRepositoryMock() => new();
    private Mock<ILogger<FetchGitLabPullRequestsTask>> CreateLoggerMock() => new();
    private Mock<IRedisService> CreateRedisServiceMock() => new();
    
    private Mock<IServiceProvider> CreateServiceProviderMock(ISourceControlRepository repository)
    {
        var serviceProviderMock = new Mock<IServiceProvider>();
        var keyedServiceProviderMock = serviceProviderMock.As<IKeyedServiceProvider>();
        keyedServiceProviderMock
            .Setup(s => s.GetKeyedService(typeof(ISourceControlRepository), "GitLab"))
            .Returns(repository);
        return serviceProviderMock;
    }

    [Fact]
    public async Task ExecuteBranchDiffMode_WhenSourceBranchIsLatestRelease_ShouldKeepTargetBranchUnchanged()
    {
        // Arrange
        var repositoryMock = CreateRepositoryMock();
        var loggerMock = CreateLoggerMock();
        var redisServiceMock = CreateRedisServiceMock();
        var serviceProviderMock = CreateServiceProviderMock(repositoryMock.Object);
        
        var repositoryMock = CreateRepositoryMock();
        var loggerMock = CreateLoggerMock();
        var redisServiceMock = CreateRedisServiceMock();
        var serviceProviderMock = CreateServiceProviderMock(repositoryMock.Object);
        
        var sourceBranch = "release/20250315";
        var originalTargetBranch = "main";

        var allBranches = new List<string>
        {
            "release/20250315",  // 最新 - 這是 source branch
            "release/20250101",
            "release/20241201",
            "main",
            "develop"
        };

        repositoryMock
            .Setup(r => r.GetBranchesAsync("test/project", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(allBranches));

        repositoryMock
            .Setup(r => r.GetMergeRequestsByBranchDiffAsync("test/project", sourceBranch, originalTargetBranch, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<MergeRequest>>.Success(Array.Empty<MergeRequest>()));

        redisServiceMock
            .Setup(r => r.ExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        redisServiceMock
            .Setup(r => r.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(true);

        var gitLabOptions = Options.Create(new GitLabOptions
        {
            ApiUrl = "https://gitlab.example.com",
            AccessToken = "test-token",
            Projects = new List<GitLabProjectOptions>
            {
                new()
                {
                    ProjectPath = "test/project",
                    SourceBranch = sourceBranch,
                    TargetBranch = originalTargetBranch,
                    FetchMode = FetchMode.BranchDiff
                }
            }
        });

        var fetchModeOptions = Options.Create(new FetchModeOptions
        {
            FetchMode = FetchMode.BranchDiff
        });

        var task = new FetchGitLabPullRequestsTask(
            serviceProviderMock.Object,
            loggerMock.Object,
            redisServiceMock.Object,
            gitLabOptions,
            fetchModeOptions);

        // Act
        await task.ExecuteAsync();

        // Assert
        // 驗證呼叫 GetBranchesAsync 以取得 release branches
        repositoryMock.Verify(
            r => r.GetBranchesAsync("test/project", "release/", It.IsAny<CancellationToken>()),
            Times.Once);

        // 驗證最終呼叫 GetMergeRequestsByBranchDiffAsync 時，targetBranch 保持不變（仍是 main）
        repositoryMock.Verify(
            r => r.GetMergeRequestsByBranchDiffAsync("test/project", sourceBranch, originalTargetBranch, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteBranchDiffMode_WhenSourceBranchIsNotLatestRelease_ShouldUpdateTargetBranchToNextNewer()
    {
        // Arrange
        var repositoryMock = CreateRepositoryMock();
        var loggerMock = CreateLoggerMock();
        var redisServiceMock = CreateRedisServiceMock();
        var serviceProviderMock = CreateServiceProviderMock(repositoryMock.Object);
        
        var sourceBranch = "release/20241201";
        var originalTargetBranch = "main";
        var expectedNewTargetBranch = "release/20241215";  // 下一個較新的版本

        var allBranches = new List<string>
        {
            "release/20250315",  // 最新
            "release/20250101",
            "release/20241215",  // 這個應該成為新的 targetBranch
            "release/20241201",  // 這是 source branch
            "release/20240601",  // 更舊
            "main",
            "develop"
        };

        repositoryMock
            .Setup(r => r.GetBranchesAsync("test/project", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(allBranches));

        repositoryMock
            .Setup(r => r.GetMergeRequestsByBranchDiffAsync("test/project", sourceBranch, expectedNewTargetBranch, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<MergeRequest>>.Success(Array.Empty<MergeRequest>()));

        redisServiceMock
            .Setup(r => r.ExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        redisServiceMock
            .Setup(r => r.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(true);

        var gitLabOptions = Options.Create(new GitLabOptions
        {
            ApiUrl = "https://gitlab.example.com",
            AccessToken = "test-token",
            Projects = new List<GitLabProjectOptions>
            {
                new()
                {
                    ProjectPath = "test/project",
                    SourceBranch = sourceBranch,
                    TargetBranch = originalTargetBranch,
                    FetchMode = FetchMode.BranchDiff
                }
            }
        });

        var fetchModeOptions = Options.Create(new FetchModeOptions
        {
            FetchMode = FetchMode.BranchDiff
        });

        var task = new FetchGitLabPullRequestsTask(
            serviceProviderMock.Object,
            loggerMock.Object,
            redisServiceMock.Object,
            gitLabOptions,
            fetchModeOptions);

        // Act
        await task.ExecuteAsync();

        // Assert
        // 驗證呼叫 GetBranchesAsync 以取得 release branches
        repositoryMock.Verify(
            r => r.GetBranchesAsync("test/project", "release/", It.IsAny<CancellationToken>()),
            Times.Once);

        // 驗證最終呼叫 GetMergeRequestsByBranchDiffAsync 時，targetBranch 已被更新為 release/20241215
        repositoryMock.Verify(
            r => r.GetMergeRequestsByBranchDiffAsync("test/project", sourceBranch, expectedNewTargetBranch, It.IsAny<CancellationToken>()),
            Times.Once);

        // 確保沒有使用原始的 targetBranch（main）呼叫
        repositoryMock.Verify(
            r => r.GetMergeRequestsByBranchDiffAsync("test/project", sourceBranch, originalTargetBranch, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteBranchDiffMode_WhenSourceBranchIsNotReleaseBranch_ShouldKeepOriginalBehavior()
    {
        // Arrange
        var repositoryMock = CreateRepositoryMock();
        var loggerMock = CreateLoggerMock();
        var redisServiceMock = CreateRedisServiceMock();
        var serviceProviderMock = CreateServiceProviderMock(repositoryMock.Object);
        
        var sourceBranch = "feature/test";
        var targetBranch = "main";

        repositoryMock
            .Setup(r => r.GetMergeRequestsByBranchDiffAsync("test/project", sourceBranch, targetBranch, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<MergeRequest>>.Success(Array.Empty<MergeRequest>()));

        redisServiceMock
            .Setup(r => r.ExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        redisServiceMock
            .Setup(r => r.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(true);

        var gitLabOptions = Options.Create(new GitLabOptions
        {
            ApiUrl = "https://gitlab.example.com",
            AccessToken = "test-token",
            Projects = new List<GitLabProjectOptions>
            {
                new()
                {
                    ProjectPath = "test/project",
                    SourceBranch = sourceBranch,
                    TargetBranch = targetBranch,
                    FetchMode = FetchMode.BranchDiff
                }
            }
        });

        var fetchModeOptions = Options.Create(new FetchModeOptions
        {
            FetchMode = FetchMode.BranchDiff
        });

        var task = new FetchGitLabPullRequestsTask(
            serviceProviderMock.Object,
            loggerMock.Object,
            redisServiceMock.Object,
            gitLabOptions,
            fetchModeOptions);

        // Act
        await task.ExecuteAsync();

        // Assert
        // 驗證不會呼叫 GetBranchesAsync（因為 source branch 不是 release branch）
        repositoryMock.Verify(
            r => r.GetBranchesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // 驗證直接使用原始的 source 和 target branch 呼叫
        repositoryMock.Verify(
            r => r.GetMergeRequestsByBranchDiffAsync("test/project", sourceBranch, targetBranch, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteBranchDiffMode_WhenGetBranchesAsyncFails_ShouldFallbackToOriginalTargetBranch()
    {
        // Arrange
        var repositoryMock = CreateRepositoryMock();
        var loggerMock = CreateLoggerMock();
        var redisServiceMock = CreateRedisServiceMock();
        var serviceProviderMock = CreateServiceProviderMock(repositoryMock.Object);
        
        var sourceBranch = "release/20241201";
        var originalTargetBranch = "main";

        repositoryMock
            .Setup(r => r.GetBranchesAsync("test/project", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Failure(Domain.Common.Error.SourceControl.ApiError("Test error")));

        repositoryMock
            .Setup(r => r.GetMergeRequestsByBranchDiffAsync("test/project", sourceBranch, originalTargetBranch, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<MergeRequest>>.Success(Array.Empty<MergeRequest>()));

        redisServiceMock
            .Setup(r => r.ExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        redisServiceMock
            .Setup(r => r.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(true);

        var gitLabOptions = Options.Create(new GitLabOptions
        {
            ApiUrl = "https://gitlab.example.com",
            AccessToken = "test-token",
            Projects = new List<GitLabProjectOptions>
            {
                new()
                {
                    ProjectPath = "test/project",
                    SourceBranch = sourceBranch,
                    TargetBranch = originalTargetBranch,
                    FetchMode = FetchMode.BranchDiff
                }
            }
        });

        var fetchModeOptions = Options.Create(new FetchModeOptions
        {
            FetchMode = FetchMode.BranchDiff
        });

        var task = new FetchGitLabPullRequestsTask(
            serviceProviderMock.Object,
            loggerMock.Object,
            redisServiceMock.Object,
            gitLabOptions,
            fetchModeOptions);

        // Act
        await task.ExecuteAsync();

        // Assert
        // 驗證有嘗試呼叫 GetBranchesAsync
        repositoryMock.Verify(
            r => r.GetBranchesAsync("test/project", "release/", It.IsAny<CancellationToken>()),
            Times.Once);

        // 驗證仍使用原始的 targetBranch（fallback 行為）
        repositoryMock.Verify(
            r => r.GetMergeRequestsByBranchDiffAsync("test/project", sourceBranch, originalTargetBranch, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
