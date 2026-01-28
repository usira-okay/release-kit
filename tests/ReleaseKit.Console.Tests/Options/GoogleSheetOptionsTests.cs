using FluentAssertions;
using ReleaseKit.Console.Options;

namespace ReleaseKit.Console.Tests.Options;

/// <summary>
/// GoogleSheetOptions 單元測試
/// </summary>
public class GoogleSheetOptionsTests
{
    [Fact]
    public void Validate_WhenSpreadsheetIdIsEmpty_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var options = new GoogleSheetOptions
        {
            SpreadsheetId = "",
            SheetName = "Sheet1",
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

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("GoogleSheet:SpreadsheetId 組態設定不得為空");
    }

    [Fact]
    public void Validate_WhenSheetNameIsEmpty_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var options = new GoogleSheetOptions
        {
            SpreadsheetId = "1234567890abcdefg",
            SheetName = "",
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

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("GoogleSheet:SheetName 組態設定不得為空");
    }

    [Fact]
    public void Validate_WhenServiceAccountCredentialPathIsEmpty_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var options = new GoogleSheetOptions
        {
            SpreadsheetId = "1234567890abcdefg",
            SheetName = "Sheet1",
            ServiceAccountCredentialPath = "",
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

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("GoogleSheet:ServiceAccountCredentialPath 組態設定不得為空");
    }

    [Fact]
    public void Validate_WhenColumnMappingIsInvalid_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var options = new GoogleSheetOptions
        {
            SpreadsheetId = "1234567890abcdefg",
            SheetName = "Sheet1",
            ServiceAccountCredentialPath = "/path/to/credentials.json",
            ColumnMapping = new ColumnMappingOptions
            {
                RepositoryNameColumn = "",  // Invalid
                FeatureColumn = "B",
                TeamColumn = "D",
                AuthorsColumn = "W",
                PullRequestUrlsColumn = "X",
                UniqueKeyColumn = "Y",
                AutoSyncColumn = "F"
            }
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("GoogleSheet:ColumnMapping:RepositoryNameColumn 組態設定不得為空");
    }

    [Fact]
    public void Validate_WhenAllFieldsAreValid_ShouldNotThrow()
    {
        // Arrange
        var options = new GoogleSheetOptions
        {
            SpreadsheetId = "1234567890abcdefg",
            SheetName = "Sheet1",
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

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }
}
