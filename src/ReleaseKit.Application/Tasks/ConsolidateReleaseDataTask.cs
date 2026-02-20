using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Application.Common;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 整合 Release 資料任務
/// </summary>
public class ConsolidateReleaseDataTask : ITask
{
    private const string UnknownProjectName = "unknown";
    private readonly IRedisService _redisService;
    private readonly AzureDevOpsTeamMappingOptions _azureDevOpsOptions;
    private readonly ILogger<ConsolidateReleaseDataTask> _logger;

    public ConsolidateReleaseDataTask(
        IRedisService redisService,
        IOptions<AzureDevOpsTeamMappingOptions> azureDevOpsOptions,
        ILogger<ConsolidateReleaseDataTask> logger)
    {
        _redisService = redisService;
        _azureDevOpsOptions = azureDevOpsOptions.Value;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        var bitbucketFetchResult = await LoadFetchResultAsync(RedisKeys.BitbucketPullRequestsByUser);
        var gitlabFetchResult = await LoadFetchResultAsync(RedisKeys.GitLabPullRequestsByUser);

        if (IsNullOrEmpty(bitbucketFetchResult) && IsNullOrEmpty(gitlabFetchResult))
        {
            throw new InvalidOperationException(
                $"缺少必要的 PR 資料，請確認 Redis Key: {RedisKeys.BitbucketPullRequestsByUser}, {RedisKeys.GitLabPullRequestsByUser}");
        }

        var userStoryFetchResult = await LoadUserStoryFetchResultAsync();
        if (userStoryFetchResult == null || userStoryFetchResult.WorkItems.Count == 0)
        {
            throw new InvalidOperationException(
                $"缺少必要的 Work Item 資料，請確認 Redis Key: {RedisKeys.AzureDevOpsUserStoryWorkItems}");
        }

        var prLookup = BuildPrLookup(bitbucketFetchResult, gitlabFetchResult);
        var teamMapping = _azureDevOpsOptions.TeamMapping
            .ToDictionary(x => x.OriginalTeamName, x => x.DisplayName, StringComparer.OrdinalIgnoreCase);

        var consolidatedEntries = BuildConsolidatedEntries(userStoryFetchResult.WorkItems, prLookup, teamMapping);

        var result = new ConsolidatedReleaseResult
        {
            Projects = consolidatedEntries
                .GroupBy(x => x.ProjectName, StringComparer.Ordinal)
                .Select(x => new ConsolidatedProjectGroup
                {
                    ProjectName = x.Key,
                    Entries = x
                        .Select(y => y.Entry)
                        .OrderBy(y => y.TeamDisplayName, StringComparer.Ordinal)
                        .ThenBy(y => y.WorkItemId)
                        .ToList()
                })
                .OrderBy(x => x.ProjectName, StringComparer.Ordinal)
                .ToList()
        };

        var json = result.ToJson();
        await _redisService.SetAsync(RedisKeys.ConsolidatedReleaseData, json);
        Console.WriteLine(json);
        _logger.LogInformation("整合 Release 資料完成，共 {Count} 個專案分組", result.Projects.Count);
    }

    private async Task<FetchResult?> LoadFetchResultAsync(string key)
    {
        var json = await _redisService.GetAsync(key);
        return string.IsNullOrWhiteSpace(json) ? null : json.ToTypedObject<FetchResult>();
    }

