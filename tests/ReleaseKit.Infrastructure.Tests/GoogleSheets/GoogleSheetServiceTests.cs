using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Infrastructure.GoogleSheets;

namespace ReleaseKit.Infrastructure.Tests.GoogleSheets;

/// <summary>
/// GoogleSheetService 建構式驗證測試
/// </summary>
public class GoogleSheetServiceTests
{
    /// <summary>
    /// 驗證缺少 ServiceAccountCredentialPath 時拋出 InvalidOperationException
    /// </summary>
    [Fact]
    public void Constructor_WithEmptyCredentialPath_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var options = Options.Create(new GoogleSheetOptions
        {
            SpreadsheetId = "test-id",
            SheetName = "Sheet1",
            ServiceAccountCredentialPath = string.Empty
        });
        var logger = new Mock<ILogger<GoogleSheetService>>();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => new GoogleSheetService(options, logger.Object));
        Assert.Contains("ServiceAccountCredentialPath", exception.Message);
    }

    /// <summary>
    /// 驗證 ServiceAccountCredentialPath 為 null 時拋出 InvalidOperationException
    /// </summary>
    [Fact]
    public void Constructor_WithNullCredentialPath_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var options = Options.Create(new GoogleSheetOptions
        {
            SpreadsheetId = "test-id",
            SheetName = "Sheet1",
            ServiceAccountCredentialPath = null!
        });
        var logger = new Mock<ILogger<GoogleSheetService>>();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => new GoogleSheetService(options, logger.Object));
        Assert.Contains("ServiceAccountCredentialPath", exception.Message);
    }
}
