using FluentAssertions;
using Microsoft.Extensions.Configuration;
using ReleaseKit.Console.Options;

namespace ReleaseKit.Console.Tests.Integration;

/// <summary>
/// 環境變數覆寫整合測試
/// </summary>
public class EnvironmentVariableOverrideTests
{
    [Fact]
    public void Configuration_WhenEnvironmentVariablesAreSet_ShouldOverrideJsonSettings()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ReleaseKit__GoogleSheet__SpreadsheetId", "env-spreadsheet-id");
        Environment.SetEnvironmentVariable("ReleaseKit__GoogleSheet__SheetName", "Env Sheet Name");
        Environment.SetEnvironmentVariable("ReleaseKit__AzureDevOps__OrganizationUrl", "https://dev.azure.com/env-org");
        
        try
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddEnvironmentVariables("ReleaseKit__")
                .Build();

            // Act
            var spreadsheetId = configuration["GoogleSheet:SpreadsheetId"];
            var sheetName = configuration["GoogleSheet:SheetName"];
            var orgUrl = configuration["AzureDevOps:OrganizationUrl"];

            // Assert
            spreadsheetId.Should().Be("env-spreadsheet-id");
            sheetName.Should().Be("Env Sheet Name");
            orgUrl.Should().Be("https://dev.azure.com/env-org");
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("ReleaseKit__GoogleSheet__SpreadsheetId", null);
            Environment.SetEnvironmentVariable("ReleaseKit__GoogleSheet__SheetName", null);
            Environment.SetEnvironmentVariable("ReleaseKit__AzureDevOps__OrganizationUrl", null);
        }
    }

    [Fact]
    public void Configuration_WhenEnvironmentVariablesOverrideArrayElements_ShouldBindCorrectly()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ReleaseKit__GitLab__Projects__0__FetchMode", "BranchDiff");
        Environment.SetEnvironmentVariable("ReleaseKit__GitLab__Projects__0__SourceBranch", "env-release-branch");
        Environment.SetEnvironmentVariable("ReleaseKit__GitLab__Projects__0__TargetBranch", "env-main");
        
        try
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddEnvironmentVariables("ReleaseKit__")
                .Build();

            // Act
            var gitlabOptions = new GitLabOptions();
            configuration.GetSection("GitLab").Bind(gitlabOptions);

            // Assert
            gitlabOptions.Projects.Should().HaveCount(1);
            gitlabOptions.Projects[0].FetchMode.Should().Be(FetchMode.BranchDiff);
            gitlabOptions.Projects[0].SourceBranch.Should().Be("env-release-branch");
            gitlabOptions.Projects[0].TargetBranch.Should().Be("env-main");
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("ReleaseKit__GitLab__Projects__0__FetchMode", null);
            Environment.SetEnvironmentVariable("ReleaseKit__GitLab__Projects__0__SourceBranch", null);
            Environment.SetEnvironmentVariable("ReleaseKit__GitLab__Projects__0__TargetBranch", null);
        }
    }

    [Fact]
    public void Configuration_WhenEnvironmentVariablesOverrideSensitiveData_ShouldNotExposeInJson()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ReleaseKit__GitLab__AccessToken", "glpat-secret-token-from-env");
        Environment.SetEnvironmentVariable("ReleaseKit__Bitbucket__AccessToken", "ATBB-secret-token-from-env");
        
        try
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddEnvironmentVariables("ReleaseKit__")
                .Build();

            // Act
            var gitlabToken = configuration["GitLab:AccessToken"];
            var bitbucketToken = configuration["Bitbucket:AccessToken"];

            // Assert
            gitlabToken.Should().Be("glpat-secret-token-from-env");
            bitbucketToken.Should().Be("ATBB-secret-token-from-env");
            
            // Verify that the original JSON file does not contain the token
            var jsonConfig = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .Build();
            
            var jsonGitlabToken = jsonConfig["GitLab:AccessToken"];
            var jsonBitbucketToken = jsonConfig["Bitbucket:AccessToken"];
            
            jsonGitlabToken.Should().BeEmpty(); // Should be empty in JSON
            jsonBitbucketToken.Should().BeEmpty(); // Should be empty in JSON
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("ReleaseKit__GitLab__AccessToken", null);
            Environment.SetEnvironmentVariable("ReleaseKit__Bitbucket__AccessToken", null);
        }
    }

    [Fact]
    public void Configuration_WhenEnvironmentVariablesOverrideNestedObjects_ShouldBindCorrectly()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ReleaseKit__GoogleSheet__ColumnMapping__RepositoryNameColumn", "AA");
        Environment.SetEnvironmentVariable("ReleaseKit__GoogleSheet__ColumnMapping__FeatureColumn", "BB");
        Environment.SetEnvironmentVariable("ReleaseKit__GoogleSheet__ColumnMapping__TeamColumn", "CC");
        
        try
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddEnvironmentVariables("ReleaseKit__")
                .Build();

            // Act
            var googleSheetOptions = new GoogleSheetOptions();
            configuration.GetSection("GoogleSheet").Bind(googleSheetOptions);

            // Assert
            googleSheetOptions.ColumnMapping.RepositoryNameColumn.Should().Be("AA");
            googleSheetOptions.ColumnMapping.FeatureColumn.Should().Be("BB");
            googleSheetOptions.ColumnMapping.TeamColumn.Should().Be("CC");
            // Other columns should still have values from JSON
            googleSheetOptions.ColumnMapping.AuthorsColumn.Should().Be("W");
            googleSheetOptions.ColumnMapping.PullRequestUrlsColumn.Should().Be("X");
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("ReleaseKit__GoogleSheet__ColumnMapping__RepositoryNameColumn", null);
            Environment.SetEnvironmentVariable("ReleaseKit__GoogleSheet__ColumnMapping__FeatureColumn", null);
            Environment.SetEnvironmentVariable("ReleaseKit__GoogleSheet__ColumnMapping__TeamColumn", null);
        }
    }

    [Fact]
    public void Configuration_WhenEnvironmentVariablesProvideRequiredFields_ShouldPassValidation()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ReleaseKit__GoogleSheet__SpreadsheetId", "env-spreadsheet-id");
        Environment.SetEnvironmentVariable("ReleaseKit__GoogleSheet__SheetName", "Env Sheet");
        Environment.SetEnvironmentVariable("ReleaseKit__GoogleSheet__ServiceAccountCredentialPath", "/env/path/to/credentials.json");
        
        try
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddEnvironmentVariables("ReleaseKit__")
                .Build();

            // Act
            var googleSheetOptions = new GoogleSheetOptions();
            configuration.GetSection("GoogleSheet").Bind(googleSheetOptions);
            
            var act = () => googleSheetOptions.Validate();

            // Assert
            act.Should().NotThrow();
            googleSheetOptions.SpreadsheetId.Should().Be("env-spreadsheet-id");
            googleSheetOptions.SheetName.Should().Be("Env Sheet");
            googleSheetOptions.ServiceAccountCredentialPath.Should().Be("/env/path/to/credentials.json");
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("ReleaseKit__GoogleSheet__SpreadsheetId", null);
            Environment.SetEnvironmentVariable("ReleaseKit__GoogleSheet__SheetName", null);
            Environment.SetEnvironmentVariable("ReleaseKit__GoogleSheet__ServiceAccountCredentialPath", null);
        }
    }
}
