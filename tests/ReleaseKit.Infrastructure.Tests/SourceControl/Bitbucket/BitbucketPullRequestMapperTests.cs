using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Infrastructure.SourceControl.Bitbucket;
using ReleaseKit.Infrastructure.SourceControl.Bitbucket.Models;

namespace ReleaseKit.Infrastructure.Tests.SourceControl.Bitbucket;

/// <summary>
/// BitbucketPullRequestMapper 單元測試
/// </summary>
public class BitbucketPullRequestMapperTests
{
    [Fact]
    public void ToDomain_WithValidResponse_ShouldMapCorrectly()
    {
        // Arrange
        var response = new BitbucketPullRequestResponse
        {
            Id = 42,
            Title = "Feature: Add new authentication",
            Summary = new BitbucketSummaryResponse
            {
                Raw = "This PR adds OAuth2 authentication support"
            },
            Source = new BitbucketBranchRefResponse
            {
                Branch = new BitbucketBranchResponse
                {
                    Name = "feature/oauth2"
                }
            },
            Destination = new BitbucketBranchRefResponse
            {
                Branch = new BitbucketBranchResponse
                {
                    Name = "main"
                }
            },
            CreatedOn = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero),
            ClosedOn = new DateTimeOffset(2024, 1, 20, 14, 45, 0, TimeSpan.Zero),
            State = "MERGED",
            Author = new BitbucketAuthorResponse
            {
                Uuid = "{123e4567-e89b-12d3-a456-426614174000}",
                DisplayName = "John Doe"
            },
            Links = new BitbucketLinksResponse
            {
                Html = new BitbucketLinkResponse
                {
                    Href = "https://bitbucket.org/myteam/myrepo/pull-requests/42"
                }
            }
        };

        var projectPath = "myteam/myrepo";

        // Act
        var result = BitbucketPullRequestMapper.ToDomain(response, projectPath);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Feature: Add new authentication", result.Title);
        Assert.Equal("This PR adds OAuth2 authentication support", result.Description);
        Assert.Equal("feature/oauth2", result.SourceBranch);
        Assert.Equal("main", result.TargetBranch);
        Assert.Equal(new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero), result.CreatedAt);
        Assert.Equal(new DateTimeOffset(2024, 1, 20, 14, 45, 0, TimeSpan.Zero), result.MergedAt);
        Assert.Equal("MERGED", result.State);
        Assert.Equal("{123e4567-e89b-12d3-a456-426614174000}", result.AuthorUserId);
        Assert.Equal("John Doe", result.AuthorName);
        Assert.Equal("https://bitbucket.org/myteam/myrepo/pull-requests/42", result.PRUrl);
        Assert.Equal(SourceControlPlatform.Bitbucket, result.Platform);
        Assert.Equal("myteam/myrepo", result.ProjectPath);
        Assert.Equal(42, result.PullRequestId);
    }

    [Fact]
    public void ToDomain_WithNullSummary_ShouldMapWithNullDescription()
    {
        // Arrange
        var response = new BitbucketPullRequestResponse
        {
            Title = "Quick fix",
            Summary = null,
            Source = new BitbucketBranchRefResponse
            {
                Branch = new BitbucketBranchResponse { Name = "hotfix/bug-123" }
            },
            Destination = new BitbucketBranchRefResponse
            {
                Branch = new BitbucketBranchResponse { Name = "main" }
            },
            CreatedOn = DateTimeOffset.UtcNow,
            ClosedOn = DateTimeOffset.UtcNow,
            State = "MERGED",
            Author = new BitbucketAuthorResponse
            {
                Uuid = "{uuid}",
                DisplayName = "Jane Doe"
            },
            Links = new BitbucketLinksResponse
            {
                Html = new BitbucketLinkResponse { Href = "https://example.com/pr/1" }
            }
        };

        var projectPath = "team/project";

        // Act
        var result = BitbucketPullRequestMapper.ToDomain(response, projectPath);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.Description);
    }

    [Fact]
    public void ToDomain_WithNullClosedOn_ShouldMapToNull()
    {
        // Arrange
        var response = new BitbucketPullRequestResponse
        {
            Title = "Work in progress",
            Summary = new BitbucketSummaryResponse { Raw = "WIP" },
            Source = new BitbucketBranchRefResponse
            {
                Branch = new BitbucketBranchResponse { Name = "feature/wip" }
            },
            Destination = new BitbucketBranchRefResponse
            {
                Branch = new BitbucketBranchResponse { Name = "develop" }
            },
            CreatedOn = DateTimeOffset.UtcNow,
            ClosedOn = null,  // Not yet closed
            State = "OPEN",
            Author = new BitbucketAuthorResponse
            {
                Uuid = "{uuid}",
                DisplayName = "Developer"
            },
            Links = new BitbucketLinksResponse
            {
                Html = new BitbucketLinkResponse { Href = "https://example.com/pr/99" }
            }
        };

        var projectPath = "org/repo";

        // Act
        var result = BitbucketPullRequestMapper.ToDomain(response, projectPath);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.MergedAt);
    }

    [Fact]
    public void ToDomain_WithComplexBranchNames_ShouldMapCorrectly()
    {
        // Arrange
        var response = new BitbucketPullRequestResponse
        {
            Title = "Release v2.3.0",
            Summary = new BitbucketSummaryResponse { Raw = "Release notes" },
            Source = new BitbucketBranchRefResponse
            {
                Branch = new BitbucketBranchResponse { Name = "release/v2.3.0" }
            },
            Destination = new BitbucketBranchRefResponse
            {
                Branch = new BitbucketBranchResponse { Name = "production" }
            },
            CreatedOn = new DateTimeOffset(2024, 2, 1, 9, 0, 0, TimeSpan.Zero),
            ClosedOn = new DateTimeOffset(2024, 2, 1, 17, 30, 0, TimeSpan.Zero),
            State = "MERGED",
            Author = new BitbucketAuthorResponse
            {
                Uuid = "{release-bot-uuid}",
                DisplayName = "Release Bot"
            },
            Links = new BitbucketLinksResponse
            {
                Html = new BitbucketLinkResponse 
                { 
                    Href = "https://bitbucket.org/company/product/pull-requests/100" 
                }
            }
        };

        var projectPath = "company/product";

        // Act
        var result = BitbucketPullRequestMapper.ToDomain(response, projectPath);

        // Assert
        Assert.Equal("release/v2.3.0", result.SourceBranch);
        Assert.Equal("production", result.TargetBranch);
    }
}
