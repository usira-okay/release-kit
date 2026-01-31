using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using Xunit;

namespace ReleaseKit.Infrastructure.Tests.Configuration;

/// <summary>
/// BitbucketOptions 配置測試
/// </summary>
public class BitbucketOptionsTests
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
                ["Bitbucket:ApiUrl"] = "https://api.bitbucket.org/2.0",
                ["Bitbucket:Email"] = "user@example.com",
                ["Bitbucket:AccessToken"] = "test-app-password",
                ["Bitbucket:Projects:0:ProjectPath"] = "workspace/repo1",
                ["Bitbucket:Projects:0:TargetBranch"] = "main",
                ["Bitbucket:Projects:0:FetchMode"] = "DateTimeRange",
                ["Bitbucket:Projects:0:StartDateTime"] = "2025-01-01T00:00:00Z",
                ["Bitbucket:Projects:0:EndDateTime"] = "2025-01-31T23:59:59Z",
                ["Bitbucket:Projects:1:ProjectPath"] = "workspace/repo2",
                ["Bitbucket:Projects:1:TargetBranch"] = "develop",
                ["Bitbucket:Projects:1:FetchMode"] = "BranchDiff",
                ["Bitbucket:Projects:1:SourceBranch"] = "release/20250128"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<BitbucketOptions>()
            .Bind(configuration.GetSection("Bitbucket"));

        var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<BitbucketOptions>>().Value;

        // Assert
        options.ApiUrl.Should().Be("https://api.bitbucket.org/2.0");
        options.Email.Should().Be("user@example.com");
        options.AccessToken.Should().Be("test-app-password");
        options.Projects.Should().HaveCount(2);
        options.Projects[0].ProjectPath.Should().Be("workspace/repo1");
        options.Projects[0].TargetBranch.Should().Be("main");
        options.Projects[0].FetchMode.Should().Be(FetchMode.DateTimeRange);
        options.Projects[1].ProjectPath.Should().Be("workspace/repo2");
        options.Projects[1].FetchMode.Should().Be(FetchMode.BranchDiff);
        options.Projects[1].SourceBranch.Should().Be("release/20250128");
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
                ["Bitbucket:ApiUrl"] = "https://api.bitbucket.org/2.0",
                ["Bitbucket:Email"] = "user@example.com",
                ["Bitbucket:AccessToken"] = "original-password"
            })
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Bitbucket:AccessToken"] = "override-password" // 環境變數覆寫
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<BitbucketOptions>()
            .Bind(configuration.GetSection("Bitbucket"));

        var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<BitbucketOptions>>().Value;

        // Assert
        options.AccessToken.Should().Be("override-password");
    }
}
