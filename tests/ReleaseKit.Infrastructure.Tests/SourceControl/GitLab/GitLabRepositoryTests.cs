using System.Net;
using Moq;
using Moq.Protected;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Infrastructure.SourceControl.GitLab;
using ReleaseKit.Infrastructure.SourceControl.GitLab.Models;
using Xunit;

namespace ReleaseKit.Infrastructure.Tests.SourceControl.GitLab;

/// <summary>
/// GitLabRepository 單元測試
/// </summary>
public class GitLabRepositoryTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;

    public GitLabRepositoryTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
    }

    [Fact]
    public async Task GetMergeRequestsByDateRangeAsync_WithValidParameters_ShouldReturnMergeRequests()
    {
        // Arrange
        var gitLabResponses = new List<GitLabMergeRequestResponse>
        {
            new()
            {
                Id = 1,
                Iid = 42,
                Title = "feat: Add feature",
                Description = "Description",
                SourceBranch = "feature/test",
                TargetBranch = "main",
                State = "merged",
                CreatedAt = DateTimeOffset.Parse("2024-03-10T09:00:00Z"),
                MergedAt = DateTimeOffset.Parse("2024-03-12T14:00:00Z"),
                WebUrl = "https://gitlab.example.com/project/-/merge_requests/42",
                Author = new GitLabAuthorResponse { Id = 123, Username = "test.user" }
            }
        };

        SetupHttpResponse(gitLabResponses);

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://gitlab.example.com/api/v4/")
        };
        _httpClientFactoryMock.Setup(x => x.CreateClient("GitLab")).Returns(httpClient);

        var repository = new GitLabRepository(_httpClientFactoryMock.Object);

        // Act
        var result = await repository.GetMergeRequestsByDateRangeAsync(
            "mygroup/myproject",
            "main",
            DateTimeOffset.Parse("2024-03-01T00:00:00Z"),
            DateTimeOffset.Parse("2024-03-31T23:59:59Z"));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Single(result.Value);
        Assert.Equal("feat: Add feature", result.Value[0].Title);
        Assert.Equal(SourceControlPlatform.GitLab, result.Value[0].Platform);
    }

    [Fact]
    public async Task GetMergeRequestsByDateRangeAsync_WithNoResults_ShouldReturnEmptyList()
    {
        // Arrange
        var emptyResponses = new List<GitLabMergeRequestResponse>();
        SetupHttpResponse(emptyResponses);

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://gitlab.example.com/api/v4/")
        };
        _httpClientFactoryMock.Setup(x => x.CreateClient("GitLab")).Returns(httpClient);

        var repository = new GitLabRepository(_httpClientFactoryMock.Object);

        // Act
        var result = await repository.GetMergeRequestsByDateRangeAsync(
            "mygroup/myproject",
            "main",
            DateTimeOffset.Parse("2024-03-01T00:00:00Z"),
            DateTimeOffset.Parse("2024-03-31T23:59:59Z"));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task GetMergeRequestsByDateRangeAsync_WithApiError_ShouldReturnFailure()
    {
        // Arrange
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Unauthorized,
                Content = new StringContent("{\"message\":\"Unauthorized\"}")
            });

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://gitlab.example.com/api/v4/")
        };
        _httpClientFactoryMock.Setup(x => x.CreateClient("GitLab")).Returns(httpClient);

        var repository = new GitLabRepository(_httpClientFactoryMock.Object);

        // Act
        var result = await repository.GetMergeRequestsByDateRangeAsync(
            "mygroup/myproject",
            "main",
            DateTimeOffset.Parse("2024-03-01T00:00:00Z"),
            DateTimeOffset.Parse("2024-03-31T23:59:59Z"));

        // Assert
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task GetMergeRequestsByDateRangeAsync_ShouldFilterByMergedAt()
    {
        // Arrange
        var gitLabResponses = new List<GitLabMergeRequestResponse>
        {
            new()
            {
                Id = 1,
                Title = "In Range",
                SourceBranch = "feature1",
                TargetBranch = "main",
                State = "merged",
                CreatedAt = DateTimeOffset.Parse("2024-03-10T09:00:00Z"),
                MergedAt = DateTimeOffset.Parse("2024-03-12T14:00:00Z"),
                WebUrl = "https://gitlab.example.com/1",
                Author = new GitLabAuthorResponse { Id = 1, Username = "user1" }
            },
            new()
            {
                Id = 2,
                Title = "Out of Range",
                SourceBranch = "feature2",
                TargetBranch = "main",
                State = "merged",
                CreatedAt = DateTimeOffset.Parse("2024-02-10T09:00:00Z"),
                MergedAt = DateTimeOffset.Parse("2024-02-28T14:00:00Z"), // Outside range
                WebUrl = "https://gitlab.example.com/2",
                Author = new GitLabAuthorResponse { Id = 2, Username = "user2" }
            }
        };

        SetupHttpResponse(gitLabResponses);

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://gitlab.example.com/api/v4/")
        };
        _httpClientFactoryMock.Setup(x => x.CreateClient("GitLab")).Returns(httpClient);

        var repository = new GitLabRepository(_httpClientFactoryMock.Object);

        // Act
        var result = await repository.GetMergeRequestsByDateRangeAsync(
            "mygroup/myproject",
            "main",
            DateTimeOffset.Parse("2024-03-01T00:00:00Z"),
            DateTimeOffset.Parse("2024-03-31T23:59:59Z"));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Single(result.Value);
        Assert.Equal("In Range", result.Value[0].Title);
    }

    // T039 [US3] Unit test for GetBranchesAsync
    [Fact]
    public async Task GetBranchesAsync_WithValidParameters_ShouldReturnBranches()
    {
        // Arrange
        var gitLabBranches = new List<GitLabBranchResponse>
        {
            new()
            {
                Name = "main",
                Default = true,
                Protected = true,
                Commit = new GitLabCommitResponse
                {
                    Id = "abc123",
                    ShortId = "abc123",
                    Title = "Initial commit",
                    AuthorName = "Test User",
                    CreatedAt = DateTimeOffset.Parse("2024-01-01T00:00:00Z")
                }
            },
            new()
            {
                Name = "release/1.0",
                Default = false,
                Protected = true,
                Commit = new GitLabCommitResponse
                {
                    Id = "def456",
                    ShortId = "def456",
                    Title = "Release 1.0",
                    AuthorName = "Test User",
                    CreatedAt = DateTimeOffset.Parse("2024-02-01T00:00:00Z")
                }
            },
            new()
            {
                Name = "release/2.0",
                Default = false,
                Protected = true,
                Commit = new GitLabCommitResponse
                {
                    Id = "ghi789",
                    ShortId = "ghi789",
                    Title = "Release 2.0",
                    AuthorName = "Test User",
                    CreatedAt = DateTimeOffset.Parse("2024-03-01T00:00:00Z")
                }
            }
        };

        SetupHttpResponse(gitLabBranches);

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://gitlab.example.com/api/v4/")
        };
        _httpClientFactoryMock.Setup(x => x.CreateClient("GitLab")).Returns(httpClient);

        var repository = new GitLabRepository(_httpClientFactoryMock.Object);

        // Act
        var result = await repository.GetBranchesAsync("mygroup/myproject", "release/");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.Count);
        Assert.Contains("release/1.0", result.Value);
        Assert.Contains("release/2.0", result.Value);
        Assert.DoesNotContain("main", result.Value);
    }

    [Fact]
    public async Task GetBranchesAsync_WithoutPattern_ShouldReturnAllBranches()
    {
        // Arrange
        var gitLabBranches = new List<GitLabBranchResponse>
        {
            new() { Name = "main", Default = true, Protected = true },
            new() { Name = "develop", Default = false, Protected = false },
            new() { Name = "release/1.0", Default = false, Protected = true }
        };

        SetupHttpResponse(gitLabBranches);

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://gitlab.example.com/api/v4/")
        };
        _httpClientFactoryMock.Setup(x => x.CreateClient("GitLab")).Returns(httpClient);

        var repository = new GitLabRepository(_httpClientFactoryMock.Object);

        // Act
        var result = await repository.GetBranchesAsync("mygroup/myproject");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(3, result.Value.Count);
    }

    // T040 [US3] Unit test for GetMergeRequestsByBranchDiffAsync
    [Fact]
    public async Task GetMergeRequestsByBranchDiffAsync_WithValidParameters_ShouldReturnMergeRequests()
    {
        // Arrange
        var compareResponse = new GitLabCompareResponse
        {
            Commits = new List<GitLabCommitResponse>
            {
                new() { Id = "commit1", ShortId = "commit1", Title = "Commit 1", AuthorName = "User1", CreatedAt = DateTimeOffset.UtcNow },
                new() { Id = "commit2", ShortId = "commit2", Title = "Commit 2", AuthorName = "User2", CreatedAt = DateTimeOffset.UtcNow }
            },
            CompareTimeout = false,
            CompareSameRef = false
        };

        var mr1 = new List<GitLabMergeRequestResponse>
        {
            new()
            {
                Id = 1,
                Iid = 100,
                Title = "MR 1",
                Description = "Description 1",
                SourceBranch = "feature/a",
                TargetBranch = "main",
                State = "merged",
                CreatedAt = DateTimeOffset.Parse("2024-01-10T09:00:00Z"),
                MergedAt = DateTimeOffset.Parse("2024-01-12T14:00:00Z"),
                WebUrl = "https://gitlab.example.com/1",
                Author = new GitLabAuthorResponse { Id = 1, Username = "user1" }
            }
        };

        var mr2 = new List<GitLabMergeRequestResponse>
        {
            new()
            {
                Id = 2,
                Iid = 101,
                Title = "MR 2",
                Description = "Description 2",
                SourceBranch = "feature/b",
                TargetBranch = "main",
                State = "merged",
                CreatedAt = DateTimeOffset.Parse("2024-01-11T09:00:00Z"),
                MergedAt = DateTimeOffset.Parse("2024-01-13T14:00:00Z"),
                WebUrl = "https://gitlab.example.com/2",
                Author = new GitLabAuthorResponse { Id = 2, Username = "user2" }
            }
        };

        SetupMultipleHttpResponses(compareResponse, mr1, mr2);

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://gitlab.example.com/api/v4/")
        };
        _httpClientFactoryMock.Setup(x => x.CreateClient("GitLab")).Returns(httpClient);

        var repository = new GitLabRepository(_httpClientFactoryMock.Object);

        // Act
        var result = await repository.GetMergeRequestsByBranchDiffAsync(
            "mygroup/myproject",
            "release/1.0",
            "release/2.0");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal("MR 1", result.Value[0].Title);
        Assert.Equal("MR 2", result.Value[1].Title);
    }

    [Fact]
    public async Task GetMergeRequestsByBranchDiffAsync_WithDuplicateMRs_ShouldDeduplicateResults()
    {
        // Arrange - both commits belong to the same MR
        var compareResponse = new GitLabCompareResponse
        {
            Commits = new List<GitLabCommitResponse>
            {
                new() { Id = "commit1", ShortId = "commit1", Title = "Commit 1", AuthorName = "User1", CreatedAt = DateTimeOffset.UtcNow },
                new() { Id = "commit2", ShortId = "commit2", Title = "Commit 2", AuthorName = "User1", CreatedAt = DateTimeOffset.UtcNow }
            },
            CompareTimeout = false,
            CompareSameRef = false
        };

        var sameMR = new List<GitLabMergeRequestResponse>
        {
            new()
            {
                Id = 1,
                Iid = 100,
                Title = "MR 1",
                Description = "Description 1",
                SourceBranch = "feature/a",
                TargetBranch = "main",
                State = "merged",
                CreatedAt = DateTimeOffset.Parse("2024-01-10T09:00:00Z"),
                MergedAt = DateTimeOffset.Parse("2024-01-12T14:00:00Z"),
                WebUrl = "https://gitlab.example.com/1",
                Author = new GitLabAuthorResponse { Id = 1, Username = "user1" }
            }
        };

        // Both commit queries return the same MR
        SetupMultipleHttpResponses(compareResponse, sameMR, sameMR);

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://gitlab.example.com/api/v4/")
        };
        _httpClientFactoryMock.Setup(x => x.CreateClient("GitLab")).Returns(httpClient);

        var repository = new GitLabRepository(_httpClientFactoryMock.Object);

        // Act
        var result = await repository.GetMergeRequestsByBranchDiffAsync(
            "mygroup/myproject",
            "release/1.0",
            "release/2.0");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Single(result.Value); // Should be deduplicated
        Assert.Equal("MR 1", result.Value[0].Title);
    }

    // T041 [US3] Unit test for GetMergeRequestsByCommitAsync
    [Fact]
    public async Task GetMergeRequestsByCommitAsync_WithValidCommit_ShouldReturnMergeRequests()
    {
        // Arrange
        var gitLabResponses = new List<GitLabMergeRequestResponse>
        {
            new()
            {
                Id = 1,
                Iid = 42,
                Title = "feat: Add feature",
                Description = "Description",
                SourceBranch = "feature/test",
                TargetBranch = "main",
                State = "merged",
                CreatedAt = DateTimeOffset.Parse("2024-03-10T09:00:00Z"),
                MergedAt = DateTimeOffset.Parse("2024-03-12T14:00:00Z"),
                WebUrl = "https://gitlab.example.com/project/-/merge_requests/42",
                Author = new GitLabAuthorResponse { Id = 123, Username = "test.user" }
            }
        };

        SetupHttpResponse(gitLabResponses);

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://gitlab.example.com/api/v4/")
        };
        _httpClientFactoryMock.Setup(x => x.CreateClient("GitLab")).Returns(httpClient);

        var repository = new GitLabRepository(_httpClientFactoryMock.Object);

        // Act
        var result = await repository.GetMergeRequestsByCommitAsync(
            "mygroup/myproject",
            "abc123def456");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Single(result.Value);
        Assert.Equal("feat: Add feature", result.Value[0].Title);
    }

    [Fact]
    public async Task GetMergeRequestsByCommitAsync_WithNoResults_ShouldReturnEmptyList()
    {
        // Arrange
        var emptyResponses = new List<GitLabMergeRequestResponse>();
        SetupHttpResponse(emptyResponses);

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://gitlab.example.com/api/v4/")
        };
        _httpClientFactoryMock.Setup(x => x.CreateClient("GitLab")).Returns(httpClient);

        var repository = new GitLabRepository(_httpClientFactoryMock.Object);

        // Act
        var result = await repository.GetMergeRequestsByCommitAsync(
            "mygroup/myproject",
            "nonexistent");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value);
    }

    private void SetupHttpResponse(List<GitLabMergeRequestResponse> responses)
    {
        var json = responses.ToJson();
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json)
            });
    }

    private void SetupHttpResponse(List<GitLabBranchResponse> branches)
    {
        var json = branches.ToJson();
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json)
            });
    }

    private void SetupMultipleHttpResponses(
        GitLabCompareResponse compareResponse,
        List<GitLabMergeRequestResponse> firstCommitMRs,
        List<GitLabMergeRequestResponse> secondCommitMRs)
    {
        var responseQueue = new Queue<string>();
        responseQueue.Enqueue(compareResponse.ToJson());
        responseQueue.Enqueue(firstCommitMRs.ToJson());
        responseQueue.Enqueue(secondCommitMRs.ToJson());

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var json = responseQueue.Count > 0 ? responseQueue.Dequeue() : "[]";
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(json)
                };
            });
    }
}
