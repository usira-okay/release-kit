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
        Assert.NotEmpty(options.Mappings);
        Assert.Equal(2, options.Mappings.Count);
        
        var firstMapping = options.Mappings[0];
        Assert.Equal("john.doe", firstMapping.GitLabUserId);
        Assert.Equal("jdoe", firstMapping.BitbucketUserId);
        Assert.Equal("John Doe", firstMapping.DisplayName);
        
        var secondMapping = options.Mappings[1];
        Assert.Equal("jane.smith", secondMapping.GitLabUserId);
        Assert.Equal("jsmith", secondMapping.BitbucketUserId);
        Assert.Equal("Jane Smith", secondMapping.DisplayName);
    }
    
    [Fact]
    public void GitLabOptions_EnvironmentVariables_ShouldOverride_JsonSettings()
    {
        // Arrange
        var basePath = GetProjectBasePath();
        Environment.SetEnvironmentVariable("GitLab__ApiUrl", "https://custom-gitlab.example.com/api/v4");
        Environment.SetEnvironmentVariable("GitLab__AccessToken", "test-token-123");
        
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
        
        // Cleanup
        Environment.SetEnvironmentVariable("GitLab__ApiUrl", null);
        Environment.SetEnvironmentVariable("GitLab__AccessToken", null);
    }
    
    [Fact]
    public void BitbucketOptions_EnvironmentVariables_ShouldOverride_JsonSettings()
    {
        // Arrange
        var basePath = GetProjectBasePath();
        Environment.SetEnvironmentVariable("Bitbucket__Email", "test@example.com");
        Environment.SetEnvironmentVariable("Bitbucket__AccessToken", "bitbucket-token-456");
        
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
        
        // Cleanup
        Environment.SetEnvironmentVariable("Bitbucket__Email", null);
        Environment.SetEnvironmentVariable("Bitbucket__AccessToken", null);
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
