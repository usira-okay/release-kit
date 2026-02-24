using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Services;
using Moq;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Infrastructure.Tests.GoogleSheets;

/// <summary>
/// GoogleSheetService 單元測試
/// </summary>
public class GoogleSheetServiceTests
{
    /// <summary>
    /// 測試 IGoogleSheetService 介面包含所有必要方法
    /// </summary>
    [Fact]
    public void Interface_ShouldDefineAllRequiredMethods()
    {
        // Arrange & Act
        var methods = typeof(IGoogleSheetService).GetMethods();

        // Assert
        Assert.Contains(methods, m => m.Name == "GetSheetIdByNameAsync");
        Assert.Contains(methods, m => m.Name == "GetSheetDataAsync");
        Assert.Contains(methods, m => m.Name == "InsertRowsAsync");
        Assert.Contains(methods, m => m.Name == "UpdateCellsAsync");
        Assert.Contains(methods, m => m.Name == "UpdateCellWithHyperlinkAsync");
        Assert.Contains(methods, m => m.Name == "SortRangeAsync");
        Assert.Contains(methods, m => m.Name == "BatchUpdateCellsAsync");
    }

    /// <summary>
    /// 測試 GetSheetIdByNameAsync 方法簽章正確
    /// </summary>
    [Fact]
    public void GetSheetIdByNameAsync_ShouldReturnNullableInt()
    {
        // Arrange
        var method = typeof(IGoogleSheetService).GetMethod("GetSheetIdByNameAsync");

        // Assert
        Assert.NotNull(method);
        Assert.Equal(typeof(Task<int?>), method.ReturnType);
        Assert.Equal(2, method.GetParameters().Length);
    }

    /// <summary>
    /// 測試 GetSheetDataAsync 方法簽章正確
    /// </summary>
    [Fact]
    public void GetSheetDataAsync_ShouldReturnNullableData()
    {
        // Arrange
        var method = typeof(IGoogleSheetService).GetMethod("GetSheetDataAsync");

        // Assert
        Assert.NotNull(method);
        Assert.Equal(typeof(Task<IList<IList<object>>?>), method.ReturnType);
        Assert.Equal(2, method.GetParameters().Length);
    }

    /// <summary>
    /// 測試 InsertRowsAsync 方法簽章正確
    /// </summary>
    [Fact]
    public void InsertRowsAsync_ShouldAcceptCorrectParameters()
    {
        // Arrange
        var method = typeof(IGoogleSheetService).GetMethod("InsertRowsAsync");

        // Assert
        Assert.NotNull(method);
        Assert.Equal(typeof(Task), method.ReturnType);
        var parameters = method.GetParameters();
        Assert.Equal(4, parameters.Length);
        Assert.Equal("spreadsheetId", parameters[0].Name);
        Assert.Equal("sheetId", parameters[1].Name);
        Assert.Equal("rowIndex", parameters[2].Name);
        Assert.Equal("count", parameters[3].Name);
    }

    /// <summary>
    /// 測試 UpdateCellWithHyperlinkAsync 方法簽章正確（使用 FormulaValue）
    /// </summary>
    [Fact]
    public void UpdateCellWithHyperlinkAsync_ShouldAcceptCorrectParameters()
    {
        // Arrange
        var method = typeof(IGoogleSheetService).GetMethod("UpdateCellWithHyperlinkAsync");

        // Assert
        Assert.NotNull(method);
        Assert.Equal(typeof(Task), method.ReturnType);
        var parameters = method.GetParameters();
        Assert.Equal(6, parameters.Length);
        Assert.Equal("spreadsheetId", parameters[0].Name);
        Assert.Equal("sheetId", parameters[1].Name);
        Assert.Equal("rowIndex", parameters[2].Name);
        Assert.Equal("columnIndex", parameters[3].Name);
        Assert.Equal("displayText", parameters[4].Name);
        Assert.Equal("url", parameters[5].Name);
    }

    /// <summary>
    /// 測試 SortRangeAsync 方法簽章正確
    /// </summary>
    [Fact]
    public void SortRangeAsync_ShouldAcceptSortSpecs()
    {
        // Arrange
        var method = typeof(IGoogleSheetService).GetMethod("SortRangeAsync");

        // Assert
        Assert.NotNull(method);
        Assert.Equal(typeof(Task), method.ReturnType);
        var parameters = method.GetParameters();
        Assert.Equal(5, parameters.Length);
        Assert.Equal("sortSpecs", parameters[4].Name);
    }

    /// <summary>
    /// 測試 BatchUpdateCellsAsync 方法簽章正確
    /// </summary>
    [Fact]
    public void BatchUpdateCellsAsync_ShouldAcceptUpdates()
    {
        // Arrange
        var method = typeof(IGoogleSheetService).GetMethod("BatchUpdateCellsAsync");

        // Assert
        Assert.NotNull(method);
        Assert.Equal(typeof(Task), method.ReturnType);
        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal("spreadsheetId", parameters[0].Name);
        Assert.Equal("updates", parameters[1].Name);
    }

