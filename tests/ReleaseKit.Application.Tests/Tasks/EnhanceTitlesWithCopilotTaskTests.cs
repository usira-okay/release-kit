using Microsoft.Extensions.Logging;
using Moq;
using ReleaseKit.Application.Common;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tests.Tasks;

/// <summary>
/// EnhanceTitlesWithCopilotTask 單元測試
/// </summary>
public class EnhanceTitlesWithCopilotTaskTests
{
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly Mock<ITitleEnhancer> _titleEnhancerMock;
    private readonly Mock<ILogger<EnhanceTitlesWithCopilotTask>> _loggerMock;
    private string? _capturedRedisJson;

    public EnhanceTitlesWithCopilotTaskTests()
    {
        _redisServiceMock = new Mock<IRedisService>();
        _titleEnhancerMock = new Mock<ITitleEnhancer>();
        _loggerMock = new Mock<ILogger<EnhanceTitlesWithCopilotTask>>();

        // 捕捉寫入 Redis 的 JSON
        _redisServiceMock.Setup(x => x.HashSetAsync(
                RedisKeys.ReleaseDataHash, RedisKeys.Fields.EnhancedTitles, It.IsAny<string>()))
            .Callback<string, string, string>((_, _, json) => _capturedRedisJson = json)
            .ReturnsAsync(true);
    }

    private EnhanceTitlesWithCopilotTask CreateTask()
    {
        return new EnhanceTitlesWithCopilotTask(
            _redisServiceMock.Object,
            _titleEnhancerMock.Object,
            _loggerMock.Object);
    }

