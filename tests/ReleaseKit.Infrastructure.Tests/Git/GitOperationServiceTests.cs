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
    public async Task GetCommitDiffAsync_無效路徑應回傳失敗()
    {
        var result = await _service.GetCommitDiffAsync("/nonexistent/path", "abc123");

        Assert.True(result.IsFailure);
        Assert.Contains("Diff", result.Error!.Code);
    }

    [Fact]
    public void ParseDiffOutput_空輸出應回傳空清單()
    {
        var result = GitOperationService.ParseDiffOutput("", "", "abc123");

        Assert.Empty(result);
    }

    [Fact]
    public void ParseDiffOutput_新增檔案_應正確解析ChangeType為Added()
    {
        var nameStatus = "A\tsrc/NewFile.cs\n";
        var diffOutput = "diff --git a/src/NewFile.cs b/src/NewFile.cs\n" +
                         "new file mode 100644\n" +
                         "--- /dev/null\n" +
                         "+++ b/src/NewFile.cs\n" +
                         "@@ -0,0 +1,3 @@\n" +
                         "+line1\n" +
                         "+line2\n" +
                         "+line3\n";

        var result = GitOperationService.ParseDiffOutput(nameStatus, diffOutput, "abc123");

        Assert.Single(result);
        Assert.Equal("src/NewFile.cs", result[0].FilePath);
        Assert.Equal(ChangeType.Added, result[0].ChangeType);
        Assert.Equal("abc123", result[0].CommitSha);
        Assert.Contains("diff --git", result[0].DiffContent);
    }

    [Fact]
    public void ParseDiffOutput_刪除檔案_應正確解析ChangeType為Deleted()
    {
        var nameStatus = "D\tsrc/OldFile.cs\n";
        var diffOutput = "diff --git a/src/OldFile.cs b/src/OldFile.cs\n" +
                         "deleted file mode 100644\n" +
                         "--- a/src/OldFile.cs\n" +
                         "+++ /dev/null\n" +
                         "@@ -1,2 +0,0 @@\n" +
                         "-line1\n" +
                         "-line2\n";

        var result = GitOperationService.ParseDiffOutput(nameStatus, diffOutput, "sha456");

        Assert.Single(result);
        Assert.Equal("src/OldFile.cs", result[0].FilePath);
        Assert.Equal(ChangeType.Deleted, result[0].ChangeType);
        Assert.Equal("sha456", result[0].CommitSha);
    }

    [Fact]
    public void ParseDiffOutput_修改檔案_應正確解析ChangeType為Modified()
    {
        var nameStatus = "M\tsrc/Existing.cs\n";
        var diffOutput = "diff --git a/src/Existing.cs b/src/Existing.cs\n" +
                         "--- a/src/Existing.cs\n" +
                         "+++ b/src/Existing.cs\n" +
                         "@@ -1,3 +1,3 @@\n" +
                         " context\n" +
                         "-old line\n" +
                         "+new line\n" +
                         " context\n";

        var result = GitOperationService.ParseDiffOutput(nameStatus, diffOutput, "sha789");

        Assert.Single(result);
        Assert.Equal("src/Existing.cs", result[0].FilePath);
        Assert.Equal(ChangeType.Modified, result[0].ChangeType);
    }

    [Fact]
    public void ParseDiffOutput_多個檔案_應分別解析每個檔案的Diff()
    {
        var nameStatus = "A\tsrc/New.cs\nM\tsrc/Changed.cs\nD\tsrc/Removed.cs\n";
        var diffOutput =
            "diff --git a/src/New.cs b/src/New.cs\n" +
            "new file mode 100644\n" +
            "+++ b/src/New.cs\n" +
            "@@ -0,0 +1 @@\n" +
            "+new\n" +
            "diff --git a/src/Changed.cs b/src/Changed.cs\n" +
            "--- a/src/Changed.cs\n" +
            "+++ b/src/Changed.cs\n" +
            "@@ -1 +1 @@\n" +
            "-old\n" +
            "+new\n" +
            "diff --git a/src/Removed.cs b/src/Removed.cs\n" +
            "deleted file mode 100644\n" +
            "--- a/src/Removed.cs\n" +
            "+++ /dev/null\n";

        var result = GitOperationService.ParseDiffOutput(nameStatus, diffOutput, "sha000");

        Assert.Equal(3, result.Count);
        Assert.Contains(result, f => f.FilePath == "src/New.cs" && f.ChangeType == ChangeType.Added);
        Assert.Contains(result, f => f.FilePath == "src/Changed.cs" && f.ChangeType == ChangeType.Modified);
        Assert.Contains(result, f => f.FilePath == "src/Removed.cs" && f.ChangeType == ChangeType.Deleted);
    }

    [Fact]
    public void ParseDiffOutput_檔案在nameStatus但無對應diff_DiffContent應為空字串()
    {
        var nameStatus = "M\tsrc/NoDiff.cs\n";
        var diffOutput = "";

        var result = GitOperationService.ParseDiffOutput(nameStatus, diffOutput, "sha001");

        Assert.Single(result);
        Assert.Equal("src/NoDiff.cs", result[0].FilePath);
        Assert.Equal(string.Empty, result[0].DiffContent);
    }
}
