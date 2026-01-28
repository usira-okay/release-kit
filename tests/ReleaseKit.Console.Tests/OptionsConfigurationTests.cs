using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReleaseKit.Console.Options;

namespace ReleaseKit.Console.Tests;

/// <summary>
/// 測試新增的設定選項載入功能
/// </summary>
public class OptionsConfigurationTests
{
    [Fact]
    public void Configuration_ShouldLoad_GitLabOptions()
    {
        // Arrange
        var basePath = GetProjectBasePath();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
        
        var services = new ServiceCollection();
        services.Configure<GitLabOptions>(configuration.GetSection("GitLab"));
        var serviceProvider = services.BuildServiceProvider();
        
        // Act
        var options = serviceProvider.GetRequiredService<IOptions<GitLabOptions>>().Value;
        
        // Assert
        Assert.NotNull(options);
        Assert.Equal("https://gitlab.com/api/v4", options.ApiUrl);
        Assert.Empty(options.AccessToken);
        Assert.NotEmpty(options.Projects);
        Assert.Single(options.Projects);
        Assert.Equal("mygroup/backend-api", options.Projects[0].ProjectPath);
        Assert.Equal("main", options.Projects[0].TargetBranch);
    }
    
    [Fact]
    public void Configuration_ShouldLoad_BitbucketOptions()
    {
        // Arrange
        var basePath = GetProjectBasePath();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
        
        var services = new ServiceCollection();
        services.Configure<BitbucketOptions>(configuration.GetSection("Bitbucket"));
        var serviceProvider = services.BuildServiceProvider();
        
        // Act
        var options = serviceProvider.GetRequiredService<IOptions<BitbucketOptions>>().Value;
        
        // Assert
        Assert.NotNull(options);
        Assert.Equal("https://api.bitbucket.org/2.0", options.ApiUrl);
        Assert.Empty(options.Email);
        Assert.Empty(options.AccessToken);
        Assert.NotEmpty(options.Projects);
        Assert.Single(options.Projects);
        Assert.Equal("mygroup/backend-api", options.Projects[0].ProjectPath);
        Assert.Equal("main", options.Projects[0].TargetBranch);
    }
    
    [Fact]
    public void Configuration_ShouldLoad_UserMappingOptions()
    {
        // Arrange
        var basePath = GetProjectBasePath();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
        
        var services = new ServiceCollection();
        services.Configure<UserMappingOptions>(configuration.GetSection("UserMapping"));
        var serviceProvider = services.BuildServiceProvider();
        
        // Act
        var options = serviceProvider.GetRequiredService<IOptions<UserMappingOptions>>().Value;
        
        // Assert
        Assert.NotNull(options);
        Assert.NotNull(options.Mappings);
        Assert.Empty(options.Mappings); // appsettings.json 中的 Mappings 為空陣列
    }

    [Fact]
    public void Configuration_ShouldLoad_GoogleSheetOptions()
    {
        // Arrange
        var basePath = GetProjectBasePath();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
        
        var services = new ServiceCollection();
        services.Configure<GoogleSheetOptions>(configuration.GetSection("GoogleSheet"));
        var serviceProvider = services.BuildServiceProvider();
        
        // Act
        var options = serviceProvider.GetRequiredService<IOptions<GoogleSheetOptions>>().Value;
        
        // Assert
        Assert.NotNull(options);
        Assert.Empty(options.SpreadsheetId);
        Assert.Equal("Sheet1", options.SheetName);
        Assert.Empty(options.ServiceAccountCredentialPath);
        Assert.NotNull(options.ColumnMapping);
        Assert.Equal("Z", options.ColumnMapping.RepositoryNameColumn);
        Assert.Equal("B", options.ColumnMapping.FeatureColumn);
        Assert.Equal("D", options.ColumnMapping.TeamColumn);
        Assert.Equal("W", options.ColumnMapping.AuthorsColumn);
        Assert.Equal("X", options.ColumnMapping.PullRequestUrlsColumn);
        Assert.Equal("Y", options.ColumnMapping.UniqueKeyColumn);
        Assert.Equal("F", options.ColumnMapping.AutoSyncColumn);
    }

    [Fact]
    public void Configuration_ShouldLoad_AzureDevOpsOptions()
    {
        // Arrange
        var basePath = GetProjectBasePath();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
        
        var services = new ServiceCollection();
        services.Configure<AzureDevOpsOptions>(configuration.GetSection("AzureDevOps"));
        var serviceProvider = services.BuildServiceProvider();
        
        // Act
        var options = serviceProvider.GetRequiredService<IOptions<AzureDevOpsOptions>>().Value;
        
        // Assert
        Assert.NotNull(options);
        Assert.Equal("https://dev.azure.com/myorganization", options.OrganizationUrl);
        Assert.NotEmpty(options.TeamMapping);
        Assert.Equal(3, options.TeamMapping.Count);
        Assert.Equal("MoneyLogistic", options.TeamMapping[0].OriginalTeamName);
        Assert.Equal("金流團隊", options.TeamMapping[0].DisplayName);
        Assert.Equal("DailyResource", options.TeamMapping[1].OriginalTeamName);
        Assert.Equal("日常資源團隊", options.TeamMapping[1].DisplayName);
        Assert.Equal("Commerce", options.TeamMapping[2].OriginalTeamName);
        Assert.Equal("商務團隊", options.TeamMapping[2].DisplayName);
    }

