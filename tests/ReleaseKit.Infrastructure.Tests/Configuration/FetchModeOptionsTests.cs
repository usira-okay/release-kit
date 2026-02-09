using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using Xunit;

namespace ReleaseKit.Infrastructure.Tests.Configuration;

/// <summary>
/// FetchModeOptions 配置測試
/// </summary>
public class FetchModeOptionsTests
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
                ["FetchMode"] = "DateTimeRange",
                ["SourceBranch"] = null,
                ["StartDateTime"] = "2025-01-01T00:00:00Z",
                ["EndDateTime"] = "2025-01-31T23:59:59Z"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<FetchModeOptions>()
            .Bind(configuration);

        var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<FetchModeOptions>>().Value;

        // Assert
        options.FetchMode.Should().Be(FetchMode.DateTimeRange);
        options.SourceBranch.Should().BeNull();
        options.StartDateTime.Should().Be(DateTimeOffset.Parse("2025-01-01T00:00:00Z"));
        options.EndDateTime.Should().Be(DateTimeOffset.Parse("2025-01-31T23:59:59Z"));
    }

    /// <summary>
    /// 測試環境變數覆寫配置
    /// </summary>
    [Fact]
    public void Bind_EnvironmentVariableOverride_ShouldUseEnvironmentValue()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FetchMode"] = "DateTimeRange",
                ["StartDateTime"] = "2025-01-01T00:00:00Z"
            })
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FetchMode"] = "BranchDiff", // 環境變數覆寫
                ["SourceBranch"] = "develop"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<FetchModeOptions>()
            .Bind(configuration);

        var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<FetchModeOptions>>().Value;

        // Assert
        options.FetchMode.Should().Be(FetchMode.BranchDiff);
        options.SourceBranch.Should().Be("develop");
    }
}