    private void SetupConsolidatedData(ConsolidatedReleaseResult? result)
    {
        _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.ReleaseDataHash, RedisKeys.Fields.Consolidated))
            .ReturnsAsync(result?.ToJson());
    }

    private static ConsolidatedReleaseResult CreateConsolidatedResult(
        params (string ProjectName, ConsolidatedReleaseEntry[] Entries)[] projects)
    {
        var dict = new Dictionary<string, List<ConsolidatedReleaseEntry>>();
        foreach (var (projectName, entries) in projects)
        {
            dict[projectName] = entries.ToList();
        }

        return new ConsolidatedReleaseResult { Projects = dict };
    }

    private static ConsolidatedReleaseEntry CreateEntry(
        int workItemId,
        string title,
        string? workItemTitle = null,
        params string[] prTitles)
    {
        var prs = prTitles.Length > 0
            ? prTitles.Select(t => new MergeRequestOutput
            {
                Title = t,
                PrId = $"pr-{workItemId}",
                SourceBranch = "feature/test",
                TargetBranch = "main",
                State = "merged",
                PRUrl = $"https://example.com/pr/{workItemId}"
            }).ToList()
            : new List<MergeRequestOutput>
            {
                new()
                {
                    Title = "Default PR",
                    PrId = $"pr-{workItemId}",
                    SourceBranch = "feature/test",
                    TargetBranch = "main",
                    State = "merged",
                    PRUrl = $"https://example.com/pr/{workItemId}"
                }
            };

        return new ConsolidatedReleaseEntry
        {
            Title = title,
            WorkItemUrl = $"https://dev.azure.com/org/proj/_workitems/edit/{workItemId}",
            WorkItemId = workItemId,
            TeamDisplayName = "測試團隊",
            Authors = new List<ConsolidatedAuthorInfo>
            {
                new() { AuthorName = "TestAuthor" }
            },
            PullRequests = prs.Select(pr => new ConsolidatedPrInfo { Url = pr.PRUrl }).ToList(),
            OriginalData = new ConsolidatedOriginalData
            {
                WorkItem = new UserStoryWorkItemOutput
                {
                    WorkItemId = workItemId,
                    Title = workItemTitle,
                    IsSuccess = true,
                    ResolutionStatus = UserStoryResolutionStatus.AlreadyUserStoryOrAbove
                },
                PullRequests = prs
            }
        };
    }

    // ===== 無資料情境 =====

    /// <summary>
    /// 當 Redis 中無整合資料時，應跳過處理且不寫入 Redis
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithNoConsolidatedData_ShouldSkipAndNotWriteRedis()
    {
        // Arrange
        SetupConsolidatedData(null);
        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _redisServiceMock.Verify(
            x => x.HashSetAsync(RedisKeys.ReleaseDataHash, RedisKeys.Fields.EnhancedTitles, It.IsAny<string>()),
            Times.Never);
        _titleEnhancerMock.Verify(
            x => x.EnhanceTitlesAsync(It.IsAny<IReadOnlyList<IReadOnlyList<string>>>()),
            Times.Never);
    }

    /// <summary>
    /// 當整合資料的 Projects 為空時，應跳過處理且不寫入 Redis
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithEmptyProjects_ShouldSkipAndNotWriteRedis()
    {
        // Arrange
        var result = new ConsolidatedReleaseResult
        {
            Projects = new Dictionary<string, List<ConsolidatedReleaseEntry>>()
        };
        SetupConsolidatedData(result);
        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _redisServiceMock.Verify(
            x => x.HashSetAsync(RedisKeys.ReleaseDataHash, RedisKeys.Fields.EnhancedTitles, It.IsAny<string>()),
            Times.Never);
    }

    // ===== 標題收集邏輯 =====

    /// <summary>
    /// 應依優先順序收集候選標題：Entry.Title → WorkItem.Title → PR Titles，並排除 null/空白
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldCollectTitlesInPriorityOrder_SkippingNullAndEmpty()
    {
        // Arrange
        var entry = CreateEntry(
            workItemId: 100,
            title: "Update README.md",
            workItemTitle: "新增登入功能",
            "feature/VSTS100-add-login", "Fix typo in login");

        var consolidatedResult = CreateConsolidatedResult(("project-a", new[] { entry }));
        SetupConsolidatedData(consolidatedResult);

        IReadOnlyList<IReadOnlyList<string>>? capturedTitleGroups = null;
        _titleEnhancerMock.Setup(x => x.EnhanceTitlesAsync(It.IsAny<IReadOnlyList<IReadOnlyList<string>>>()))
            .Returns((IReadOnlyList<IReadOnlyList<string>> groups) =>
            {
                capturedTitleGroups = groups;
                return Task.FromResult<IReadOnlyList<string>>(new List<string> { "新增登入功能" });
            });

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert - 驗證收集到的標題依優先順序排列
        Assert.NotNull(capturedTitleGroups);
        Assert.Single(capturedTitleGroups);
        var titles = capturedTitleGroups[0];
        Assert.Equal(4, titles.Count);
        Assert.Equal("Update README.md", titles[0]); // Entry.Title
        Assert.Equal("新增登入功能", titles[1]); // WorkItem.Title
        Assert.Equal("feature/VSTS100-add-login", titles[2]); // PR Title 1
        Assert.Equal("Fix typo in login", titles[3]); // PR Title 2
    }

    /// <summary>
    /// 當 WorkItem.Title 為 null 時，應跳過該標題不加入候選清單
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithNullWorkItemTitle_ShouldSkipIt()
    {
        // Arrange
        var entry = CreateEntry(
            workItemId: 200,
            title: "Fix bug",
            workItemTitle: null,
            prTitles: "Hotfix for crash");

        var consolidatedResult = CreateConsolidatedResult(("project-b", new[] { entry }));
        SetupConsolidatedData(consolidatedResult);

        IReadOnlyList<IReadOnlyList<string>>? capturedTitleGroups = null;
        _titleEnhancerMock.Setup(x => x.EnhanceTitlesAsync(It.IsAny<IReadOnlyList<IReadOnlyList<string>>>()))
            .Returns((IReadOnlyList<IReadOnlyList<string>> groups) =>
            {
                capturedTitleGroups = groups;
                return Task.FromResult<IReadOnlyList<string>>(new List<string> { "Hotfix for crash" });
            });

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert - WorkItem.Title 為 null，只有 Entry.Title 和 PR Title
        Assert.NotNull(capturedTitleGroups);
        var titles = capturedTitleGroups[0];
        Assert.Equal(2, titles.Count);
        Assert.Equal("Fix bug", titles[0]);
        Assert.Equal("Hotfix for crash", titles[1]);
    }

    /// <summary>
    /// 當 Entry.Title 為空白時，應跳過該標題不加入候選清單
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithEmptyEntryTitle_ShouldSkipIt()
    {
        // Arrange
        var entry = CreateEntry(
            workItemId: 300,
            title: "",
            workItemTitle: "實作金流模組",
            prTitles: "Add payment module");

        var consolidatedResult = CreateConsolidatedResult(("project-c", new[] { entry }));
        SetupConsolidatedData(consolidatedResult);

        IReadOnlyList<IReadOnlyList<string>>? capturedTitleGroups = null;
        _titleEnhancerMock.Setup(x => x.EnhanceTitlesAsync(It.IsAny<IReadOnlyList<IReadOnlyList<string>>>()))
            .Returns((IReadOnlyList<IReadOnlyList<string>> groups) =>
            {
                capturedTitleGroups = groups;
                return Task.FromResult<IReadOnlyList<string>>(new List<string> { "實作金流模組" });
            });

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert - Entry.Title 為空白，只有 WorkItem.Title 和 PR Title
        Assert.NotNull(capturedTitleGroups);
        var titles = capturedTitleGroups[0];
        Assert.Equal(2, titles.Count);
        Assert.Equal("實作金流模組", titles[0]);
        Assert.Equal("Add payment module", titles[1]);
    }

    // ===== 結果寫入 Redis =====

    /// <summary>
    /// 應將增強標題與原始資料一起寫入新的 Redis Key
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldWriteEnhancedResultToRedis()
    {
        // Arrange
        var entry1 = CreateEntry(workItemId: 400, title: "Update README.md", workItemTitle: "新增登入功能");
        var entry2 = CreateEntry(workItemId: 401, title: "Fix typo", workItemTitle: "修正認證錯誤");
        var consolidatedResult = CreateConsolidatedResult(("project-d", new[] { entry1, entry2 }));
        SetupConsolidatedData(consolidatedResult);

        _titleEnhancerMock.Setup(x => x.EnhanceTitlesAsync(It.IsAny<IReadOnlyList<IReadOnlyList<string>>>()))
            .ReturnsAsync(new List<string> { "新增登入功能", "修正認證錯誤" });

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _redisServiceMock.Verify(
            x => x.HashSetAsync(RedisKeys.ReleaseDataHash, RedisKeys.Fields.EnhancedTitles, It.IsAny<string>()),
            Times.Once);

        Assert.NotNull(_capturedRedisJson);
        var result = _capturedRedisJson.ToTypedObject<ConsolidatedReleaseResult>();
        Assert.NotNull(result);
        Assert.Single(result.Projects);
        Assert.True(result.Projects.ContainsKey("project-d"));

        var entries = result.Projects["project-d"];
        Assert.Equal(2, entries.Count);
        Assert.Equal("新增登入功能", entries[0].Title);
        Assert.Equal("修正認證錯誤", entries[1].Title);

        // 驗證原始資料保留
        Assert.Equal(400, entries[0].WorkItemId);
        Assert.Equal(401, entries[1].WorkItemId);
    }

    // ===== 多專案情境 =====

    /// <summary>
    /// 多個專案的 entries 應正確對應增強標題並分組寫入
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithMultipleProjects_ShouldEnhanceAndGroupCorrectly()
    {
        // Arrange
        var entryA = CreateEntry(workItemId: 500, title: "Add feature", workItemTitle: "新增報表功能");
        var entryB = CreateEntry(workItemId: 501, title: "Fix issue", workItemTitle: "修正匯出問題");
        var consolidatedResult = CreateConsolidatedResult(
            ("project-x", new[] { entryA }),
            ("project-y", new[] { entryB }));
        SetupConsolidatedData(consolidatedResult);

        _titleEnhancerMock.Setup(x => x.EnhanceTitlesAsync(It.IsAny<IReadOnlyList<IReadOnlyList<string>>>()))
            .ReturnsAsync(new List<string> { "新增報表功能", "修正匯出問題" });

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        Assert.NotNull(_capturedRedisJson);
        var result = _capturedRedisJson.ToTypedObject<ConsolidatedReleaseResult>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Projects.Count);

        Assert.Equal("新增報表功能", result.Projects["project-x"][0].Title);
        Assert.Equal("修正匯出問題", result.Projects["project-y"][0].Title);
    }

    // ===== PR Title 中的空白項應排除 =====

    /// <summary>
    /// PR Title 中的 null 或空白項目應被排除，不加入候選清單
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithEmptyPrTitles_ShouldSkipThem()
    {
        // Arrange - 建立一個 Entry，其中 PR 有空白 Title
        var prs = new List<MergeRequestOutput>
        {
            new()
            {
                Title = "Valid PR Title",
                PrId = "pr-600",
                SourceBranch = "feature/test",
                TargetBranch = "main",
                State = "merged",
                PRUrl = "https://example.com/pr/600"
            },
            new()
            {
                Title = "",
                PrId = "pr-601",
                SourceBranch = "feature/test2",
                TargetBranch = "main",
                State = "merged",
                PRUrl = "https://example.com/pr/601"
            }
        };

        var entry = new ConsolidatedReleaseEntry
        {
            Title = "Some title",
            WorkItemUrl = "https://dev.azure.com/org/proj/_workitems/edit/600",
            WorkItemId = 600,
            TeamDisplayName = "測試團隊",
            Authors = new List<ConsolidatedAuthorInfo> { new() { AuthorName = "Author" } },
            PullRequests = prs.Select(pr => new ConsolidatedPrInfo { Url = pr.PRUrl }).ToList(),
            OriginalData = new ConsolidatedOriginalData
            {
                WorkItem = new UserStoryWorkItemOutput
                {
                    WorkItemId = 600,
                    Title = "Work Item Title",
                    IsSuccess = true,
                    ResolutionStatus = UserStoryResolutionStatus.AlreadyUserStoryOrAbove
                },
                PullRequests = prs
            }
        };

        var consolidatedResult = CreateConsolidatedResult(("project-e", new[] { entry }));
        SetupConsolidatedData(consolidatedResult);

        IReadOnlyList<IReadOnlyList<string>>? capturedTitleGroups = null;
        _titleEnhancerMock.Setup(x => x.EnhanceTitlesAsync(It.IsAny<IReadOnlyList<IReadOnlyList<string>>>()))
            .Returns((IReadOnlyList<IReadOnlyList<string>> groups) =>
            {
                capturedTitleGroups = groups;
                return Task.FromResult<IReadOnlyList<string>>(new List<string> { "Work Item Title" });
            });

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert - 空白的 PR Title 被排除
        Assert.NotNull(capturedTitleGroups);
        var titles = capturedTitleGroups[0];
        Assert.Equal(3, titles.Count); // Entry.Title + WorkItem.Title + Valid PR Title
        Assert.Equal("Some title", titles[0]);
        Assert.Equal("Work Item Title", titles[1]);
        Assert.Equal("Valid PR Title", titles[2]);
    }
}
