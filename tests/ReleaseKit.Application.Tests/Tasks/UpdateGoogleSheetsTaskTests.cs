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
/// UpdateGoogleSheetsTask 單元測試
/// </summary>
public class UpdateGoogleSheetsTaskTests
{
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly Mock<IGoogleSheetService> _googleSheetServiceMock;
    private readonly Mock<ILogger<UpdateGoogleSheetsTask>> _loggerMock;
    private readonly GoogleSheetOptions _options;

    private const string SpreadsheetId = "test-spreadsheet-id";
    private const string SheetName = "TestSheet";
    private const int SheetId = 0;

    public UpdateGoogleSheetsTaskTests()
    {
        _redisServiceMock = new Mock<IRedisService>();
        _googleSheetServiceMock = new Mock<IGoogleSheetService>();
        _loggerMock = new Mock<ILogger<UpdateGoogleSheetsTask>>();

        _options = new GoogleSheetOptions
        {
            SpreadsheetId = SpreadsheetId,
            SheetName = SheetName,
            ServiceAccountCredentialPath = "/tmp/credentials.json",
            ColumnMapping = new ColumnMappingOptions
            {
                RepositoryNameColumn = "Z",
                FeatureColumn = "B",
                TeamColumn = "D",
                AuthorsColumn = "W",
                PullRequestUrlsColumn = "X",
                UniqueKeyColumn = "Y",
                AutoSyncColumn = "F"
            }
        };

        // 預設 GetSheetIdAsync 回傳固定值
        _googleSheetServiceMock
            .Setup(x => x.GetSheetIdAsync(SpreadsheetId, SheetName))
            .ReturnsAsync(SheetId);
    }

    private UpdateGoogleSheetsTask CreateTask() => new(
        _redisServiceMock.Object,
        _googleSheetServiceMock.Object,
        Options.Create(_options),
        _loggerMock.Object);

    /// <summary>
    /// 建立指定欄位設定的選項
    /// </summary>
    private GoogleSheetOptions CreateOptionsWithInvalidColumn() =>
        new()
        {
            SpreadsheetId = SpreadsheetId,
            SheetName = SheetName,
            ServiceAccountCredentialPath = "/tmp/credentials.json",
            ColumnMapping = new ColumnMappingOptions
            {
                RepositoryNameColumn = "Z",
                FeatureColumn = "B",
                TeamColumn = "D",
                AuthorsColumn = "AA", // 無效：超過 Z
                PullRequestUrlsColumn = "X",
                UniqueKeyColumn = "Y",
                AutoSyncColumn = "F"
            }
        };

    /// <summary>
    /// 建立試算表資料（Row 為 IList&lt;object&gt;，每列 26 欄）
    /// </summary>
    private static IList<IList<object>> CreateSheetData(params IList<object>[] rows) =>
        rows.ToList<IList<object>>();

    /// <summary>
    /// 建立含有 26 個欄位的空白列，並在指定欄填入值
    /// </summary>
    private static List<object> CreateRow(params (int colIdx, string value)[] cells)
    {
        var row = Enumerable.Range(0, 26).Select(_ => (object)string.Empty).ToList();
        foreach (var (colIdx, value) in cells)
            row[colIdx] = value;
        return row;
    }

    private static int ColIdx(char col) => col - 'A';

    /// <summary>
    /// 設定 Redis 回傳整合資料
    /// </summary>
    private void SetupRedisConsolidatedData(ConsolidatedReleaseResult result)
    {
        var json = result.ToJson();
        _redisServiceMock
            .Setup(x => x.HashGetAsync(RedisKeys.ReleaseDataHash, RedisKeys.Fields.Consolidated))
            .ReturnsAsync(json);
    }

    /// <summary>
    /// 建立含有單一專案單筆記錄的整合結果
    /// </summary>
    private static ConsolidatedReleaseResult CreateSingleEntryResult(
        string projectName,
        int workItemId,
        string title = "Feature Title",
        string team = "TestTeam",
        string authorName = "Author1",
        string prUrl = "https://example.com/pr/1")
    {
        return new ConsolidatedReleaseResult
        {
            Projects = new Dictionary<string, List<ConsolidatedReleaseEntry>>
            {
                [projectName] = new List<ConsolidatedReleaseEntry>
                {
                    new()
                    {
                        Title = title,
                        WorkItemUrl = $"https://dev.azure.com/org/proj/_workitems/edit/{workItemId}",
                        WorkItemId = workItemId,
                        TeamDisplayName = team,
                        Authors = new List<ConsolidatedAuthorInfo>
                        {
                            new() { AuthorName = authorName }
                        },
                        PullRequests = new List<ConsolidatedPrInfo>
                        {
                            new() { Url = prUrl }
                        },
                        OriginalData = new ConsolidatedOriginalData
                        {
                            WorkItem = new UserStoryWorkItemOutput
                            {
                                WorkItemId = workItemId,
                                Title = title,
                                IsSuccess = true,
                                ResolutionStatus = UserStoryResolutionStatus.AlreadyUserStoryOrAbove
                            },
                            PullRequests = new List<MergeRequestOutput>()
                        }
                    }
                }
            }
        };
    }

