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

    public UpdateGoogleSheetsTaskTests()
    {
        _redisServiceMock = new Mock<IRedisService>();
        _googleSheetServiceMock = new Mock<IGoogleSheetService>();
        _loggerMock = new Mock<ILogger<UpdateGoogleSheetsTask>>();
        _options = new GoogleSheetOptions
        {
            SpreadsheetId = "test-spreadsheet-id",
            SheetId = 0,
            SheetName = "Sheet1",
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
            Options.Create(options ?? _options),
            _loggerMock.Object);
    }

    private void SetupRedisConsolidatedData(ConsolidatedReleaseResult? result)
    {
        _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.ReleaseDataHash, RedisKeys.Fields.Consolidated))
            .ReturnsAsync(result?.ToJson());
    }

    private void SetupSheetData(IList<IList<object>>? data)
    {
        _googleSheetServiceMock.Setup(x => x.GetSheetDataAsync("test-spreadsheet-id", "Sheet1", "A:Z"))
            .ReturnsAsync(data);
    }

    private static ConsolidatedReleaseResult CreateConsolidatedResult(
        params (string ProjectName, ConsolidatedReleaseEntry[] Entries)[] projects)
    {
        var dict = new Dictionary<string, List<ConsolidatedReleaseEntry>>();
        foreach (var (name, entries) in projects)
        {
            dict[name] = entries.ToList();
        }
        return new ConsolidatedReleaseResult { Projects = dict };
    }

    private static ConsolidatedReleaseEntry CreateEntry(
        int workItemId, string title = "Test Feature", string teamDisplayName = "TestTeam",
        string workItemUrl = "https://dev.azure.com/test",
        string[]? authors = null, string[]? prUrls = null)
    {
        return new ConsolidatedReleaseEntry
        {
            Title = title,
            WorkItemUrl = workItemUrl,
            WorkItemId = workItemId,
            TeamDisplayName = teamDisplayName,
            Authors = (authors ?? new[] { "Author1" })
                .Select(a => new ConsolidatedAuthorInfo { AuthorName = a })
                .ToList(),
            PullRequests = (prUrls ?? new[] { "https://example.com/pr/1" })
                .Select(u => new ConsolidatedPrInfo { Url = u })
                .ToList(),
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
        };
    }

    /// <summary>
    /// 建立模擬的 Sheet 資料（26 欄 A-Z）
    /// </summary>
    private static List<IList<object>> CreateSheetData(params object[][] rows)
    {
        var result = new List<IList<object>>();
        foreach (var row in rows)
        {
            var paddedRow = new object[26];
            Array.Copy(row, paddedRow, Math.Min(row.Length, 26));
            for (var i = row.Length; i < 26; i++)
            {
                paddedRow[i] = "";
            }
            result.Add(paddedRow.ToList());
        }
        return result;
    }

    /// <summary>
    /// 建立一個有 Project 標記的 row（在 Z 欄放 Project 名稱）
    /// </summary>
    private static object[] CreateProjectHeaderRow(string projectName)
    {
        var row = new object[26];
        for (var i = 0; i < 26; i++) row[i] = "";
        row[25] = projectName; // Z column = index 25
        return row;
    }

    /// <summary>
    /// 建立一個資料列（在指定欄位放值）
    /// </summary>
    private static object[] CreateDataRow(
        string feature = "", string team = "", string authors = "",
        string prUrls = "", string uniqueKey = "", string autoSync = "")
    {
        var row = new object[26];
        for (var i = 0; i < 26; i++) row[i] = "";
        row[1] = feature;   // B
        row[3] = team;      // D
        row[5] = autoSync;  // F
        row[22] = authors;  // W
        row[23] = prUrls;   // X
        row[24] = uniqueKey; // Y
        return row;
    }

    // ===== 4.2 Redis 無整合資料時優雅結束 =====

    /// <summary>
    /// 測試 Redis 無整合資料時，記錄 Warning 並正常結束
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithNoRedisData_ShouldLogWarningAndReturn()
    {
        // Arrange
        SetupRedisConsolidatedData(null);
        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert - 不應呼叫 Google Sheet
        _googleSheetServiceMock.Verify(
            x => x.GetSheetDataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    /// <summary>
    /// 測試 Redis 整合資料為空 Projects 時，記錄 Warning 並正常結束
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithEmptyProjects_ShouldLogWarningAndReturn()
    {
        // Arrange
        var result = new ConsolidatedReleaseResult
        {
            Projects = new Dictionary<string, List<ConsolidatedReleaseEntry>>()
        };
        SetupRedisConsolidatedData(result);
        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert - 不應呼叫 Google Sheet
        _googleSheetServiceMock.Verify(
            x => x.GetSheetDataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    // ===== 4.3 ColumnMapping 驗證 =====

    /// <summary>
    /// 測試 ColumnMapping 設定超過 A-Z 時拋出 InvalidOperationException
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithInvalidColumnMapping_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var invalidOptions = new GoogleSheetOptions
        {
            SpreadsheetId = "test",
            SheetName = "Sheet1",
            ColumnMapping = new ColumnMappingOptions
            {
                RepositoryNameColumn = "AA", // 超過 Z
                FeatureColumn = "B",
                TeamColumn = "D",
                AuthorsColumn = "W",
                PullRequestUrlsColumn = "X",
                UniqueKeyColumn = "Y",
                AutoSyncColumn = "F"
            }
        };

        SetupRedisConsolidatedData(CreateConsolidatedResult(
            ("repo1", new[] { CreateEntry(100) })));
        var task = CreateTask(invalidOptions);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => task.ExecuteAsync());
        Assert.Contains("RepositoryNameColumn", exception.Message);
        Assert.Contains("不在 A-Z 範圍內", exception.Message);
    }

    // ===== 4.4 Google Sheet 讀取失敗 =====

    /// <summary>
    /// 測試 Google Sheet 讀取失敗時記錄 Warning 並結束
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithNullSheetData_ShouldLogWarningAndReturn()
    {
        // Arrange
        SetupRedisConsolidatedData(CreateConsolidatedResult(
            ("repo1", new[] { CreateEntry(100) })));
        SetupSheetData(null);
        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert - 不應呼叫 InsertRow 或 UpdateCells
        _googleSheetServiceMock.Verify(
            x => x.InsertRowAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()),
            Times.Never);
    }

    // ===== 4.5 ParseProjectBlocks =====

    /// <summary>
    /// 測試 ParseProjectBlocks 正確解析 RepositoryNameColumn 的 Project 區塊
    /// </summary>
    [Fact]
    public void ParseProjectBlocks_ShouldReturnCorrectBlocks()
    {
        // Arrange
        var sheetData = CreateSheetData(
            CreateProjectHeaderRow("repo1"),
            CreateDataRow(uniqueKey: "100repo1"),
            CreateDataRow(uniqueKey: "200repo1"),
            CreateProjectHeaderRow("repo2"),
            CreateDataRow(uniqueKey: "300repo2"));

        // Act
        var blocks = UpdateGoogleSheetsTask.ParseProjectBlocks(sheetData, 25); // Z = index 25

        // Assert
        Assert.Equal(2, blocks.Count);
        Assert.Equal((0, "repo1"), blocks[0]);
        Assert.Equal((3, "repo2"), blocks[1]);
    }

    // ===== 4.6 ParseUniqueKeys =====

    /// <summary>
    /// 測試 ParseUniqueKeys 正確解析 UniqueKeyColumn 的既有 UK 值與 row index
    /// </summary>
    [Fact]
    public void ParseUniqueKeys_ShouldReturnCorrectMapping()
    {
        // Arrange
        var sheetData = CreateSheetData(
            CreateProjectHeaderRow("repo1"),
            CreateDataRow(uniqueKey: "100repo1"),
            CreateDataRow(uniqueKey: "200repo1"),
            CreateProjectHeaderRow("repo2"),
            CreateDataRow(uniqueKey: "300repo2"));

        // Act
        var keys = UpdateGoogleSheetsTask.ParseUniqueKeys(sheetData, 24); // Y = index 24

        // Assert
        Assert.Equal(3, keys.Count);
        Assert.Equal(1, keys["100repo1"]);
        Assert.Equal(2, keys["200repo1"]);
        Assert.Equal(4, keys["300repo2"]);
    }

    // ===== 5.1-5.4 新增資料邏輯 =====

    /// <summary>
    /// 測試第一個 Project 區塊的新增列定位（往後插入）
    /// </summary>
    [Fact]
    public void CalculateInsertRowIndex_ForFirstProject_ShouldInsertAfterHeader()
    {
        // Arrange
        var blocks = new List<(int RowIndex, string ProjectName)>
        {
            (0, "repo1"),
            (5, "repo2")
        };

        // Act
        var result = UpdateGoogleSheetsTask.CalculateInsertRowIndex(blocks, "repo1", 0);

        // Assert - 第一個 Project 在 header 下一行插入
        Assert.Equal(1, result);
    }

    /// <summary>
    /// 測試中間 Project 區塊的新增列定位（在下一個 Project 前插入）
    /// </summary>
    [Fact]
    public void CalculateInsertRowIndex_ForMiddleProject_ShouldInsertBeforeNextProject()
    {
        // Arrange
        var blocks = new List<(int RowIndex, string ProjectName)>
        {
            (0, "repo1"),
            (5, "repo2"),
            (10, "repo3")
        };

        // Act
        var result = UpdateGoogleSheetsTask.CalculateInsertRowIndex(blocks, "repo2", 0);

        // Assert - 在下一個 RepositoryNameColumn 之前
        Assert.Equal(10, result);
    }

    /// <summary>
    /// 測試最後一個 Project 區塊的新增列定位（往後插入）
    /// </summary>
    [Fact]
    public void CalculateInsertRowIndex_ForLastProject_ShouldInsertAfterHeader()
    {
        // Arrange
        var blocks = new List<(int RowIndex, string ProjectName)>
        {
            (0, "repo1"),
            (5, "repo2"),
            (10, "repo3")
        };

        // Act
        var result = UpdateGoogleSheetsTask.CalculateInsertRowIndex(blocks, "repo3", 0);

        // Assert - 最後一個 Project 在 header 下一行插入
        Assert.Equal(11, result);
    }

    /// <summary>
    /// 測試找不到 Project 時回傳 -1
    /// </summary>
    [Fact]
    public void CalculateInsertRowIndex_ForUnknownProject_ShouldReturnNegative()
    {
        // Arrange
        var blocks = new List<(int RowIndex, string ProjectName)> { (0, "repo1") };

        // Act
        var result = UpdateGoogleSheetsTask.CalculateInsertRowIndex(blocks, "unknown", 0);

        // Assert
        Assert.Equal(-1, result);
    }

    /// <summary>
    /// 測試新增資料時正確呼叫 InsertRow 與 UpdateCells
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithNewData_ShouldInsertRowAndFillValues()
    {
        // Arrange
        var entry = CreateEntry(100, "Login Feature", "金流團隊",
            "https://dev.azure.com/org/proj/_workitems/edit/100",
            new[] { "John", "Alice" },
            new[] { "https://gitlab.com/pr/1", "https://gitlab.com/pr/2" });

        SetupRedisConsolidatedData(CreateConsolidatedResult(
            ("repo1", new[] { entry })));

        var sheetData = CreateSheetData(
            CreateProjectHeaderRow("repo1"),
            CreateDataRow(uniqueKey: "200repo1"));

        SetupSheetData(sheetData);

        // 排序用的第二次讀取
        _googleSheetServiceMock.SetupSequence(x => x.GetSheetDataAsync("test-spreadsheet-id", "Sheet1", "A:Z"))
            .ReturnsAsync(sheetData)
            .ReturnsAsync(sheetData); // 排序階段重新讀取

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert - 應呼叫 InsertRow
        _googleSheetServiceMock.Verify(
            x => x.InsertRowAsync("test-spreadsheet-id", 0, It.IsAny<int>()),
            Times.Once);

        // Assert - 應呼叫 UpdateCells 填入欄位值（含 AutoSync = TRUE）
        _googleSheetServiceMock.Verify(
            x => x.UpdateCellsAsync("test-spreadsheet-id", "Sheet1", It.Is<IDictionary<string, object>>(
                d => d.Values.Any(v => v.ToString() == "TRUE"))),
            Times.AtLeastOnce);
    }

    // ===== 6.1-6.2 更新資料邏輯 =====

    /// <summary>
    /// 測試更新資料時僅更新 Authors 與 PullRequestUrls，不呼叫 InsertRow
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithExistingData_ShouldUpdateOnlyAuthorsAndPrUrls()
    {
        // Arrange
        var entry = CreateEntry(100, "Login Feature", "金流團隊",
            authors: new[] { "John", "Alice" },
            prUrls: new[] { "https://gitlab.com/pr/1" });

        SetupRedisConsolidatedData(CreateConsolidatedResult(
            ("repo1", new[] { entry })));

        var sheetData = CreateSheetData(
            CreateProjectHeaderRow("repo1"),
            CreateDataRow(feature: "existing", team: "金流團隊",
                authors: "John", prUrls: "https://old.com",
                uniqueKey: "100repo1", autoSync: "TRUE"));

        SetupSheetData(sheetData);

        // 排序用的第二次讀取
        _googleSheetServiceMock.SetupSequence(x => x.GetSheetDataAsync("test-spreadsheet-id", "Sheet1", "A:Z"))
            .ReturnsAsync(sheetData)
            .ReturnsAsync(sheetData);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert - 不應呼叫 InsertRow
        _googleSheetServiceMock.Verify(
            x => x.InsertRowAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()),
            Times.Never);

        // Assert - 應呼叫 UpdateCells，且僅包含 Authors (W) 和 PullRequestUrls (X) 欄位
        _googleSheetServiceMock.Verify(
            x => x.UpdateCellsAsync("test-spreadsheet-id", "Sheet1", It.Is<IDictionary<string, object>>(
                d => d.Count == 2 && d.ContainsKey("W2") && d.ContainsKey("X2"))),
            Times.Once);
    }

    // ===== 7.2 排序邏輯 =====

    /// <summary>
    /// 測試 FormatAuthors 排序 + 換行分隔
    /// </summary>
    [Fact]
    public void FormatAuthors_ShouldSortAndJoinWithNewline()
    {
        // Arrange
        var authors = new List<ConsolidatedAuthorInfo>
        {
            new() { AuthorName = "Charlie" },
            new() { AuthorName = "Alice" },
            new() { AuthorName = "Bob" }
        };

        // Act
        var result = UpdateGoogleSheetsTask.FormatAuthors(authors);

        // Assert
        Assert.Equal("Alice\nBob\nCharlie", result);
    }

    /// <summary>
    /// 測試 FormatPullRequestUrls 排序 + 換行分隔
    /// </summary>
    [Fact]
    public void FormatPullRequestUrls_ShouldSortAndJoinWithNewline()
    {
        // Arrange
        var prs = new List<ConsolidatedPrInfo>
        {
            new() { Url = "https://gitlab.com/pr/3" },
            new() { Url = "https://gitlab.com/pr/1" },
            new() { Url = "https://gitlab.com/pr/2" }
        };

        // Act
        var result = UpdateGoogleSheetsTask.FormatPullRequestUrls(prs);

        // Assert
        Assert.Equal("https://gitlab.com/pr/1\nhttps://gitlab.com/pr/2\nhttps://gitlab.com/pr/3", result);
    }

    /// <summary>
    /// 測試 FormatFeatureWithHyperlink 產生正確的 HYPERLINK 公式
    /// </summary>
    [Fact]
    public void FormatFeatureWithHyperlink_ShouldReturnCorrectFormula()
    {
        // Arrange
        var entry = CreateEntry(12345, "Add Login Feature",
            workItemUrl: "https://dev.azure.com/org/proj/_workitems/edit/12345");

        // Act
        var result = UpdateGoogleSheetsTask.FormatFeatureWithHyperlink(entry);

        // Assert
        Assert.Equal(
            "=HYPERLINK(\"https://dev.azure.com/org/proj/_workitems/edit/12345\", \"VSTS12345 - Add Login Feature\")",
            result);
    }

    /// <summary>
    /// 測試 FormatFeatureWithHyperlink 在無 URL 時回傳純文字
    /// </summary>
    [Fact]
    public void FormatFeatureWithHyperlink_WithNoUrl_ShouldReturnPlainText()
    {
        // Arrange
        var entry = CreateEntry(12345, "Add Login Feature", workItemUrl: "");

        // Act
        var result = UpdateGoogleSheetsTask.FormatFeatureWithHyperlink(entry);

        // Assert
        Assert.Equal("VSTS12345 - Add Login Feature", result);
    }

    /// <summary>
    /// 測試 ColumnLetterToIndex 轉換
    /// </summary>
    [Theory]
    [InlineData("A", 0)]
    [InlineData("B", 1)]
    [InlineData("Z", 25)]
    public void ColumnLetterToIndex_ShouldConvertCorrectly(string letter, int expected)
    {
        Assert.Equal(expected, UpdateGoogleSheetsTask.ColumnLetterToIndex(letter));
    }

    /// <summary>
    /// 測試 ValidateColumnMapping 有效設定不拋錯
    /// </summary>
    [Fact]
    public void ValidateColumnMapping_WithValidMapping_ShouldNotThrow()
    {
        // Arrange
        var mapping = new ColumnMappingOptions
        {
            RepositoryNameColumn = "Z",
            FeatureColumn = "B",
            TeamColumn = "D",
            AuthorsColumn = "W",
            PullRequestUrlsColumn = "X",
            UniqueKeyColumn = "Y",
            AutoSyncColumn = "F"
        };

        // Act & Assert - 不應拋出例外
        UpdateGoogleSheetsTask.ValidateColumnMapping(mapping);
    }

    /// <summary>
    /// 測試 ValidateColumnMapping 無效設定拋出 InvalidOperationException
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("AA")]
    [InlineData("a")]
    [InlineData("1")]
    public void ValidateColumnMapping_WithInvalidValue_ShouldThrow(string invalidValue)
    {
        // Arrange
        var mapping = new ColumnMappingOptions
        {
            RepositoryNameColumn = invalidValue,
            FeatureColumn = "B",
            TeamColumn = "D",
            AuthorsColumn = "W",
            PullRequestUrlsColumn = "X",
            UniqueKeyColumn = "Y",
            AutoSyncColumn = "F"
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(
            () => UpdateGoogleSheetsTask.ValidateColumnMapping(mapping));
    }

    /// <summary>
    /// 測試 CalculateInsertRowIndex 考慮 insertOffset
    /// </summary>
    [Fact]
    public void CalculateInsertRowIndex_WithOffset_ShouldApplyOffset()
    {
        // Arrange
        var blocks = new List<(int RowIndex, string ProjectName)>
        {
            (0, "repo1"),
            (5, "repo2")
        };

        // Act - 已有 2 筆插入的偏移
        var result = UpdateGoogleSheetsTask.CalculateInsertRowIndex(blocks, "repo1", 2);

        // Assert
        Assert.Equal(3, result); // 0 + 1 + 2 offset
    }
}
