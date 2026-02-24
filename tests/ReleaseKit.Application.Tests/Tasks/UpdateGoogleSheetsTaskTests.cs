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
/// UpdateGoogleSheetsTask 單元測試
/// </summary>
public class UpdateGoogleSheetsTaskTests
{
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly Mock<IGoogleSheetService> _googleSheetServiceMock;
    private readonly Mock<ILogger<UpdateGoogleSheetsTask>> _loggerMock;
    private readonly GoogleSheetOptions _defaultOptions;

    public UpdateGoogleSheetsTaskTests()
    {
        _redisServiceMock = new Mock<IRedisService>();
        _googleSheetServiceMock = new Mock<IGoogleSheetService>();
        _loggerMock = new Mock<ILogger<UpdateGoogleSheetsTask>>();

        _defaultOptions = new GoogleSheetOptions
        {
            SpreadsheetId = "test-spreadsheet-id",
            SheetName = "Release Notes",
            ServiceAccountCredentialPath = "/path/to/creds.json",
            ColumnMapping = new ColumnMappingOptions
            {
                RepositoryNameColumn = "Z",
                FeatureColumn = "B",
                TeamColumn = "D",
                AuthorsColumn = "E",
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
            Options.Create(options ?? _defaultOptions),
            _loggerMock.Object);
    }

    private void SetupRedisConsolidatedData(ConsolidatedReleaseResult? result)
    {
        _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.ReleaseDataHash, RedisKeys.Fields.Consolidated))
            .ReturnsAsync(result?.ToJson());
    }

    private void SetupSheetId(int? sheetId)
    {
        _googleSheetServiceMock.Setup(x => x.GetSheetIdByNameAsync(
                _defaultOptions.SpreadsheetId, _defaultOptions.SheetName))
            .ReturnsAsync(sheetId);
    }

    private void SetupSheetData(IList<IList<object>>? data)
    {
        _googleSheetServiceMock.Setup(x => x.GetSheetDataAsync(
                _defaultOptions.SpreadsheetId, It.IsAny<string>()))
            .ReturnsAsync(data);
        _googleSheetServiceMock.Setup(x => x.GetSheetDataWithFormulasAsync(
                _defaultOptions.SpreadsheetId, It.IsAny<string>()))
            .ReturnsAsync(data);
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
        int workItemId, string title = "Test Title",
        string teamDisplayName = "測試團隊",
        string workItemUrl = "https://dev.azure.com/org/proj/_workitems/edit/123",
        List<ConsolidatedAuthorInfo>? authors = null,
        List<ConsolidatedPrInfo>? pullRequests = null)
    {
        return new ConsolidatedReleaseEntry
        {
            WorkItemId = workItemId,
            Title = title,
            TeamDisplayName = teamDisplayName,
            WorkItemUrl = workItemUrl,
            Authors = authors ?? new List<ConsolidatedAuthorInfo>
            {
                new() { AuthorName = "Author1" }
            },
            PullRequests = pullRequests ?? new List<ConsolidatedPrInfo>
            {
                new() { Url = "https://gitlab.com/pr/1" }
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
        };
    }

    /// <summary>
    /// 建立模擬的 Sheet 資料，包含專案區段
    /// </summary>
    private static IList<IList<object>> CreateSheetData(
        params (string? ProjectName, string? UniqueKey)[] rows)
    {
        var data = new List<IList<object>>();
        foreach (var (projectName, uniqueKey) in rows)
        {
            // 建立一個 26 欄的列（A-Z）
            var row = new List<object>(new object[26]);
            if (!string.IsNullOrEmpty(projectName))
            {
                row[25] = projectName; // Z 欄 = index 25 (RepositoryNameColumn)
            }
            if (!string.IsNullOrEmpty(uniqueKey))
            {
                row[24] = uniqueKey; // Y 欄 = index 24 (UniqueKeyColumn)
            }
            data.Add(row);
        }
        return data;
    }

    // ===== T008: 讀取 Redis 整合資料並反序列化 =====

    /// <summary>
    /// T008: 測試讀取 Redis 整合資料並反序列化為 ConsolidatedReleaseResult
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldReadRedisConsolidatedData()
    {
        // Arrange
        var result = CreateConsolidatedResult(
            ("my-repo", new[] { CreateEntry(12345) }));
        SetupRedisConsolidatedData(result);
        SetupSheetId(0);

        var sheetData = CreateSheetData(
            ("my-repo", null),
            (null, null));
        SetupSheetData(sheetData);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _redisServiceMock.Verify(
            x => x.HashGetAsync(RedisKeys.ReleaseDataHash, RedisKeys.Fields.Consolidated),
            Times.Once);
    }

    // ===== T009: 從 Sheet RepositoryNameColumn 建立專案區段索引 =====

    /// <summary>
    /// T009: 測試僅一個專案時，資料範圍到 Sheet 末尾
    /// </summary>
    [Fact]
    public void BuildProjectSegments_SingleProject_DataRangeToSheetEnd()
    {
        // Arrange
        var sheetData = CreateSheetData(
            ("my-repo", null),    // row 0: header
            (null, "123my-repo"), // row 1: data
            (null, "456my-repo")  // row 2: data
        );

        // Act
        var segments = UpdateGoogleSheetsTask.BuildProjectSegments(sheetData, 25);

        // Assert
        Assert.Single(segments);
        Assert.Equal("my-repo", segments[0].ProjectName);
        Assert.Equal(0, segments[0].HeaderRowIndex);
        Assert.Equal(1, segments[0].DataStartRowIndex);
        Assert.Equal(2, segments[0].DataEndRowIndex);
    }

    /// <summary>
    /// T009: 測試多個專案時，相鄰 header 之間
    /// </summary>
    [Fact]
    public void BuildProjectSegments_MultipleProjects_AdjacentHeaders()
    {
        // Arrange
        var sheetData = CreateSheetData(
            ("project-a", null),      // row 0: header A
            (null, "100project-a"),    // row 1: data A
            (null, "200project-a"),    // row 2: data A
            ("project-b", null),      // row 3: header B
            (null, "300project-b")    // row 4: data B
        );

        // Act
        var segments = UpdateGoogleSheetsTask.BuildProjectSegments(sheetData, 25);

        // Assert
        Assert.Equal(2, segments.Count);

        Assert.Equal("project-a", segments[0].ProjectName);
        Assert.Equal(0, segments[0].HeaderRowIndex);
        Assert.Equal(1, segments[0].DataStartRowIndex);
        Assert.Equal(2, segments[0].DataEndRowIndex);

        Assert.Equal("project-b", segments[1].ProjectName);
        Assert.Equal(3, segments[1].HeaderRowIndex);
        Assert.Equal(4, segments[1].DataStartRowIndex);
        Assert.Equal(4, segments[1].DataEndRowIndex);
    }

    // ===== T010: UniqueKey Insert/Update 分類 =====

    /// <summary>
    /// T010: 測試新 UniqueKey 判定為 Insert、既有 UniqueKey 判定為 Update
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldClassifyInsertAndUpdate()
    {
        // Arrange
        var result = CreateConsolidatedResult(
            ("my-repo", new[]
            {
                CreateEntry(100), // new
                CreateEntry(200)  // existing
            }));
        SetupRedisConsolidatedData(result);
        SetupSheetId(0);

        var sheetData = CreateSheetData(
            ("my-repo", null),          // row 0: header
            (null, "200my-repo")        // row 1: existing entry with key 200my-repo
        );
        SetupSheetData(sheetData);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert - InsertRows called for 1 new entry
        _googleSheetServiceMock.Verify(
            x => x.InsertRowsAsync(_defaultOptions.SpreadsheetId, 0, 1, 1),
            Times.Once);
    }

    // ===== T011: 批次插入空白列的位置計算 =====

    /// <summary>
    /// T011: 測試首筆專案的新列插入在 headerRow+1
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_InsertPosition_ShouldBeAfterHeader()
    {
        // Arrange
        var result = CreateConsolidatedResult(
            ("my-repo", new[] { CreateEntry(100) }));
        SetupRedisConsolidatedData(result);
        SetupSheetId(0);

        var sheetData = CreateSheetData(
            ("my-repo", null) // row 0: header, no data rows
        );
        SetupSheetData(sheetData);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert - insert at row 1 (headerRow + 1)
        _googleSheetServiceMock.Verify(
            x => x.InsertRowsAsync(_defaultOptions.SpreadsheetId, 0, 1, 1),
            Times.Once);
    }

    /// <summary>
    /// T011: 測試多筆同專案新增時插入數量正確
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_MultipleInserts_ShouldInsertCorrectCount()
    {
        // Arrange
        var result = CreateConsolidatedResult(
            ("my-repo", new[]
            {
                CreateEntry(100),
                CreateEntry(200),
                CreateEntry(300)
            }));
        SetupRedisConsolidatedData(result);
        SetupSheetId(0);

        var sheetData = CreateSheetData(
            ("my-repo", null) // row 0: header only
        );
        SetupSheetData(sheetData);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert - 3 rows inserted at once
        _googleSheetServiceMock.Verify(
            x => x.InsertRowsAsync(_defaultOptions.SpreadsheetId, 0, 1, 3),
            Times.Once);
    }

    // ===== T012: 新增列資料填入格式 =====

    /// <summary>
    /// T012: 測試 FeatureColumn 含超連結
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_NewRow_FeatureColumnShouldHaveHyperlink()
    {
        // Arrange
        var entry = CreateEntry(12345, "Login Feature",
            workItemUrl: "https://dev.azure.com/org/proj/_workitems/edit/12345");
        var result = CreateConsolidatedResult(
            ("my-repo", new[] { entry }));
        SetupRedisConsolidatedData(result);
        SetupSheetId(0);

        var sheetData = CreateSheetData(("my-repo", null));
        SetupSheetData(sheetData);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert - HYPERLINK 公式應以 '=' 開頭加入 BatchUpdateCellsAsync
        _googleSheetServiceMock.Verify(
            x => x.BatchUpdateCellsAsync(
                _defaultOptions.SpreadsheetId,
                It.Is<IList<(string Range, IList<IList<object>> Values)>>(updates =>
                    updates.Any(u =>
                        u.Range.Contains("B") &&
                        u.Values[0][0].ToString()!.StartsWith("=HYPERLINK(") &&
                        u.Values[0][0].ToString()!.Contains("VSTS12345 - Login Feature") &&
                        u.Values[0][0].ToString()!.Contains("https://dev.azure.com/org/proj/_workitems/edit/12345")))),
            Times.Once);
        _googleSheetServiceMock.Verify(
            x => x.UpdateCellWithHyperlinkAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    /// <summary>
    /// T012: 測試 AuthorsColumn 排序後換行分隔
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_NewRow_AuthorsShouldBeSortedAndNewlineSeparated()
    {
        // Arrange
        var entry = CreateEntry(100, authors: new List<ConsolidatedAuthorInfo>
        {
            new() { AuthorName = "Charlie" },
            new() { AuthorName = "Alice" },
            new() { AuthorName = "Bob" }
        });
        var result = CreateConsolidatedResult(("my-repo", new[] { entry }));
        SetupRedisConsolidatedData(result);
        SetupSheetId(0);

        var sheetData = CreateSheetData(("my-repo", null));
        SetupSheetData(sheetData);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert - BatchUpdateCellsAsync should include sorted authors
        _googleSheetServiceMock.Verify(
            x => x.BatchUpdateCellsAsync(
                _defaultOptions.SpreadsheetId,
                It.Is<IList<(string Range, IList<IList<object>> Values)>>(updates =>
                    updates.Any(u =>
                        u.Range.Contains("E") &&
                        u.Values[0][0].ToString() == "Alice\nBob\nCharlie"))),
            Times.Once);
    }

    /// <summary>
    /// T012: 測試 PullRequestUrlsColumn 排序後換行分隔
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_NewRow_PrUrlsShouldBeSortedAndNewlineSeparated()
    {
        // Arrange
        var entry = CreateEntry(100, pullRequests: new List<ConsolidatedPrInfo>
        {
            new() { Url = "https://gitlab.com/pr/3" },
            new() { Url = "https://gitlab.com/pr/1" },
            new() { Url = "https://gitlab.com/pr/2" }
        });
        var result = CreateConsolidatedResult(("my-repo", new[] { entry }));
        SetupRedisConsolidatedData(result);
        SetupSheetId(0);

        var sheetData = CreateSheetData(("my-repo", null));
        SetupSheetData(sheetData);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _googleSheetServiceMock.Verify(
            x => x.BatchUpdateCellsAsync(
                _defaultOptions.SpreadsheetId,
                It.Is<IList<(string Range, IList<IList<object>> Values)>>(updates =>
                    updates.Any(u =>
                        u.Range.Contains("X") &&
                        u.Values[0][0].ToString() == "https://gitlab.com/pr/1\nhttps://gitlab.com/pr/2\nhttps://gitlab.com/pr/3"))),
            Times.Once);
    }

    /// <summary>
    /// T012: 測試 UniqueKeyColumn 格式為 {workItemId}{projectName}
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_NewRow_UniqueKeyShouldBeCorrectFormat()
    {
        // Arrange
        var entry = CreateEntry(12345);
        var result = CreateConsolidatedResult(("my-repo", new[] { entry }));
        SetupRedisConsolidatedData(result);
        SetupSheetId(0);

        var sheetData = CreateSheetData(("my-repo", null));
        SetupSheetData(sheetData);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _googleSheetServiceMock.Verify(
            x => x.BatchUpdateCellsAsync(
                _defaultOptions.SpreadsheetId,
                It.Is<IList<(string Range, IList<IList<object>> Values)>>(updates =>
                    updates.Any(u =>
                        u.Range.Contains("Y") &&
                        u.Values[0][0].ToString() == "12345my-repo"))),
            Times.Once);
    }

    /// <summary>
    /// T012: 測試 AutoSyncColumn 為 TRUE
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_NewRow_AutoSyncShouldBeTrue()
    {
        // Arrange
        var entry = CreateEntry(100);
        var result = CreateConsolidatedResult(("my-repo", new[] { entry }));
        SetupRedisConsolidatedData(result);
        SetupSheetId(0);

        var sheetData = CreateSheetData(("my-repo", null));
        SetupSheetData(sheetData);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _googleSheetServiceMock.Verify(
            x => x.BatchUpdateCellsAsync(
                _defaultOptions.SpreadsheetId,
                It.Is<IList<(string Range, IList<IList<object>> Values)>>(updates =>
                    updates.Any(u =>
                        u.Range.Contains("F") &&
                        u.Values[0][0].ToString() == "TRUE"))),
            Times.Once);
    }

    /// <summary>
    /// T012: 測試空 Authors 時欄位留空
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_NewRow_EmptyAuthors_ShouldBeEmptyString()
    {
        // Arrange
        var entry = CreateEntry(100, authors: new List<ConsolidatedAuthorInfo>());
        var result = CreateConsolidatedResult(("my-repo", new[] { entry }));
        SetupRedisConsolidatedData(result);
        SetupSheetId(0);

        var sheetData = CreateSheetData(("my-repo", null));
        SetupSheetData(sheetData);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _googleSheetServiceMock.Verify(
            x => x.BatchUpdateCellsAsync(
                _defaultOptions.SpreadsheetId,
                It.Is<IList<(string Range, IList<IList<object>> Values)>>(updates =>
                    updates.Any(u =>
                        u.Range.Contains("E") &&
                        u.Values[0][0].ToString() == ""))),
            Times.Once);
    }

    // ===== T013: 更新既有列僅更新 AuthorsColumn 與 PullRequestUrlsColumn =====

    /// <summary>
    /// T013: 測試更新既有列僅更新 Authors 與 PRUrls
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ExistingRow_ShouldOnlyUpdateAuthorsAndPrUrls()
    {
        // Arrange
        var entry = CreateEntry(200, authors: new List<ConsolidatedAuthorInfo>
        {
            new() { AuthorName = "NewAuthor" }
        }, pullRequests: new List<ConsolidatedPrInfo>
        {
            new() { Url = "https://gitlab.com/pr/new" }
        });
        var result = CreateConsolidatedResult(("my-repo", new[] { entry }));
        SetupRedisConsolidatedData(result);
        SetupSheetId(0);

        var sheetData = CreateSheetData(
            ("my-repo", null),          // row 0: header
            (null, "200my-repo")        // row 1: existing data
        );
        SetupSheetData(sheetData);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert - no InsertRows called
        _googleSheetServiceMock.Verify(
            x => x.InsertRowsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()),
            Times.Never);

        // BatchUpdate includes only E (Authors) and X (PRUrls) for the update
        _googleSheetServiceMock.Verify(
            x => x.BatchUpdateCellsAsync(
                _defaultOptions.SpreadsheetId,
                It.Is<IList<(string Range, IList<IList<object>> Values)>>(updates =>
                    updates.Count == 2 &&
                    updates.Any(u => u.Range.Contains("E")) &&
                    updates.Any(u => u.Range.Contains("X")))),
            Times.Once);

        // No hyperlink update
        _googleSheetServiceMock.Verify(
            x => x.UpdateCellWithHyperlinkAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    /// <summary>
    /// T013: 測試更新既有列的 Authors 依 authorName 排序
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ExistingRow_AuthorsShouldBeSorted()
    {
        // Arrange
        var entry = CreateEntry(200, authors: new List<ConsolidatedAuthorInfo>
        {
            new() { AuthorName = "Zoe" },
            new() { AuthorName = "Alice" }
        });
        var result = CreateConsolidatedResult(("my-repo", new[] { entry }));
        SetupRedisConsolidatedData(result);
        SetupSheetId(0);

        var sheetData = CreateSheetData(
            ("my-repo", null),
            (null, "200my-repo")
        );
        SetupSheetData(sheetData);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _googleSheetServiceMock.Verify(
            x => x.BatchUpdateCellsAsync(
                _defaultOptions.SpreadsheetId,
                It.Is<IList<(string Range, IList<IList<object>> Values)>>(updates =>
                    updates.Any(u =>
                        u.Range.Contains("E") &&
                        u.Values[0][0].ToString() == "Alice\nZoe"))),
            Times.Once);
    }

    // ===== T014: 專案區段排序 =====

    /// <summary>
    /// T014: 測試排序範圍為 headerRow+1 到 nextHeaderRow-1
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_Sort_ShouldUseCorrectRange()
    {
        // Arrange
        var entry = CreateEntry(100);
        var result = CreateConsolidatedResult(("my-repo", new[] { entry }));
        SetupRedisConsolidatedData(result);
        SetupSheetId(0);

        var sheetData = CreateSheetData(
            ("my-repo", null),          // row 0: header
            (null, null),               // row 1: data (will be filled after insert)
            ("other-repo", null)        // row 2: next header
        );
        SetupSheetData(sheetData);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert - 排序應在記憶體中完成後以 BatchUpdateCellsAsync 寫回，不呼叫 SortRangeAsync
        _googleSheetServiceMock.Verify(
            x => x.SortRangeAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<IList<(int ColumnIndex, bool Ascending)>>()),
            Times.Never);
        // 排序後回寫的範圍應涵蓋 my-repo 區段的資料列（0-based row 1 → 1-based A2:Z2）
        _googleSheetServiceMock.Verify(
            x => x.BatchUpdateCellsAsync(
                _defaultOptions.SpreadsheetId,
                It.Is<IList<(string Range, IList<IList<object>> Values)>>(updates =>
                    updates.Any(u => u.Range == $"'{_defaultOptions.SheetName}'!A2:Z2"))),
            Times.AtLeastOnce);
    }

    /// <summary>
    /// T014: 測試排序時 feature 有填寫的列依序排列，feature 為空白的列排在最後面
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_Sort_EmptyFieldsShouldBeSortedLast()
    {
        // Arrange - 3 個現有列（update 路徑），確保專案被加入 affectedProjects 而觸發排序
        var result = CreateConsolidatedResult(
            ("test-repo", new[]
            {
                CreateEntry(100),  // UniqueKey = "100test-repo"
                CreateEntry(200),  // UniqueKey = "200test-repo"
                CreateEntry(300),  // UniqueKey = "300test-repo"
            }));
        SetupRedisConsolidatedData(result);
        SetupSheetId(0);

        // 建立 Sheet 資料：header + 3 個資料列
        // row1: Team Z, Feature 有填寫 → feature 有填寫，Team Z 排在 Team A 後面，故排第二
        // row2: Team A, Feature 空白   → feature 未填寫，直接排在最後面（不參與排序）
        // row3: Team A, Feature 有填寫 → feature 有填寫，Team A 排在最前面，故排第一
        var sheetData = new List<IList<object>>();

        var headerRow = new List<object>(new object[26]);
        headerRow[25] = "test-repo"; // Z 欄 = RepositoryNameColumn
        sheetData.Add(headerRow);

        var row1 = new List<object>(new object[26]);
        row1[1] = "Feature Z";        // B 欄 = FeatureColumn
        row1[3] = "Team Z";           // D 欄 = TeamColumn
        row1[24] = "100test-repo";    // Y 欄 = UniqueKeyColumn
        sheetData.Add(row1);

        var row2 = new List<object>(new object[26]);
        row2[1] = string.Empty;       // B 欄 = FeatureColumn（空白）→ 排到最後
        row2[3] = "Team A";           // D 欄 = TeamColumn
        row2[24] = "200test-repo";    // Y 欄 = UniqueKeyColumn
        sheetData.Add(row2);

        var row3 = new List<object>(new object[26]);
        row3[1] = "Feature A";        // B 欄 = FeatureColumn
        row3[3] = "Team A";           // D 欄 = TeamColumn
        row3[24] = "300test-repo";    // Y 欄 = UniqueKeyColumn
        sheetData.Add(row3);

        SetupSheetData(sheetData);

        IList<IList<object>>? capturedSortedRows = null;
        _googleSheetServiceMock
            .Setup(x => x.BatchUpdateCellsAsync(It.IsAny<string>(), It.IsAny<IList<(string, IList<IList<object>>)>>()))
            .Callback<string, IList<(string, IList<IList<object>>)>>((_, updates) =>
            {
                // 排序的 BatchUpdate 範圍涵蓋 3 個資料列（A2:Z4）
                var sortUpdate = updates.FirstOrDefault(u => u.Item1 == $"'{_defaultOptions.SheetName}'!A2:Z4");
                if (sortUpdate.Item2 != null)
                    capturedSortedRows = sortUpdate.Item2;
            });

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert - feature 有填寫的列依 Team 排序在前，feature 空白的列排在最後面
        Assert.NotNull(capturedSortedRows);
        Assert.Equal(3, capturedSortedRows!.Count);
        // Feature 有填寫的列依 Team 排序（Team A 在 Team Z 前）
        Assert.Equal("Feature A", capturedSortedRows[0][1]?.ToString() ?? string.Empty);
        Assert.Equal("Feature Z", capturedSortedRows[1][1]?.ToString() ?? string.Empty);
        // Feature 空白的列排在最後面
        Assert.Equal(string.Empty, capturedSortedRows[2][1]?.ToString() ?? string.Empty);
    }

    /// <summary>
    /// T014: 排序後 HYPERLINK 公式應原樣保留，不應退化為純文字
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_Sort_ShouldPreserveHyperlinkFormulas()
    {
        // Arrange - 2 個現有列（update 路徑），Feature 欄位包含 HYPERLINK 公式
        var result = CreateConsolidatedResult(
            ("test-repo", new[]
            {
                CreateEntry(100),  // UniqueKey = "100test-repo"
                CreateEntry(200),  // UniqueKey = "200test-repo"
            }));
        SetupRedisConsolidatedData(result);
        SetupSheetId(0);

        var headerRow = new List<object>(new object[26]);
        headerRow[25] = "test-repo"; // Z 欄 = RepositoryNameColumn

        // row1: Team B, Feature 為 HYPERLINK 公式（排序後應原樣保留）
        var row1 = new List<object>(new object[26]);
        row1[1] = "=HYPERLINK(\"https://dev.azure.com/1\",\"VSTS100 - Feature B\")"; // B 欄 = FeatureColumn
        row1[3] = "Team B";        // D 欄 = TeamColumn
        row1[24] = "100test-repo"; // Y 欄 = UniqueKeyColumn
        // row2: Team A, Feature 為 HYPERLINK 公式（排序後應排在 row1 之前）
        var row2 = new List<object>(new object[26]);
        row2[1] = "=HYPERLINK(\"https://dev.azure.com/2\",\"VSTS200 - Feature A\")"; // B 欄 = FeatureColumn
        row2[3] = "Team A";        // D 欄 = TeamColumn
        row2[24] = "200test-repo"; // Y 欄 = UniqueKeyColumn

        var sheetData = new List<IList<object>> { headerRow, row1, row2 };

        // GetSheetDataAsync 回傳一般資料（初始讀取用）
        _googleSheetServiceMock.Setup(x => x.GetSheetDataAsync(
                _defaultOptions.SpreadsheetId, It.IsAny<string>()))
            .ReturnsAsync(sheetData);
        // GetSheetDataWithFormulasAsync 回傳含公式的資料（排序重讀用）
        _googleSheetServiceMock.Setup(x => x.GetSheetDataWithFormulasAsync(
                _defaultOptions.SpreadsheetId, It.IsAny<string>()))
            .ReturnsAsync(sheetData);
        _googleSheetServiceMock.Setup(x => x.GetSheetIdByNameAsync(
                _defaultOptions.SpreadsheetId, _defaultOptions.SheetName))
            .ReturnsAsync(0);

        IList<IList<object>>? capturedSortedRows = null;
        _googleSheetServiceMock
            .Setup(x => x.BatchUpdateCellsAsync(It.IsAny<string>(), It.IsAny<IList<(string, IList<IList<object>>)>>()))
            .Callback<string, IList<(string, IList<IList<object>>)>>((_, updates) =>
            {
                var sortUpdate = updates.FirstOrDefault(u => u.Item1 == $"'{_defaultOptions.SheetName}'!A2:Z3");
                if (sortUpdate.Item2 != null)
                    capturedSortedRows = sortUpdate.Item2;
            });

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert - HYPERLINK 公式應保留，且依顯示文字排序（Team A 排在 Team B 前）
        Assert.NotNull(capturedSortedRows);
        Assert.Equal(2, capturedSortedRows!.Count);
        // 排序後 Team A 的那列排在第一位
        Assert.Equal("=HYPERLINK(\"https://dev.azure.com/2\",\"VSTS200 - Feature A\")",
            capturedSortedRows[0][1]?.ToString() ?? string.Empty);
        // 排序後 Team B 的那列排在第二位
        Assert.Equal("=HYPERLINK(\"https://dev.azure.com/1\",\"VSTS100 - Feature B\")",
            capturedSortedRows[1][1]?.ToString() ?? string.Empty);
    }

    // ===== T015: 完整 ExecuteAsync 端對端流程 =====

    /// <summary>
    /// T015: 測試完整執行流程 — 讀取 Redis → 讀取 Sheet → 分類 → 插入 → 填入/更新 → 排序
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_EndToEnd_ShouldFollowCorrectOrder()
    {
        // Arrange
        var result = CreateConsolidatedResult(
            ("project-a", new[]
            {
                CreateEntry(100, "New Feature"), // will be inserted
                CreateEntry(200, "Existing Feature") // will be updated
            }));
        SetupRedisConsolidatedData(result);
        SetupSheetId(42);

        var sheetData = CreateSheetData(
            ("project-a", null),        // row 0: header
            (null, "200project-a")      // row 1: existing
        );
        SetupSheetData(sheetData);

        var callOrder = new List<string>();

        _redisServiceMock.Setup(x => x.HashGetAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback(() => callOrder.Add("Redis.Read"))
            .ReturnsAsync(result.ToJson());

        _googleSheetServiceMock.Setup(x => x.GetSheetIdByNameAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback(() => callOrder.Add("Sheet.GetId"))
            .ReturnsAsync(42);

        _googleSheetServiceMock.Setup(x => x.GetSheetDataAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback(() => callOrder.Add("Sheet.Read"))
            .ReturnsAsync(sheetData);

        _googleSheetServiceMock.Setup(x => x.GetSheetDataWithFormulasAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback(() => callOrder.Add("Sheet.ReadFormulas"))
            .ReturnsAsync(sheetData);

        _googleSheetServiceMock.Setup(x => x.InsertRowsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .Callback(() => callOrder.Add("Sheet.InsertRows"));

        _googleSheetServiceMock.Setup(x => x.UpdateCellWithHyperlinkAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string>(), It.IsAny<string>()))
            .Callback(() => callOrder.Add("Sheet.Hyperlink"));

        _googleSheetServiceMock.Setup(x => x.BatchUpdateCellsAsync(It.IsAny<string>(), It.IsAny<IList<(string, IList<IList<object>>)>>()))
            .Callback(() => callOrder.Add("Sheet.BatchUpdate"));

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert - verify order
        Assert.True(callOrder.IndexOf("Redis.Read") < callOrder.IndexOf("Sheet.GetId"));
        Assert.True(callOrder.IndexOf("Sheet.GetId") < callOrder.IndexOf("Sheet.Read"));
        Assert.True(callOrder.IndexOf("Sheet.InsertRows") < callOrder.IndexOf("Sheet.BatchUpdate"));
        // 排序的 re-read（使用公式模式）必須在資料填入的 BatchUpdate 之後
        Assert.True(callOrder.IndexOf("Sheet.ReadFormulas") > callOrder.IndexOf("Sheet.BatchUpdate"));
    }

    /// <summary>
    /// T015: 測試多專案情境各區段獨立處理
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_MultipleProjects_ShouldProcessIndependently()
    {
        // Arrange
        var result = CreateConsolidatedResult(
            ("project-a", new[] { CreateEntry(100) }),
            ("project-b", new[] { CreateEntry(200) }));
        SetupRedisConsolidatedData(result);
        SetupSheetId(0);

        var sheetData = CreateSheetData(
            ("project-a", null),        // row 0: header A
            (null, null),               // row 1: data A
            ("project-b", null),        // row 2: header B
            (null, null)                // row 3: data B
        );
        SetupSheetData(sheetData);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert - 兩個專案的排序應在記憶體中完成後以單一 BatchUpdateCellsAsync 呼叫寫回，不呼叫 SortRangeAsync
        _googleSheetServiceMock.Verify(
            x => x.SortRangeAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<IList<(int ColumnIndex, bool Ascending)>>()),
            Times.Never);
        _googleSheetServiceMock.Verify(
            x => x.BatchUpdateCellsAsync(
                _defaultOptions.SpreadsheetId,
                It.Is<IList<(string Range, IList<IList<object>> Values)>>(updates =>
                    updates.Any(u => u.Range == $"'{_defaultOptions.SheetName}'!A2:Z2") &&
                    updates.Any(u => u.Range == $"'{_defaultOptions.SheetName}'!A4:Z4"))),
            Times.AtLeastOnce);
    }

    // ===== ColumnLetterToIndex 輔助方法 =====

    /// <summary>
    /// 測試欄位字母轉換為索引
    /// </summary>
    [Theory]
    [InlineData("A", 0)]
    [InlineData("B", 1)]
    [InlineData("D", 3)]
    [InlineData("E", 4)]
    [InlineData("X", 23)]
    [InlineData("Y", 24)]
    [InlineData("Z", 25)]
    public void ColumnLetterToIndex_ShouldReturnCorrectIndex(string letter, int expected)
    {
        Assert.Equal(expected, UpdateGoogleSheetsTask.ColumnLetterToIndex(letter));
    }

    // ===== T032: ColumnMapping 欄位驗證 =====

    /// <summary>
    /// T032: 測試欄位值超過 Z 時拋出 InvalidOperationException
    /// </summary>
    [Fact]
    public void ValidateColumnMapping_ColumnExceedZ_ShouldThrow()
    {
        // Arrange
        var mapping = new ColumnMappingOptions
        {
            RepositoryNameColumn = "AA",
            FeatureColumn = "B",
            TeamColumn = "D",
            AuthorsColumn = "E",
            PullRequestUrlsColumn = "X",
            UniqueKeyColumn = "Y",
            AutoSyncColumn = "F"
        };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(
            () => UpdateGoogleSheetsTask.ValidateColumnMapping(mapping));
        Assert.Contains("RepositoryNameColumn", ex.Message);
        Assert.Contains("AA", ex.Message);
        Assert.Contains("A–Z", ex.Message);
    }

    /// <summary>
    /// T032: 測試所有欄位均在 A–Z 範圍內時驗證通過
    /// </summary>
    [Fact]
    public void ValidateColumnMapping_AllColumnsValid_ShouldNotThrow()
    {
        // Arrange
        var mapping = new ColumnMappingOptions
        {
            RepositoryNameColumn = "Z",
            FeatureColumn = "B",
            TeamColumn = "D",
            AuthorsColumn = "E",
            PullRequestUrlsColumn = "X",
            UniqueKeyColumn = "Y",
            AutoSyncColumn = "F"
        };

        // Act & Assert - should not throw
        UpdateGoogleSheetsTask.ValidateColumnMapping(mapping);
    }

    // ===== T025: Redis 無整合資料時正常結束 =====

    /// <summary>
    /// T025: 測試 Redis 無整合資料（null）時正常結束
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_NullRedisData_ShouldReturnNormally()
    {
        // Arrange
        _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.ReleaseDataHash, RedisKeys.Fields.Consolidated))
            .ReturnsAsync((string?)null);

        var task = CreateTask();

        // Act & Assert - should not throw
        await task.ExecuteAsync();

        // No Sheet operations
        _googleSheetServiceMock.Verify(
            x => x.GetSheetIdByNameAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    /// <summary>
    /// T025: 測試 Redis 空字串時正常結束
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_EmptyRedisData_ShouldReturnNormally()
    {
        // Arrange
        _redisServiceMock.Setup(x => x.HashGetAsync(RedisKeys.ReleaseDataHash, RedisKeys.Fields.Consolidated))
            .ReturnsAsync(string.Empty);

        var task = CreateTask();

        // Act & Assert - should not throw
        await task.ExecuteAsync();

        _googleSheetServiceMock.Verify(
            x => x.GetSheetIdByNameAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    // ===== T033: GetSheetIdByNameAsync 回傳 null 時正常結束 =====

    /// <summary>
    /// T033: 測試 Sheet Name 找不到時正常結束
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_SheetNotFound_ShouldReturnNormally()
    {
        // Arrange
        var result = CreateConsolidatedResult(("my-repo", new[] { CreateEntry(100) }));
        SetupRedisConsolidatedData(result);
        SetupSheetId(null);

        var task = CreateTask();

        // Act & Assert - should not throw
        await task.ExecuteAsync();

        _googleSheetServiceMock.Verify(
            x => x.GetSheetDataAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    // ===== T034: GetSheetDataAsync 回傳 null 時正常結束 =====

    /// <summary>
    /// T034: 測試無法讀取 Sheet 資料時正常結束
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_SheetDataNull_ShouldReturnNormally()
    {
        // Arrange
        var result = CreateConsolidatedResult(("my-repo", new[] { CreateEntry(100) }));
        SetupRedisConsolidatedData(result);
        SetupSheetId(0);
        SetupSheetData(null);

        var task = CreateTask();

        // Act & Assert - should not throw
        await task.ExecuteAsync();

        _googleSheetServiceMock.Verify(
            x => x.InsertRowsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()),
            Times.Never);
    }

    /// <summary>
    /// 測試 TeamColumn 資料正確填入
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_NewRow_TeamColumnShouldBeTeamDisplayName()
    {
        // Arrange
        var entry = CreateEntry(100, teamDisplayName: "金流團隊");
        var result = CreateConsolidatedResult(("my-repo", new[] { entry }));
        SetupRedisConsolidatedData(result);
        SetupSheetId(0);

        var sheetData = CreateSheetData(("my-repo", null));
        SetupSheetData(sheetData);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _googleSheetServiceMock.Verify(
            x => x.BatchUpdateCellsAsync(
                _defaultOptions.SpreadsheetId,
                It.Is<IList<(string Range, IList<IList<object>> Values)>>(updates =>
                    updates.Any(u =>
                        u.Range.Contains("D") &&
                        u.Values[0][0].ToString() == "金流團隊"))),
            Times.Once);
    }
}
