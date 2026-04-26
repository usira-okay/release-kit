using ReleaseKit.Domain.Entities;
using DomainChangeType = ReleaseKit.Domain.ValueObjects.ChangeType;

namespace ReleaseKit.Domain.Tests.Entities;

/// <summary>
/// FileDiff 與 ProjectDiffResult 實體單元測試
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
            DiffContent = "- old\n+ new",
            CommitSha = "abc123"
        };

        Assert.Equal("Controllers/UserController.cs", diff.FilePath);
        Assert.Equal(DomainChangeType.Modified, diff.ChangeType);
        Assert.Equal("- old\n+ new", diff.DiffContent);
        Assert.Equal("abc123", diff.CommitSha);
    }

    [Fact]
    public void ProjectDiffResult_應正確建立()
    {
        var result = new ProjectDiffResult
        {
            ProjectPath = "mygroup/backend-api",
            FileDiffs = new List<FileDiff>
            {
                new()
                {
                    FilePath = "test.cs",
                    ChangeType = DomainChangeType.Added,
                    DiffContent = "+ new file",
                    CommitSha = "def456"
                }
            }
        };

        Assert.Equal("mygroup/backend-api", result.ProjectPath);
        Assert.Single(result.FileDiffs);
    }
}
