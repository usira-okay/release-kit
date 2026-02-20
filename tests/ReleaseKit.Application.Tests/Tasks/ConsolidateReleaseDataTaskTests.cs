using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ReleaseKit.Application.Common;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Application.Tests.Tasks;

/// <summary>
/// ConsolidateReleaseDataTask 單元測試
/// </summary>
public class ConsolidateReleaseDataTaskTests
{
    private readonly Mock<IRedisService> _redisServiceMock = new();
    private readonly Mock<ILogger<ConsolidateReleaseDataTask>> _loggerMock = new();
    private string? _capturedJson;

    public ConsolidateReleaseDataTaskTests()
    {
        _redisServiceMock
            .Setup(x => x.SetAsync(RedisKeys.ConsolidatedReleaseData, It.IsAny<string>(), null))
            .Callback<string, string, TimeSpan?>((_, json, _) => _capturedJson = json)
            .ReturnsAsync(true);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldConsolidateBitbucketAndGitLabDataByPrId()
    {
        SetupPrData(
            bitbucketPrs: new[] { CreatePullRequest("100", "Bitbucket PR", "Alice", "https://bb/pr/100") },
            gitlabPrs: new[] { CreatePullRequest("200", "GitLab PR", "Bob", "https://gl/pr/200") });
        SetupWorkItems(
            CreateWorkItem(5001, "100", "MoneyLogistic"),
            CreateWorkItem(5002, "200", "Platform/Web"));

        var task = CreateTask();

        await task.ExecuteAsync();

        var result = _capturedJson!.ToTypedObject<ConsolidatedReleaseResult>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Projects.Sum(x => x.Entries.Count));
        Assert.Contains(result.Projects.SelectMany(x => x.Entries), x => x.WorkItemId == 5001 && x.PrTitle == "Bitbucket PR");
        Assert.Contains(result.Projects.SelectMany(x => x.Entries), x => x.WorkItemId == 5002 && x.PrTitle == "GitLab PR");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldGroupByLastSegmentOfProjectPath()
    {
        SetupPrData(bitbucketPrs: new[] { CreatePullRequest("100", "PR", "Alice", "https://bb/pr/100") }, bitbucketProjectPath: "group/subgroup/project-a");
        SetupWorkItems(CreateWorkItem(5001, "100", "MoneyLogistic"));

        var task = CreateTask();
        await task.ExecuteAsync();

        var result = _capturedJson!.ToTypedObject<ConsolidatedReleaseResult>();
        Assert.Single(result!.Projects);
        Assert.Equal("project-a", result.Projects[0].ProjectName);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSortEntriesByTeamDisplayNameThenWorkItemId()
    {
        SetupPrData(bitbucketPrs: new[]
        {
            CreatePullRequest("100", "PR A", "Alice", "https://bb/pr/100"),
            CreatePullRequest("101", "PR B", "Bob", "https://bb/pr/101"),
            CreatePullRequest("102", "PR C", "Carol", "https://bb/pr/102")
        }, bitbucketProjectPath: "group/project");
        SetupWorkItems(
            CreateWorkItem(5003, "100", "z-team"),
            CreateWorkItem(5002, "101", "a-team"),
            CreateWorkItem(5001, "102", "a-team"));

        var task = CreateTask();
        await task.ExecuteAsync();

        var entries = _capturedJson!.ToTypedObject<ConsolidatedReleaseResult>()!.Projects.Single().Entries;
        Assert.Equal(new[] { 5001, 5002, 5003 }, entries.Select(x => x.WorkItemId));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapOriginalTeamNameToDisplayName()
    {
        SetupPrData(bitbucketPrs: new[] { CreatePullRequest("100", "PR", "Alice", "https://bb/pr/100") });
        SetupWorkItems(CreateWorkItem(5001, "100", "MoneyLogistic"));

        var task = CreateTask(new AzureDevOpsTeamMappingOptions
        {
            TeamMapping = new List<TeamMappingOption>
            {
                new() { OriginalTeamName = "MoneyLogistic", DisplayName = "金流團隊" }
            }
        });
        await task.ExecuteAsync();

        var entry = _capturedJson!.ToTypedObject<ConsolidatedReleaseResult>()!.Projects.Single().Entries.Single();
        Assert.Equal("金流團隊", entry.TeamDisplayName);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMergeMultiplePrsForSameWorkItemAndDeduplicateAuthors()
    {
        SetupPrData(bitbucketPrs: new[]
        {
            CreatePullRequest("100", "PR 1", "Alice", "https://bb/pr/100"),
            CreatePullRequest("101", "PR 2", "Alice", "https://bb/pr/101")
        });
        SetupWorkItems(
            CreateWorkItem(5001, "100", "MoneyLogistic"),
            CreateWorkItem(5001, "101", "MoneyLogistic"));

        var task = CreateTask();
        await task.ExecuteAsync();

        var entry = _capturedJson!.ToTypedObject<ConsolidatedReleaseResult>()!.Projects.Single().Entries.Single();
        Assert.Equal(1, entry.Authors.Count);
        Assert.Equal(2, entry.PullRequests.Count);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullPrIdWorkItem_ShouldKeepEntryWithUnknownProjectAndEmptyPrData()
    {
        SetupPrData(bitbucketPrs: new[] { CreatePullRequest("100", "PR", "Alice", "https://bb/pr/100") });
        SetupWorkItems(CreateWorkItem(5001, null, "MoneyLogistic"));

        var task = CreateTask();
        await task.ExecuteAsync();

        var project = _capturedJson!.ToTypedObject<ConsolidatedReleaseResult>()!.Projects.Single();
        var entry = project.Entries.Single();
        Assert.Equal("unknown", project.ProjectName);
        Assert.Empty(entry.Authors);
        Assert.Empty(entry.PullRequests);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldWriteConsolidatedResultToRedis()
    {
        SetupPrData(bitbucketPrs: new[] { CreatePullRequest("100", "PR", "Alice", "https://bb/pr/100") });
        SetupWorkItems(CreateWorkItem(5001, "100", "MoneyLogistic"));

        var task = CreateTask();
        await task.ExecuteAsync();

        _redisServiceMock.Verify(x => x.SetAsync(RedisKeys.ConsolidatedReleaseData, It.IsAny<string>(), null), Times.Once);
        Assert.False(string.IsNullOrWhiteSpace(_capturedJson));
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingBitbucketAndGitLabData_ShouldThrowInvalidOperationException()
    {
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.BitbucketPullRequestsByUser)).ReturnsAsync((string?)null);
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.GitLabPullRequestsByUser)).ReturnsAsync((string?)null);
        SetupWorkItems(CreateWorkItem(5001, "100", "MoneyLogistic"));

        var task = CreateTask();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => task.ExecuteAsync());
        Assert.Contains(RedisKeys.BitbucketPullRequestsByUser, exception.Message);
        Assert.Contains(RedisKeys.GitLabPullRequestsByUser, exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyBitbucketAndGitLabResults_ShouldThrowInvalidOperationException()
    {
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.BitbucketPullRequestsByUser)).ReturnsAsync(new FetchResult { Results = new List<ProjectResult>() }.ToJson());
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.GitLabPullRequestsByUser)).ReturnsAsync(new FetchResult { Results = new List<ProjectResult>() }.ToJson());
        SetupWorkItems(CreateWorkItem(5001, "100", "MoneyLogistic"));

        var task = CreateTask();
        await Assert.ThrowsAsync<InvalidOperationException>(() => task.ExecuteAsync());
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingUserStoryData_ShouldThrowInvalidOperationException()
    {
        SetupPrData(bitbucketPrs: new[] { CreatePullRequest("100", "PR", "Alice", "https://bb/pr/100") });
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems)).ReturnsAsync((string?)null);