    [Fact]
    public void Configuration_ShouldLoad_AppOptions()
    {
        // Arrange
        var basePath = GetProjectBasePath();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
        
        var services = new ServiceCollection();
        services.Configure<AppOptions>(configuration);
        var serviceProvider = services.BuildServiceProvider();
        
        // Act
        var options = serviceProvider.GetRequiredService<IOptions<AppOptions>>().Value;
        
        // Assert
        Assert.NotNull(options);
        Assert.Empty(options.FetchMode);
        Assert.Equal("release/yyyyMMdd", options.SourceBranch);
        Assert.Equal(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), options.StartDateTime);
        Assert.Equal(new DateTimeOffset(2025, 1, 31, 0, 0, 0, TimeSpan.Zero), options.EndDateTime);
    }
    
    [Fact]
    public void Configuration_ShouldLoad_GitLabProjectOptions_WithNewFields()
    {
        // Arrange
        var basePath = GetProjectBasePath();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
        
        var services = new ServiceCollection();
        services.Configure<GitLabOptions>(configuration.GetSection("GitLab"));
        var serviceProvider = services.BuildServiceProvider();
        
        // Act
        var options = serviceProvider.GetRequiredService<IOptions<GitLabOptions>>().Value;
        var projectOptions = options.Projects[0];
        
        // Assert
        Assert.NotNull(projectOptions);
        Assert.Equal("", projectOptions.FetchMode);
        Assert.Equal("release/yyyyMMdd", projectOptions.SourceBranch);
        Assert.Equal(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), projectOptions.StartDateTime);
        Assert.Equal(new DateTimeOffset(2025, 1, 31, 0, 0, 0, TimeSpan.Zero), projectOptions.EndDateTime);
    }
    
    [Fact]
    public void GitLabOptions_EnvironmentVariables_ShouldOverride_JsonSettings()
    {
        // Arrange
        var basePath = GetProjectBasePath();
        var envVars = new Dictionary<string, string>
        {
            { "GitLab__ApiUrl", "https://custom-gitlab.example.com/api/v4" },
            { "GitLab__AccessToken", "test-token-123" }
        };

        try
        {
            foreach (var (key, value) in envVars)
            {
                Environment.SetEnvironmentVariable(key, value);
            }

            var configuration = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
            
            var services = new ServiceCollection();
            services.Configure<GitLabOptions>(configuration.GetSection("GitLab"));
            var serviceProvider = services.BuildServiceProvider();
            
            // Act
            var options = serviceProvider.GetRequiredService<IOptions<GitLabOptions>>().Value;
            
            // Assert
            Assert.Equal("https://custom-gitlab.example.com/api/v4", options.ApiUrl);
            Assert.Equal("test-token-123", options.AccessToken);
        }
        finally
        {
            // Cleanup: 確保無論測試成功或失敗都會清理環境變數
            foreach (var key in envVars.Keys)
            {
                Environment.SetEnvironmentVariable(key, null);
            }
        }
    }
    
    [Fact]
    public void BitbucketOptions_EnvironmentVariables_ShouldOverride_JsonSettings()
    {
        // Arrange
        var basePath = GetProjectBasePath();
        var envVars = new Dictionary<string, string>
        {
            { "Bitbucket__Email", "test@example.com" },
            { "Bitbucket__AccessToken", "bitbucket-token-456" }
        };

        try
        {
            foreach (var (key, value) in envVars)
            {
                Environment.SetEnvironmentVariable(key, value);
            }

            var configuration = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
            
            var services = new ServiceCollection();
            services.Configure<BitbucketOptions>(configuration.GetSection("Bitbucket"));
            var serviceProvider = services.BuildServiceProvider();
            
            // Act
            var options = serviceProvider.GetRequiredService<IOptions<BitbucketOptions>>().Value;
            
            // Assert
            Assert.Equal("test@example.com", options.Email);
            Assert.Equal("bitbucket-token-456", options.AccessToken);
        }
        finally
        {
            // Cleanup: 確保無論測試成功或失敗都會清理環境變數
            foreach (var key in envVars.Keys)
            {
                Environment.SetEnvironmentVariable(key, null);
            }
        }
    }
    
    /// <summary>
    /// 取得專案基礎路徑（包含 appsettings.json 的路徑）
    /// </summary>
    private static string GetProjectBasePath()
    {
        // 從測試專案目錄向上找到包含 src 目錄的根目錄
        var currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());
        
        while (currentDirectory != null && !Directory.Exists(Path.Combine(currentDirectory.FullName, "src")))
        {
            currentDirectory = currentDirectory.Parent;
        }
        
        if (currentDirectory == null)
        {
            throw new DirectoryNotFoundException("找不到包含 src 目錄的專案根目錄");
        }
        
        var projectPath = Path.Combine(currentDirectory.FullName, "src", "ReleaseKit.Console");
        
        if (!Directory.Exists(projectPath))
        {
            throw new DirectoryNotFoundException($"找不到專案目錄: {projectPath}");
        }
        
        return projectPath;
    }
}
