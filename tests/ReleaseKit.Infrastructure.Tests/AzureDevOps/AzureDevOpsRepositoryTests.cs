using System.Net;
using Moq;
using Moq.Protected;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Infrastructure.AzureDevOps;
using ReleaseKit.Infrastructure.AzureDevOps.Models;
using Xunit;

namespace ReleaseKit.Infrastructure.Tests.AzureDevOps;

/// <summary>
/// AzureDevOpsRepository 單元測試
/// </summary>
public class AzureDevOpsRepositoryTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;

    public AzureDevOpsRepositoryTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
    }

    [Fact]
    public async Task GetWorkItemAsync_WithValidWorkItemId_ShouldReturnWorkItem()
    {
        // Arrange
        var workItemId = 12345;
        var response = new AzureDevOpsWorkItemResponse
        {
            Id = workItemId,
            Fields = new Dictionary<string, object?>
            {
                ["System.Title"] = "修復登入頁面 500 錯誤",
                ["System.WorkItemType"] = "Bug",
                ["System.State"] = "Active",
                ["System.AreaPath"] = "MyProject\\TeamA"
            },
            Links = new AzureDevOpsLinksResponse
            {
                Html = new AzureDevOpsLinkResponse
                {
                    Href = "https://dev.azure.com/org/project/_workitems/edit/12345"
                }
            }
        };

        SetupHttpResponse(HttpStatusCode.OK, response);

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://dev.azure.com/org/")
        };
        _httpClientFactoryMock.Setup(x => x.CreateClient("AzureDevOps")).Returns(httpClient);

        var repository = new AzureDevOpsRepository(_httpClientFactoryMock.Object);

        // Act
        var result = await repository.GetWorkItemAsync(workItemId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(workItemId, result.Value.WorkItemId);
        Assert.Equal("修復登入頁面 500 錯誤", result.Value.Title);
        Assert.Equal("Bug", result.Value.Type);
        Assert.Equal("Active", result.Value.State);
        Assert.Equal("https://dev.azure.com/org/project/_workitems/edit/12345", result.Value.Url);
        Assert.Equal("MyProject\\TeamA", result.Value.OriginalTeamName);
    }

    [Fact]
    public async Task GetWorkItemAsync_WithNotFound_ShouldReturnWorkItemNotFoundError()
    {
        // Arrange
        var workItemId = 99999;
        SetupHttpResponse(HttpStatusCode.NotFound, null);

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://dev.azure.com/org/")
        };
        _httpClientFactoryMock.Setup(x => x.CreateClient("AzureDevOps")).Returns(httpClient);

        var repository = new AzureDevOpsRepository(_httpClientFactoryMock.Object);

        // Act
        var result = await repository.GetWorkItemAsync(workItemId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("AzureDevOps.WorkItemNotFound", result.Error.Code);
        Assert.Contains("99999", result.Error.Message);
    }

    [Fact]
    public async Task GetWorkItemAsync_WithUnauthorized_ShouldReturnUnauthorizedError()
    {
        // Arrange
        var workItemId = 12345;
        SetupHttpResponse(HttpStatusCode.Unauthorized, null);

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://dev.azure.com/org/")
        };
        _httpClientFactoryMock.Setup(x => x.CreateClient("AzureDevOps")).Returns(httpClient);

        var repository = new AzureDevOpsRepository(_httpClientFactoryMock.Object);

        // Act
        var result = await repository.GetWorkItemAsync(workItemId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("AzureDevOps.Unauthorized", result.Error.Code);
    }

    [Fact]
    public async Task GetWorkItemAsync_WithOtherHttpError_ShouldReturnApiError()
    {
        // Arrange
        var workItemId = 12345;
        SetupHttpResponse(HttpStatusCode.InternalServerError, null);

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://dev.azure.com/org/")
        };
        _httpClientFactoryMock.Setup(x => x.CreateClient("AzureDevOps")).Returns(httpClient);

        var repository = new AzureDevOpsRepository(_httpClientFactoryMock.Object);

        // Act
        var result = await repository.GetWorkItemAsync(workItemId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("AzureDevOps.ApiError", result.Error.Code);
        Assert.Contains("500", result.Error.Message);
    }

    private void SetupHttpResponse(HttpStatusCode statusCode, AzureDevOpsWorkItemResponse? response)
    {
        var httpResponseMessage = new HttpResponseMessage(statusCode);

        if (response is not null)
        {
            httpResponseMessage.Content = new StringContent(response.ToJson());
        }

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponseMessage);
    }
}
