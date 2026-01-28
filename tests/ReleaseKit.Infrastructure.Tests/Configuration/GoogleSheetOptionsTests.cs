using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReleaseKit.Infrastructure.Configuration;
using Xunit;

namespace ReleaseKit.Infrastructure.Tests.Configuration;

/// <summary>
/// GoogleSheetOptions 配置測試
/// </summary>
public class GoogleSheetOptionsTests
{
    /// <summary>
    /// 測試有效配置是否正確綁定
    /// </summary>
    [Fact]
    public void Bind_ValidConfiguration_ShouldBindCorrectly()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GoogleSheet:SpreadsheetId"] = "test-id-123",
                ["GoogleSheet:SheetName"] = "TestSheet",
                ["GoogleSheet:ServiceAccountCredentialPath"] = "/test/path/credentials.json",
                ["GoogleSheet:ColumnMapping:RepositoryNameColumn"] = "Z",
                ["GoogleSheet:ColumnMapping:FeatureColumn"] = "B",
                ["GoogleSheet:ColumnMapping:TeamColumn"] = "D",
                ["GoogleSheet:ColumnMapping:AuthorsColumn"] = "W",
                ["GoogleSheet:ColumnMapping:PullRequestUrlsColumn"] = "X",
                ["GoogleSheet:ColumnMapping:UniqueKeyColumn"] = "Y",
                ["GoogleSheet:ColumnMapping:AutoSyncColumn"] = "F"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<GoogleSheetOptions>()
            .Bind(configuration.GetSection("GoogleSheet"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<GoogleSheetOptions>>().Value;

        // Assert
        options.SpreadsheetId.Should().Be("test-id-123");
        options.SheetName.Should().Be("TestSheet");
        options.ServiceAccountCredentialPath.Should().Be("/test/path/credentials.json");
        options.ColumnMapping.Should().NotBeNull();
        options.ColumnMapping.FeatureColumn.Should().Be("B");
        options.ColumnMapping.TeamColumn.Should().Be("D");
    }

    /// <summary>
    /// 測試缺少必要屬性時應拋出驗證異常
    /// </summary>
    [Fact]
    public void Validate_MissingRequiredProperty_ShouldThrowException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GoogleSheet:SpreadsheetId"] = "", // 空值，違反 Required
                ["GoogleSheet:SheetName"] = "TestSheet"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<GoogleSheetOptions>()
            .Bind(configuration.GetSection("GoogleSheet"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        // Act & Assert - 嘗試訪問選項值應觸發驗證
        var act = () => serviceProvider.GetRequiredService<IOptions<GoogleSheetOptions>>().Value;
        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*SpreadsheetId*");
    }
}