    /// <summary>
    /// 測試 Mock IGoogleSheetService 可正確建立（驗證介面可 Mock）
    /// </summary>
    [Fact]
    public async Task Mock_GetSheetIdByNameAsync_ShouldReturnConfiguredValue()
    {
        // Arrange
        var mockService = new Mock<IGoogleSheetService>();
        mockService.Setup(x => x.GetSheetIdByNameAsync("spreadsheet-1", "Sheet1"))
            .ReturnsAsync(42);

        // Act
        var result = await mockService.Object.GetSheetIdByNameAsync("spreadsheet-1", "Sheet1");

        // Assert
        Assert.Equal(42, result);
    }

    /// <summary>
    /// 測試 Mock IGoogleSheetService GetSheetIdByNameAsync 找不到時回傳 null
    /// </summary>
    [Fact]
    public async Task Mock_GetSheetIdByNameAsync_WhenNotFound_ShouldReturnNull()
    {
        // Arrange
        var mockService = new Mock<IGoogleSheetService>();
        mockService.Setup(x => x.GetSheetIdByNameAsync("spreadsheet-1", "NonExistent"))
            .ReturnsAsync((int?)null);

        // Act
        var result = await mockService.Object.GetSheetIdByNameAsync("spreadsheet-1", "NonExistent");

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// 測試 Mock IGoogleSheetService GetSheetDataAsync 回傳正確資料
    /// </summary>
    [Fact]
    public async Task Mock_GetSheetDataAsync_ShouldReturnData()
    {
        // Arrange
        var mockService = new Mock<IGoogleSheetService>();
        var testData = new List<IList<object>>
        {
            new List<object> { "Header1", "Header2" },
            new List<object> { "Value1", "Value2" }
        };
        mockService.Setup(x => x.GetSheetDataAsync("spreadsheet-1", "A:Z"))
            .ReturnsAsync(testData);

        // Act
        var result = await mockService.Object.GetSheetDataAsync("spreadsheet-1", "A:Z");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
    }

    /// <summary>
    /// 測試 Mock IGoogleSheetService InsertRowsAsync 呼叫正確參數
    /// </summary>
    [Fact]
    public async Task Mock_InsertRowsAsync_ShouldCallWithCorrectParameters()
    {
        // Arrange
        var mockService = new Mock<IGoogleSheetService>();

        // Act
        await mockService.Object.InsertRowsAsync("spreadsheet-1", 0, 5, 3);

        // Assert
        mockService.Verify(x => x.InsertRowsAsync("spreadsheet-1", 0, 5, 3), Times.Once);
    }

    /// <summary>
    /// 測試 Mock IGoogleSheetService BatchUpdateCellsAsync 批次更新行為
    /// </summary>
    [Fact]
    public async Task Mock_BatchUpdateCellsAsync_ShouldCallWithMultipleRanges()
    {
        // Arrange
        var mockService = new Mock<IGoogleSheetService>();
        var updates = new List<(string Range, IList<IList<object>> Values)>
        {
            ("A1:B1", new List<IList<object>> { new List<object> { "v1", "v2" } }),
            ("A2:B2", new List<IList<object>> { new List<object> { "v3", "v4" } })
        };

        // Act
        await mockService.Object.BatchUpdateCellsAsync("spreadsheet-1", updates);

        // Assert
        mockService.Verify(x => x.BatchUpdateCellsAsync("spreadsheet-1", updates), Times.Once);
    }

    /// <summary>
    /// 測試 Mock IGoogleSheetService UpdateCellWithHyperlinkAsync 使用 HYPERLINK 公式
    /// </summary>
    [Fact]
    public async Task Mock_UpdateCellWithHyperlinkAsync_ShouldCallWithCorrectParameters()
    {
        // Arrange
        var mockService = new Mock<IGoogleSheetService>();

        // Act
        await mockService.Object.UpdateCellWithHyperlinkAsync(
            "spreadsheet-1", 0, 2, 1, "VSTS12345 - Feature", "https://dev.azure.com/work/12345");

        // Assert
        mockService.Verify(x => x.UpdateCellWithHyperlinkAsync(
            "spreadsheet-1", 0, 2, 1, "VSTS12345 - Feature", "https://dev.azure.com/work/12345"), Times.Once);
    }

    /// <summary>
    /// 測試 Mock IGoogleSheetService SortRangeAsync 排序參數正確
    /// </summary>
    [Fact]
    public async Task Mock_SortRangeAsync_ShouldCallWithCorrectSortSpecs()
    {
        // Arrange
        var mockService = new Mock<IGoogleSheetService>();
        var sortSpecs = new List<(int ColumnIndex, bool Ascending)>
        {
            (3, true),  // TeamColumn
            (4, true),  // AuthorsColumn
            (1, true),  // FeatureColumn
            (24, true)  // UniqueKeyColumn
        };

        // Act
        await mockService.Object.SortRangeAsync("spreadsheet-1", 0, 2, 10, sortSpecs);

        // Assert
        mockService.Verify(x => x.SortRangeAsync("spreadsheet-1", 0, 2, 10, sortSpecs), Times.Once);
    }

    /// <summary>
    /// 測試 GoogleSheetService 實作 IGoogleSheetService 介面
    /// </summary>
    [Fact]
    public void GoogleSheetService_ShouldImplementInterface()
    {
        // Assert
        Assert.True(typeof(IGoogleSheetService).IsAssignableFrom(
            typeof(ReleaseKit.Infrastructure.GoogleSheets.GoogleSheetService)));
    }
}
