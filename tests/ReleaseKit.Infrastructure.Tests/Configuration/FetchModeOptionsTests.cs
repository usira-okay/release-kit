using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReleaseKit.Infrastructure.Configuration;
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
            .Bind(configuration)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<FetchModeOptions>>().Value;

        // Assert
        options.FetchMode.Should().Be("DateTimeRange");
        options.SourceBranch.Should().BeNull();
        options.StartDateTime.Should().Be(DateTimeOffset.Parse("2025-01-01T00:00:00Z"));
        options.EndDateTime.Should().Be(DateTimeOffset.Parse("2025-01-31T23:59:59Z"));
    }

    /// <summary>
    /// 測試缺少必要屬性時應拋出驗證異常
    /// </summary>
    [Fact]
    public void Validate_MissingRequiredProperty_ShouldThrowException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FetchMode"] = "" // 空值，違反 Required
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<FetchModeOptions>()
            .Bind(configuration)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        // Act & Assert - 嘗試訪問選項值應觸發驗證
        var act = () => serviceProvider.GetRequiredService<IOptions<FetchModeOptions>>().Value;
        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*FetchMode*");
    }
}
