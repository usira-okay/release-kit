namespace ReleaseKit.Domain.Tests.Entities;

using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

/// <summary>
/// PrDiffContext 實體單元測試
/// </summary>
public class PrDiffContextTests
{
    [Fact]
    public void PrDiffContext_ShouldBeCreatedWithRequiredProperties()
    {
        var context = new PrDiffContext
        {
            Title = "修改 API endpoint",
            SourceBranch = "feature/VSTS12345-api-change",
            TargetBranch = "develop",
            AuthorName = "developer1",
            PrUrl = "https://gitlab.example.com/project/-/merge_requests/1",
            DiffContent = "diff --git a/file.cs b/file.cs\n...",
            ChangedFiles = new List<string> { "Controllers/OrderController.cs" },
            Platform = SourceControlPlatform.GitLab
        };

        Assert.Equal("修改 API endpoint", context.Title);
        Assert.Null(context.Description);
        Assert.Equal(SourceControlPlatform.GitLab, context.Platform);
    }

    [Fact]
    public void PrDiffContext_WithDescription_ShouldSetCorrectly()
    {
        var context = new PrDiffContext
        {
            Title = "修改 DB Schema",
            Description = "新增 Status 欄位至 Orders table",
            SourceBranch = "feature/db-change",
            TargetBranch = "main",
            AuthorName = "developer2",
            PrUrl = "https://bitbucket.org/team/repo/pull-requests/1",
            DiffContent = "ALTER TABLE Orders ADD Status INT",
            ChangedFiles = new List<string> { "Migrations/001_AddStatus.sql" },
            Platform = SourceControlPlatform.Bitbucket
        };

        Assert.Equal("新增 Status 欄位至 Orders table", context.Description);
    }
}
