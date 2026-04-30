using Microsoft.Extensions.Logging;
using Moq;
using ReleaseKit.Domain.ValueObjects;
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
    public async Task GetCommitStatAsync_無效路徑應回傳失敗()
    {
        var result = await _service.GetCommitStatAsync("/nonexistent/path", "abc123");

        Assert.True(result.IsFailure);
        Assert.Contains("Diff", result.Error!.Code);
    }

    [Fact]
    public void ParseNameStatusToFileDiffs_空輸出應回傳空清單()
    {
        var result = GitOperationService.ParseNameStatusToFileDiffs("", "abc123");

        Assert.Empty(result);
    }

    [Fact]
    public void ParseNameStatusToFileDiffs_新增檔案_應正確解析ChangeType為Added()
    {
        var nameStatus = "A\tsrc/NewFile.cs\n";

        var result = GitOperationService.ParseNameStatusToFileDiffs(nameStatus, "abc123");

        Assert.Single(result);
        Assert.Equal("src/NewFile.cs", result[0].FilePath);
        Assert.Equal(ChangeType.Added, result[0].ChangeType);
        Assert.Equal("abc123", result[0].CommitSha);
    }

    [Fact]
    public void ParseNameStatusToFileDiffs_刪除檔案_應正確解析ChangeType為Deleted()
    {
        var nameStatus = "D\tsrc/OldFile.cs\n";

        var result = GitOperationService.ParseNameStatusToFileDiffs(nameStatus, "sha456");

        Assert.Single(result);
        Assert.Equal("src/OldFile.cs", result[0].FilePath);
        Assert.Equal(ChangeType.Deleted, result[0].ChangeType);
        Assert.Equal("sha456", result[0].CommitSha);
    }

    [Fact]
    public void ParseNameStatusToFileDiffs_修改檔案_應正確解析ChangeType為Modified()
    {
        var nameStatus = "M\tsrc/Existing.cs\n";

        var result = GitOperationService.ParseNameStatusToFileDiffs(nameStatus, "sha789");

        Assert.Single(result);
        Assert.Equal("src/Existing.cs", result[0].FilePath);
        Assert.Equal(ChangeType.Modified, result[0].ChangeType);
    }

    [Fact]
    public void ParseNameStatusToFileDiffs_多個檔案_應分別解析每個檔案()
    {
        var nameStatus = "A\tsrc/New.cs\nM\tsrc/Changed.cs\nD\tsrc/Removed.cs\n";

        var result = GitOperationService.ParseNameStatusToFileDiffs(nameStatus, "sha000");

        Assert.Equal(3, result.Count);
        Assert.Contains(result, f => f.FilePath == "src/New.cs" && f.ChangeType == ChangeType.Added);
        Assert.Contains(result, f => f.FilePath == "src/Changed.cs" && f.ChangeType == ChangeType.Modified);
        Assert.Contains(result, f => f.FilePath == "src/Removed.cs" && f.ChangeType == ChangeType.Deleted);
    }

    [Fact]
    public void ParseShortStat_標準輸出應正確解析新增與刪除行數()
    {
        var shortStat = " 5 files changed, 120 insertions(+), 45 deletions(-)";

        var (linesAdded, linesRemoved) = GitOperationService.ParseShortStat(shortStat);

        Assert.Equal(120, linesAdded);
        Assert.Equal(45, linesRemoved);
    }

    [Fact]
    public void ParseShortStat_只有新增無刪除時應正確解析()
    {
        var shortStat = " 2 files changed, 30 insertions(+)";

        var (linesAdded, linesRemoved) = GitOperationService.ParseShortStat(shortStat);

        Assert.Equal(30, linesAdded);
        Assert.Equal(0, linesRemoved);
    }

    [Fact]
    public void ParseShortStat_空字串應回傳零值()
    {
        var (linesAdded, linesRemoved) = GitOperationService.ParseShortStat("");

        Assert.Equal(0, linesAdded);
        Assert.Equal(0, linesRemoved);
    }
}
