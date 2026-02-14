using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;
using Xunit;

namespace ReleaseKit.Domain.Tests.Entities;

/// <summary>
/// MergeRequest 實體單元測試
/// </summary>
public class MergeRequestTests
{
    [Fact]
    public void Constructor_WithValidData_ShouldCreateMergeRequest()
    {
        // Arrange
        var createdAt = DateTimeOffset.UtcNow.AddDays(-5);
        var mergedAt = DateTimeOffset.UtcNow;

        // Act
        var mergeRequest = new MergeRequest
        {
            PullRequestId = 1,
            Title = "feat: 新增使用者驗證功能",
            Description = "實作 JWT 驗證機制",
            SourceBranch = "feature/user-auth",
            TargetBranch = "main",
            CreatedAt = createdAt,
            MergedAt = mergedAt,
            State = "merged",
            AuthorUserId = "12345",
            AuthorName = "john.doe",
            PRUrl = "https://gitlab.example.com/mygroup/backend-api/-/merge_requests/42",
            Platform = SourceControlPlatform.GitLab,
            ProjectPath = "mygroup/backend-api"
        };

        // Assert
        Assert.Equal("feat: 新增使用者驗證功能", mergeRequest.Title);
        Assert.Equal("實作 JWT 驗證機制", mergeRequest.Description);
        Assert.Equal("feature/user-auth", mergeRequest.SourceBranch);
        Assert.Equal("main", mergeRequest.TargetBranch);
        Assert.Equal(createdAt, mergeRequest.CreatedAt);
        Assert.Equal(mergedAt, mergeRequest.MergedAt);
        Assert.Equal("merged", mergeRequest.State);
        Assert.Equal("12345", mergeRequest.AuthorUserId);
        Assert.Equal("john.doe", mergeRequest.AuthorName);
        Assert.Equal("https://gitlab.example.com/mygroup/backend-api/-/merge_requests/42", mergeRequest.PRUrl);
        Assert.Equal(SourceControlPlatform.GitLab, mergeRequest.Platform);
        Assert.Equal("mygroup/backend-api", mergeRequest.ProjectPath);
    }

    [Fact]
    public void Constructor_WithNullDescription_ShouldAllowNull()
    {
        // Arrange & Act
        var mergeRequest = new MergeRequest
        {
            PullRequestId = 1,
            Title = "feat: 測試",
            Description = null,
            SourceBranch = "feature/test",
            TargetBranch = "main",
            CreatedAt = DateTimeOffset.UtcNow,
            MergedAt = DateTimeOffset.UtcNow,
            State = "merged",
            AuthorUserId = "123",
            AuthorName = "test",
            PRUrl = "https://example.com/pr/1",
            Platform = SourceControlPlatform.GitLab,
            ProjectPath = "test/project"
        };

        // Assert
        Assert.Null(mergeRequest.Description);
    }

    [Fact]
    public void Platform_ShouldSupportGitLab()
    {
        // Arrange & Act
        var mergeRequest = new MergeRequest
        {
            PullRequestId = 1,
            Title = "test",
            SourceBranch = "feature",
            TargetBranch = "main",
            CreatedAt = DateTimeOffset.UtcNow,
            MergedAt = DateTimeOffset.UtcNow,
            State = "merged",
            AuthorUserId = "123",
            AuthorName = "test",
            PRUrl = "https://example.com",
            Platform = SourceControlPlatform.GitLab,
            ProjectPath = "test/project"
        };

        // Assert
        Assert.Equal(SourceControlPlatform.GitLab, mergeRequest.Platform);
    }

    [Fact]
    public void Platform_ShouldSupportBitbucket()
    {
        // Arrange & Act
        var mergeRequest = new MergeRequest
        {
            PullRequestId = 1,
            Title = "test",
            SourceBranch = "feature",
            TargetBranch = "main",
            CreatedAt = DateTimeOffset.UtcNow,
            MergedAt = DateTimeOffset.UtcNow,
            State = "merged",
            AuthorUserId = "123",
            AuthorName = "test",
            PRUrl = "https://example.com",
            Platform = SourceControlPlatform.Bitbucket,
            ProjectPath = "test/project"
        };

        // Assert
        Assert.Equal(SourceControlPlatform.Bitbucket, mergeRequest.Platform);
    }
}
