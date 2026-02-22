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
    private readonly Mock<ILogger<UpdateGoogleSheetsTask>> _loggerMock;
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly Mock<IGoogleSheetService> _googleSheetServiceMock;
    private readonly GoogleSheetOptions _googleSheetOptions;

    public UpdateGoogleSheetsTaskTests()
    {
        _loggerMock = new Mock<ILogger<UpdateGoogleSheetsTask>>();
        _redisServiceMock = new Mock<IRedisService>();
        _googleSheetServiceMock = new Mock<IGoogleSheetService>();
        _googleSheetOptions = new GoogleSheetOptions
        {
            SpreadsheetId = "test-spreadsheet-id",
            SheetName = "TestSheet",
            ServiceAccountCredentialPath = "/path/to/credentials.json",
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
    }

    private UpdateGoogleSheetsTask CreateTask(GoogleSheetOptions? options = null)
    {
        return new UpdateGoogleSheetsTask(
            _redisServiceMock.Object,
            _googleSheetServiceMock.Object,
            Options.Create(options ?? _googleSheetOptions),
            _loggerMock.Object);
    }

    private void SetupConsolidatedData(ConsolidatedReleaseResult? result)
    {
        _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.ReleaseDataHash, RedisKeys.Fields.Consolidated))
            .ReturnsAsync(result?.ToJson());
    }

    private static ConsolidatedReleaseResult CreateConsolidatedResult(string projectName, params ConsolidatedReleaseEntry[] entries)
    {
        return new ConsolidatedReleaseResult
        {
            Projects = new Dictionary<string, List<ConsolidatedReleaseEntry>>
            {
                [projectName] = entries.ToList()
            }
        };
    }

    private static ConsolidatedReleaseEntry CreateEntry(int workItemId, string title, string teamDisplayName = "Team A",
        string workItemUrl = "https://dev.azure.com/org/proj/_workitems/edit/123",
        string[]? authors = null, string[]? prUrls = null)
    {
        return new ConsolidatedReleaseEntry
        {
            WorkItemId = workItemId,
            Title = title,
            TeamDisplayName = teamDisplayName,
            WorkItemUrl = workItemUrl,
            Authors = (authors ?? new[] { "Author1" }).Select(a => new ConsolidatedAuthorInfo { AuthorName = a }).ToList(),
            PullRequests = (prUrls ?? new[] { "https://example.com/pr/1" }).Select(u => new ConsolidatedPrInfo { Url = u }).ToList(),
            OriginalData = new ConsolidatedOriginalData
            {
                WorkItem = new UserStoryWorkItemOutput
                {
                    WorkItemId = workItemId,
                    Title = title,
                    Type = "User Story",
                    State = "Active",
                    Url = workItemUrl,
                    IsSuccess = true,
                    ResolutionStatus = UserStoryResolutionStatus.AlreadyUserStoryOrAbove
                },
                PullRequests = new List<MergeRequestOutput>()
            }
        };
    }

    // ===== Task 4.2: Redis 中無整合資料時記錄 Warning 並結束 =====

    /// <summary>
    /// 測試 Redis 中無整合資料時，記錄 Warning 並正常結束
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithNoConsolidatedData_ShouldLogWarningAndReturn()
    {
        // Arrange
        SetupConsolidatedData(null);
        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — Google Sheet 服務未被呼叫
        _googleSheetServiceMock.Verify(
            x => x.GetSheetDataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    // ===== Task 4.3: ColumnMapping 驗證 =====

    /// <summary>
    /// 測試 ColumnMapping 欄位超過 Z 時拋出 InvalidOperationException
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithInvalidColumnMapping_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var invalidOptions = new GoogleSheetOptions
        {
            SpreadsheetId = "test-id",
            SheetName = "TestSheet",
            ServiceAccountCredentialPath = "/path/credentials.json",
            ColumnMapping = new ColumnMappingOptions
            {
                RepositoryNameColumn = "AA", // 無效欄位（超過 Z）
                FeatureColumn = "B",
                TeamColumn = "D",
                AuthorsColumn = "W",
                PullRequestUrlsColumn = "X",
                UniqueKeyColumn = "Y",
                AutoSyncColumn = "F"
            }
        };

        var task = CreateTask(invalidOptions);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => task.ExecuteAsync());
        Assert.Contains("RepositoryNameColumn", exception.Message);
        Assert.Contains("AA", exception.Message);
    }

    /// <summary>
    /// 測試 ColumnMapping 欄位為空字串時拋出 InvalidOperationException
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithEmptyColumnMapping_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var invalidOptions = new GoogleSheetOptions
        {
            SpreadsheetId = "test-id",
            SheetName = "TestSheet",
            ServiceAccountCredentialPath = "/path/credentials.json",
            ColumnMapping = new ColumnMappingOptions
            {
                RepositoryNameColumn = "Z",
                FeatureColumn = "", // 空欄位
                TeamColumn = "D",
                AuthorsColumn = "W",
                PullRequestUrlsColumn = "X",
                UniqueKeyColumn = "Y",
                AutoSyncColumn = "F"
            }
        };

        var task = CreateTask(invalidOptions);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => task.ExecuteAsync());
    }

    // ===== Task 4.4: 讀取 Google Sheet 失敗時優雅結束 =====

    /// <summary>
    /// 測試讀取 Google Sheet 失敗時，記錄 Warning 並正常結束
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenGetSheetDataFails_ShouldLogWarningAndReturn()
    {
        // Arrange
        var result = CreateConsolidatedResult("repo1", CreateEntry(100, "Feature A"));
        SetupConsolidatedData(result);

        _googleSheetServiceMock.Setup(x => x.GetSheetDataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Network error"));

        var task = CreateTask();

        // Act — 不應拋出例外
        await task.ExecuteAsync();

        // Assert — InsertRow 未被呼叫
        _googleSheetServiceMock.Verify(
            x => x.InsertRowAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()),
            Times.Never);
    }

    // ===== Task 5: 新增資料邏輯 =====

    /// <summary>
    /// 測試 UK 不在 Sheet 中時，執行新增流程（插入列並填入值）
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithNewEntry_ShouldInsertRowAndFillValues()
    {
        // Arrange
        var result = CreateConsolidatedResult("repo1",
            CreateEntry(100, "Feature A", "Team A", "https://dev.azure.com/wi/100",
                new[] { "Alice", "Bob" }, new[] { "https://pr.com/1" }));
        SetupConsolidatedData(result);

        // Sheet 有 repo1 標記列在第 0 行
        var sheetData = new List<IList<object>>
        {
            CreateRow("Z", "repo1") // row 0: repo1 標記列
        };
        _googleSheetServiceMock.Setup(x => x.GetSheetDataAsync(It.IsAny<string>(), It.IsAny<string>(), "A:Z"))
            .ReturnsAsync(sheetData);
        _googleSheetServiceMock.Setup(x => x.InsertRowAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);
        _googleSheetServiceMock.Setup(x => x.UpdateCellsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>()))
            .Returns(Task.CompletedTask);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 第一個 Project，在 row 0 (標記列) 後插入，所以 insertRowIndex = 1
        _googleSheetServiceMock.Verify(
            x => x.InsertRowAsync(_googleSheetOptions.SpreadsheetId, 0, 1),
            Times.Once);

        // 驗證 UpdateCells 被呼叫（填入新列欄位值）
        _googleSheetServiceMock.Verify(
            x => x.UpdateCellsAsync(
                _googleSheetOptions.SpreadsheetId,
                _googleSheetOptions.SheetName,
                It.IsAny<IReadOnlyDictionary<string, string>>()),
            Times.AtLeastOnce);
    }

    /// <summary>
    /// 測試第一個 Project 區塊插入位置（在標記列後插入）
    /// </summary>
    [Fact]
    public void CalculateInsertRowIndex_FirstProject_ShouldInsertAfterRepoRow()
    {
        // Arrange
        var repoRows = new List<(int RowIndex, string ProjectName)>
        {
            (0, "repo1"),
            (5, "repo2")
        };

        // Act — 第一個 Project (repo1)：在 row 0 後插入 → row 1
        var insertIdx = GetInsertRowIndex("repo1", repoRows, 10);

        // Assert
        Assert.Equal(1, insertIdx);
    }

    /// <summary>
    /// 測試中間 Project 區塊插入位置（在下一個標記列前插入）
    /// </summary>
    [Fact]
    public void CalculateInsertRowIndex_MiddleProject_ShouldInsertBeforeNextRepoRow()
    {
        // Arrange
        var repoRows = new List<(int RowIndex, string ProjectName)>
        {
            (0, "repo1"),
            (5, "repo2"), // 這個是中間的
            (10, "repo3")
        };

        // Act — 中間 Project (repo2)：在下一個標記列 (row 10) 前插入 → row 10
        var insertIdx = GetInsertRowIndex("repo2", repoRows, 15);

        // Assert
        Assert.Equal(10, insertIdx);
    }

    /// <summary>
    /// 測試最後一個 Project 區塊插入位置（在標記列後插入）
    /// </summary>
    [Fact]
    public void CalculateInsertRowIndex_LastProject_ShouldInsertAfterRepoRow()
    {
        // Arrange
        var repoRows = new List<(int RowIndex, string ProjectName)>
        {
            (0, "repo1"),
            (5, "repo2")
        };

        // Act — 最後一個 Project (repo2)：在 row 5 後插入 → row 6
        var insertIdx = GetInsertRowIndex("repo2", repoRows, 8);

        // Assert
        Assert.Equal(6, insertIdx);
    }

    // ===== Task 6: 更新資料邏輯 =====

    /// <summary>
    /// 測試 UK 已存在時，僅更新 Authors 與 PullRequestUrls
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithExistingEntry_ShouldOnlyUpdateAuthorsAndPrUrls()
    {
        // Arrange
        var uk = "100repo1";
        var result = CreateConsolidatedResult("repo1",
            CreateEntry(100, "Feature A", "Team A", "https://dev.azure.com/wi/100",
                new[] { "Charlie", "Alice" }, new[] { "https://pr.com/2", "https://pr.com/1" }));
        SetupConsolidatedData(result);

        // Sheet 第 0 行有 repo1 標記，第 1 行有 UK=100repo1 的既有資料
        var sheetData = new List<IList<object>>
        {
            CreateRow("Z", "repo1"), // row 0: 標記列
            CreateRow("Y", uk, "B", "old feature", "W", "OldAuthor", "X", "https://old-pr.com") // row 1: 既有資料
        };
        _googleSheetServiceMock.Setup(x => x.GetSheetDataAsync(It.IsAny<string>(), It.IsAny<string>(), "A:Z"))
            .ReturnsAsync(sheetData);
        _googleSheetServiceMock.Setup(x => x.UpdateCellsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>()))
            .Returns(Task.CompletedTask);

        Dictionary<string, string>? capturedUpdates = null;
        _googleSheetServiceMock.Setup(x => x.UpdateCellsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>()))
            .Callback<string, string, IReadOnlyDictionary<string, string>>((_, _, updates) => capturedUpdates = new Dictionary<string, string>(updates))
            .Returns(Task.CompletedTask);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 不應插入新列
        _googleSheetServiceMock.Verify(
            x => x.InsertRowAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()),
            Times.Never);

        // Assert — 更新了 Authors 與 PullRequestUrls（已排序）
        Assert.NotNull(capturedUpdates);
        // Authors 應按字母排序：Alice, Charlie
        Assert.True(capturedUpdates.ContainsKey("W2"));
        Assert.Equal("Alice\nCharlie", capturedUpdates["W2"]);
        // PrUrls 應按字母排序：https://pr.com/1, https://pr.com/2
        Assert.True(capturedUpdates.ContainsKey("X2"));
        Assert.Equal("https://pr.com/1\nhttps://pr.com/2", capturedUpdates["X2"]);
    }

    // ===== Task 7: 排序邏輯 =====

    /// <summary>
    /// 測試 ColumnLetterToIndex 轉換正確性
    /// </summary>
    [Theory]
    [InlineData("A", 0)]
    [InlineData("B", 1)]
    [InlineData("Z", 25)]
    [InlineData("a", 0)]
    [InlineData("z", 25)]
    public void ColumnLetterToIndex_ShouldConvertCorrectly(string letter, int expectedIndex)
    {
        // Act
        var index = UpdateGoogleSheetsTask.ColumnLetterToIndex(letter);

        // Assert
        Assert.Equal(expectedIndex, index);
    }

    /// <summary>
    /// 測試 ColumnLetterToIndex 傳入多字母時拋出例外
    /// </summary>
    [Fact]
    public void ColumnLetterToIndex_WithMultipleLetters_ShouldThrowArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => UpdateGoogleSheetsTask.ColumnLetterToIndex("AA"));
        Assert.Throws<ArgumentException>(() => UpdateGoogleSheetsTask.ColumnLetterToIndex(""));
    }

    // ===== Task 2.3: IGoogleSheetService 介面可正確注入與呼叫 =====

    /// <summary>
    /// 測試 IGoogleSheetService 介面可正確注入並呼叫 GetSheetDataAsync
    /// </summary>
    [Fact]
    public async Task IGoogleSheetService_ShouldBeInjectableAndCallable()
    {
        // Arrange
        var mockService = new Mock<IGoogleSheetService>();
        mockService.Setup(x => x.GetSheetDataAsync("id", "sheet", "A:Z"))
            .ReturnsAsync(new List<IList<object>>());
        mockService.Setup(x => x.InsertRowAsync("id", 0, 5))
            .Returns(Task.CompletedTask);
        mockService.Setup(x => x.UpdateCellsAsync("id", "sheet", It.IsAny<IReadOnlyDictionary<string, string>>()))
            .Returns(Task.CompletedTask);

        // Act
        await mockService.Object.GetSheetDataAsync("id", "sheet", "A:Z");
        await mockService.Object.InsertRowAsync("id", 0, 5);
        await mockService.Object.UpdateCellsAsync("id", "sheet", new Dictionary<string, string> { ["A1"] = "test" });

        // Assert
        mockService.Verify(x => x.GetSheetDataAsync("id", "sheet", "A:Z"), Times.Once);
        mockService.Verify(x => x.InsertRowAsync("id", 0, 5), Times.Once);
        mockService.Verify(x => x.UpdateCellsAsync("id", "sheet", It.IsAny<IReadOnlyDictionary<string, string>>()), Times.Once);
    }

    // ===== 輔助方法 =====

    /// <summary>
    /// 建立包含指定欄位值的列（使用欄位字母指定位置）
    /// </summary>
    private static IList<object> CreateRow(params string[] columnValuePairs)
    {
        var row = new List<object>(new string[26].Select(_ => (object)""));
        for (int i = 0; i < columnValuePairs.Length - 1; i += 2)
        {
            var col = columnValuePairs[i];
            var value = columnValuePairs[i + 1];
            var idx = UpdateGoogleSheetsTask.ColumnLetterToIndex(col);
            while (row.Count <= idx) row.Add("");
            row[idx] = value;
        }
        return row;
    }

    /// <summary>
    /// 使用反射呼叫 CalculateInsertRowIndex（private static method）
    /// </summary>
    private static int GetInsertRowIndex(string projectName, List<(int RowIndex, string ProjectName)> repoRows, int totalRows)
    {
        var method = typeof(UpdateGoogleSheetsTask)
            .GetMethod("CalculateInsertRowIndex",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (int)method.Invoke(null, new object[] { projectName, repoRows, totalRows })!;
    }
}
