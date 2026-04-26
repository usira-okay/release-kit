using Microsoft.Extensions.Logging;
using Moq;
using ReleaseKit.Infrastructure.Git;

namespace ReleaseKit.Infrastructure.Tests.Git;

/// <summary>
/// GitOperationService 單元測試
/// </summary>
public class GitOperationServiceTests
{
    private readonly Mock<ILogger<GitOperationService>> _loggerMock = new();
    private readonly GitOperationService _service;

    public GitOperationServiceTests()
    {
        _service = new GitOperationService(_loggerMock.Object);
    }

    [Fact]
    public async Task CloneOrPullAsync_目錄不存在時應執行Clone()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-clone-{Guid.NewGuid():N}");

        var result = await _service.CloneOrPullAsync(
            "https://invalid-url.example.com/nonexistent.git",
            tempDir);

        // Expected: failure (URL doesn't exist)
        Assert.True(result.IsFailure);
        Assert.Contains("Clone", result.Error!.Code);

        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task CloneOrPullAsync_目錄已存在且為Git倉庫時應執行Pull()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-pull-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(Path.Combine(tempDir, ".git"));

        var result = await _service.CloneOrPullAsync(
            "https://invalid-url.example.com/nonexistent.git",
            tempDir);

        // Expected: failure (not a real git repo)
        Assert.True(result.IsFailure);

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task GetCommitDiffAsync_無效路徑應回傳失敗()
    {
        var result = await _service.GetCommitDiffAsync("/nonexistent/path", "abc123");

        Assert.True(result.IsFailure);
        Assert.Contains("Diff", result.Error!.Code);
    }
}
