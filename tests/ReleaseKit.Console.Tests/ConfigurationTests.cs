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
        Assert.Equal("Information", configuration["Logging:LogLevel:Default"]);
        Assert.Equal("Warning", configuration["Logging:LogLevel:Microsoft"]);
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
        Assert.Equal(expectedLogLevel, configuration["Logging:LogLevel:Default"]);
    }
    
    [Fact]
    public void Configuration_ShouldSupport_EnvironmentVariables()
    {
        // Arrange
        var basePath = GetProjectBasePath();
        Environment.SetEnvironmentVariable("Logging__LogLevel__TestValue", "TestFromEnvVar");
        
        // Act
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();
        
        // Assert
        Assert.Equal("TestFromEnvVar", configuration["Logging:LogLevel:TestValue"]);
        
        // Cleanup
        Environment.SetEnvironmentVariable("Logging__LogLevel__TestValue", null);
    }
    
    [Fact]
    public void Configuration_EnvironmentVariables_ShouldOverride_JsonSettings()
    {
        // Arrange
        var basePath = GetProjectBasePath();
        Environment.SetEnvironmentVariable("Logging__LogLevel__Default", "Critical");
        
        // Act
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();
        
        // Assert
        Assert.Equal("Critical", configuration["Logging:LogLevel:Default"]);
        
        // Cleanup
        Environment.SetEnvironmentVariable("Logging__LogLevel__Default", null);
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
