using System.Net;
using System.Text.Json;
using Moq;
using Moq.Protected;
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

    private void SetupHttpResponse(List<GitLabMergeRequestResponse> responses)
    {
        var json = JsonSerializer.Serialize(responses);
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
}