        var task = CreateTask();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => task.ExecuteAsync());
        Assert.Contains(RedisKeys.AzureDevOpsUserStoryWorkItems, exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyUserStoryWorkItems_ShouldThrowInvalidOperationException()
    {
        SetupPrData(bitbucketPrs: new[] { CreatePullRequest("100", "PR", "Alice", "https://bb/pr/100") });
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems)).ReturnsAsync(new UserStoryFetchResult
        {
            WorkItems = new List<UserStoryWorkItemOutput>(),
            TotalWorkItems = 0,
            AlreadyUserStoryCount = 0,
            FoundViaRecursionCount = 0,
            NotFoundCount = 0,
            OriginalFetchFailedCount = 0
        }.ToJson());

        var task = CreateTask();
        await Assert.ThrowsAsync<InvalidOperationException>(() => task.ExecuteAsync());
    }

    [Fact]
    public async Task ExecuteAsync_TeamMappingShouldBeCaseInsensitive()
    {
        SetupPrData(bitbucketPrs: new[] { CreatePullRequest("100", "PR", "Alice", "https://bb/pr/100") });
        SetupWorkItems(CreateWorkItem(5001, "100", "moneylogistic"));

        var task = CreateTask(new AzureDevOpsTeamMappingOptions
        {
            TeamMapping = new List<TeamMappingOption>
            {
                new() { OriginalTeamName = "MoneyLogistic", DisplayName = "金流團隊" }
            }
        });
        await task.ExecuteAsync();

        var entry = _capturedJson!.ToTypedObject<ConsolidatedReleaseResult>()!.Projects.Single().Entries.Single();
        Assert.Equal("金流團隊", entry.TeamDisplayName);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTeamMappingNotFound_ShouldUseOriginalTeamName()
    {
        SetupPrData(bitbucketPrs: new[] { CreatePullRequest("100", "PR", "Alice", "https://bb/pr/100") });
        SetupWorkItems(CreateWorkItem(5001, "100", "unknown-team"));

        var task = CreateTask();
        await task.ExecuteAsync();

        var entry = _capturedJson!.ToTypedObject<ConsolidatedReleaseResult>()!.Projects.Single().Entries.Single();
        Assert.Equal("unknown-team", entry.TeamDisplayName);
    }

    private ConsolidateReleaseDataTask CreateTask(AzureDevOpsTeamMappingOptions? options = null)
    {
        return new ConsolidateReleaseDataTask(
            _redisServiceMock.Object,
            Options.Create(options ?? new AzureDevOpsTeamMappingOptions
            {
                TeamMapping = new List<TeamMappingOption>
                {
                    new() { OriginalTeamName = "MoneyLogistic", DisplayName = "金流團隊" }
                }
            }),
            _loggerMock.Object);
    }

    private void SetupPrData(
        IEnumerable<MergeRequestOutput>? bitbucketPrs = null,
        IEnumerable<MergeRequestOutput>? gitlabPrs = null,
        string bitbucketProjectPath = "group/project-a",
        string gitlabProjectPath = "group/project-b")
    {
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.BitbucketPullRequestsByUser)).ReturnsAsync(new FetchResult
        {
            Results = bitbucketPrs == null
                ? new List<ProjectResult>()
                : new List<ProjectResult>
                {
                    new()
                    {
                        ProjectPath = bitbucketProjectPath,
                        Platform = SourceControlPlatform.Bitbucket,
                        PullRequests = bitbucketPrs.ToList()
                    }
                }
        }.ToJson());

        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.GitLabPullRequestsByUser)).ReturnsAsync(new FetchResult
        {
            Results = gitlabPrs == null
                ? new List<ProjectResult>()
                : new List<ProjectResult>
                {
                    new()
                    {
                        ProjectPath = gitlabProjectPath,
                        Platform = SourceControlPlatform.GitLab,
                        PullRequests = gitlabPrs.ToList()
                    }
                }
        }.ToJson());
    }

    private void SetupWorkItems(params UserStoryWorkItemOutput[] workItems)
    {
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems)).ReturnsAsync(new UserStoryFetchResult
        {
            WorkItems = workItems.ToList(),
            TotalWorkItems = workItems.Length,
            AlreadyUserStoryCount = workItems.Length,
            FoundViaRecursionCount = 0,
            NotFoundCount = 0,
            OriginalFetchFailedCount = 0
        }.ToJson());
    }

    private static MergeRequestOutput CreatePullRequest(string prId, string title, string authorName, string url)
    {
        return new MergeRequestOutput
        {
            Title = title,
            Description = title,
            SourceBranch = "feature/test",
            TargetBranch = "main",
            CreatedAt = DateTimeOffset.UtcNow,
            MergedAt = DateTimeOffset.UtcNow,
            State = "merged",
            AuthorUserId = authorName.ToLowerInvariant(),
            AuthorName = authorName,
            PrId = prId,
            PRUrl = url,
            WorkItemId = null
        };
    }

    private static UserStoryWorkItemOutput CreateWorkItem(int workItemId, string? prId, string teamName)
    {
        return new UserStoryWorkItemOutput
        {
            WorkItemId = workItemId,
            Title = $"WI-{workItemId}",
            Type = "User Story",
            State = "Active",
            Url = $"https://ado/{workItemId}",
            OriginalTeamName = teamName,
            IsSuccess = true,
            ErrorMessage = null,
            ResolutionStatus = UserStoryResolutionStatus.AlreadyUserStoryOrAbove,
            PrId = prId,
            OriginalWorkItem = null
        };
    }
}
