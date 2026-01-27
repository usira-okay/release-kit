using Microsoft.Extensions.Configuration;

namespace ReleaseKit.Console.Tests;

/// <summary>
/// 測試應用程式組態載入功能
/// </summary>
public class ConfigurationTests
{
    [Fact]
    public void Configuration_ShouldLoad_AppsettingsJson()
    {
        // Arrange
        var basePath = GetProjectBasePath();
        
        // Act
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
        
        // Assert
        Assert.NotNull(configuration);
        Assert.Equal("Information", configuration["Serilog:MinimumLevel:Default"]);
        Assert.Equal("Warning", configuration["Serilog:MinimumLevel:Override:Microsoft"]);
    }
    
    [Theory]
    [InlineData("Development", "Debug")]
    [InlineData("Qa", "Information")]
    [InlineData("Production", "Information")]
    public void Configuration_ShouldLoad_EnvironmentSpecificSettings(string environment, string expectedLogLevel)
    {
        // Arrange
        var basePath = GetProjectBasePath();
        
        // Act
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
            .Build();
        
        // Assert
        Assert.NotNull(configuration);
        Assert.Equal(expectedLogLevel, configuration["Serilog:MinimumLevel:Default"]);
    }
    
    [Fact]
    public void Configuration_ShouldSupport_EnvironmentVariables()
    {
        // Arrange
        var basePath = GetProjectBasePath();
        Environment.SetEnvironmentVariable("Serilog__MinimumLevel__Override__TestValue", "TestFromEnvVar");
        
        // Act
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();
        
        // Assert
        Assert.Equal("TestFromEnvVar", configuration["Serilog:MinimumLevel:Override:TestValue"]);
        
        // Cleanup
        Environment.SetEnvironmentVariable("Serilog__MinimumLevel__Override__TestValue", null);
    }
    
    [Fact]
    public void Configuration_EnvironmentVariables_ShouldOverride_JsonSettings()
    {
        // Arrange
        var basePath = GetProjectBasePath();
        Environment.SetEnvironmentVariable("Serilog__MinimumLevel__Default", "Critical");
        
        // Act
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();
        
        // Assert
        Assert.Equal("Critical", configuration["Serilog:MinimumLevel:Default"]);
        
        // Cleanup
        Environment.SetEnvironmentVariable("Serilog__MinimumLevel__Default", null);
    }
    
    [Fact]
    public void Configuration_ShouldLoad_GitLabSettings()
    {
        // Arrange
        var basePath = GetProjectBasePath();
        
        // Act
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
        
        // Assert
        Assert.Equal("https://gitlab.com/api/v4", configuration["GitLab:ApiUrl"]);
        Assert.Equal("", configuration["GitLab:AccessToken"]);
        Assert.Equal("mygroup/backend-api", configuration["GitLab:Projects:0:ProjectPath"]);
        Assert.Equal("main", configuration["GitLab:Projects:0:TargetBranch"]);
    }
    
    [Fact]
    public void Configuration_ShouldLoad_BitBucketSettings()
    {
        // Arrange
        var basePath = GetProjectBasePath();
        
        // Act
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
        
        // Assert
        Assert.Equal("https://api.bitbucket.org/2.0", configuration["BitBucket:ApiUrl"]);
        Assert.Equal("", configuration["BitBucket:Email"]);
        Assert.Equal("", configuration["BitBucket:AccessToken"]);
        Assert.Equal("mygroup/backend-api", configuration["BitBucket:Projects:0:ProjectPath"]);
        Assert.Equal("main", configuration["BitBucket:Projects:0:TargetBranch"]);
    }
    
    [Fact]
    public void Configuration_ShouldLoad_UserMappingSettings()
    {
        // Arrange
        var basePath = GetProjectBasePath();
        
        // Act
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
        
        // Assert
        Assert.Equal("john.doe", configuration["UserMapping:Mappings:0:GitLabUserId"]);
        Assert.Equal("jdoe", configuration["UserMapping:Mappings:0:BitBucketUserId"]);
        Assert.Equal("John Doe", configuration["UserMapping:Mappings:0:DisplayName"]);
        Assert.Equal("jane.smith", configuration["UserMapping:Mappings:1:GitLabUserId"]);
        Assert.Equal("jsmith", configuration["UserMapping:Mappings:1:BitBucketUserId"]);
        Assert.Equal("Jane Smith", configuration["UserMapping:Mappings:1:DisplayName"]);
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
