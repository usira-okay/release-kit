using ReleaseKit.Domain.Entities;
using DomainChangeType = ReleaseKit.Domain.ValueObjects.ChangeType;

namespace ReleaseKit.Domain.Tests.Entities;

/// <summary>
/// FileDiff、CommitSummary 與 ProjectDiffResult 實體單元測試
/// </summary>
public class FileDiffTests
{
    [Fact]
    public void FileDiff_應正確建立()
    {
        var diff = new FileDiff
        {
            FilePath = "Controllers/UserController.cs",
            ChangeType = DomainChangeType.Modified,
            CommitSha = "abc123"
        };

        Assert.Equal("Controllers/UserController.cs", diff.FilePath);
        Assert.Equal(DomainChangeType.Modified, diff.ChangeType);
        Assert.Equal("abc123", diff.CommitSha);
    }

    [Fact]
    public void CommitSummary_應正確建立()
    {
        var fileDiff = new FileDiff
        {
            FilePath = "src/Service.cs",
            ChangeType = DomainChangeType.Modified,
            CommitSha = "def456abc"
        };

        var summary = new CommitSummary
        {
            CommitSha = "def456abc",
            ChangedFiles = new List<FileDiff> { fileDiff },
            TotalFilesChanged = 1,
            TotalLinesAdded = 20,
            TotalLinesRemoved = 10
        };

        Assert.Equal("def456abc", summary.CommitSha);
        Assert.Single(summary.ChangedFiles);
        Assert.Equal("src/Service.cs", summary.ChangedFiles[0].FilePath);
        Assert.Equal(1, summary.TotalFilesChanged);
        Assert.Equal(20, summary.TotalLinesAdded);
        Assert.Equal(10, summary.TotalLinesRemoved);
    }

    [Fact]
    public void CommitSummary_多個異動檔案_應正確建立()
    {
        var summary = new CommitSummary
        {
            CommitSha = "aabbcc",
            ChangedFiles = new List<FileDiff>
            {
                new() { FilePath = "src/A.cs", ChangeType = DomainChangeType.Added, CommitSha = "aabbcc" },
                new() { FilePath = "src/B.cs", ChangeType = DomainChangeType.Deleted, CommitSha = "aabbcc" }
            },
            TotalFilesChanged = 2,
            TotalLinesAdded = 50,
            TotalLinesRemoved = 30
        };

        Assert.Equal(2, summary.ChangedFiles.Count);
        Assert.Equal(2, summary.TotalFilesChanged);
        Assert.Equal(50, summary.TotalLinesAdded);
        Assert.Equal(30, summary.TotalLinesRemoved);
    }

    [Fact]
    public void ProjectDiffResult_應正確建立()
    {
        var commitSummary = new CommitSummary
        {
            CommitSha = "def456",
            ChangedFiles = new List<FileDiff>
            {
                new() { FilePath = "test.cs", ChangeType = DomainChangeType.Added, CommitSha = "def456" }
            },
            TotalFilesChanged = 1,
            TotalLinesAdded = 5,
            TotalLinesRemoved = 0
        };

        var result = new ProjectDiffResult
        {
            ProjectPath = "mygroup/backend-api",
            CommitSummaries = new List<CommitSummary> { commitSummary }
        };

        Assert.Equal("mygroup/backend-api", result.ProjectPath);
        Assert.Single(result.CommitSummaries);
        Assert.Equal("def456", result.CommitSummaries[0].CommitSha);
    }

    [Fact]
    public void ProjectDiffResult_無CommitSummaries_應回傳空清單()
    {
        var result = new ProjectDiffResult
        {
            ProjectPath = "mygroup/empty-project",
            CommitSummaries = new List<CommitSummary>()
        };

        Assert.Equal("mygroup/empty-project", result.ProjectPath);
        Assert.Empty(result.CommitSummaries);
    }
}
