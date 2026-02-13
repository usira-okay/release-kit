using System.Net;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Common;
using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Infrastructure.SourceControl.Bitbucket;
using ReleaseKit.Infrastructure.SourceControl.Bitbucket.Models;

namespace ReleaseKit.Infrastructure.Tests.SourceControl.Bitbucket;

/// <summary>
/// BitbucketRepository 單元測試
/// </summary>
public class BitbucketRepositoryTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly Mock<ILogger<BitbucketRepository>> _loggerMock;

    public BitbucketRepositoryTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _loggerMock = new Mock<ILogger<BitbucketRepository>>();
    }

    [Fact]
    public async Task GetMergeRequestsByDateRangeAsync_WithValidResponse_ShouldReturnMergeRequests()
    {
        // Arrange
        var response1 = new BitbucketPageResponse<BitbucketPullRequestResponse>
        {
            Values = new List<BitbucketPullRequestResponse>
            {
                CreateSamplePullRequest("PR 1", "2024-01-15T10:00:00Z", "2024-01-20T14:00:00Z"),
                CreateSamplePullRequest("PR 2", "2024-01-16T09:00:00Z", "2024-01-21T15:00:00Z")
            },
            Next = "https://api.bitbucket.org/2.0/repositories/test/repo/pullrequests?page=2"
        };

        var response2 = new BitbucketPageResponse<BitbucketPullRequestResponse>
        {
            Values = new List<BitbucketPullRequestResponse>
            {
                CreateSamplePullRequest("PR 3", "2024-01-17T11:00:00Z", "2024-01-22T16:00:00Z")
            },
            Next = null  // Last page
        };

        SetupHttpResponse(_httpMessageHandlerMock, response1, response2);

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://api.bitbucket.org/2.0/")
        };
        _httpClientFactoryMock.Setup(x => x.CreateClient("Bitbucket")).Returns(httpClient);

        var repository = new BitbucketRepository(_httpClientFactoryMock.Object, _loggerMock.Object);
        var startDateTime = new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var endDateTime = new DateTimeOffset(2024, 1, 25, 0, 0, 0, TimeSpan.Zero);

        // Act
        var result = await repository.GetMergeRequestsByDateRangeAsync(
            "test/repo",
            "main",
            startDateTime,
            endDateTime);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(3, result.Value.Count);
        Assert.Equal("PR 1", result.Value[0].Title);
        Assert.Equal("PR 2", result.Value[1].Title);
        Assert.Equal("PR 3", result.Value[2].Title);
        Assert.All(result.Value, mr => Assert.Equal(SourceControlPlatform.Bitbucket, mr.Platform));
    }

    [Fact]
    public async Task GetMergeRequestsByDateRangeAsync_WithEmptyResponse_ShouldReturnEmptyList()
    {
        // Arrange
        var emptyResponse = new BitbucketPageResponse<BitbucketPullRequestResponse>
        {
            Values = new List<BitbucketPullRequestResponse>(),
            Next = null
        };

        SetupHttpResponse(_httpMessageHandlerMock, emptyResponse);

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://api.bitbucket.org/2.0/")
        };
        _httpClientFactoryMock.Setup(x => x.CreateClient("Bitbucket")).Returns(httpClient);

        var repository = new BitbucketRepository(_httpClientFactoryMock.Object, _loggerMock.Object);
        var startDateTime = DateTimeOffset.UtcNow.AddDays(-7);
        var endDateTime = DateTimeOffset.UtcNow;

        // Act
        var result = await repository.GetMergeRequestsByDateRangeAsync(
            "test/repo",
            "main",
            startDateTime,
            endDateTime);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task GetMergeRequestsByDateRangeAsync_WithUnauthorized_ShouldReturnUnauthorizedError()
    {
        // Arrange
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Unauthorized
            });

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://api.bitbucket.org/2.0/")
        };
        _httpClientFactoryMock.Setup(x => x.CreateClient("Bitbucket")).Returns(httpClient);

        var repository = new BitbucketRepository(_httpClientFactoryMock.Object, _loggerMock.Object);
        var startDateTime = DateTimeOffset.UtcNow.AddDays(-7);
        var endDateTime = DateTimeOffset.UtcNow;

        // Act
        var result = await repository.GetMergeRequestsByDateRangeAsync(
            "test/repo",
            "main",
            startDateTime,
            endDateTime);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(Error.SourceControl.Unauthorized, result.Error);
    }

    [Fact]
    public async Task GetMergeRequestsByDateRangeAsync_WithApiError_ShouldReturnApiError()
    {
        // Arrange
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            });

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://api.bitbucket.org/2.0/")
        };
        _httpClientFactoryMock.Setup(x => x.CreateClient("Bitbucket")).Returns(httpClient);

        var repository = new BitbucketRepository(_httpClientFactoryMock.Object, _loggerMock.Object);
        var startDateTime = DateTimeOffset.UtcNow.AddDays(-7);
        var endDateTime = DateTimeOffset.UtcNow;

        // Act
        var result = await repository.GetMergeRequestsByDateRangeAsync(
            "test/repo",
            "main",
            startDateTime,
            endDateTime);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Contains("HTTP 500", result.Error.Message);
    }

    [Fact]
    public async Task GetMergeRequestsByDateRangeAsync_WithFilteringByClosedOn_ShouldOnlyReturnMatchingMRs()
    {
        // Arrange
        var response = new BitbucketPageResponse<BitbucketPullRequestResponse>
        {
            Values = new List<BitbucketPullRequestResponse>
            {
                // Within range
                CreateSamplePullRequest("PR 1", "2024-01-10T10:00:00Z", "2024-01-15T14:00:00Z"),
                // Before range (should be filtered out)
                CreateSamplePullRequest("PR 2", "2024-01-05T09:00:00Z", "2024-01-10T08:00:00Z"),
                // After range (should be filtered out)
                CreateSamplePullRequest("PR 3", "2024-01-20T11:00:00Z", "2024-01-25T16:00:00Z"),
                // Within range
                CreateSamplePullRequest("PR 4", "2024-01-12T11:00:00Z", "2024-01-18T16:00:00Z")
            },
            Next = null
        };

        SetupHttpResponse(_httpMessageHandlerMock, response);

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://api.bitbucket.org/2.0/")
        };
        _httpClientFactoryMock.Setup(x => x.CreateClient("Bitbucket")).Returns(httpClient);

        var repository = new BitbucketRepository(_httpClientFactoryMock.Object, _loggerMock.Object);
        var startDateTime = new DateTimeOffset(2024, 1, 10, 9, 0, 0, TimeSpan.Zero);
        var endDateTime = new DateTimeOffset(2024, 1, 20, 0, 0, 0, TimeSpan.Zero);

        // Act
        var result = await repository.GetMergeRequestsByDateRangeAsync(
            "test/repo",
            "main",
            startDateTime,
            endDateTime);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal("PR 1", result.Value[0].Title);
        Assert.Equal("PR 4", result.Value[1].Title);
    }

    // T042 [US3] Unit test for GetBranchesAsync
    [Fact]
    public async Task GetBranchesAsync_WithValidParameters_ShouldReturnBranches()
    {
        // Arrange
        var branchesResponse = new BitbucketPageResponse<BitbucketBranchResponse>
        {
            Values = new List<BitbucketBranchResponse>
            {
                new() { Name = "main" },
                new() { Name = "develop" },
                new() { Name = "release/1.0" },
                new() { Name = "release/2.0" },
                new() { Name = "feature/test" }
            },
            Next = null
        };

        SetupHttpResponseForBranches(_httpMessageHandlerMock, branchesResponse);

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://api.bitbucket.org/2.0/")
        };
        _httpClientFactoryMock.Setup(x => x.CreateClient("Bitbucket")).Returns(httpClient);

        var repository = new BitbucketRepository(_httpClientFactoryMock.Object, _loggerMock.Object);

        // Act
        var result = await repository.GetBranchesAsync("test/repo", "release/");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.Count);
        Assert.Contains("release/1.0", result.Value);
        Assert.Contains("release/2.0", result.Value);
    }

    [Fact]
    public async Task GetBranchesAsync_WithoutPattern_ShouldReturnAllBranches()
    {
        // Arrange
        var branchesResponse = new BitbucketPageResponse<BitbucketBranchResponse>
        {
            Values = new List<BitbucketBranchResponse>
            {
                new() { Name = "main" },
                new() { Name = "develop" },
                new() { Name = "feature/test" }
            },
            Next = null
        };

        SetupHttpResponseForBranches(_httpMessageHandlerMock, branchesResponse);

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://api.bitbucket.org/2.0/")
        };
        _httpClientFactoryMock.Setup(x => x.CreateClient("Bitbucket")).Returns(httpClient);

        var repository = new BitbucketRepository(_httpClientFactoryMock.Object, _loggerMock.Object);

        // Act
        var result = await repository.GetBranchesAsync("test/repo");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(3, result.Value.Count);
    }

    // T043 [US3] Unit test for GetMergeRequestsByBranchDiffAsync
    [Fact]
    public async Task GetMergeRequestsByBranchDiffAsync_WithValidParameters_ShouldReturnMergeRequests()
    {
        // Arrange
        var commitsResponse = new BitbucketPageResponse<BitbucketCommitResponse>
        {
            Values = new List<BitbucketCommitResponse>
            {
                new() { Hash = "abc123", Message = "Commit 1", Date = DateTimeOffset.UtcNow },
                new() { Hash = "def456", Message = "Commit 2", Date = DateTimeOffset.UtcNow }
            },
            Next = null
        };

        var pr1Response = new BitbucketPageResponse<BitbucketPullRequestResponse>
        {
            Values = new List<BitbucketPullRequestResponse>
            {
                CreateSamplePullRequest("PR 1", "2024-01-10T09:00:00Z", "2024-01-12T14:00:00Z")
            },
            Next = null
        };

        var pr2Response = new BitbucketPageResponse<BitbucketPullRequestResponse>
        {
            Values = new List<BitbucketPullRequestResponse>
            {
                CreateSamplePullRequest("PR 2", "2024-01-11T09:00:00Z", "2024-01-13T14:00:00Z")
            },
            Next = null
        };

        SetupMultipleHttpResponses(_httpMessageHandlerMock, commitsResponse, pr1Response, pr2Response);

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://api.bitbucket.org/2.0/")
        };
        _httpClientFactoryMock.Setup(x => x.CreateClient("Bitbucket")).Returns(httpClient);

        var repository = new BitbucketRepository(_httpClientFactoryMock.Object, _loggerMock.Object);

        // Act
        var result = await repository.GetMergeRequestsByBranchDiffAsync(
            "test/repo",
            "release/1.0",
            "release/2.0");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.Count);
    }

    [Fact]
    public async Task GetMergeRequestsByBranchDiffAsync_WithDuplicatePRs_ShouldDeduplicateResults()
    {
        // Arrange - both commits belong to the same PR
        var commitsResponse = new BitbucketPageResponse<BitbucketCommitResponse>
        {
            Values = new List<BitbucketCommitResponse>
            {
                new() { Hash = "abc123", Message = "Commit 1", Date = DateTimeOffset.UtcNow },
                new() { Hash = "def456", Message = "Commit 2", Date = DateTimeOffset.UtcNow }
            },
            Next = null
        };

        var samePR = CreateSamplePullRequest("PR 1", "2024-01-10T09:00:00Z", "2024-01-12T14:00:00Z");
        var pr1Response = new BitbucketPageResponse<BitbucketPullRequestResponse>
        {
            Values = new List<BitbucketPullRequestResponse> { samePR },
            Next = null
        };

        var pr2Response = new BitbucketPageResponse<BitbucketPullRequestResponse>
        {
            Values = new List<BitbucketPullRequestResponse> { samePR },
            Next = null
        };

        SetupMultipleHttpResponses(_httpMessageHandlerMock, commitsResponse, pr1Response, pr2Response);

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://api.bitbucket.org/2.0/")
        };
        _httpClientFactoryMock.Setup(x => x.CreateClient("Bitbucket")).Returns(httpClient);

        var repository = new BitbucketRepository(_httpClientFactoryMock.Object, _loggerMock.Object);

        // Act
        var result = await repository.GetMergeRequestsByBranchDiffAsync(
            "test/repo",
            "release/1.0",
            "release/2.0");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Single(result.Value); // Should be deduplicated
    }

    // T044 [US3] Unit test for GetMergeRequestsByCommitAsync
    [Fact]
    public async Task GetMergeRequestsByCommitAsync_WithValidCommit_ShouldReturnMergeRequests()
    {
        // Arrange
        var prResponse = new BitbucketPageResponse<BitbucketPullRequestResponse>
        {
            Values = new List<BitbucketPullRequestResponse>
            {
                CreateSamplePullRequest("PR 1", "2024-01-10T09:00:00Z", "2024-01-12T14:00:00Z")
            },
            Next = null
        };

        SetupHttpResponse(_httpMessageHandlerMock, prResponse);

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://api.bitbucket.org/2.0/")
        };
        _httpClientFactoryMock.Setup(x => x.CreateClient("Bitbucket")).Returns(httpClient);

        var repository = new BitbucketRepository(_httpClientFactoryMock.Object, _loggerMock.Object);

        // Act
        var result = await repository.GetMergeRequestsByCommitAsync(
            "test/repo",
            "abc123def456");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Single(result.Value);
    }

    [Fact]
    public async Task GetMergeRequestsByCommitAsync_WithNoResults_ShouldReturnEmptyList()
    {
        // Arrange
        var emptyResponse = new BitbucketPageResponse<BitbucketPullRequestResponse>
        {
            Values = new List<BitbucketPullRequestResponse>(),
            Next = null
        };

        SetupHttpResponse(_httpMessageHandlerMock, emptyResponse);

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://api.bitbucket.org/2.0/")
        };
        _httpClientFactoryMock.Setup(x => x.CreateClient("Bitbucket")).Returns(httpClient);

        var repository = new BitbucketRepository(_httpClientFactoryMock.Object, _loggerMock.Object);

        // Act
        var result = await repository.GetMergeRequestsByCommitAsync(
            "test/repo",
            "nonexistent");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value);
    }

    // Helper methods
    private static BitbucketPullRequestResponse CreateSamplePullRequest(
        string title,
        string createdOn,
        string closedOn)
    {
        return new BitbucketPullRequestResponse
        {
            Title = title,
            Summary = new BitbucketSummaryResponse { Raw = $"Description for {title}" },
            Source = new BitbucketBranchRefResponse
            {
                Branch = new BitbucketBranchResponse { Name = "feature/test" }
            },
            Destination = new BitbucketBranchRefResponse
            {
                Branch = new BitbucketBranchResponse { Name = "main" }
            },
            CreatedOn = DateTimeOffset.Parse(createdOn),
            ClosedOn = DateTimeOffset.Parse(closedOn),
            State = "MERGED",
            Author = new BitbucketAuthorResponse
            {
                Uuid = "{test-uuid}",
                DisplayName = "Test User"
            },
            Links = new BitbucketLinksResponse
            {
                Html = new BitbucketLinkResponse
                {
                    Href = $"https://bitbucket.org/test/repo/pull-requests/{title.GetHashCode()}"
                }
            }
        };
    }

    private static void SetupHttpResponse(
        Mock<HttpMessageHandler> handlerMock,
        params BitbucketPageResponse<BitbucketPullRequestResponse>[] responses)
    {
        var responseQueue = new Queue<BitbucketPageResponse<BitbucketPullRequestResponse>>(responses);

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                if (responseQueue.Count == 0)
                {
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.NotFound
                    };
                }

                var response = responseQueue.Dequeue();
                var json = response.ToJson();

                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });
    }

    private static void SetupHttpResponseForBranches(
        Mock<HttpMessageHandler> handlerMock,
        params BitbucketPageResponse<BitbucketBranchResponse>[] responses)
    {
        var responseQueue = new Queue<BitbucketPageResponse<BitbucketBranchResponse>>(responses);

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                if (responseQueue.Count == 0)
                {
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.NotFound
                    };
                }

                var response = responseQueue.Dequeue();
                var json = response.ToJson();

                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });
    }

    private static void SetupMultipleHttpResponses(
        Mock<HttpMessageHandler> handlerMock,
        BitbucketPageResponse<BitbucketCommitResponse> commitsResponse,
        params BitbucketPageResponse<BitbucketPullRequestResponse>[] prResponses)
    {
        var responseQueue = new Queue<string>();
        responseQueue.Enqueue(commitsResponse.ToJson());
        
        foreach (var prResponse in prResponses)
        {
            responseQueue.Enqueue(prResponse.ToJson());
        }

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                if (responseQueue.Count == 0)
                {
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.NotFound
                    };
                }

                var json = responseQueue.Dequeue();

                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });
    }
}
