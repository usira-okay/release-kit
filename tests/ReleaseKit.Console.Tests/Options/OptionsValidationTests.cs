using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReleaseKit.Console.Extensions;
using ReleaseKit.Console.Options;

namespace ReleaseKit.Console.Tests.Options;

/// <summary>
/// 測試組態設定驗證功能
/// </summary>
public class OptionsValidationTests
{
    [Fact]
    public void GitLabOptions_ShouldValidate_WhenApiUrlIsMissing()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "GitLab:ApiUrl", "" },
                { "GitLab:AccessToken", "" }
            })
            .Build();

        var services = new ServiceCollection();
        services.AddConfigurationOption(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        var exception = Assert.Throws<OptionsValidationException>(() =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<GitLabOptions>>().Value;
        });

        Assert.Contains("GitLab:ApiUrl", exception.Message);
    }

    [Fact]
    public void BitbucketOptions_ShouldValidate_WhenApiUrlIsMissing()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Bitbucket:ApiUrl", "" },
                { "Bitbucket:Email", "" },
                { "Bitbucket:AccessToken", "" }
            })
            .Build();

        var services = new ServiceCollection();
        services.AddConfigurationOption(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        var exception = Assert.Throws<OptionsValidationException>(() =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<BitbucketOptions>>().Value;
        });

        Assert.Contains("Bitbucket:ApiUrl", exception.Message);
    }

    [Fact]
    public void GitLabOptions_ShouldValidate_WhenProjectPathIsMissing()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "GitLab:ApiUrl", "https://gitlab.com/api/v4" },
                { "GitLab:Projects:0:ProjectPath", "" },
                { "GitLab:Projects:0:TargetBranch", "main" }
            })
            .Build();

        var services = new ServiceCollection();
        services.AddConfigurationOption(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        var exception = Assert.Throws<OptionsValidationException>(() =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<GitLabOptions>>().Value;
        });

        Assert.Contains("ProjectPath", exception.Message);
    }

    [Fact]
    public void GitLabOptions_ShouldValidate_WhenTargetBranchIsMissing()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "GitLab:ApiUrl", "https://gitlab.com/api/v4" },
                { "GitLab:Projects:0:ProjectPath", "mygroup/myproject" },
                { "GitLab:Projects:0:TargetBranch", "" }
            })
            .Build();

        var services = new ServiceCollection();
        services.AddConfigurationOption(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        var exception = Assert.Throws<OptionsValidationException>(() =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<GitLabOptions>>().Value;
        });

        Assert.Contains("TargetBranch", exception.Message);
    }

    [Fact]
    public void BitbucketOptions_ShouldValidate_WhenProjectPathIsMissing()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Bitbucket:ApiUrl", "https://api.bitbucket.org/2.0" },
                { "Bitbucket:Projects:0:ProjectPath", "" },
                { "Bitbucket:Projects:0:TargetBranch", "main" }
            })
            .Build();

        var services = new ServiceCollection();
        services.AddConfigurationOption(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        var exception = Assert.Throws<OptionsValidationException>(() =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<BitbucketOptions>>().Value;
        });

        Assert.Contains("ProjectPath", exception.Message);
    }

    [Fact]
    public void BitbucketOptions_ShouldValidate_WhenTargetBranchIsMissing()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Bitbucket:ApiUrl", "https://api.bitbucket.org/2.0" },
                { "Bitbucket:Projects:0:ProjectPath", "mygroup/myproject" },
                { "Bitbucket:Projects:0:TargetBranch", "" }
            })
            .Build();

        var services = new ServiceCollection();
        services.AddConfigurationOption(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        var exception = Assert.Throws<OptionsValidationException>(() =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<BitbucketOptions>>().Value;
        });

        Assert.Contains("TargetBranch", exception.Message);
    }
}
