using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ReleaseKit.Application.Common;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;

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
            .Callback<string, string, TimeSpan?>((_, json, _) => _capturedRedisJson = json)
            .ReturnsAsync(true);
    }

    // ─────────────────────────────────────────────────────────────────
    // T014: 讀取 Bitbucket + GitLab PR 資料並以 PrId 配對 Work Item
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// T014: 驗證整合記錄數量與欄位正確
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithBitbucketAndGitLabPrs_ShouldConsolidateCorrectly()
    {
        // Arrange
        SetupBitbucketPrData(new FetchResult
        {
            Results =
            [
                new ProjectResult
                {
                    ProjectPath = "group/my-repo",
                    PullRequests =
                    [
                        new MergeRequestOutput
                        {
                            PrId = "pr-001",
                            Title = "feature/VSTS12345-add-login",
                            AuthorName = "John Doe",
                            PRUrl = "https://bitbucket.org/group/my-repo/pull-requests/1"
                        }
                    ]
                }
            ]
        });
        SetupGitLabPrData(null);

        SetupUserStoryData(new UserStoryFetchResult
        {
            WorkItems =
            [
                CreateWorkItem(12345, "pr-001", "MoneyLogistic")
            ],
            TotalWorkItems = 1,
            AlreadyUserStoryCount = 1,
            FoundViaRecursionCount = 0,
            NotFoundCount = 0,
            OriginalFetchFailedCount = 0
        });

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        Assert.NotNull(_capturedRedisJson);
        var result = _capturedRedisJson.ToTypedObject<ConsolidatedReleaseResult>();
        Assert.NotNull(result);
        Assert.Single(result.Projects);
        var entry = result.Projects[0].Entries[0];
        Assert.Equal(12345, entry.WorkItemId);
        Assert.Equal("feature/VSTS12345-add-login", entry.PrTitle);
        Assert.Single(entry.Authors);
        Assert.Equal("John Doe", entry.Authors[0].AuthorName);
        Assert.Single(entry.PullRequests);
        Assert.Equal("https://bitbucket.org/group/my-repo/pull-requests/1", entry.PullRequests[0].Url);
    }

    // ─────────────────────────────────────────────────────────────────
    // T015: 依 ProjectPath 最後一段分組
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// T015: 驗證分組使用 ProjectPath 最後一段
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithNestedProjectPath_ShouldGroupByLastSegment()
    {
        // Arrange
        SetupBitbucketPrData(new FetchResult
        {
            Results =
            [
                new ProjectResult
                {
                    ProjectPath = "group/subgroup/project",
                    PullRequests =
                    [
                        new MergeRequestOutput
                        {
                            PrId = "pr-100",
                            Title = "feat/add-feature",
                            AuthorName = "Alice",
                            PRUrl = "https://example.com/pr/1"
                        }
                    ]
                }
            ]
        });
        SetupGitLabPrData(null);

        SetupUserStoryData(new UserStoryFetchResult
        {
            WorkItems = [CreateWorkItem(111, "pr-100", "Team")],
            TotalWorkItems = 1,
            AlreadyUserStoryCount = 1,
            FoundViaRecursionCount = 0,
            NotFoundCount = 0,
            OriginalFetchFailedCount = 0
        });

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        Assert.NotNull(_capturedRedisJson);
        var result = _capturedRedisJson.ToTypedObject<ConsolidatedReleaseResult>();
        Assert.NotNull(result);
        Assert.Single(result.Projects);
        Assert.Equal("project", result.Projects[0].ProjectName);
    }

    // ─────────────────────────────────────────────────────────────────
    // T016: 同一專案內記錄依 TeamDisplayName 升冪 → WorkItemId 升冪排序
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// T016: 驗證同一專案排序邏輯
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithMultipleEntries_ShouldSortByTeamThenWorkItemId()
    {
        // Arrange - 兩個不同 team，同一專案
        SetupBitbucketPrData(new FetchResult
        {
            Results =
            [
                new ProjectResult
                {
                    ProjectPath = "org/repo",
                    PullRequests =
                    [
                        new MergeRequestOutput { PrId = "pr-1", Title = "T1", AuthorName = "A", PRUrl = "http://u1" },
                        new MergeRequestOutput { PrId = "pr-2", Title = "T2", AuthorName = "B", PRUrl = "http://u2" },
                        new MergeRequestOutput { PrId = "pr-3", Title = "T3", AuthorName = "C", PRUrl = "http://u3" }
                    ]
                }
            ]
        });
        SetupGitLabPrData(null);

        // TeamB 下有 WorkItemId 300, TeamA 下有 WorkItemId 100 和 200
        SetupUserStoryData(new UserStoryFetchResult
        {
            WorkItems =
            [
                CreateWorkItem(300, "pr-1", "TeamB"),
                CreateWorkItem(200, "pr-2", "TeamA"),
                CreateWorkItem(100, "pr-3", "TeamA")
            ],
            TotalWorkItems = 3,
            AlreadyUserStoryCount = 3,
            FoundViaRecursionCount = 0,
            NotFoundCount = 0,
            OriginalFetchFailedCount = 0
        });

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        Assert.NotNull(_capturedRedisJson);
        var result = _capturedRedisJson.ToTypedObject<ConsolidatedReleaseResult>();
        Assert.NotNull(result);
        var entries = result.Projects[0].Entries;
        Assert.Equal(3, entries.Count);
        // TeamA-100, TeamA-200, TeamB-300
        Assert.Equal("TeamA", entries[0].TeamDisplayName);
        Assert.Equal(100, entries[0].WorkItemId);
        Assert.Equal("TeamA", entries[1].TeamDisplayName);
        Assert.Equal(200, entries[1].WorkItemId);
        Assert.Equal("TeamB", entries[2].TeamDisplayName);
        Assert.Equal(300, entries[2].WorkItemId);
    }

    // ─────────────────────────────────────────────────────────────────
    // T017: TeamMapping 正確將 OriginalTeamName 轉換為 DisplayName
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// T017: 驗證 TeamMapping 轉換
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithTeamMapping_ShouldMapTeamNameToDisplayName()
    {
        // Arrange
        SetupBitbucketPrData(new FetchResult
        {
            Results =
            [
                new ProjectResult
                {
                    ProjectPath = "org/repo",
                    PullRequests = [new MergeRequestOutput { PrId = "pr-1", Title = "T1", AuthorName = "A", PRUrl = "http://u1" }]
                }
            ]
        });
        SetupGitLabPrData(null);

        SetupUserStoryData(new UserStoryFetchResult
        {
            WorkItems = [CreateWorkItem(999, "pr-1", "MoneyLogistic")],
            TotalWorkItems = 1,
            AlreadyUserStoryCount = 1,
            FoundViaRecursionCount = 0,
            NotFoundCount = 0,
            OriginalFetchFailedCount = 0
        });

        var task = CreateTask(new List<TeamMappingEntry>
        {
            new TeamMappingEntry { OriginalTeamName = "MoneyLogistic", DisplayName = "金流團隊" }
        });

        // Act
        await task.ExecuteAsync();

        // Assert
        Assert.NotNull(_capturedRedisJson);
        var result = _capturedRedisJson.ToTypedObject<ConsolidatedReleaseResult>();
        Assert.NotNull(result);
        Assert.Equal("金流團隊", result.Projects[0].Entries[0].TeamDisplayName);
    }

    // ─────────────────────────────────────────────────────────────────
    // T018: 同一 Work Item 有多個 PR 時，Authors 與 PullRequests 包含所有 PR 資訊（去重 AuthorName）
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// T018: 驗證多 PR 對同一 Work Item 時，Authors 去重且 PullRequests 完整
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithMultiplePrsForSameWorkItem_ShouldAggregateAuthorsAndPrs()
    {
        // Arrange - 兩個 PR 配對同一 Work Item
        SetupBitbucketPrData(new FetchResult
        {
            Results =
            [
                new ProjectResult
                {
                    ProjectPath = "org/repo",
                    PullRequests =
                    [
                        new MergeRequestOutput { PrId = "pr-A", Title = "feat/US100-part1", AuthorName = "John", PRUrl = "http://url-A" },
                        new MergeRequestOutput { PrId = "pr-B", Title = "feat/US100-part2", AuthorName = "John", PRUrl = "http://url-B" },
                        new MergeRequestOutput { PrId = "pr-C", Title = "feat/US100-part3", AuthorName = "Jane", PRUrl = "http://url-C" }
                    ]
                }
            ]
        });
        SetupGitLabPrData(null);

        // 三個 Work Item 記錄都指向 WorkItemId=100，但 PrId 不同
        SetupUserStoryData(new UserStoryFetchResult
        {
            WorkItems =
            [
                CreateWorkItem(100, "pr-A", "Team"),
                CreateWorkItem(100, "pr-B", "Team"),
                CreateWorkItem(100, "pr-C", "Team")
            ],
            TotalWorkItems = 3,
            AlreadyUserStoryCount = 3,
            FoundViaRecursionCount = 0,
            NotFoundCount = 0,
            OriginalFetchFailedCount = 0
        });

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        Assert.NotNull(_capturedRedisJson);
        var result = _capturedRedisJson.ToTypedObject<ConsolidatedReleaseResult>();
        Assert.NotNull(result);
        var entry = result.Projects[0].Entries.Single(e => e.WorkItemId == 100);
        // Authors: John (去重) + Jane = 2
        Assert.Equal(2, entry.Authors.Count);
        Assert.Contains(entry.Authors, a => a.AuthorName == "John");
        Assert.Contains(entry.Authors, a => a.AuthorName == "Jane");
        // PullRequests: 3 個
        Assert.Equal(3, entry.PullRequests.Count);
    }

    // ─────────────────────────────────────────────────────────────────
    // T019: PrId 為 null 的 Work Item 仍出現在結果中
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// T019: 驗證 PrId 為 null 時 Work Item 仍出現，ProjectName 為 "unknown"
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithNullPrId_ShouldIncludeEntryWithUnknownProject()
    {
        // Arrange
        SetupBitbucketPrData(new FetchResult
        {
            Results =
            [
                new ProjectResult
                {
                    ProjectPath = "org/repo",
                    PullRequests = [new MergeRequestOutput { PrId = "pr-1", Title = "T1", AuthorName = "A", PRUrl = "http://u1" }]
                }
            ]
        });
        SetupGitLabPrData(null);

        SetupUserStoryData(new UserStoryFetchResult
        {
            WorkItems =
            [
                CreateWorkItemWithNullPrId(999, "Team"),   // PrId = null
                CreateWorkItem(1, "pr-1", "Team")          // 正常配對
            ],
            TotalWorkItems = 2,
            AlreadyUserStoryCount = 2,
            FoundViaRecursionCount = 0,
            NotFoundCount = 0,
            OriginalFetchFailedCount = 0
        });

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        Assert.NotNull(_capturedRedisJson);
        var result = _capturedRedisJson.ToTypedObject<ConsolidatedReleaseResult>();
        Assert.NotNull(result);
        var unknownProject = result.Projects.FirstOrDefault(p => p.ProjectName == "unknown");
        Assert.NotNull(unknownProject);
        var entry = unknownProject.Entries.Single(e => e.WorkItemId == 999);
        Assert.Empty(entry.Authors);
        Assert.Empty(entry.PullRequests);
    }

    // ─────────────────────────────────────────────────────────────────
    // T020: 驗證結果以 JSON 序列化後正確寫入 Redis Key ConsolidatedReleaseData
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// T020: 驗證整合結果寫入正確的 Redis Key
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldWriteToCorrectRedisKey()
    {
        // Arrange
        SetupBitbucketPrData(new FetchResult
        {
            Results =
            [
                new ProjectResult
                {
                    ProjectPath = "org/repo",
                    PullRequests = [new MergeRequestOutput { PrId = "pr-1", Title = "T", AuthorName = "A", PRUrl = "http://u" }]
                }
            ]
        });
        SetupGitLabPrData(null);

        SetupUserStoryData(new UserStoryFetchResult
        {
            WorkItems = [CreateWorkItem(1, "pr-1", "Team")],
            TotalWorkItems = 1,
            AlreadyUserStoryCount = 1,
            FoundViaRecursionCount = 0,
            NotFoundCount = 0,
            OriginalFetchFailedCount = 0
        });

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _redisServiceMock.Verify(
            x => x.SetAsync(RedisKeys.ConsolidatedReleaseData, It.IsAny<string>(), null),
            Times.Once);
        Assert.NotNull(_capturedRedisJson);
        var result = _capturedRedisJson.ToTypedObject<ConsolidatedReleaseResult>();
        Assert.NotNull(result);
        Assert.NotNull(result.Projects);
    }

    // ─────────────────────────────────────────────────────────────────
    // T022: 當 Bitbucket 與 GitLab ByUser PR 資料 Key 均不存在時拋出例外
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// T022: 驗證兩個 PR Key 均不存在時拋出 InvalidOperationException
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithNoPrData_ShouldThrowInvalidOperationException()
    {
        // Arrange
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.BitbucketPullRequestsByUser)).ReturnsAsync((string?)null);
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.GitLabPullRequestsByUser)).ReturnsAsync((string?)null);

        SetupUserStoryData(new UserStoryFetchResult
        {
            WorkItems = [CreateWorkItem(1, "pr-1", "Team")],
            TotalWorkItems = 1,
            AlreadyUserStoryCount = 1,
            FoundViaRecursionCount = 0,
            NotFoundCount = 0,
            OriginalFetchFailedCount = 0
        });

        var task = CreateTask();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => task.ExecuteAsync());
        Assert.Contains(RedisKeys.BitbucketPullRequestsByUser, ex.Message);
        Assert.Contains(RedisKeys.GitLabPullRequestsByUser, ex.Message);
    }

    // ─────────────────────────────────────────────────────────────────
    // T023: 當 Bitbucket 與 GitLab ByUser PR 資料均為空集合時拋出例外
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// T023: 驗證兩個 PR Key 均為空集合時拋出 InvalidOperationException
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithEmptyPrResults_ShouldThrowInvalidOperationException()
    {
        // Arrange
        SetupBitbucketPrData(new FetchResult { Results = [] });
        SetupGitLabPrData(new FetchResult { Results = [] });

        SetupUserStoryData(new UserStoryFetchResult
        {
            WorkItems = [CreateWorkItem(1, "pr-1", "Team")],
            TotalWorkItems = 1,
            AlreadyUserStoryCount = 1,
            FoundViaRecursionCount = 0,
            NotFoundCount = 0,
            OriginalFetchFailedCount = 0
        });

        var task = CreateTask();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => task.ExecuteAsync());
    }

    // ─────────────────────────────────────────────────────────────────
    // T025: 當 UserStories Work Item 資料 Key 不存在時拋出例外
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// T025: 驗證 UserStories Key 不存在時拋出 InvalidOperationException 且訊息指出缺少的 Key
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithNoWorkItemData_ShouldThrowInvalidOperationException()
    {
        // Arrange
        SetupBitbucketPrData(new FetchResult
        {
            Results =
            [
                new ProjectResult
                {
                    ProjectPath = "org/repo",
                    PullRequests = [new MergeRequestOutput { PrId = "pr-1", Title = "T", AuthorName = "A", PRUrl = "http://u" }]
                }
            ]
        });
        SetupGitLabPrData(null);

        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems)).ReturnsAsync((string?)null);

        var task = CreateTask();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => task.ExecuteAsync());
        Assert.Contains(RedisKeys.AzureDevOpsUserStoryWorkItems, ex.Message);
    }

    // ─────────────────────────────────────────────────────────────────
    // T026: 當 UserStories Work Item 資料為空集合時拋出例外
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// T026: 驗證 WorkItems 為空集合時拋出 InvalidOperationException
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithEmptyWorkItems_ShouldThrowInvalidOperationException()
    {
        // Arrange
        SetupBitbucketPrData(new FetchResult
        {
            Results =
            [
                new ProjectResult
                {
                    ProjectPath = "org/repo",
                    PullRequests = [new MergeRequestOutput { PrId = "pr-1", Title = "T", AuthorName = "A", PRUrl = "http://u" }]
                }
            ]
        });
        SetupGitLabPrData(null);

        SetupUserStoryData(new UserStoryFetchResult
        {
            WorkItems = [],
            TotalWorkItems = 0,
            AlreadyUserStoryCount = 0,
            FoundViaRecursionCount = 0,
            NotFoundCount = 0,
            OriginalFetchFailedCount = 0
        });

        var task = CreateTask();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => task.ExecuteAsync());
    }

    // ─────────────────────────────────────────────────────────────────
    // T028: TeamMapping 忽略大小寫
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// T028: 驗證 TeamMapping 忽略大小寫 — OriginalTeamName 全小寫仍正確對映
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithCaseInsensitiveTeamMapping_ShouldMapCorrectly()
    {
        // Arrange
        SetupBitbucketPrData(new FetchResult
        {
            Results =
            [
                new ProjectResult
                {
                    ProjectPath = "org/repo",
                    PullRequests = [new MergeRequestOutput { PrId = "pr-1", Title = "T", AuthorName = "A", PRUrl = "http://u" }]
                }
            ]
        });
        SetupGitLabPrData(null);

        // OriginalTeamName 全小寫 "moneylogistic"，但 mapping 中是 "MoneyLogistic"
        SetupUserStoryData(new UserStoryFetchResult
        {
            WorkItems = [CreateWorkItem(1, "pr-1", "moneylogistic")],
            TotalWorkItems = 1,
            AlreadyUserStoryCount = 1,
            FoundViaRecursionCount = 0,
            NotFoundCount = 0,
            OriginalFetchFailedCount = 0
        });

        var task = CreateTask(new List<TeamMappingEntry>
        {
            new TeamMappingEntry { OriginalTeamName = "MoneyLogistic", DisplayName = "金流團隊" }
        });

        // Act
        await task.ExecuteAsync();

        // Assert
        Assert.NotNull(_capturedRedisJson);
        var result = _capturedRedisJson.ToTypedObject<ConsolidatedReleaseResult>();
        Assert.NotNull(result);
        Assert.Equal("金流團隊", result.Projects[0].Entries[0].TeamDisplayName);
    }

    // ─────────────────────────────────────────────────────────────────
    // T029: TeamMapping 找不到對映時使用原始 OriginalTeamName
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// T029: 驗證 TeamMapping 找不到時使用原始名稱
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithUnmappedTeamName_ShouldUseOriginalTeamName()
    {
        // Arrange
        SetupBitbucketPrData(new FetchResult
        {
            Results =
            [
                new ProjectResult
                {
                    ProjectPath = "org/repo",
                    PullRequests = [new MergeRequestOutput { PrId = "pr-1", Title = "T", AuthorName = "A", PRUrl = "http://u" }]
                }
            ]
        });
        SetupGitLabPrData(null);

        SetupUserStoryData(new UserStoryFetchResult
        {
            WorkItems = [CreateWorkItem(1, "pr-1", "UnknownTeam")],
            TotalWorkItems = 1,
            AlreadyUserStoryCount = 1,
            FoundViaRecursionCount = 0,
            NotFoundCount = 0,
            OriginalFetchFailedCount = 0
        });

        // No team mapping configured
        var task = CreateTask(new List<TeamMappingEntry>());

        // Act
        await task.ExecuteAsync();

        // Assert
        Assert.NotNull(_capturedRedisJson);
        var result = _capturedRedisJson.ToTypedObject<ConsolidatedReleaseResult>();
        Assert.NotNull(result);
        Assert.Equal("UnknownTeam", result.Projects[0].Entries[0].TeamDisplayName);
    }

    // ─────────────────────────────────────────────────────────────────
    // Helper methods
    // ─────────────────────────────────────────────────────────────────

    private ConsolidateReleaseDataTask CreateTask(List<TeamMappingEntry>? teamMapping = null)
    {
        var options = Options.Create(new ConsolidateReleaseDataOptions
        {
            TeamMapping = teamMapping ?? new List<TeamMappingEntry>()
        });
        return new ConsolidateReleaseDataTask(_redisServiceMock.Object, options, _loggerMock.Object);
    }

    private void SetupBitbucketPrData(FetchResult? fetchResult)
    {
        var json = fetchResult != null ? fetchResult.ToJson() : null;
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.BitbucketPullRequestsByUser)).ReturnsAsync(json);
    }

    private void SetupGitLabPrData(FetchResult? fetchResult)
    {
        var json = fetchResult != null ? fetchResult.ToJson() : null;
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.GitLabPullRequestsByUser)).ReturnsAsync(json);
    }

    private void SetupUserStoryData(UserStoryFetchResult fetchResult)
    {
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.AzureDevOpsUserStoryWorkItems))
            .ReturnsAsync(fetchResult.ToJson());
    }

    private static UserStoryWorkItemOutput CreateWorkItem(int workItemId, string prId, string? originalTeamName) =>
        new UserStoryWorkItemOutput
        {
            WorkItemId = workItemId,
            Title = $"User Story {workItemId}",
            Type = "User Story",
            State = "Active",
            IsSuccess = true,
            ResolutionStatus = UserStoryResolutionStatus.AlreadyUserStoryOrAbove,
            PrId = prId,
            OriginalTeamName = originalTeamName
        };

    private static UserStoryWorkItemOutput CreateWorkItemWithNullPrId(int workItemId, string? originalTeamName) =>
        new UserStoryWorkItemOutput
        {
            WorkItemId = workItemId,
            Title = $"User Story {workItemId}",
            Type = "User Story",
            State = "Active",
            IsSuccess = true,
            ResolutionStatus = UserStoryResolutionStatus.AlreadyUserStoryOrAbove,
            PrId = null,
            OriginalTeamName = originalTeamName
        };
}
