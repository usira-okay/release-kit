using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReleaseKit.Infrastructure.Configuration;
using Xunit;

namespace ReleaseKit.Infrastructure.Tests.Configuration;

/// <summary>
/// AzureDevOpsOptions 配置測試
/// </summary>
public class AzureDevOpsOptionsTests
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
                ["AzureDevOps:OrganizationUrl"] = "https://dev.azure.com/testorg",
                ["AzureDevOps:TeamMapping:0:OriginalTeamName"] = "MoneyLogistic",
                ["AzureDevOps:TeamMapping:0:DisplayName"] = "金流團隊",
                ["AzureDevOps:TeamMapping:1:OriginalTeamName"] = "DailyResource",
                ["AzureDevOps:TeamMapping:1:DisplayName"] = "日常資源團隊"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<AzureDevOpsOptions>()
            .Bind(configuration.GetSection("AzureDevOps"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<AzureDevOpsOptions>>().Value;

        // Assert
        options.OrganizationUrl.Should().Be("https://dev.azure.com/testorg");
        options.TeamMapping.Should().HaveCount(2);
        options.TeamMapping[0].OriginalTeamName.Should().Be("MoneyLogistic");
        options.TeamMapping[0].DisplayName.Should().Be("金流團隊");
        options.TeamMapping[1].OriginalTeamName.Should().Be("DailyResource");
        options.TeamMapping[1].DisplayName.Should().Be("日常資源團隊");
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
                ["AzureDevOps:OrganizationUrl"] = "" // 空值，違反 Required
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<AzureDevOpsOptions>()
            .Bind(configuration.GetSection("AzureDevOps"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        // Act & Assert - 嘗試訪問選項值應觸發驗證
        var act = () => serviceProvider.GetRequiredService<IOptions<AzureDevOpsOptions>>().Value;
        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*OrganizationUrl*");
    }
}
