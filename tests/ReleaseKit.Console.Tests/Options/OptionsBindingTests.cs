using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReleaseKit.Console.Extensions;
using ReleaseKit.Console.Options;

namespace ReleaseKit.Console.Tests.Options;

/// <summary>
/// 測試應用程式組態設定綁定功能
/// </summary>
public class OptionsBindingTests
{
    [Fact]
    public void GitLabOptions_ShouldBind_FromConfiguration()
    {
        // Arrange
        var basePath = GetProjectBasePath();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var services = new ServiceCollection();
        services.AddOptions(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var gitLabOptions = serviceProvider.GetRequiredService<IOptions<GitLabOptions>>().Value;

        // Assert
        Assert.NotNull(gitLabOptions);
        Assert.Equal("https://gitlab.com/api/v4", gitLabOptions.ApiUrl);
        Assert.Empty(gitLabOptions.AccessToken);
        Assert.NotEmpty(gitLabOptions.Projects);
        Assert.Single(gitLabOptions.Projects);
        Assert.Equal("mygroup/backend-api", gitLabOptions.Projects[0].ProjectPath);
        Assert.Equal("main", gitLabOptions.Projects[0].TargetBranch);
    }

    [Fact]
    public void BitbucketOptions_ShouldBind_FromConfiguration()
    {
        // Arrange
        var basePath = GetProjectBasePath();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var services = new ServiceCollection();
        services.AddOptions(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var bitbucketOptions = serviceProvider.GetRequiredService<IOptions<BitbucketOptions>>().Value;

        // Assert
        Assert.NotNull(bitbucketOptions);
        Assert.Equal("https://api.bitbucket.org/2.0", bitbucketOptions.ApiUrl);
        Assert.Empty(bitbucketOptions.Email);
        Assert.Empty(bitbucketOptions.AccessToken);
        Assert.NotEmpty(bitbucketOptions.Projects);
        Assert.Single(bitbucketOptions.Projects);
        Assert.Equal("mygroup/backend-api", bitbucketOptions.Projects[0].ProjectPath);
        Assert.Equal("main", bitbucketOptions.Projects[0].TargetBranch);
    }

    [Fact]
    public void UserMappingOptions_ShouldBind_FromConfiguration()
    {
        // Arrange
        var basePath = GetProjectBasePath();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var services = new ServiceCollection();
        services.AddOptions(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var userMappingOptions = serviceProvider.GetRequiredService<IOptions<UserMappingOptions>>().Value;

        // Assert
        Assert.NotNull(userMappingOptions);
        Assert.NotEmpty(userMappingOptions.Mappings);
        Assert.Equal(2, userMappingOptions.Mappings.Count);
        
        // 檢查第一個使用者對應
        Assert.Equal("john.doe", userMappingOptions.Mappings[0].GitLabUserId);
        Assert.Equal("jdoe", userMappingOptions.Mappings[0].BitbucketUserId);
        Assert.Equal("John Doe", userMappingOptions.Mappings[0].DisplayName);
        
        // 檢查第二個使用者對應
        Assert.Equal("jane.smith", userMappingOptions.Mappings[1].GitLabUserId);
        Assert.Equal("jsmith", userMappingOptions.Mappings[1].BitbucketUserId);
        Assert.Equal("Jane Smith", userMappingOptions.Mappings[1].DisplayName);
    }

    [Fact]
    public void AllOptions_ShouldBind_Simultaneously()
    {
        // Arrange
        var basePath = GetProjectBasePath();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var services = new ServiceCollection();
        services.AddOptions(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        var gitLabOptions = serviceProvider.GetRequiredService<IOptions<GitLabOptions>>().Value;
        Assert.NotNull(gitLabOptions);
        Assert.NotEmpty(gitLabOptions.ApiUrl);

        var bitbucketOptions = serviceProvider.GetRequiredService<IOptions<BitbucketOptions>>().Value;
        Assert.NotNull(bitbucketOptions);
        Assert.NotEmpty(bitbucketOptions.ApiUrl);

        var userMappingOptions = serviceProvider.GetRequiredService<IOptions<UserMappingOptions>>().Value;
        Assert.NotNull(userMappingOptions);
        Assert.NotEmpty(userMappingOptions.Mappings);
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
