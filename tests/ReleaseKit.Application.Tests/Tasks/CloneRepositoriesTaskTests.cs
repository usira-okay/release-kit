using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Common;

namespace ReleaseKit.Application.Tests.Tasks;

/// <summary>
/// CloneRepositoriesTask 單元測試
/// </summary>
public class CloneRepositoriesTaskTests
{
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly Mock<IGitService> _gitServiceMock;
    private readonly Mock<ILogger<CloneRepositoriesTask>> _loggerMock;
    private readonly GitLabOptions _gitLabOptions;
    private readonly BitbucketOptions _bitbucketOptions;
    private readonly RiskAnalysisOptions _riskAnalysisOptions;
    private string? _capturedRedisJson;

    public CloneRepositoriesTaskTests()
    {
        _redisServiceMock = new Mock<IRedisService>();
        _gitServiceMock = new Mock<IGitService>();
        _loggerMock = new Mock<ILogger<CloneRepositoriesTask>>();

        _gitLabOptions = new GitLabOptions
        {
            ApiUrl = "https://gitlab.example.com/api/v4",
            AccessToken = "gl-token",
            Projects = new List<GitLabProjectOptions>
            {
                new() { ProjectPath = "group/project-a", TargetBranch = "main" }
            }
        };

        _bitbucketOptions = new BitbucketOptions
        {
            ApiUrl = "https://api.bitbucket.org/2.0",
            Email = "test@example.com",
            AccessToken = "test-token",
            Projects = new List<BitbucketProjectOptions>
            {
                new() { ProjectPath = "team/repo-b", TargetBranch = "main" }
            }
        };

        _riskAnalysisOptions = new RiskAnalysisOptions
        {
            CloneBasePath = "/clone-base",
            ReportOutputPath = "/reports"
        };

        // 預設 Clone 成功
        _gitServiceMock.Setup(x => x.CloneRepositoryAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string path, CancellationToken _) =>
                Result<string>.Success(path));

        // 捕捉寫入 Redis 的 JSON
        _redisServiceMock.Setup(x => x.HashSetAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.ClonePaths, It.IsAny<string>()))
            .Callback<string, string, string>((_, _, json) => _capturedRedisJson = json)
            .ReturnsAsync(true);
    }

    private CloneRepositoriesTask CreateTask()
    {
        return new CloneRepositoriesTask(
            _redisServiceMock.Object,
            _gitServiceMock.Object,
            Options.Create(_gitLabOptions),
            Options.Create(_bitbucketOptions),
            Options.Create(_riskAnalysisOptions),
            _loggerMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCloneAllProjects()
    {
        // Arrange
        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 應呼叫兩次 Clone（1 GitLab + 1 Bitbucket）
        _gitServiceMock.Verify(
            x => x.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldBuildCorrectGitLabCloneUrl()
    {
        // Arrange
        string? capturedUrl = null;
        _gitServiceMock.Setup(x => x.CloneRepositoryAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string url, string path, CancellationToken _) =>
            {
                if (url.Contains("gitlab"))
                {
                    capturedUrl = url;
                }

                return Result<string>.Success(path);
            });

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 移除 /api/v4 後組合 URL
        Assert.Equal("https://gitlab.example.com/group/project-a.git", capturedUrl);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldBuildCorrectBitbucketCloneUrl()
    {
        // Arrange
        string? capturedUrl = null;
        _gitServiceMock.Setup(x => x.CloneRepositoryAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string url, string path, CancellationToken _) =>
            {
                if (url.Contains("bitbucket"))
                {
                    capturedUrl = url;
                }

                return Result<string>.Success(path);
            });

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 內嵌 email:token（URL Encoded）
        Assert.Equal("https://test%40example.com:test-token@bitbucket.org/team/repo-b.git", capturedUrl);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStoreClonePathsInRedis()
    {
        // Arrange
        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 應將 Clone 路徑對照表寫入 Redis
        Assert.NotNull(_capturedRedisJson);

        var paths = _capturedRedisJson.ToTypedObject<Dictionary<string, string>>();
        Assert.NotNull(paths);
        Assert.Equal(2, paths.Count);
        Assert.Equal("/clone-base/group/project-a", paths["group/project-a"]);
        Assert.Equal("/clone-base/team/repo-b", paths["team/repo-b"]);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCloneFails_ShouldContinueWithOtherProjects()
    {
        // Arrange — 第一次 Clone 失敗，第二次成功
        var callCount = 0;
        _gitServiceMock.Setup(x => x.CloneRepositoryAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string url, string path, CancellationToken _) =>
            {
                callCount++;
                return callCount == 1
                    ? Result<string>.Failure(Error.RiskAnalysis.CloneFailed(url))
                    : Result<string>.Success(path);
            });

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 兩次 Clone 都應被嘗試
        _gitServiceMock.Verify(
            x => x.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        // 只有成功的路徑應寫入 Redis
        Assert.NotNull(_capturedRedisJson);
        var paths = _capturedRedisJson.ToTypedObject<Dictionary<string, string>>();
        Assert.NotNull(paths);
        Assert.Single(paths);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoProjects_ShouldComplete()
    {
        // Arrange — 空專案清單
        _gitLabOptions.Projects.Clear();
        _bitbucketOptions.Projects.Clear();

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 不應呼叫 Clone
        _gitServiceMock.Verify(
            x => x.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
