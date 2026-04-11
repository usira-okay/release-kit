using Xunit;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Tests.ValueObjects;

/// <summary>
/// ProjectAnalysisContext 值物件測試
/// </summary>
public class ProjectAnalysisContextTests
{
    [Fact]
    public void 建構_應正確設定所有屬性()
    {
        // Arrange & Act
        var context = new ProjectAnalysisContext
        {
            ProjectName = "my-service",
            RepoPath = "/repos/my-service",
            CommitShas = new List<string> { "abc123", "def456" }
        };

        // Assert
        Assert.Equal("my-service", context.ProjectName);
        Assert.Equal("/repos/my-service", context.RepoPath);
        Assert.Equal(2, context.CommitShas.Count);
        Assert.Equal(new[] { "abc123", "def456" }, context.CommitShas);
    }

    [Fact]
    public void 兩個相同值的Context_應視為相等()
    {
        // Arrange
        var shas = new List<string> { "abc123" };
        var context1 = new ProjectAnalysisContext
        {
            ProjectName = "svc",
            RepoPath = "/repos/svc",
            CommitShas = shas
        };
        var context2 = new ProjectAnalysisContext
        {
            ProjectName = "svc",
            RepoPath = "/repos/svc",
            CommitShas = shas
        };

        // Assert
        Assert.Equal(context1, context2);
    }
}