    private async Task<UserStoryFetchResult?> LoadUserStoryFetchResultAsync()
    {
        var json = await _redisService.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems);
        return string.IsNullOrWhiteSpace(json) ? null : json.ToTypedObject<UserStoryFetchResult>();
    }

    private static bool IsNullOrEmpty(FetchResult? fetchResult)
    {
        return fetchResult == null || fetchResult.Results.Count == 0 || fetchResult.Results.All(x => x.PullRequests.Count == 0);
    }

    private static Dictionary<string, List<(MergeRequestOutput PullRequest, string ProjectName)>> BuildPrLookup(params FetchResult?[] fetchResults)
    {
        var result = new Dictionary<string, List<(MergeRequestOutput PullRequest, string ProjectName)>>(StringComparer.Ordinal);

        foreach (var fetchResult in fetchResults.Where(x => x != null))
        {
            foreach (var project in fetchResult!.Results)
            {
                var projectName = GetProjectName(project.ProjectPath);
                foreach (var pullRequest in project.PullRequests.Where(x => !string.IsNullOrWhiteSpace(x.PrId)))
                {
                    if (!result.TryGetValue(pullRequest.PrId, out var list))
                    {
                        list = new List<(MergeRequestOutput PullRequest, string ProjectName)>();
                        result[pullRequest.PrId] = list;
                    }

                    list.Add((pullRequest, projectName));
                }
            }
        }

        return result;
    }

    private static List<(string ProjectName, ConsolidatedReleaseEntry Entry)> BuildConsolidatedEntries(
        List<UserStoryWorkItemOutput> workItems,
        Dictionary<string, List<(MergeRequestOutput PullRequest, string ProjectName)>> prLookup,
        Dictionary<string, string> teamMapping)
    {
        var mergedByWorkItemId = new Dictionary<int, (UserStoryWorkItemOutput WorkItem, List<(MergeRequestOutput PullRequest, string ProjectName)> PullRequests)>();

        foreach (var workItem in workItems)
        {
            if (!mergedByWorkItemId.TryGetValue(workItem.WorkItemId, out var current))
            {
                current = (workItem, new List<(MergeRequestOutput PullRequest, string ProjectName)>());
            }

            if (!string.IsNullOrWhiteSpace(workItem.PrId) && prLookup.TryGetValue(workItem.PrId, out var matchedPullRequests))
            {
                current.PullRequests.AddRange(matchedPullRequests);
            }

            mergedByWorkItemId[workItem.WorkItemId] = current;
        }

        return mergedByWorkItemId.Values.Select(x =>
        {
            var matchedPullRequests = x.PullRequests;
            var projectName = matchedPullRequests.Count == 0 ? UnknownProjectName : matchedPullRequests[0].ProjectName;
            var teamDisplayName = ResolveTeamDisplayName(x.WorkItem.OriginalTeamName, teamMapping);
            var distinctPullRequests = matchedPullRequests
                .GroupBy(y => y.PullRequest.PRUrl, StringComparer.Ordinal)
                .Select(y => y.First().PullRequest)
                .ToList();

            return (
                projectName,
                new ConsolidatedReleaseEntry
                {
                    PrTitle = distinctPullRequests.FirstOrDefault()?.Title ?? string.Empty,
                    WorkItemId = x.WorkItem.WorkItemId,
                    TeamDisplayName = teamDisplayName,
                    Authors = distinctPullRequests
                        .Select(y => y.AuthorName)
                        .Where(y => !string.IsNullOrWhiteSpace(y))
                        .Distinct(StringComparer.Ordinal)
                        .Select(y => new ConsolidatedAuthorInfo { AuthorName = y })
                        .ToList(),
                    PullRequests = distinctPullRequests
                        .Select(y => new ConsolidatedPrInfo { Url = y.PRUrl })
                        .ToList(),
                    OriginalData = new ConsolidatedOriginalData
                    {
                        WorkItem = x.WorkItem,
                        PullRequests = distinctPullRequests
                    }
                });
        }).ToList();
    }

    private static string ResolveTeamDisplayName(string? originalTeamName, IReadOnlyDictionary<string, string> teamMapping)
    {
        if (string.IsNullOrWhiteSpace(originalTeamName))
        {
            return string.Empty;
        }

        return teamMapping.TryGetValue(originalTeamName, out var displayName) ? displayName : originalTeamName;
    }

    private static string GetProjectName(string projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return UnknownProjectName;
        }

        var parts = projectPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? UnknownProjectName : parts[^1];
    }
}
