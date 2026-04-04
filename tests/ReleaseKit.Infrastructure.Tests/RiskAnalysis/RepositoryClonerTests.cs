using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReleaseKit.Infrastructure.RiskAnalysis;
using Xunit;

namespace ReleaseKit.Infrastructure.Tests.RiskAnalysis;

/// <summary>
/// RepositoryCloner 單元測試
/// </summary>
public class RepositoryClonerTests : IDisposable
{
    private readonly Mock<IProcessRunner> _processRunnerMock;
    private readonly Mock<ILogger<RepositoryCloner>> _loggerMock;
    private readonly RepositoryCloner _sut;
    private readonly List<string> _tempDirectories = new();

    public RepositoryClonerTests()
    {
        _processRunnerMock = new Mock<IProcessRunner>();
        _loggerMock = new Mock<ILogger<RepositoryCloner>>();
        _sut = new RepositoryCloner(_processRunnerMock.Object, _loggerMock.Object);
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirectories)
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }

    private string CreateTempDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        _tempDirectories.Add(dir);
        return dir;
    }

    [Fact]
    public async Task CloneAsync_WhenDirectoryNotExists_ShouldRunGitClone()
    {
        // Arrange
        var cloneUrl = "https://gitlab.example.com/group/repo.git";
        var targetPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "repo");

        _processRunnerMock
            .Setup(x => x.RunAsync("git", $"clone {cloneUrl} {targetPath}", null))
            .ReturnsAsync(new ProcessRunResult
            {
                ExitCode = 0,
                StandardOutput = "Cloning into 'repo'...\n",
                StandardError = string.Empty
            });

        // Act
        var result = await _sut.CloneAsync(cloneUrl, targetPath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(targetPath);
        _processRunnerMock.Verify(
            x => x.RunAsync("git", $"clone {cloneUrl} {targetPath}", null),
            Times.Once);
    }

    [Fact]
    public async Task CloneAsync_WhenGitDirectoryExists_ShouldRunGitPull()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(tempDir, ".git"));

        var cloneUrl = "https://gitlab.example.com/group/repo.git";

        _processRunnerMock
            .Setup(x => x.RunAsync("git", "pull", tempDir))
            .ReturnsAsync(new ProcessRunResult
            {
                ExitCode = 0,
                StandardOutput = "Already up to date.\n",
                StandardError = string.Empty
            });

        // Act
        var result = await _sut.CloneAsync(cloneUrl, tempDir);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(tempDir);
        _processRunnerMock.Verify(
            x => x.RunAsync("git", "pull", tempDir),
            Times.Once);
        _processRunnerMock.Verify(
            x => x.RunAsync("git", It.Is<string>(s => s.StartsWith("clone")), It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task CloneAsync_WhenCloneFails_ShouldReturnFailure()
    {
        // Arrange
        var cloneUrl = "https://gitlab.example.com/group/repo.git";
        var targetPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "repo");
        var errorMessage = "fatal: repository not found";

        _processRunnerMock
            .Setup(x => x.RunAsync("git", $"clone {cloneUrl} {targetPath}", null))
            .ReturnsAsync(new ProcessRunResult
            {
                ExitCode = 128,
                StandardOutput = string.Empty,
                StandardError = errorMessage
            });

        // Act
        var result = await _sut.CloneAsync(cloneUrl, targetPath);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("RiskAnalysis.CloneFailed");
        result.Error.Message.Should().Contain(cloneUrl);
        result.Error.Message.Should().Contain(errorMessage);
    }

    [Fact]
    public async Task CloneAsync_WhenPullFails_ShouldReturnFailure()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(tempDir, ".git"));

        var cloneUrl = "https://gitlab.example.com/group/repo.git";
        var errorMessage = "fatal: unable to access remote";

        _processRunnerMock
            .Setup(x => x.RunAsync("git", "pull", tempDir))
            .ReturnsAsync(new ProcessRunResult
            {
                ExitCode = 1,
                StandardOutput = string.Empty,
                StandardError = errorMessage
            });

        // Act
        var result = await _sut.CloneAsync(cloneUrl, tempDir);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("RiskAnalysis.CloneFailed");
        result.Error.Message.Should().Contain(cloneUrl);
        result.Error.Message.Should().Contain(errorMessage);
    }

    [Fact]
    public async Task CleanupAsync_WhenDirectoryExists_ShouldDeleteDirectory()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        var subFile = Path.Combine(tempDir, "test.txt");
        await File.WriteAllTextAsync(subFile, "test content");

        // Act
        await _sut.CleanupAsync(tempDir);

        // Assert
        Directory.Exists(tempDir).Should().BeFalse();

        // 從追蹤清單移除，避免 Dispose 時再次刪除
        _tempDirectories.Remove(tempDir);
    }

    [Fact]
    public async Task CleanupAsync_WhenDirectoryNotExists_ShouldNotThrow()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        // Act
        var act = () => _sut.CleanupAsync(nonExistentPath);

        // Assert
        await act.Should().NotThrowAsync();
    }
}
