using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Common;
using ReleaseKit.Common.Git;

namespace ReleaseKit.Application.Tests.Tasks;

/// <summary>
/// CloneRepositoriesTask 單元測試
/// </summary>
public class CloneRepositoriesTaskTests
{
    private readonly Mock<IGitOperationService> _gitServiceMock;
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly Mock<INow> _nowMock;
    private readonly Mock<ILogger<CloneRepositoriesTask>> _loggerMock;

    private readonly GitLabOptions _gitLabOptions;
    private readonly BitbucketOptions _bitbucketOptions;
    private readonly RiskAnalysisOptions _riskOptions;

    private static readonly DateTimeOffset FixedTime = new(2024, 3, 15, 10, 30, 45, TimeSpan.Zero);
    private const string ExpectedRunId = "20240315103045";

    public CloneRepositoriesTaskTests()
    {
        _gitServiceMock = new Mock<IGitOperationService>();
        _redisServiceMock = new Mock<IRedisService>();
        _nowMock = new Mock<INow>();
        _loggerMock = new Mock<ILogger<CloneRepositoriesTask>>();

        _nowMock.Setup(x => x.UtcNow).Returns(FixedTime);

        _redisServiceMock
            .Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(true);

        _redisServiceMock
            .Setup(x => x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        _gitLabOptions = new GitLabOptions
        {
            ApiUrl = "https://gitlab.example.com/api/v4",
            AccessToken = "gitlab-token",
            Projects = new List<GitLabProjectOptions>
            {
                new() { ProjectPath = "group/project-a", TargetBranch = "main" }
            }
        };

        _bitbucketOptions = new BitbucketOptions
        {
            ApiUrl = "https://api.bitbucket.org/2.0",
            Email = "user@example.com",
            AccessToken = "bb-token",
            Projects = new List<BitbucketProjectOptions>
            {
                new() { ProjectPath = "workspace/repo-b", TargetBranch = "main" }
            }
        };

        _riskOptions = new RiskAnalysisOptions
        {
            CloneBasePath = "/repos"
        };

        _gitServiceMock
            .Setup(x => x.CloneOrPullAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("/repos/some/path"));
    }

    private CloneRepositoriesTask CreateTask(
        GitLabOptions? gitLabOptions = null,
        BitbucketOptions? bitbucketOptions = null,
        RiskAnalysisOptions? riskOptions = null)
    {
        return new CloneRepositoriesTask(
            _gitServiceMock.Object,
            _redisServiceMock.Object,
            _nowMock.Object,
            Options.Create(gitLabOptions ?? _gitLabOptions),
            Options.Create(bitbucketOptions ?? _bitbucketOptions),
            Options.Create(riskOptions ?? _riskOptions),
            _loggerMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_應產生RunId並儲存至Redis()
    {
        // Arrange
        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _redisServiceMock.Verify(x =>
            x.SetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey, ExpectedRunId, null),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_應使用正確URL_Clone_GitLab專案()
    {
        // Arrange
        var task = CreateTask();
        var expectedUrl = CloneUrlBuilder.BuildGitLabCloneUrl(_gitLabOptions, "group/project-a");
        var expectedLocalPath = Path.Combine("/repos", "group", "project-a");

        // Act
        await task.ExecuteAsync();

        // Assert
        _gitServiceMock.Verify(x =>
            x.CloneOrPullAsync(expectedUrl, expectedLocalPath, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_應使用正確URL_Clone_Bitbucket專案()
    {
        // Arrange
        var task = CreateTask();
        var expectedUrl = CloneUrlBuilder.BuildBitbucketCloneUrl(_bitbucketOptions, "workspace/repo-b");
        var expectedLocalPath = Path.Combine("/repos", "workspace", "repo-b");

        // Act
        await task.ExecuteAsync();

        // Assert
        _gitServiceMock.Verify(x =>
            x.CloneOrPullAsync(expectedUrl, expectedLocalPath, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_成功Clone後_應將結果寫入Redis_Stage1_Hash()
    {
        // Arrange
        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert - GitLab project result stored
        _redisServiceMock.Verify(x =>
            x.HashSetAsync(
                RiskAnalysisRedisKeys.Stage1Hash(ExpectedRunId),
                "group/project-a",
                It.Is<string>(v => v.Contains("Success") && v.Contains("/repos"))),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Clone失敗時_應儲存失敗狀態並繼續()
    {
        // Arrange
        var error = Error.Git.CloneFailed("https://gitlab.example.com/group/project-a.git", "connection refused");
        _gitServiceMock
            .Setup(x => x.CloneOrPullAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Failure(error));

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert - 仍寫入失敗狀態（不拋出例外）
        _redisServiceMock.Verify(x =>
            x.HashSetAsync(
                RiskAnalysisRedisKeys.Stage1Hash(ExpectedRunId),
                It.IsAny<string>(),
                It.Is<string>(v => v.Contains("Failed"))),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_有多個GitLab專案時_應全部Clone()
    {
        // Arrange
        var gitLabOptions = new GitLabOptions
        {
            ApiUrl = "https://gitlab.example.com/api/v4",
            AccessToken = "token",
            Projects = new List<GitLabProjectOptions>
            {
                new() { ProjectPath = "group/project-1", TargetBranch = "main" },
                new() { ProjectPath = "group/project-2", TargetBranch = "main" },
                new() { ProjectPath = "group/project-3", TargetBranch = "main" }
            }
        };
        var bitbucketOptions = new BitbucketOptions { Projects = new List<BitbucketProjectOptions>() };
        var task = CreateTask(gitLabOptions: gitLabOptions, bitbucketOptions: bitbucketOptions);

        // Act
        await task.ExecuteAsync();

        // Assert
        _gitServiceMock.Verify(x =>
            x.CloneOrPullAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task ExecuteAsync_無專案時_應僅儲存RunId()
    {
        // Arrange
        var gitLabOptions = new GitLabOptions { Projects = new List<GitLabProjectOptions>() };
        var bitbucketOptions = new BitbucketOptions { Projects = new List<BitbucketProjectOptions>() };
        var task = CreateTask(gitLabOptions: gitLabOptions, bitbucketOptions: bitbucketOptions);

        // Act
        await task.ExecuteAsync();

        // Assert
        _redisServiceMock.Verify(x =>
            x.SetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey, ExpectedRunId, null),
            Times.Once);
        _gitServiceMock.Verify(x =>
            x.CloneOrPullAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
