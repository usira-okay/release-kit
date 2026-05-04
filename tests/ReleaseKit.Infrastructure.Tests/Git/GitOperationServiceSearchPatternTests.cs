using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReleaseKit.Infrastructure.Git;

namespace ReleaseKit.Infrastructure.Tests.Git;

/// <summary>
/// GitOperationService.SearchPatternAsync 單元測試
/// </summary>
public class GitOperationServiceSearchPatternTests
{
    private readonly GitOperationService _sut;
    private readonly FakeGitCommandRunner _gitRunner = new();

    public GitOperationServiceSearchPatternTests()
    {
        var logger = new Mock<ILogger<GitOperationService>>();
        _sut = new GitOperationService(logger.Object, _gitRunner);
    }

    [Fact]
    public async Task SearchPatternAsync_非Git倉庫_應回傳失敗()
    {
        // Arrange
        var nonGitPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(nonGitPath);

        try
        {
            // Act
            var result = await _sut.SearchPatternAsync(nonGitPath, "test");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error!.Code.Should().Be("Git.SearchFailed");
        }
        finally
        {
            Directory.Delete(nonGitPath, true);
        }
    }

    [Fact]
    public async Task SearchPatternAsync_有效倉庫但無符合結果_應回傳空字串()
    {
        // Arrange
        using var repo = TemporaryGitRepository.Create();
        _gitRunner.NextResult = new GitCommandResult(1, string.Empty, string.Empty);

        // Act
        var result = await _sut.SearchPatternAsync(repo.Path, "pattern-without-match");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchPatternAsync_有效倉庫有符合結果_應回傳搜尋內容()
    {
        // Arrange
        using var repo = TemporaryGitRepository.Create();
        _gitRunner.NextResult = new GitCommandResult(0, "src/File.cs:1:IGitOperationService", string.Empty);

        // Act
        var result = await _sut.SearchPatternAsync(repo.Path, "IGitOperationService", "*.cs");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("IGitOperationService");
        var call = _gitRunner.Calls.Should().ContainSingle().Subject;
        call.Arguments.Should().Equal("grep", "-n", "-E", "-e", "IGitOperationService", "--", "*.cs");
        call.WorkingDirectory.Should().Be(repo.Path);
    }

    [Fact]
    public async Task SearchPatternAsync_含特殊字元Pattern_應以獨立參數傳遞()
    {
        // Arrange
        using var repo = TemporaryGitRepository.Create();
        const string pattern = "a\"b\\\\c";

        // Act
        var result = await _sut.SearchPatternAsync(repo.Path, pattern, "src/*.cs");

        // Assert
        result.IsSuccess.Should().BeTrue();
        var call = _gitRunner.Calls.Should().ContainSingle().Subject;
        call.Arguments.Should().Equal("grep", "-n", "-E", "-e", pattern, "--", "src/*.cs");
    }

    private sealed class FakeGitCommandRunner : IGitCommandRunner
    {
        public List<(IReadOnlyList<string> Arguments, string WorkingDirectory)> Calls { get; } = new();
        public GitCommandResult? NextResult { get; set; } = new(0, string.Empty, string.Empty);

        public Task<GitCommandResult?> RunAsync(
            IReadOnlyList<string> arguments,
            string workingDirectory,
            CancellationToken cancellationToken)
        {
            Calls.Add((arguments.ToList(), workingDirectory));
            return Task.FromResult(NextResult);
        }
    }

    private sealed class TemporaryGitRepository : IDisposable
    {
        private TemporaryGitRepository(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryGitRepository Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"test-repo-{Guid.NewGuid():N}");
            // SearchPatternAsync 僅檢查 .git 目錄後交由 fake runner，不需要初始化真實 Git 倉庫。
            Directory.CreateDirectory(System.IO.Path.Combine(path, ".git"));
            return new TemporaryGitRepository(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, true);
            }
        }
    }
}
