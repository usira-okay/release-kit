using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReleaseKit.Console.Options;

namespace ReleaseKit.Console.Tests.Options;

/// <summary>
/// 測試配置選項綁定功能
/// </summary>
public class ConfigurationOptionsTests
{
    [Fact]
    public void GitLabOptions_ShouldBind_FromConfiguration()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(GetAppsettingsPath(), optional: false)
            .Build();

        var services = new ServiceCollection();
        services.Configure<GitLabOptions>(configuration.GetSection(GitLabOptions.SectionName));
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<GitLabOptions>>().Value;

        // Assert
        Assert.NotNull(options);
        Assert.Equal("https://gitlab.com/api/v4", options.ApiUrl);
        Assert.Equal("", options.AccessToken);
        Assert.NotEmpty(options.Projects);
        Assert.Equal("mygroup/backend-api", options.Projects[0].ProjectPath);
        Assert.Equal("main", options.Projects[0].TargetBranch);
    }

    [Fact]
    public void BitbucketOptions_ShouldBind_FromConfiguration()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(GetAppsettingsPath(), optional: false)
            .Build();

        var services = new ServiceCollection();
        services.Configure<BitbucketOptions>(configuration.GetSection(BitbucketOptions.SectionName));
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<BitbucketOptions>>().Value;

        // Assert
        Assert.NotNull(options);
        Assert.Equal("https://api.bitbucket.org/2.0", options.ApiUrl);
        Assert.Equal("", options.Email);
        Assert.Equal("", options.AccessToken);
        Assert.NotEmpty(options.Projects);
        Assert.Equal("mygroup/backend-api", options.Projects[0].ProjectPath);
        Assert.Equal("main", options.Projects[0].TargetBranch);
    }

    [Fact]
    public void UserMappingOptions_ShouldBind_FromConfiguration()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(GetAppsettingsPath(), optional: false)
            .Build();

        var services = new ServiceCollection();
        services.Configure<UserMappingOptions>(configuration.GetSection(UserMappingOptions.SectionName));
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<UserMappingOptions>>().Value;

        // Assert
        Assert.NotNull(options);
        Assert.NotEmpty(options.Mappings);
        Assert.Equal(2, options.Mappings.Count);
        
        Assert.Equal("john.doe", options.Mappings[0].GitLabUserId);
        Assert.Equal("jdoe", options.Mappings[0].BitbucketUserId);
        Assert.Equal("John Doe", options.Mappings[0].DisplayName);
        
        Assert.Equal("jane.smith", options.Mappings[1].GitLabUserId);
        Assert.Equal("jsmith", options.Mappings[1].BitbucketUserId);
        Assert.Equal("Jane Smith", options.Mappings[1].DisplayName);
    }

    /// <summary>
    /// 取得 appsettings.json 檔案路徑
    /// </summary>
    private static string GetAppsettingsPath()
    {
        var currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());
        
        while (currentDirectory != null && !Directory.Exists(Path.Combine(currentDirectory.FullName, "src")))
        {
            currentDirectory = currentDirectory.Parent;
        }
        
        if (currentDirectory == null)
        {
            throw new DirectoryNotFoundException("找不到包含 src 目錄的專案根目錄");
        }
        
        var appsettingsPath = Path.Combine(currentDirectory.FullName, "src", "ReleaseKit.Console", "appsettings.json");
        
        if (!File.Exists(appsettingsPath))
        {
            throw new FileNotFoundException($"找不到 appsettings.json 檔案: {appsettingsPath}");
        }
        
        return appsettingsPath;
    }
}
