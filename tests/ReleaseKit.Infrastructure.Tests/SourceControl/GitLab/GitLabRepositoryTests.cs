using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using ReleaseKit.Infrastructure.SourceControl.GitLab;

namespace ReleaseKit.Infrastructure.Tests.SourceControl.GitLab;

/// <summary>
/// GitLabRepository 整合測試
/// </summary>
public class GitLabRepositoryTests
{
    private readonly Mock<ILogger<GitLabRepository>> _mockLogger;

    public GitLabRepositoryTests()
    {
        _mockLogger = new Mock<ILogger<GitLabRepository>>();
    }

    [Fact(Skip = "HttpClient stream disposal issue - needs investigation")]
    public async Task FetchMergeRequestsByTimeRangeAsync_ShouldReturnMergeRequests_WhenApiReturnsData()
    {
        // Arrange
        var projectId = "test/project";
        var startTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var endTime = new DateTimeOffset(2024, 1, 31, 23, 59, 59, TimeSpan.Zero);
        
        var mockResponse = """
        [
          {
            "id": 123,
            "iid": 1,
            "title": "Test MR",
            "description": "Test Description",
            "source_branch": "feature/test",
            "target_branch": "main",
            "state": "merged",
            "author": {
              "username": "test-user"
            },
            "created_at": "2024-01-15T10:00:00Z",
            "updated_at": "2024-01-15T11:00:00Z",
            "merged_at": "2024-01-15T12:00:00Z",
            "web_url": "https://gitlab.com/test/project/-/merge_requests/1"
          }
        ]
        """;

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(mockResponse, Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("https://gitlab.com")
        };

        var repository = new GitLabRepository(httpClient, _mockLogger.Object);

        // Act
        var result = await repository.FetchMergeRequestsByTimeRangeAsync(
            projectId,
            startTime,
            endTime,
            "merged");

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        
        var mr = result[0];
        Assert.Equal("123", mr.Id);
        Assert.Equal(1, mr.Number);
        Assert.Equal("Test MR", mr.Title);
        Assert.Equal("Test Description", mr.Description);
        Assert.Equal("feature/test", mr.SourceBranch);
        Assert.Equal("main", mr.TargetBranch);
        Assert.Equal("merged", mr.State);
        Assert.Equal("test-user", mr.Author);
        Assert.Equal("https://gitlab.com/test/project/-/merge_requests/1", mr.WebUrl);
    }

    [Fact]
    public async Task FetchMergeRequestsByTimeRangeAsync_ShouldReturnEmptyList_WhenNoDataAvailable()
    {
        // Arrange
        var projectId = "test/project";
        var startTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var endTime = new DateTimeOffset(2024, 1, 31, 23, 59, 59, TimeSpan.Zero);
        
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("https://gitlab.com")
        };

        var repository = new GitLabRepository(httpClient, _mockLogger.Object);

        // Act
        var result = await repository.FetchMergeRequestsByTimeRangeAsync(
            projectId,
            startTime,
            endTime);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task FetchMergeRequestsByTimeRangeAsync_ShouldThrowHttpRequestException_WhenApiFails()
    {
        // Arrange
        var projectId = "test/project";
        var startTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var endTime = new DateTimeOffset(2024, 1, 31, 23, 59, 59, TimeSpan.Zero);
        
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Unauthorized,
                Content = new StringContent("Unauthorized", Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("https://gitlab.com")
        };

        var repository = new GitLabRepository(httpClient, _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            repository.FetchMergeRequestsByTimeRangeAsync(
                projectId,
                startTime,
                endTime));
    }

    [Fact]
    public async Task FetchMergeRequestsByTimeRangeAsync_ShouldThrowArgumentException_WhenProjectIdIsEmpty()
    {
        // Arrange
        var httpClient = new HttpClient { BaseAddress = new Uri("https://gitlab.com") };
        var repository = new GitLabRepository(httpClient, _mockLogger.Object);
        var startTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var endTime = new DateTimeOffset(2024, 1, 31, 23, 59, 59, TimeSpan.Zero);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.FetchMergeRequestsByTimeRangeAsync(
                string.Empty,
                startTime,
                endTime));
    }

    [Fact]
    public async Task FetchMergeRequestsByTimeRangeAsync_ShouldThrowArgumentException_WhenStartTimeAfterEndTime()
    {
        // Arrange
        var httpClient = new HttpClient { BaseAddress = new Uri("https://gitlab.com") };
        var repository = new GitLabRepository(httpClient, _mockLogger.Object);
        var startTime = new DateTimeOffset(2024, 1, 31, 23, 59, 59, TimeSpan.Zero);
        var endTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.FetchMergeRequestsByTimeRangeAsync(
                "test/project",
                startTime,
                endTime));
    }

    [Fact]
    public async Task FetchMergeRequestsByBranchComparisonAsync_ShouldReturnMergeRequests_WhenBranchesHaveDifferences()
    {
        // Arrange
        var projectId = "test/project";
        var sourceBranch = "develop";
        var targetBranch = "main";
        
        var compareResponse = """
        {
          "commits": [
            {
              "id": "abc123",
              "title": "Test commit",
              "created_at": "2024-01-15T10:00:00Z"
            }
          ]
        }
        """;

        var mrResponse = """
        [
          {
            "id": 456,
            "iid": 2,
            "title": "Feature MR",
            "description": null,
            "source_branch": "develop",
            "target_branch": "main",
            "state": "opened",
            "author": {
              "username": "developer"
            },
            "created_at": "2024-01-15T09:00:00Z",
            "updated_at": "2024-01-15T10:00:00Z",
            "merged_at": null,
            "web_url": "https://gitlab.com/test/project/-/merge_requests/2"
          }
        ]
        """;

        var callCount = 0;
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(
                        callCount == 1 ? compareResponse : mrResponse,
                        Encoding.UTF8,
                        "application/json")
                };
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("https://gitlab.com")
        };

        var repository = new GitLabRepository(httpClient, _mockLogger.Object);

        // Act
        var result = await repository.FetchMergeRequestsByBranchComparisonAsync(
            projectId,
            sourceBranch,
            targetBranch);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        
        var mr = result[0];
        Assert.Equal("456", mr.Id);
        Assert.Equal(2, mr.Number);
        Assert.Equal("Feature MR", mr.Title);
        Assert.Null(mr.Description);
        Assert.Equal("develop", mr.SourceBranch);
        Assert.Equal("main", mr.TargetBranch);
        Assert.Equal("opened", mr.State);
        Assert.Equal("developer", mr.Author);
        Assert.Null(mr.MergedAt);
    }

    [Fact]
    public async Task FetchMergeRequestsByBranchComparisonAsync_ShouldReturnEmptyList_WhenNoDifferences()
    {
        // Arrange
        var projectId = "test/project";
        var sourceBranch = "develop";
        var targetBranch = "main";
        
        var compareResponse = """
        {
          "commits": []
        }
        """;

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(compareResponse, Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("https://gitlab.com")
        };

        var repository = new GitLabRepository(httpClient, _mockLogger.Object);

        // Act
        var result = await repository.FetchMergeRequestsByBranchComparisonAsync(
            projectId,
            sourceBranch,
            targetBranch);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenHttpClientIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new GitLabRepository(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenLoggerIsNull()
    {
        // Arrange
        var httpClient = new HttpClient();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new GitLabRepository(httpClient, null!));
    }
}
