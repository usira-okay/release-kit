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

    public GitOperationServiceSearchPatternTests()
    {
        var logger = new Mock<ILogger<GitOperationService>>();
        _sut = new GitOperationService(logger.Object);
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
        // Arrange — 使用當前專案 repo
        var repoPath = GetCurrentRepoRoot();

        // Act — 搜尋一個不可能存在的模式
        // 使用一個不太可能在任何程式碼中出現的特殊字串
        var impossiblePattern = $"{Guid.NewGuid():N}_PATTERN_THAT_NEVER_EXISTS";
        var result = await _sut.SearchPatternAsync(repoPath, impossiblePattern);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchPatternAsync_有效倉庫有符合結果_應回傳搜尋內容()
    {
        // Arrange — 使用當前專案 repo
        var repoPath = GetCurrentRepoRoot();

        // Act — 搜尋 "IGitOperationService"，這在 Domain 層一定存在
        var result = await _sut.SearchPatternAsync(repoPath, "IGitOperationService", "*.cs");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("IGitOperationService");
    }

    private static string GetCurrentRepoRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null && !Directory.Exists(Path.Combine(dir, ".git")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("找不到 Git 倉庫根目錄");
    }
}
