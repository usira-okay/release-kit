using Moq;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tests.Abstractions;

/// <summary>
/// IGoogleSheetService 介面單元測試，驗證 Mock 可正確注入與呼叫
/// </summary>
public class IGoogleSheetServiceTests
{
    /// <summary>
    /// 驗證 IGoogleSheetService 的 GetSheetDataAsync 可被 Mock 並正確回傳資料
    /// </summary>
    [Fact]
    public async Task GetSheetDataAsync_WithMock_ShouldReturnExpectedData()
    {
        // Arrange
        var mock = new Mock<IGoogleSheetService>();
        var expectedData = new List<IList<object>>
        {
            new List<object> { "A1", "B1" },
            new List<object> { "A2", "B2" }
        };

        mock.Setup(x => x.GetSheetDataAsync("spreadsheet-id", "Sheet1", "A:Z"))
            .ReturnsAsync(expectedData);

        IGoogleSheetService service = mock.Object;

        // Act
        var result = await service.GetSheetDataAsync("spreadsheet-id", "Sheet1", "A:Z");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        mock.Verify(x => x.GetSheetDataAsync("spreadsheet-id", "Sheet1", "A:Z"), Times.Once);
    }

    /// <summary>
    /// 驗證 IGoogleSheetService 的 InsertRowAsync 可被 Mock 並正確呼叫
    /// </summary>
    [Fact]
    public async Task InsertRowAsync_WithMock_ShouldBeCallable()
    {
        // Arrange
        var mock = new Mock<IGoogleSheetService>();
        mock.Setup(x => x.InsertRowAsync("spreadsheet-id", 0, 5))
            .Returns(Task.CompletedTask);

        IGoogleSheetService service = mock.Object;

        // Act
        await service.InsertRowAsync("spreadsheet-id", 0, 5);

        // Assert
        mock.Verify(x => x.InsertRowAsync("spreadsheet-id", 0, 5), Times.Once);
    }

    /// <summary>
    /// 驗證 IGoogleSheetService 的 UpdateCellsAsync 可被 Mock 並正確呼叫
    /// </summary>
    [Fact]
    public async Task UpdateCellsAsync_WithMock_ShouldBeCallable()
    {
        // Arrange
        var mock = new Mock<IGoogleSheetService>();
        var updates = new Dictionary<string, object> { { "A1", "value1" }, { "B1", "value2" } };
        mock.Setup(x => x.UpdateCellsAsync("spreadsheet-id", "Sheet1", updates))
            .Returns(Task.CompletedTask);

        IGoogleSheetService service = mock.Object;

        // Act
        await service.UpdateCellsAsync("spreadsheet-id", "Sheet1", updates);

        // Assert
        mock.Verify(x => x.UpdateCellsAsync("spreadsheet-id", "Sheet1", updates), Times.Once);
    }
}
