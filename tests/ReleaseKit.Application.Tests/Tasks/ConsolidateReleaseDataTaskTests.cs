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
    private readonly Mock<ILogger<ConsolidateReleaseDataTask>> _loggerMock;
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly ConsolidateReleaseDataOptions _options;
    private string? _capturedRedisJson;

    public ConsolidateReleaseDataTaskTests()
    {
        _loggerMock = new Mock<ILogger<ConsolidateReleaseDataTask>>();
        _redisServiceMock = new Mock<IRedisService>();
        _options = new ConsolidateReleaseDataOptions
        {
            TeamMapping = new List<TeamMappingOptions>
            {
                new() { OriginalTeamName = "MoneyLogistic", DisplayName = "金流團隊" },
                new() { OriginalTeamName = "DailyResource", DisplayName = "日常資源團隊" }
            }
        };

        // Setup Redis write capture
        _redisServiceMock.Setup(x => x.HashSetAsync(RedisKeys.ReleaseDataHash, RedisKeys.Fields.Consolidated, It.IsAny<string>()))
            .Callback<string, string, string>((hashKey, field, json) => _capturedRedisJson = json)
            .ReturnsAsync(true);
    }

    private ConsolidateReleaseDataTask CreateTask(ConsolidateReleaseDataOptions? options = null)
    {
        return new ConsolidateReleaseDataTask(
            _redisServiceMock.Object,
            Options.Create(options ?? _options),
            _loggerMock.Object);
    }

    private void SetupPrData(FetchResult? bitbucketResult, FetchResult? gitLabResult)
    {
        _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.BitbucketHash, RedisKeys.Fields.PullRequestsByUser))
            .ReturnsAsync(bitbucketResult?.ToJson());
        _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.GitLabHash, RedisKeys.Fields.PullRequestsByUser))
            .ReturnsAsync(gitLabResult?.ToJson());
    }

    private void SetupUserStoryData(UserStoryFetchResult? result)
    {
        _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.AzureDevOpsHash, RedisKeys.Fields.WorkItemsUserStories))
            .ReturnsAsync(result?.ToJson());
    }

    private static FetchResult CreateFetchResult(params ProjectResult[] projects)
    {
        return new FetchResult { Results = projects.ToList() };
    }

    private static ProjectResult CreateProject(string projectPath, params MergeRequestOutput[] prs)
    {
        return new ProjectResult
        {
            ProjectPath = projectPath,
            Platform = SourceControlPlatform.GitLab,
            PullRequests = prs.ToList()
        };
    }

    private static MergeRequestOutput CreatePr(string prId, string authorName, string title = "PR Title", string prUrl = "https://example.com/pr")
    {
        return new MergeRequestOutput
        {
            PrId = prId,
            AuthorName = authorName,
            Title = title,
            PRUrl = prUrl,
            SourceBranch = "feature/test",
            TargetBranch = "main",
            State = "merged"
        };
    }

    private static UserStoryFetchResult CreateUserStoryResult(params UserStoryWorkItemOutput[] workItems)
    {
        return new UserStoryFetchResult
        {
            WorkItems = workItems.ToList(),
            TotalWorkItems = workItems.Length,
            AlreadyUserStoryCount = 0,
            FoundViaRecursionCount = 0,
            NotFoundCount = 0,
            OriginalFetchFailedCount = 0
        };
    }

    private static UserStoryWorkItemOutput CreateWorkItem(int workItemId, string? prId, string? originalTeamName = null, string? projectName = null)
    {
        return new UserStoryWorkItemOutput
        {
            WorkItemId = workItemId,
            Title = $"Work Item {workItemId}",
            Type = "User Story",
            State = "Active",
            Url = $"https://dev.azure.com/org/proj/_workitems/edit/{workItemId}",
            OriginalTeamName = originalTeamName,
            IsSuccess = true,
            ResolutionStatus = UserStoryResolutionStatus.AlreadyUserStoryOrAbove,
            PrId = prId,
            ProjectName = projectName
        };
    }

    // ===== T014: 讀取 Bitbucket + GitLab PR 資料並以 PrId 配對 Work Item =====

    /// <summary>
    /// T014: 測試讀取 Bitbucket + GitLab PR 資料並以 PrId 配對 Work Item，驗證整合記錄數量與欄位正確
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithMatchingPrAndWorkItem_ShouldConsolidateCorrectly()
    {
        // Arrange
        var gitLabResult = CreateFetchResult(
            CreateProject("group/my-repo",
                CreatePr("pr-1", "John Doe", "feature/VSTS12345-add-login", "https://gitlab.com/pr/1")));

        var bitbucketResult = CreateFetchResult(
            CreateProject("workspace/other-repo",
                CreatePr("pr-2", "Jane Smith", "bugfix/VSTS67890", "https://bitbucket.org/pr/2")));

        SetupPrData(bitbucketResult, gitLabResult);

        var workItems = CreateUserStoryResult(
            CreateWorkItem(12345, "pr-1", "MoneyLogistic", "my-repo"),
            CreateWorkItem(67890, "pr-2", "DailyResource", "other-repo"));
        SetupUserStoryData(workItems);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        Assert.NotNull(_capturedRedisJson);
        var result = _capturedRedisJson.ToTypedObject<ConsolidatedReleaseResult>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Projects.Count);

        Assert.True(result.Projects.ContainsKey("my-repo"));
        var myRepoEntries = result.Projects["my-repo"];
        Assert.Single(myRepoEntries);
        Assert.Equal(12345, myRepoEntries[0].WorkItemId);
        Assert.Equal("Work Item 12345", myRepoEntries[0].Title);
        Assert.Equal("https://dev.azure.com/org/proj/_workitems/edit/12345", myRepoEntries[0].WorkItemUrl);
        Assert.Equal("金流團隊", myRepoEntries[0].TeamDisplayName);
        Assert.Single(myRepoEntries[0].Authors);
        Assert.Equal("John Doe", myRepoEntries[0].Authors[0].AuthorName);
    }

    // ===== T015: 驗證整合結果依 ProjectPath 最後一段分組 =====

    /// <summary>
    /// T015: 測試整合結果依 ProjectPath 最後一段分組（如 group/subgroup/project → project）
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithNestedProjectPath_ShouldGroupByLastSegment()
    {
        // Arrange
        var gitLabResult = CreateFetchResult(
            CreateProject("group/subgroup/my-project",
                CreatePr("pr-1", "Author1", "PR Title 1")));

        SetupPrData(null, gitLabResult);

        var workItems = CreateUserStoryResult(
            CreateWorkItem(100, "pr-1", "MoneyLogistic", "my-project"));
        SetupUserStoryData(workItems);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        Assert.NotNull(_capturedRedisJson);
        var result = _capturedRedisJson.ToTypedObject<ConsolidatedReleaseResult>();
        Assert.NotNull(result);
        Assert.Single(result.Projects);
        Assert.True(result.Projects.ContainsKey("my-project"));
    }

    // ===== T016: 驗證同一專案內記錄依 TeamDisplayName 升冪、再依 WorkItemId 升冪排序 =====

    /// <summary>
    /// T016: 測試同一專案內記錄依 TeamDisplayName 升冪、再依 WorkItemId 升冪排序
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldSortByTeamDisplayNameThenWorkItemId()
    {
        // Arrange
        var gitLabResult = CreateFetchResult(
            CreateProject("group/project",
                CreatePr("pr-1", "Author1"),
                CreatePr("pr-2", "Author2"),
                CreatePr("pr-3", "Author3")));

        SetupPrData(null, gitLabResult);

        var workItems = CreateUserStoryResult(
            CreateWorkItem(300, "pr-3", "DailyResource", "project"),
            CreateWorkItem(100, "pr-1", "MoneyLogistic", "project"),
            CreateWorkItem(200, "pr-2", "DailyResource", "project"));
        SetupUserStoryData(workItems);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        Assert.NotNull(_capturedRedisJson);
        var result = _capturedRedisJson.ToTypedObject<ConsolidatedReleaseResult>();
        Assert.NotNull(result);
        Assert.Single(result.Projects);
        var entries = result.Projects["project"];
        Assert.Equal(3, entries.Count);

        // 日常資源團隊 (DailyResource → 日常資源團隊) sorted first, then 金流團隊 (MoneyLogistic → 金流團隊)
        Assert.Equal("日常資源團隊", entries[0].TeamDisplayName);
        Assert.Equal(200, entries[0].WorkItemId);
        Assert.Equal("日常資源團隊", entries[1].TeamDisplayName);
        Assert.Equal(300, entries[1].WorkItemId);
        Assert.Equal("金流團隊", entries[2].TeamDisplayName);
        Assert.Equal(100, entries[2].WorkItemId);
    }

    // ===== T017: 驗證 TeamMapping 正確將 OriginalTeamName 轉換為 DisplayName =====

    /// <summary>
    /// T017: 測試 TeamMapping 正確將 OriginalTeamName 轉換為 DisplayName
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithTeamMapping_ShouldConvertToDisplayName()
    {
        // Arrange
        var gitLabResult = CreateFetchResult(
            CreateProject("group/project",
                CreatePr("pr-1", "Author1")));

        SetupPrData(null, gitLabResult);

        var workItems = CreateUserStoryResult(
            CreateWorkItem(100, "pr-1", "MoneyLogistic", "project"));
        SetupUserStoryData(workItems);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        Assert.NotNull(_capturedRedisJson);
        var result = _capturedRedisJson.ToTypedObject<ConsolidatedReleaseResult>();
        Assert.NotNull(result);
        Assert.Equal("金流團隊", result.Projects["project"][0].TeamDisplayName);
    }

    // ===== T018: 驗證同一 Work Item 有多個 PR 時，各自獨立為不同 Entry =====

    /// <summary>
    /// T018: 測試同一 Work Item 有多個 PR 時，以 (WorkItemId, PrId) 為複合 Key 產生獨立 Entry
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithMultiplePrsForSameWorkItem_ShouldCreateSeparateEntries()
    {
        // Arrange
        var gitLabResult = CreateFetchResult(
            CreateProject("group/project",
                CreatePr("pr-1", "John Doe", "PR 1", "https://gitlab.com/pr/1"),
                CreatePr("pr-2", "Jane Smith", "PR 2", "https://gitlab.com/pr/2"),
                CreatePr("pr-3", "John Doe", "PR 3", "https://gitlab.com/pr/3")));

        SetupPrData(null, gitLabResult);

        // 同一 WorkItemId 透過不同 PrId 出現多次，複合 Key 使其各自獨立
        var workItems = CreateUserStoryResult(
            CreateWorkItem(100, "pr-1", "MoneyLogistic", "project"),
            CreateWorkItem(100, "pr-2", "MoneyLogistic", "project"),
            CreateWorkItem(100, "pr-3", "MoneyLogistic", "project"));
        SetupUserStoryData(workItems);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        Assert.NotNull(_capturedRedisJson);
        var result = _capturedRedisJson.ToTypedObject<ConsolidatedReleaseResult>();
        Assert.NotNull(result);
        Assert.Single(result.Projects);

        // 複合 Key (100, "pr-1"), (100, "pr-2"), (100, "pr-3") → 3 筆獨立 Entry
        var entries = result.Projects["project"];
        Assert.Equal(3, entries.Count);
        Assert.All(entries, e => Assert.Equal(100, e.WorkItemId));
        Assert.Contains(entries, e => e.PullRequests.Any(p => p.Url == "https://gitlab.com/pr/1"));
        Assert.Contains(entries, e => e.PullRequests.Any(p => p.Url == "https://gitlab.com/pr/2"));
        Assert.Contains(entries, e => e.PullRequests.Any(p => p.Url == "https://gitlab.com/pr/3"));
    }

    // ===== T019: 驗證 PrId 為 null 的 Work Item 拋出 InvalidOperationException =====

    /// <summary>
    /// T019: 測試 PrId 為 null 的 Work Item 拋出 InvalidOperationException
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithNullPrId_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var gitLabResult = CreateFetchResult(
            CreateProject("group/project",
                CreatePr("pr-1", "Author1")));

        SetupPrData(null, gitLabResult);

        var workItems = CreateUserStoryResult(
            CreateWorkItem(100, "pr-1", "MoneyLogistic", "project"),
            CreateWorkItem(200, null, "DailyResource"));
        SetupUserStoryData(workItems);

        var task = CreateTask();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => task.ExecuteAsync());
        Assert.Contains("缺少 PrId", exception.Message);
    }

    // ===== T020: 驗證整合結果以 JSON 序列化後正確寫入 Redis Key =====

    /// <summary>
    /// T020: 測試整合結果以 JSON 序列化後正確寫入 Redis Key ConsolidatedReleaseData
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldWriteToCorrectRedisKey()
    {
        // Arrange
        var gitLabResult = CreateFetchResult(
            CreateProject("group/project",
                CreatePr("pr-1", "Author1")));

        SetupPrData(null, gitLabResult);

        var workItems = CreateUserStoryResult(
            CreateWorkItem(100, "pr-1", "MoneyLogistic", "project"));
        SetupUserStoryData(workItems);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _redisServiceMock.Verify(
            x => x.HashSetAsync(RedisKeys.ReleaseDataHash, RedisKeys.Fields.Consolidated, It.IsAny<string>()),
            Times.Once);

        Assert.NotNull(_capturedRedisJson);
        var result = _capturedRedisJson.ToTypedObject<ConsolidatedReleaseResult>();
        Assert.NotNull(result);
        Assert.NotEmpty(result.Projects);
    }

    // ===== T021b: 驗證 PrId 存在但在 PR 資料中找不到對應記錄時拋出 InvalidOperationException =====

    /// <summary>
    /// T021b: 測試 PrId 存在但在 PR 資料中找不到對應記錄時，拋出 InvalidOperationException
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithUnmatchedPrId_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var gitLabResult = CreateFetchResult(
            CreateProject("group/project",
                CreatePr("pr-1", "Author1")));

        SetupPrData(null, gitLabResult);

        // Work Item 的 PrId "pr-999" 不存在於 PR 資料中
        var workItems = CreateUserStoryResult(
            CreateWorkItem(100, "pr-999", "MoneyLogistic", "project"));
        SetupUserStoryData(workItems);

        var task = CreateTask();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => task.ExecuteAsync());
        Assert.Contains("pr-999", exception.Message);
        Assert.Contains("找不到對應記錄", exception.Message);
    }

    // ===== T022: 當 Bitbucket 與 GitLab ByUser PR 資料 Key 均不存在時優雅結束 =====

    /// <summary>
    /// T022: 測試當 Bitbucket 與 GitLab ByUser PR 資料 Key 均不存在時，記錄 Warning 並正常結束
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithNoPrDataKeys_ShouldLogWarningAndReturn()
    {
        // Arrange
        SetupPrData(null, null);

        var workItems = CreateUserStoryResult(CreateWorkItem(100, "pr-1"));
        SetupUserStoryData(workItems);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert - 不應寫入 Redis
        _redisServiceMock.Verify(
            x => x.HashSetAsync(RedisKeys.ReleaseDataHash, RedisKeys.Fields.Consolidated, It.IsAny<string>()),
            Times.Never);
    }

    // ===== T023: 當 Bitbucket 與 GitLab ByUser PR 資料均為空集合時優雅結束 =====

    /// <summary>
    /// T023: 測試當 Bitbucket 與 GitLab ByUser PR 資料均為空集合（Results 為空 List）時，記錄 Warning 並正常結束
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithEmptyPrResults_ShouldLogWarningAndReturn()
    {
        // Arrange
        var emptyBitbucket = CreateFetchResult();
        var emptyGitLab = CreateFetchResult();
        SetupPrData(emptyBitbucket, emptyGitLab);

        var workItems = CreateUserStoryResult(CreateWorkItem(100, "pr-1"));
        SetupUserStoryData(workItems);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert - 不應寫入 Redis
        _redisServiceMock.Verify(
            x => x.HashSetAsync(RedisKeys.ReleaseDataHash, RedisKeys.Fields.Consolidated, It.IsAny<string>()),
            Times.Never);
    }

    // ===== T025: 當 UserStories Work Item 資料 Key 不存在時優雅結束 =====

    /// <summary>
    /// T025: 測試當 UserStories Work Item 資料 Key 不存在時，記錄 Warning 並正常結束
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithNoWorkItemDataKey_ShouldLogWarningAndReturn()
    {
        // Arrange
        var gitLabResult = CreateFetchResult(
            CreateProject("group/project",
                CreatePr("pr-1", "Author1")));
        SetupPrData(null, gitLabResult);
        SetupUserStoryData(null);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert - 不應寫入 Redis
        _redisServiceMock.Verify(
            x => x.HashSetAsync(RedisKeys.ReleaseDataHash, RedisKeys.Fields.Consolidated, It.IsAny<string>()),
            Times.Never);
    }

    // ===== T026: 當 UserStories Work Item 資料為空集合時優雅結束 =====

    /// <summary>
    /// T026: 測試當 UserStories Work Item 資料為空集合（WorkItems 為空 List）時，記錄 Warning 並正常結束
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithEmptyWorkItems_ShouldLogWarningAndReturn()
    {
        // Arrange
        var gitLabResult = CreateFetchResult(
            CreateProject("group/project",
                CreatePr("pr-1", "Author1")));
        SetupPrData(null, gitLabResult);
        SetupUserStoryData(CreateUserStoryResult());

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert - 不應寫入 Redis
        _redisServiceMock.Verify(
            x => x.HashSetAsync(RedisKeys.ReleaseDataHash, RedisKeys.Fields.Consolidated, It.IsAny<string>()),
            Times.Never);
    }

    // ===== T028: TeamMapping 忽略大小寫 =====

    /// <summary>
    /// T028: 測試 TeamMapping 忽略大小寫 — OriginalTeamName 為 "moneylogistic"（全小寫）仍正確對映為 "金流團隊"
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithCaseInsensitiveTeamMapping_ShouldMapCorrectly()
    {
        // Arrange
        var gitLabResult = CreateFetchResult(
            CreateProject("group/project",
                CreatePr("pr-1", "Author1")));

        SetupPrData(null, gitLabResult);

        // OriginalTeamName 為全小寫
        var workItems = CreateUserStoryResult(
            CreateWorkItem(100, "pr-1", "moneylogistic", "project"));
        SetupUserStoryData(workItems);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        Assert.NotNull(_capturedRedisJson);
        var result = _capturedRedisJson.ToTypedObject<ConsolidatedReleaseResult>();
        Assert.NotNull(result);
        Assert.Equal("金流團隊", result.Projects["project"][0].TeamDisplayName);
    }

    // ===== T029: TeamMapping 找不到對映時使用原始 OriginalTeamName =====

    /// <summary>
    /// T029: 測試 TeamMapping 找不到對映時 — TeamDisplayName 使用原始 OriginalTeamName
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithUnmappedTeamName_ShouldUseOriginalName()
    {
        // Arrange
        var gitLabResult = CreateFetchResult(
            CreateProject("group/project",
                CreatePr("pr-1", "Author1")));

        SetupPrData(null, gitLabResult);

        var workItems = CreateUserStoryResult(
            CreateWorkItem(100, "pr-1", "UnknownTeam", "project"));
        SetupUserStoryData(workItems);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        Assert.NotNull(_capturedRedisJson);
        var result = _capturedRedisJson.ToTypedObject<ConsolidatedReleaseResult>();
        Assert.NotNull(result);
        Assert.Equal("UnknownTeam", result.Projects["project"][0].TeamDisplayName);
    }

    // ===== T030: 驗證 Work Item 無標題時 Title 使用第一筆 PR 標題 =====

    /// <summary>
    /// T030: 測試 Work Item 無標題（IsSuccess = false）時，Title 應 fallback 使用第一筆配對 PR 的標題
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithNoWorkItemTitle_ShouldFallbackToPrTitle()
    {
        // Arrange
        var gitLabResult = CreateFetchResult(
            CreateProject("group/project",
                CreatePr("pr-1", "Author1", "PR Title Fallback")));

        SetupPrData(null, gitLabResult);

        // Work Item 查詢失敗，Title 為 null
        var failedWorkItem = new UserStoryWorkItemOutput
        {
            WorkItemId = 0,
            Title = null,
            IsSuccess = false,
            ResolutionStatus = UserStoryResolutionStatus.OriginalFetchFailed,
            PrId = "pr-1",
            ProjectName = "project"
        };
        SetupUserStoryData(CreateUserStoryResult(failedWorkItem));

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        Assert.NotNull(_capturedRedisJson);
        var result = _capturedRedisJson.ToTypedObject<ConsolidatedReleaseResult>();
        Assert.NotNull(result);
        Assert.Equal("PR Title Fallback", result.Projects["project"][0].Title);
    }
}
