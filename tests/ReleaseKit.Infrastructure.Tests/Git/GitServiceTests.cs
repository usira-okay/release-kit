using Microsoft.Extensions.Logging;
using Moq;
using ReleaseKit.Infrastructure.Git;

namespace ReleaseKit.Infrastructure.Tests.Git;

/// <summary>
/// GitService 單元測試
/// </summary>
public class GitServiceTests
{
    private readonly Mock<ILogger<GitService>> _loggerMock = new();
    private readonly GitService _sut;

    public GitServiceTests()
    {
        _sut = new GitService(_loggerMock.Object);
    }

    [Fact]
    public async Task CloneRepositoryAsync_WithEmptyUrl_ShouldReturnFailure()
    {
        var result = await _sut.CloneRepositoryAsync("", "/some/path", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Clone 失敗", result.Error!.Message);
    }

    [Fact]
    public async Task CloneRepositoryAsync_WithEmptyTargetPath_ShouldReturnFailure()
    {
        var result = await _sut.CloneRepositoryAsync("https://example.com/repo.git", "", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Clone 失敗", result.Error!.Message);
    }

    [Fact]
    public async Task GetBranchDiffAsync_WithEmptyRepoPath_ShouldReturnFailure()
    {
        var result = await _sut.GetBranchDiffAsync("", "main", "develop", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Git 命令失敗", result.Error!.Message);
    }

    [Fact]
    public async Task GetCommitDiffAsync_WithEmptyRepoPath_ShouldReturnFailure()
    {
        var result = await _sut.GetCommitDiffAsync("", "abc123", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Git 命令", result.Error!.Message);
    }

    [Fact]
    public async Task GetRemoteUrlAsync_WithEmptyRepoPath_ShouldReturnFailure()
    {
        var result = await _sut.GetRemoteUrlAsync("", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Git 命令失敗", result.Error!.Message);
    }

    [Fact]
    public async Task FindMergeCommitAsync_WithEmptyRepoPath_ShouldReturnFailure()
    {
        var result = await _sut.FindMergeCommitAsync("", "feature/test", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Git 命令失敗", result.Error!.Message);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CloneRepositoryAsync_WithNonExistentRepo_ShouldReturnFailure()
    {
        var result = await _sut.CloneRepositoryAsync(
            "https://example.com/nonexistent.git",
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
    }
}
