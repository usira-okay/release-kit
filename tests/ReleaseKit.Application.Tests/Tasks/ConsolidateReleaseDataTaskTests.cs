using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ReleaseKit.Application.Common;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Common.Configuration;
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
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly Mock<ILogger<ConsolidateReleaseDataTask>> _loggerMock;
    private string? _capturedRedisJson;

    public ConsolidateReleaseDataTaskTests()
    {
        _redisServiceMock = new Mock<IRedisService>();
        _loggerMock = new Mock<ILogger<ConsolidateReleaseDataTask>>();

        _redisServiceMock
            .Setup(x => x.SetAsync(RedisKeys.ConsolidatedReleaseData, It.IsAny<string>(), null))
            .Callback<string, string, TimeSpan?>((key, json, ttl) => _capturedRedisJson = json)
            .ReturnsAsync(true);
    }

    private ConsolidateReleaseDataTask CreateTask(List<TeamMapping>? teamMappings = null)
    {
        var azureDevOpsOptions = new AzureDevOpsOptions
        {
            TeamMapping = teamMappings ?? new List<TeamMapping>()
        };
        var options = Options.Create(azureDevOpsOptions);
        return new ConsolidateReleaseDataTask(_redisServiceMock.Object, options, _loggerMock.Object);
    }

    private static FetchResult BuildFetchResult(string projectPath, params (string PrId, string Title, string AuthorName, string PRUrl)[] prs)
    {
        var pullRequests = prs.Select(p => new MergeRequestOutput
        {
            PrId = p.PrId,
            Title = p.Title,
            AuthorName = p.AuthorName,
            PRUrl = p.PRUrl,
            SourceBranch = "feature/branch",
            TargetBranch = "main",
            State = "merged",
            AuthorUserId = "user1",
            CreatedAt = DateTimeOffset.UtcNow
        }).ToList();

        return new FetchResult
        {
            Results = new List<ProjectResult>
            {
                new ProjectResult
                {
                    ProjectPath = projectPath,
                    Platform = SourceControlPlatform.GitLab,
                    PullRequests = pullRequests
                }
            }
        };
    }

    private static UserStoryFetchResult BuildUserStoriesResult(params (int WorkItemId, string? PrId, string? OriginalTeamName)[] workItems)
    {
        return new UserStoryFetchResult
        {
            WorkItems = workItems.Select(wi => new UserStoryWorkItemOutput
            {
                WorkItemId = wi.WorkItemId,
                Title = $"User Story {wi.WorkItemId}",
                Type = "User Story",
                State = "Active",
                IsSuccess = true,
                ResolutionStatus = UserStoryResolutionStatus.AlreadyUserStoryOrAbove,
                PrId = wi.PrId,
                OriginalTeamName = wi.OriginalTeamName
            }).ToList(),
            TotalWorkItems = workItems.Length,
            AlreadyUserStoryCount = workItems.Length,
            FoundViaRecursionCount = 0,
            NotFoundCount = 0,
            OriginalFetchFailedCount = 0
        };
    }

    /// <summary>
    /// T014: 測試讀取 Bitbucket + GitLab PR 資料並以 PrId 配對 Work Item
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithBitbucketAndGitLabPrData_ShouldMatchByPrId()
    {
        // Arrange
        var bitbucketResult = BuildFetchResult("group/repo-a", ("pr-101", "Feature A", "Alice", "https://bb.com/pr/101"));
        var gitlabResult = BuildFetchResult("group/repo-b", ("pr-202", "Feature B", "Bob", "https://gitlab.com/pr/202"));
        var userStories = BuildUserStoriesResult((1001, "pr-101", "TeamA"), (1002, "pr-202", "TeamB"));

        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.BitbucketPullRequestsByUser))
            .ReturnsAsync(bitbucketResult.ToJson());
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.GitLabPullRequestsByUser))
            .ReturnsAsync(gitlabResult.ToJson());
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems))
            .ReturnsAsync(userStories.ToJson());

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        Assert.NotNull(_capturedRedisJson);
        var result = _capturedRedisJson!.ToTypedObject<ConsolidatedReleaseResult>();
        Assert.NotNull(result);
        var allEntries = result!.Projects.SelectMany(p => p.Entries).ToList();
        Assert.Equal(2, allEntries.Count);
        Assert.Contains(allEntries, e => e.WorkItemId == 1001 && e.PrTitle == "Feature A");
        Assert.Contains(allEntries, e => e.WorkItemId == 1002 && e.PrTitle == "Feature B");
    }

    /// <summary>
    /// T015: 測試整合結果依 ProjectPath 最後一段分組
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithMultiLayerProjectPath_ShouldGroupByLastSegment()
    {
        // Arrange
        var gitlabResult = new FetchResult
        {
            Results = new List<ProjectResult>
            {
                new ProjectResult
                {
                    ProjectPath = "group/subgroup/my-repo",
                    Platform = SourceControlPlatform.GitLab,
                    PullRequests = new List<MergeRequestOutput>
                    {
                        new MergeRequestOutput
                        {
                            PrId = "pr-1", Title = "PR 1", AuthorName = "Alice",
                            PRUrl = "https://gitlab.com/pr/1", SourceBranch = "feat", TargetBranch = "main",
                            State = "merged", AuthorUserId = "u1", CreatedAt = DateTimeOffset.UtcNow
                        }
                    }
                }
            }
        };

        var userStories = BuildUserStoriesResult((100, "pr-1", "TeamA"));

        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.BitbucketPullRequestsByUser)).ReturnsAsync((string?)null);
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.GitLabPullRequestsByUser))
            .ReturnsAsync(gitlabResult.ToJson());
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems))
            .ReturnsAsync(userStories.ToJson());

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        Assert.NotNull(_capturedRedisJson);
        var result = _capturedRedisJson!.ToTypedObject<ConsolidatedReleaseResult>();
        Assert.NotNull(result);
        Assert.Single(result!.Projects);
        Assert.Equal("my-repo", result.Projects[0].ProjectName);
    }

    /// <summary>
    /// T016: 測試同一專案內記錄依 TeamDisplayName 升冪、再依 WorkItemId 升冪排序
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldSortEntriesByTeamDisplayNameThenWorkItemId()
    {
        // Arrange
        var gitlabResult = new FetchResult
        {
            Results = new List<ProjectResult>
            {
                new ProjectResult
                {
                    ProjectPath = "group/my-repo",
                    Platform = SourceControlPlatform.GitLab,
                    PullRequests = new List<MergeRequestOutput>
                    {
                        new MergeRequestOutput { PrId = "pr-1", Title = "PR 1", AuthorName = "A", PRUrl = "u1", SourceBranch = "f", TargetBranch = "m", State = "merged", AuthorUserId = "u", CreatedAt = DateTimeOffset.UtcNow },
                        new MergeRequestOutput { PrId = "pr-2", Title = "PR 2", AuthorName = "B", PRUrl = "u2", SourceBranch = "f", TargetBranch = "m", State = "merged", AuthorUserId = "u", CreatedAt = DateTimeOffset.UtcNow },
                        new MergeRequestOutput { PrId = "pr-3", Title = "PR 3", AuthorName = "C", PRUrl = "u3", SourceBranch = "f", TargetBranch = "m", State = "merged", AuthorUserId = "u", CreatedAt = DateTimeOffset.UtcNow },
                        new MergeRequestOutput { PrId = "pr-4", Title = "PR 4", AuthorName = "D", PRUrl = "u4", SourceBranch = "f", TargetBranch = "m", State = "merged", AuthorUserId = "u", CreatedAt = DateTimeOffset.UtcNow }
                    }
                }
            }
        };

        var userStories = BuildUserStoriesResult(
            (200, "pr-1", "Beta"),
            (100, "pr-2", "Alpha"),
            (300, "pr-3", "Alpha"),
            (150, "pr-4", "Beta")
        );

        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.BitbucketPullRequestsByUser)).ReturnsAsync((string?)null);
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.GitLabPullRequestsByUser))
            .ReturnsAsync(gitlabResult.ToJson());
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems))
            .ReturnsAsync(userStories.ToJson());

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        var result = _capturedRedisJson!.ToTypedObject<ConsolidatedReleaseResult>();
        var entries = result!.Projects.First(p => p.ProjectName == "my-repo").Entries;
        Assert.Equal(4, entries.Count);
        Assert.Equal("Alpha", entries[0].TeamDisplayName);
        Assert.Equal(100, entries[0].WorkItemId);
        Assert.Equal("Alpha", entries[1].TeamDisplayName);
        Assert.Equal(300, entries[1].WorkItemId);
        Assert.Equal("Beta", entries[2].TeamDisplayName);
        Assert.Equal(150, entries[2].WorkItemId);
        Assert.Equal("Beta", entries[3].TeamDisplayName);
        Assert.Equal(200, entries[3].WorkItemId);
    }

    /// <summary>
    /// T017: 測試 TeamMapping 正確將 OriginalTeamName 轉換為 DisplayName
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithTeamMapping_ShouldConvertToDisplayName()
    {
        // Arrange
        var gitlabResult = BuildFetchResult("group/repo", ("pr-1", "PR 1", "Alice", "https://gitlab.com/1"));
        var userStories = BuildUserStoriesResult((101, "pr-1", "MoneyLogistic"));

        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.BitbucketPullRequestsByUser)).ReturnsAsync((string?)null);
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.GitLabPullRequestsByUser))
            .ReturnsAsync(gitlabResult.ToJson());
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems))
            .ReturnsAsync(userStories.ToJson());

        var teamMappings = new List<TeamMapping>
        {
            new TeamMapping { OriginalTeamName = "MoneyLogistic", DisplayName = "金流團隊" }
        };
        var task = CreateTask(teamMappings);

        // Act
        await task.ExecuteAsync();

        // Assert
        var result = _capturedRedisJson!.ToTypedObject<ConsolidatedReleaseResult>();
        var entry = result!.Projects.SelectMany(p => p.Entries).First(e => e.WorkItemId == 101);
        Assert.Equal("金流團隊", entry.TeamDisplayName);
    }

    /// <summary>
    /// T018: 測試同一 Work Item 有多個 PR 時，Authors 與 PullRequests 包含所有相關 PR 資訊
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithMultiplePrsForSameWorkItem_ShouldIncludeAllPrInfo()
    {
        // Arrange
        var bitbucketResult = BuildFetchResult("group/repo",
            ("bb-101", "Feature X (BB)", "Alice", "https://bb.com/101"));
        var gitlabResult = BuildFetchResult("group/repo",
            ("gl-101", "Feature X (GL)", "Bob", "https://gitlab.com/101"));

        var userStories = new UserStoryFetchResult
        {
            WorkItems = new List<UserStoryWorkItemOutput>
            {
                new UserStoryWorkItemOutput
                {
                    WorkItemId = 5001,
                    Title = "US 5001",
                    IsSuccess = true,
                    ResolutionStatus = UserStoryResolutionStatus.AlreadyUserStoryOrAbove,
                    PrId = "bb-101",
                    OriginalTeamName = "TeamA"
                },
                new UserStoryWorkItemOutput
                {
                    WorkItemId = 5001,
                    Title = "US 5001",
                    IsSuccess = true,
                    ResolutionStatus = UserStoryResolutionStatus.AlreadyUserStoryOrAbove,
                    PrId = "gl-101",
                    OriginalTeamName = "TeamA"
                }
            },
            TotalWorkItems = 2,
            AlreadyUserStoryCount = 2,
            FoundViaRecursionCount = 0,
            NotFoundCount = 0,
            OriginalFetchFailedCount = 0
        };

        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.BitbucketPullRequestsByUser))
            .ReturnsAsync(bitbucketResult.ToJson());
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.GitLabPullRequestsByUser))
            .ReturnsAsync(gitlabResult.ToJson());
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems))
            .ReturnsAsync(userStories.ToJson());

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        var result = _capturedRedisJson!.ToTypedObject<ConsolidatedReleaseResult>();
        var entry = result!.Projects.SelectMany(p => p.Entries).Single(e => e.WorkItemId == 5001);
        Assert.Equal(2, entry.PullRequests.Count);
        Assert.Equal(2, entry.Authors.Count);
        Assert.Contains(entry.Authors, a => a.AuthorName == "Alice");
        Assert.Contains(entry.Authors, a => a.AuthorName == "Bob");
        Assert.Contains(entry.PullRequests, p => p.Url == "https://bb.com/101");
        Assert.Contains(entry.PullRequests, p => p.Url == "https://gitlab.com/101");
    }

    /// <summary>
    /// T019: 測試 PrId 為 null 的 Work Item 仍出現在結果中
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithNullPrIdWorkItem_ShouldAppearWithUnknownProject()
    {
        // Arrange
        var gitlabResult = BuildFetchResult("group/repo", ("pr-1", "Feature", "Alice", "https://gitlab.com/1"));
        var userStories = BuildUserStoriesResult(
            (200, "pr-1", "TeamA"),
            (999, null, "TeamB")  // PrId が null
        );

        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.BitbucketPullRequestsByUser)).ReturnsAsync((string?)null);
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.GitLabPullRequestsByUser))
            .ReturnsAsync(gitlabResult.ToJson());
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems))
            .ReturnsAsync(userStories.ToJson());

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        var result = _capturedRedisJson!.ToTypedObject<ConsolidatedReleaseResult>();
        Assert.NotNull(result);
        var unknownProject = result!.Projects.FirstOrDefault(p => p.ProjectName == "unknown");
        Assert.NotNull(unknownProject);
        var nullPrEntry = unknownProject!.Entries.First(e => e.WorkItemId == 999);
        Assert.Empty(nullPrEntry.PullRequests);
        Assert.Empty(nullPrEntry.Authors);
    }

    /// <summary>
    /// T020: 測試整合結果以 JSON 序列化後正確寫入 Redis Key ConsolidatedReleaseData
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldWriteJsonToRedis()
    {
        // Arrange
        var gitlabResult = BuildFetchResult("group/repo", ("pr-1", "Feature", "Alice", "https://gitlab.com/1"));
        var userStories = BuildUserStoriesResult((100, "pr-1", "TeamA"));

        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.BitbucketPullRequestsByUser)).ReturnsAsync((string?)null);
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.GitLabPullRequestsByUser))
            .ReturnsAsync(gitlabResult.ToJson());
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems))
            .ReturnsAsync(userStories.ToJson());

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _redisServiceMock.Verify(
            x => x.SetAsync(RedisKeys.ConsolidatedReleaseData, It.IsAny<string>(), null),
            Times.Once);
        Assert.NotNull(_capturedRedisJson);
        var result = _capturedRedisJson!.ToTypedObject<ConsolidatedReleaseResult>();
        Assert.NotNull(result);
        Assert.NotEmpty(result!.Projects);
    }

    /// <summary>
    /// T022: 測試當 Bitbucket 與 GitLab PR 資料均不存在時拋出 InvalidOperationException
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenBothPrDataMissing_ShouldThrowInvalidOperationException()
    {
        // Arrange
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.BitbucketPullRequestsByUser)).ReturnsAsync((string?)null);
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.GitLabPullRequestsByUser)).ReturnsAsync((string?)null);

        var task = CreateTask();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => task.ExecuteAsync());
        Assert.Contains(RedisKeys.BitbucketPullRequestsByUser, exception.Message);
        Assert.Contains(RedisKeys.GitLabPullRequestsByUser, exception.Message);
    }

    /// <summary>
    /// T023: 測試當 Bitbucket 與 GitLab PR 資料均為空集合時拋出 InvalidOperationException
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenBothPrDataEmpty_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var emptyBitbucket = new FetchResult { Results = new List<ProjectResult>() };
        var emptyGitlab = new FetchResult { Results = new List<ProjectResult>() };

        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.BitbucketPullRequestsByUser))
            .ReturnsAsync(emptyBitbucket.ToJson());
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.GitLabPullRequestsByUser))
            .ReturnsAsync(emptyGitlab.ToJson());

        var task = CreateTask();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => task.ExecuteAsync());
    }

    /// <summary>
    /// T025: 測試當 UserStories Work Item 資料 Key 不存在時拋出 InvalidOperationException
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenWorkItemDataMissing_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var gitlabResult = BuildFetchResult("group/repo", ("pr-1", "Feature", "Alice", "https://gitlab.com/1"));

        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.BitbucketPullRequestsByUser)).ReturnsAsync((string?)null);
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.GitLabPullRequestsByUser))
            .ReturnsAsync(gitlabResult.ToJson());
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems)).ReturnsAsync((string?)null);

        var task = CreateTask();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => task.ExecuteAsync());
        Assert.Contains(RedisKeys.AzureDevOpsUserStoryWorkItems, exception.Message);
    }

    /// <summary>
    /// T026: 測試當 UserStories Work Item 資料為空集合時拋出 InvalidOperationException
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenWorkItemDataEmpty_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var gitlabResult = BuildFetchResult("group/repo", ("pr-1", "Feature", "Alice", "https://gitlab.com/1"));
        var emptyUserStories = new UserStoryFetchResult
        {
            WorkItems = new List<UserStoryWorkItemOutput>(),
            TotalWorkItems = 0,
            AlreadyUserStoryCount = 0,
            FoundViaRecursionCount = 0,
            NotFoundCount = 0,
            OriginalFetchFailedCount = 0
        };

        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.BitbucketPullRequestsByUser)).ReturnsAsync((string?)null);
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.GitLabPullRequestsByUser))
            .ReturnsAsync(gitlabResult.ToJson());
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems))
            .ReturnsAsync(emptyUserStories.ToJson());

        var task = CreateTask();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => task.ExecuteAsync());
    }

    /// <summary>
    /// T028: 測試 TeamMapping 忽略大小寫 — OriginalTeamName 為全小寫仍正確對映
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithLowercaseOriginalTeamName_ShouldMatchCaseInsensitively()
    {
        // Arrange
        var gitlabResult = BuildFetchResult("group/repo", ("pr-1", "PR 1", "Alice", "https://gitlab.com/1"));
        var userStories = BuildUserStoriesResult((101, "pr-1", "moneylogistic"));  // 全小寫

        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.BitbucketPullRequestsByUser)).ReturnsAsync((string?)null);
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.GitLabPullRequestsByUser))
            .ReturnsAsync(gitlabResult.ToJson());
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems))
            .ReturnsAsync(userStories.ToJson());

        var teamMappings = new List<TeamMapping>
        {
            new TeamMapping { OriginalTeamName = "MoneyLogistic", DisplayName = "金流團隊" }  // 混合大小寫
        };
        var task = CreateTask(teamMappings);

        // Act
        await task.ExecuteAsync();

        // Assert
        var result = _capturedRedisJson!.ToTypedObject<ConsolidatedReleaseResult>();
        var entry = result!.Projects.SelectMany(p => p.Entries).First(e => e.WorkItemId == 101);
        Assert.Equal("金流團隊", entry.TeamDisplayName);
    }

    /// <summary>
    /// T029: 測試 TeamMapping 找不到對映時 TeamDisplayName 使用原始 OriginalTeamName
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenTeamMappingNotFound_ShouldUseFallbackOriginalName()
    {
        // Arrange
        var gitlabResult = BuildFetchResult("group/repo", ("pr-1", "PR 1", "Alice", "https://gitlab.com/1"));
        var userStories = BuildUserStoriesResult((101, "pr-1", "UnknownTeam"));

        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.BitbucketPullRequestsByUser)).ReturnsAsync((string?)null);
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.GitLabPullRequestsByUser))
            .ReturnsAsync(gitlabResult.ToJson());
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems))
            .ReturnsAsync(userStories.ToJson());

        var teamMappings = new List<TeamMapping>
        {
            new TeamMapping { OriginalTeamName = "MoneyLogistic", DisplayName = "金流團隊" }
        };
        var task = CreateTask(teamMappings);

        // Act
        await task.ExecuteAsync();

        // Assert
        var result = _capturedRedisJson!.ToTypedObject<ConsolidatedReleaseResult>();
        var entry = result!.Projects.SelectMany(p => p.Entries).First(e => e.WorkItemId == 101);
        Assert.Equal("UnknownTeam", entry.TeamDisplayName);
    }
}
