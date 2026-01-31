using System.Net;
using System.Text.Json;
using Moq;
using Moq.Protected;
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

    public BitbucketRepositoryTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
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

        var repository = new BitbucketRepository(_httpClientFactoryMock.Object);
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

        var repository = new BitbucketRepository(_httpClientFactoryMock.Object);
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

        var repository = new BitbucketRepository(_httpClientFactoryMock.Object);
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

        var repository = new BitbucketRepository(_httpClientFactoryMock.Object);
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

        var repository = new BitbucketRepository(_httpClientFactoryMock.Object);
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
                var json = JsonSerializer.Serialize(response);

                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });
    }
}