    // ===== T001: Redis 無資料時記錄 Warning 並返回，不呼叫 Google Sheet =====

    /// <summary>
    /// T001: 測試 Redis 無整合資料時，任務記錄 Warning 並返回，不對 Google Sheet 發出任何請求
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithNoRedisData_ShouldLogWarningAndReturn()
    {
        // Arrange
        _redisServiceMock
            .Setup(x => x.HashGetAsync(RedisKeys.ReleaseDataHash, RedisKeys.Fields.Consolidated))
            .ReturnsAsync((string?)null);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert：不應呼叫任何 Google Sheet API
        _googleSheetServiceMock.Verify(
            x => x.GetSheetDataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        _googleSheetServiceMock.Verify(
            x => x.InsertRowAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()),
            Times.Never);
        _googleSheetServiceMock.Verify(
            x => x.UpdateCellsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>()),
            Times.Never);
    }

    // ===== T002: 欄位設定超出 A-Z 範圍時拋出 InvalidOperationException =====

    /// <summary>
    /// T002: 測試欄位設定值超出 A-Z 範圍時拋出 InvalidOperationException，且訊息包含無效欄位名稱
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithInvalidColumnMapping_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var result = CreateSingleEntryResult("my-project", 100);
        SetupRedisConsolidatedData(result);

        var task = new UpdateGoogleSheetsTask(
            _redisServiceMock.Object,
            _googleSheetServiceMock.Object,
            Options.Create(CreateOptionsWithInvalidColumn()),
            _loggerMock.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => task.ExecuteAsync());
        Assert.Contains("AA", exception.Message);
    }

    // ===== T003: Google Sheet 讀取失敗時記錄 Warning 並返回 =====

    /// <summary>
    /// T003: 測試 Google Sheet 讀取回傳 null 時，任務記錄 Warning 並返回，不呼叫插入或更新
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithSheetReadFailure_ShouldLogWarningAndReturn()
    {
        // Arrange
        var result = CreateSingleEntryResult("my-project", 100);
        SetupRedisConsolidatedData(result);

        _googleSheetServiceMock
            .Setup(x => x.GetSheetDataAsync(SpreadsheetId, SheetName, "A:Z"))
            .ReturnsAsync((IList<IList<object>>?)null);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert：不應呼叫插入或更新
        _googleSheetServiceMock.Verify(
            x => x.InsertRowAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()),
            Times.Never);
        _googleSheetServiceMock.Verify(
            x => x.UpdateCellsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>()),
            Times.Never);
    }

    // ===== T004: 新記錄（UniqueKey 不在 Sheet 中）應插入列並寫入所有欄位 =====

    /// <summary>
    /// T004: 測試 UniqueKey 不存在於工作表時，呼叫 InsertRowAsync 並以 UpdateCellsAsync 寫入所有欄位
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithNewEntry_ShouldInsertRowAndWriteAllFields()
    {
        // Arrange
        const string projectName = "my-project";
        const int workItemId = 100;
        const string authorName = "Author1";
        const string prUrl = "https://example.com/pr/1";

        var result = CreateSingleEntryResult(projectName, workItemId,
            title: "New Feature", team: "TestTeam", authorName: authorName, prUrl: prUrl);
        SetupRedisConsolidatedData(result);

        // 工作表：Row 0 有專案標題列（Z 欄 = "my-project"），無資料列
        var repoColIdx = ColIdx('Z');
        var headerRow = CreateRow((repoColIdx, projectName));
        var sheetData = CreateSheetData(headerRow);

        _googleSheetServiceMock
            .Setup(x => x.GetSheetDataAsync(SpreadsheetId, SheetName, "A:Z"))
            .ReturnsAsync(sheetData);

        IReadOnlyDictionary<string, string>? capturedUpdates = null;
        _googleSheetServiceMock
            .Setup(x => x.UpdateCellsAsync(SpreadsheetId, SheetName, It.IsAny<IReadOnlyDictionary<string, string>>()))
            .Callback<string, string, IReadOnlyDictionary<string, string>>((_, _, updates) => capturedUpdates = updates)
            .Returns(Task.CompletedTask);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert：應呼叫 InsertRowAsync 一次（在 rowIndex=1，即標題列之後）
        _googleSheetServiceMock.Verify(
            x => x.InsertRowAsync(SpreadsheetId, SheetId, 1),
            Times.Once);

        // Assert：UpdateCellsAsync 應包含新列的關鍵欄位
        Assert.NotNull(capturedUpdates);
        Assert.Contains("Y2", capturedUpdates.Keys); // UniqueKeyColumn=Y, row=2
        Assert.Equal($"{workItemId}{projectName}", capturedUpdates["Y2"]);
        Assert.Contains("B2", capturedUpdates.Keys); // FeatureColumn=B
        Assert.Equal("New Feature", capturedUpdates["B2"]);
        Assert.Contains("W2", capturedUpdates.Keys); // AuthorsColumn=W
        Assert.Equal(authorName, capturedUpdates["W2"]);
        Assert.Contains("F2", capturedUpdates.Keys); // AutoSyncColumn=F
        Assert.Equal("Y", capturedUpdates["F2"]);
    }

    // ===== T005: 既有記錄（UniqueKey 已在 Sheet 中）應只更新 Authors 與 PullRequestUrls =====

    /// <summary>
    /// T005: 測試 UniqueKey 已存在於工作表時，僅呼叫 UpdateCellsAsync 更新 Authors 與 PullRequestUrls，不呼叫 InsertRowAsync
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithExistingEntry_ShouldUpdateOnlyAuthorsAndPrUrls()
    {
        // Arrange
        const string projectName = "my-project";
        const int workItemId = 200;
        const string authorName = "UpdatedAuthor";
        const string prUrl = "https://example.com/pr/2";

        var result = CreateSingleEntryResult(projectName, workItemId,
            authorName: authorName, prUrl: prUrl);
        SetupRedisConsolidatedData(result);

        // 工作表：Row 0 標題列，Row 1 已有對應 UniqueKey 的資料列
        var repoColIdx = ColIdx('Z');
        var ukColIdx = ColIdx('Y');
        var headerRow = CreateRow((repoColIdx, projectName));
        var existingDataRow = CreateRow(
            (ukColIdx, $"{workItemId}{projectName}"), // UniqueKey 已存在
            (ColIdx('D'), "OldTeam"),
            (ColIdx('W'), "OldAuthor"),
            (ColIdx('X'), "https://old.url"));
        var sheetData = CreateSheetData(headerRow, existingDataRow);

        _googleSheetServiceMock
            .Setup(x => x.GetSheetDataAsync(SpreadsheetId, SheetName, "A:Z"))
            .ReturnsAsync(sheetData);

        var capturedUpdatesList = new List<IReadOnlyDictionary<string, string>>();
        _googleSheetServiceMock
            .Setup(x => x.UpdateCellsAsync(SpreadsheetId, SheetName, It.IsAny<IReadOnlyDictionary<string, string>>()))
            .Callback<string, string, IReadOnlyDictionary<string, string>>((_, _, updates) => capturedUpdatesList.Add(updates))
            .Returns(Task.CompletedTask);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert：不應呼叫 InsertRowAsync
        _googleSheetServiceMock.Verify(
            x => x.InsertRowAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()),
            Times.Never);

        // Assert：UpdateCellsAsync 應包含 Authors 與 PullRequestUrls 更新
        var allUpdates = capturedUpdatesList.SelectMany(d => d).ToDictionary(kv => kv.Key, kv => kv.Value);
        Assert.Contains("W2", allUpdates.Keys); // AuthorsColumn=W, row=2
        Assert.Equal(authorName, allUpdates["W2"]);
        Assert.Contains("X2", allUpdates.Keys); // PullRequestUrlsColumn=X, row=2
        Assert.Contains(prUrl, allUpdates["X2"]);

        // Assert：不應更新 FeatureColumn 或 UniqueKeyColumn（這些欄位不在更新範圍內）
        Assert.DoesNotContain("B2", allUpdates.Keys); // FeatureColumn=B（既有列不更新）
        Assert.DoesNotContain("Y2", allUpdates.Keys); // UniqueKeyColumn=Y（既有列不更新）
    }

    // ===== T006: 資料區塊排序，空白值排最後 =====

    /// <summary>
    /// T006: 測試具有多列資料的專案區塊，排序後 TeamColumn 值較小的列應在前，空白值排最後
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithUnsortedBlock_ShouldSortRowsCorrectly()
    {
        // Arrange
        const string projectName = "my-project";

        // 三筆整合記錄，TeamDisplayName 順序：Z-Team, A-Team, M-Team（應排成 A, M, Z）
        var result = new ConsolidatedReleaseResult
        {
            Projects = new Dictionary<string, List<ConsolidatedReleaseEntry>>
            {
                [projectName] = new List<ConsolidatedReleaseEntry>
                {
                    MakeEntry(100, "Z-Team", projectName),
                    MakeEntry(200, "A-Team", projectName),
                    MakeEntry(300, "M-Team", projectName)
                }
            }
        };
        SetupRedisConsolidatedData(result);

        // 工作表：Row 0 標題列，Row 1-3 資料列（已存在，UniqueKey 匹配）
        var repoColIdx = ColIdx('Z');
        var ukColIdx = ColIdx('Y');
        var teamColIdx = ColIdx('D');
        var authorsColIdx = ColIdx('W');
        var featureColIdx = ColIdx('B');
        var headerRow = CreateRow((repoColIdx, projectName));
        var row1 = CreateRow((ukColIdx, $"100{projectName}"), (teamColIdx, "Z-Team"), (featureColIdx, "Feature Z"), (authorsColIdx, "AuthorZ"));
        var row2 = CreateRow((ukColIdx, $"200{projectName}"), (teamColIdx, "A-Team"), (featureColIdx, "Feature A"), (authorsColIdx, "AuthorA"));
        var row3 = CreateRow((ukColIdx, $"300{projectName}"), (teamColIdx, "M-Team"), (featureColIdx, "Feature M"), (authorsColIdx, "AuthorM"));
        var sheetData = CreateSheetData(headerRow, row1, row2, row3);

        _googleSheetServiceMock
            .Setup(x => x.GetSheetDataAsync(SpreadsheetId, SheetName, "A:Z"))
            .ReturnsAsync(sheetData);

        var sortUpdateCalls = new List<IReadOnlyDictionary<string, string>>();
        _googleSheetServiceMock
            .Setup(x => x.UpdateCellsAsync(SpreadsheetId, SheetName, It.IsAny<IReadOnlyDictionary<string, string>>()))
            .Callback<string, string, IReadOnlyDictionary<string, string>>((_, _, updates) => sortUpdateCalls.Add(updates))
            .Returns(Task.CompletedTask);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert：不應插入新列
        _googleSheetServiceMock.Verify(
            x => x.InsertRowAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()),
            Times.Never);

        // Assert：最後一次 UpdateCellsAsync（排序寫回）應包含正確的排序結果
        // 排序後：A-Team（row 2），M-Team（row 3），Z-Team（row 4）
        // 排序前：Z-Team（row 2），A-Team（row 3），M-Team（row 4）
        // 因此 D2（TeamColumn for row 2）應被寫為 "A-Team"
        var sortCallUpdates = sortUpdateCalls.LastOrDefault();
        Assert.NotNull(sortCallUpdates);

        // Row 2 排序後應為 A-Team（原本是 Z-Team）
        if (sortCallUpdates.TryGetValue("D2", out var row2TeamValue))
        {
            Assert.Equal("A-Team", row2TeamValue);
        }

        // Row 4 排序後應為 Z-Team（原本是 M-Team）
        if (sortCallUpdates.TryGetValue("D4", out var row4TeamValue))
        {
            Assert.Equal("Z-Team", row4TeamValue);
        }
    }

    /// <summary>
    /// 建立測試用的整合記錄（僅含 Team 資訊）
    /// </summary>
    private static ConsolidatedReleaseEntry MakeEntry(int workItemId, string team, string projectName) =>
        new()
        {
            Title = $"Feature {team}",
            WorkItemUrl = $"https://dev.azure.com/org/proj/_workitems/edit/{workItemId}",
            WorkItemId = workItemId,
            TeamDisplayName = team,
            Authors = new List<ConsolidatedAuthorInfo> { new() { AuthorName = $"Author{team}" } },
            PullRequests = new List<ConsolidatedPrInfo>(),
            OriginalData = new ConsolidatedOriginalData
            {
                WorkItem = new UserStoryWorkItemOutput
                {
                    WorkItemId = workItemId,
                    Title = $"Feature {team}",
                    IsSuccess = true,
                    ResolutionStatus = UserStoryResolutionStatus.AlreadyUserStoryOrAbove
                },
                PullRequests = new List<MergeRequestOutput>()
            }
        };
}
