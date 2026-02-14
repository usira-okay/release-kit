using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Infrastructure.SourceControl.GitLab;
using ReleaseKit.Infrastructure.SourceControl.GitLab.Models;
using Xunit;

namespace ReleaseKit.Infrastructure.Tests.SourceControl.GitLab;

/// <summary>
/// GitLabMergeRequestMapper 單元測試
/// </summary>
public class GitLabMergeRequestMapperTests
{
    [Fact]
    public void ToDomain_WithValidResponse_ShouldMapCorrectly()
    {
        // Arrange
        var response = new GitLabMergeRequestResponse
        {
            Id = 1,
            Iid = 42,
            Title = "feat: 新增使用者驗證功能",
            Description = "實作 JWT 驗證機制",
            SourceBranch = "feature/user-auth",
            TargetBranch = "main",
            State = "merged",
            CreatedAt = DateTimeOffset.Parse("2024-03-10T09:30:00Z"),
            MergedAt = DateTimeOffset.Parse("2024-03-12T14:22:00Z"),
            WebUrl = "https://gitlab.example.com/mygroup/backend-api/-/merge_requests/42",
            Author = new GitLabAuthorResponse
            {
                Id = 12345,
                Username = "john.doe"
            }
        };

        // Act
        var domain = GitLabMergeRequestMapper.ToDomain(response, "mygroup/backend-api");

        // Assert
        Assert.Equal("feat: 新增使用者驗證功能", domain.Title);
        Assert.Equal("實作 JWT 驗證機制", domain.Description);
        Assert.Equal("feature/user-auth", domain.SourceBranch);
        Assert.Equal("main", domain.TargetBranch);
        Assert.Equal("merged", domain.State);
        Assert.Equal(DateTimeOffset.Parse("2024-03-10T09:30:00Z"), domain.CreatedAt);
        Assert.Equal(DateTimeOffset.Parse("2024-03-12T14:22:00Z"), domain.MergedAt);
        Assert.Equal("12345", domain.AuthorUserId);
        Assert.Equal("john.doe", domain.AuthorName);
        Assert.Equal("https://gitlab.example.com/mygroup/backend-api/-/merge_requests/42", domain.PRUrl);
        Assert.Equal(SourceControlPlatform.GitLab, domain.Platform);
        Assert.Equal("mygroup/backend-api", domain.ProjectPath);
    }

    [Fact]
    public void ToDomain_WithNullDescription_ShouldMapToNull()
    {
        // Arrange
        var response = new GitLabMergeRequestResponse
        {
            Id = 1,
            Title = "test",
            Description = null,
            SourceBranch = "feature",
            TargetBranch = "main",
            State = "merged",
            CreatedAt = DateTimeOffset.UtcNow,
            MergedAt = DateTimeOffset.UtcNow,
            WebUrl = "https://example.com",
            Author = new GitLabAuthorResponse { Id = 1, Username = "test" }
        };

        // Act
        var domain = GitLabMergeRequestMapper.ToDomain(response, "test/project");

        // Assert
        Assert.Null(domain.Description);
    }

    [Fact]
    public void ToDomain_WithNullMergedAt_ShouldMapToNull()
    {
        // Arrange
        var response = new GitLabMergeRequestResponse
        {
            Id = 1,
            Title = "test",
            SourceBranch = "feature",
            TargetBranch = "main",
            State = "opened",
            CreatedAt = DateTimeOffset.UtcNow,
            MergedAt = null,
            WebUrl = "https://example.com",
            Author = new GitLabAuthorResponse { Id = 1, Username = "test" }
        };

        // Act
        var domain = GitLabMergeRequestMapper.ToDomain(response, "test/project");

        // Assert
        Assert.Null(domain.MergedAt);
    }

    [Fact]
    public void ToDomain_ShouldConvertAuthorIdToString()
    {
        // Arrange
        var response = new GitLabMergeRequestResponse
        {
            Id = 1,
            Title = "test",
            SourceBranch = "feature",
            TargetBranch = "main",
            State = "merged",
            CreatedAt = DateTimeOffset.UtcNow,
            MergedAt = DateTimeOffset.UtcNow,
            WebUrl = "https://example.com",
            Author = new GitLabAuthorResponse { Id = 99999, Username = "test" }
        };

        // Act
        var domain = GitLabMergeRequestMapper.ToDomain(response, "test/project");

        // Assert
        Assert.Equal("99999", domain.AuthorUserId);
        Assert.IsType<string>(domain.AuthorUserId);
    }

    [Fact]
    public void ToDomain_ShouldMapIidToPullRequestId()
    {
        // Arrange
        var response = new GitLabMergeRequestResponse
        {
            Id = 1000,
            Iid = 42,
            Title = "test",
            SourceBranch = "feature",
            TargetBranch = "main",
            State = "merged",
            CreatedAt = DateTimeOffset.UtcNow,
            MergedAt = DateTimeOffset.UtcNow,
            WebUrl = "https://example.com",
            Author = new GitLabAuthorResponse { Id = 1, Username = "test" }
        };

        // Act
        var domain = GitLabMergeRequestMapper.ToDomain(response, "test/project");

        // Assert
        Assert.Equal(42, domain.PullRequestId);
    }
}
