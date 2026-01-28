using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReleaseKit.Infrastructure.Configuration;
using Xunit;

namespace ReleaseKit.Infrastructure.Tests.Configuration;

/// <summary>
/// GitLabOptions 配置測試
/// </summary>
public class GitLabOptionsTests
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
                ["GitLab:ApiUrl"] = "https://gitlab.com/api/v4",
                ["GitLab:AccessToken"] = "glpat-test-token",
                ["GitLab:Projects:0:ProjectPath"] = "group/project1",
                ["GitLab:Projects:0:TargetBranch"] = "main",
                ["GitLab:Projects:0:FetchMode"] = "DateTimeRange",
                ["GitLab:Projects:0:StartDateTime"] = "2025-01-01T00:00:00Z",
                ["GitLab:Projects:0:EndDateTime"] = "2025-01-31T23:59:59Z",
                ["GitLab:Projects:1:ProjectPath"] = "group/project2",
                ["GitLab:Projects:1:TargetBranch"] = "develop",
                ["GitLab:Projects:1:FetchMode"] = "BranchDiff",
                ["GitLab:Projects:1:SourceBranch"] = "release/20250128"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<GitLabOptions>()
            .Bind(configuration.GetSection("GitLab"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<GitLabOptions>>().Value;

        // Assert
        options.ApiUrl.Should().Be("https://gitlab.com/api/v4");
        options.AccessToken.Should().Be("glpat-test-token");
        options.Projects.Should().HaveCount(2);
        options.Projects[0].ProjectPath.Should().Be("group/project1");
        options.Projects[0].TargetBranch.Should().Be("main");
        options.Projects[0].FetchMode.Should().Be("DateTimeRange");
        options.Projects[1].ProjectPath.Should().Be("group/project2");
        options.Projects[1].FetchMode.Should().Be("BranchDiff");
        options.Projects[1].SourceBranch.Should().Be("release/20250128");
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
                ["GitLab:ApiUrl"] = "", // 空值，違反 Required
                ["GitLab:AccessToken"] = "token"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<GitLabOptions>()
            .Bind(configuration.GetSection("GitLab"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        // Act & Assert - 嘗試訪問選項值應觸發驗證
        var act = () => serviceProvider.GetRequiredService<IOptions<GitLabOptions>>().Value;
        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*ApiUrl*");
    }
}
