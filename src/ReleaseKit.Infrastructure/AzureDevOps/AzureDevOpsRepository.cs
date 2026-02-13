using System.Net;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Common;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Infrastructure.AzureDevOps.Mappers;
using ReleaseKit.Infrastructure.AzureDevOps.Models;

namespace ReleaseKit.Infrastructure.AzureDevOps;

/// <summary>
/// Azure DevOps Repository 實作
/// </summary>
public class AzureDevOpsRepository : IAzureDevOpsRepository
{
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="httpClientFactory">HttpClient 工廠</param>
    public AzureDevOpsRepository(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc />
    public async Task<Result<WorkItem>> GetWorkItemAsync(int workItemId)
    {
        var httpClient = _httpClientFactory.CreateClient(HttpClientNames.AzureDevOps);
        var url = $"_apis/wit/workitems/{workItemId}?$expand=all&api-version=7.0";

        var response = await httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            return Result<WorkItem>.Failure(response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => Error.AzureDevOps.Unauthorized,
                HttpStatusCode.NotFound => Error.AzureDevOps.WorkItemNotFound(workItemId),
                _ => Error.AzureDevOps.ApiError($"HTTP {(int)response.StatusCode}")
            });
        }

        var content = await response.Content.ReadAsStringAsync();
        var workItemResponse = content.ToTypedObject<AzureDevOpsWorkItemResponse>();

        if (workItemResponse is null)
        {
            return Result<WorkItem>.Failure(Error.AzureDevOps.ApiError("回應內容無法解析"));
        }

        var workItem = AzureDevOpsWorkItemMapper.ToDomain(workItemResponse);
        return Result<WorkItem>.Success(workItem);
    }
}
