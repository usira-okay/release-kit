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
        Assert.Equal("ReleaseKit", configuration["Application:Name"]);
        Assert.Equal("1.0.0", configuration["Application:Version"]);
    }
    
    [Theory]
    [InlineData("Development")]
    [InlineData("Qa")]
    [InlineData("Production")]
    public void Configuration_ShouldLoad_EnvironmentSpecificSettings(string environment)
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
        Assert.Equal(environment, configuration["Application:Environment"]);
    }
    
    [Fact]
    public void Configuration_ShouldSupport_EnvironmentVariables()
    {
        // Arrange
        var basePath = GetProjectBasePath();
        Environment.SetEnvironmentVariable("Application__TestValue", "TestFromEnvVar");
        
        // Act
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();
        
        // Assert
        Assert.Equal("TestFromEnvVar", configuration["Application:TestValue"]);
        
        // Cleanup
        Environment.SetEnvironmentVariable("Application__TestValue", null);
    }
    
    [Fact]
    public void Configuration_EnvironmentVariables_ShouldOverride_JsonSettings()
    {
        // Arrange
        var basePath = GetProjectBasePath();
        Environment.SetEnvironmentVariable("Application__Name", "OverriddenName");
        
        // Act
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();
        
        // Assert
        Assert.Equal("OverriddenName", configuration["Application:Name"]);
        
        // Cleanup
        Environment.SetEnvironmentVariable("Application__Name", null);
    }
    
    /// <summary>
    /// 取得專案基礎路徑（包含 appsettings.json 的路徑）
    /// </summary>
    private static string GetProjectBasePath()
    {
        // 從測試專案目錄向上找到 src/ReleaseKit.Console 目錄
        var currentDirectory = Directory.GetCurrentDirectory();
        var projectPath = Path.GetFullPath(Path.Combine(currentDirectory, "..", "..", "..", "..", "..", "src", "ReleaseKit.Console"));
        
        if (!Directory.Exists(projectPath))
        {
            throw new DirectoryNotFoundException($"找不到專案目錄: {projectPath}");
        }
        
        return projectPath;
    }
}
