using FluentAssertions;
using Microsoft.Extensions.Configuration;
using ReleaseKit.Console.Options;

namespace ReleaseKit.Console.Tests.Integration;

/// <summary>
/// 環境特定配置整合測試
/// </summary>
public class EnvironmentConfigurationTests
{
    [Fact]
    public void Configuration_WhenLoadingDevelopmentEnvironment_ShouldOverrideBaseSettings()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        // Act
        var fetchMode = configuration["FetchMode"];
        var sourceBranch = configuration["SourceBranch"];
        var googleSheetSpreadsheetId = configuration["GoogleSheet:SpreadsheetId"];
        var googleSheetSheetName = configuration["GoogleSheet:SheetName"];
        var azureDevOpsOrgUrl = configuration["AzureDevOps:OrganizationUrl"];
        
        var gitlabProjectTargetBranch = configuration["GitLab:Projects:0:TargetBranch"];
        var gitlabProjectFetchMode = configuration["GitLab:Projects:0:FetchMode"];

        // Assert
        fetchMode.Should().Be("BranchDiff"); // Overridden in Development
        sourceBranch.Should().Be("develop"); // New in Development
        googleSheetSpreadsheetId.Should().Be("development-spreadsheet-id"); // Overridden
        googleSheetSheetName.Should().Be("Development Sheet"); // Overridden
        azureDevOpsOrgUrl.Should().Be("https://dev.azure.com/dev-organization"); // Overridden
        
        gitlabProjectTargetBranch.Should().Be("develop"); // Overridden
        gitlabProjectFetchMode.Should().Be("DateTimeRange"); // New in Development
    }

    [Fact]
    public void Configuration_WhenLoadingProductionEnvironment_ShouldOverrideBaseSettings()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Production.json", optional: true)
            .Build();

        // Act
        var fetchMode = configuration["FetchMode"];
        var startDateTime = configuration["StartDateTime"];
        var endDateTime = configuration["EndDateTime"];
        var googleSheetSpreadsheetId = configuration["GoogleSheet:SpreadsheetId"];
        var googleSheetSheetName = configuration["GoogleSheet:SheetName"];
        var azureDevOpsOrgUrl = configuration["AzureDevOps:OrganizationUrl"];
        
        var gitlabProjectTargetBranch = configuration["GitLab:Projects:0:TargetBranch"];
        var gitlabProjectFetchMode = configuration["GitLab:Projects:0:FetchMode"];
        var gitlabProjectSourceBranch = configuration["GitLab:Projects:0:SourceBranch"];

        // Assert
        fetchMode.Should().Be("DateTimeRange"); // Same as base (could be overridden)
        startDateTime.Should().Be("2025-01-01"); // Same as base (could be overridden)
        endDateTime.Should().Be("2025-01-31"); // Same as base (could be overridden)
        googleSheetSpreadsheetId.Should().Be("production-spreadsheet-id"); // Overridden
        googleSheetSheetName.Should().Be("Production Sheet"); // Overridden
        azureDevOpsOrgUrl.Should().Be("https://dev.azure.com/prod-organization"); // Overridden
        
        gitlabProjectTargetBranch.Should().Be("main"); // Overridden
        gitlabProjectFetchMode.Should().Be("BranchDiff"); // Overridden
        gitlabProjectSourceBranch.Should().Be("release/20260128"); // New in Production
    }

    [Fact]
    public void Configuration_WhenBindingToDevelopmentOptions_ShouldBindCorrectly()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        // Act
        var googleSheetOptions = new GoogleSheetOptions();
        configuration.GetSection("GoogleSheet").Bind(googleSheetOptions);

        var azureDevOpsOptions = new AzureDevOpsOptions();
        configuration.GetSection("AzureDevOps").Bind(azureDevOpsOptions);

        // Assert
        googleSheetOptions.SpreadsheetId.Should().Be("development-spreadsheet-id");
        googleSheetOptions.SheetName.Should().Be("Development Sheet");
        googleSheetOptions.ColumnMapping.RepositoryNameColumn.Should().Be("Z"); // From base config
        
        azureDevOpsOptions.OrganizationUrl.Should().Be("https://dev.azure.com/dev-organization");
    }

    [Fact]
    public void Configuration_WhenBindingToProductionOptions_ShouldBindCorrectly()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Production.json", optional: true)
            .Build();

        // Act
        var googleSheetOptions = new GoogleSheetOptions();
        configuration.GetSection("GoogleSheet").Bind(googleSheetOptions);

        var azureDevOpsOptions = new AzureDevOpsOptions();
        configuration.GetSection("AzureDevOps").Bind(azureDevOpsOptions);

        var gitlabOptions = new GitLabOptions();
        configuration.GetSection("GitLab").Bind(gitlabOptions);

        // Assert
        googleSheetOptions.SpreadsheetId.Should().Be("production-spreadsheet-id");
        googleSheetOptions.SheetName.Should().Be("Production Sheet");
        googleSheetOptions.ColumnMapping.RepositoryNameColumn.Should().Be("Z"); // From base config
        
        azureDevOpsOptions.OrganizationUrl.Should().Be("https://dev.azure.com/prod-organization");
        
        gitlabOptions.Projects.Should().HaveCount(1);
        gitlabOptions.Projects[0].TargetBranch.Should().Be("main");
        gitlabOptions.Projects[0].FetchMode.Should().Be(FetchMode.BranchDiff);
        gitlabOptions.Projects[0].SourceBranch.Should().Be("release/20260128");
    }
}
