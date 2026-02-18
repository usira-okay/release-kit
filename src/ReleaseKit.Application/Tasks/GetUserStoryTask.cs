using Microsoft.Extensions.Logging;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 取得 User Story 層級的 Work Item 任務
/// </summary>
/// <remarks>
/// 將 Redis 中低於 User Story 層級的 Azure Work Item（如 Bug、Task）遞迴轉換為其對應的 User Story，
/// 並存入新的 Redis Key（`AzureDevOps:WorkItems:UserStories`）。
/// </remarks>
public class GetUserStoryTask : ITask
{
    private readonly IAzureDevOpsRepository _azureDevOpsRepository;
    private readonly IRedisService _redisService;
    private readonly ILogger<GetUserStoryTask> _logger;

    public GetUserStoryTask(
        IAzureDevOpsRepository azureDevOpsRepository,
        IRedisService redisService,
        ILogger<GetUserStoryTask> logger)
    {
        _azureDevOpsRepository = azureDevOpsRepository;
        _redisService = redisService;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        // TODO: Implement
        await Task.CompletedTask;
    }
}
