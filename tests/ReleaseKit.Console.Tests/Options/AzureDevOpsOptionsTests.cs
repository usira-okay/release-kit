using FluentAssertions;
using ReleaseKit.Console.Options;

namespace ReleaseKit.Console.Tests.Options;

/// <summary>
/// AzureDevOpsOptions 單元測試
/// </summary>
public class AzureDevOpsOptionsTests
{
    [Fact]
    public void Validate_WhenOrganizationUrlIsEmpty_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var options = new AzureDevOpsOptions
        {
            OrganizationUrl = "",
            TeamMapping = new List<TeamMappingOptions>()
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("AzureDevOps:OrganizationUrl 組態設定不得為空");
    }

    [Fact]
    public void Validate_WhenOrganizationUrlIsInvalidFormat_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var options = new AzureDevOpsOptions
        {
            OrganizationUrl = "not-a-valid-url",
            TeamMapping = new List<TeamMappingOptions>()
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("AzureDevOps:OrganizationUrl 必須為有效的 URL 格式");
    }

    [Fact]
    public void Validate_WhenTeamMappingIsInvalid_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var options = new AzureDevOpsOptions
        {
            OrganizationUrl = "https://dev.azure.com/myorganization",
            TeamMapping = new List<TeamMappingOptions>
            {
                new TeamMappingOptions
                {
                    OriginalTeamName = "",  // Invalid
                    DisplayName = "金流團隊"
                }
            }
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("AzureDevOps:TeamMapping:OriginalTeamName 組態設定不得為空");
    }

    [Fact]
    public void Validate_WhenOrganizationUrlIsValidAndTeamMappingIsEmpty_ShouldNotThrow()
    {
        // Arrange
        var options = new AzureDevOpsOptions
        {
            OrganizationUrl = "https://dev.azure.com/myorganization",
            TeamMapping = new List<TeamMappingOptions>()
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WhenAllFieldsAreValid_ShouldNotThrow()
    {
        // Arrange
        var options = new AzureDevOpsOptions
        {
            OrganizationUrl = "https://dev.azure.com/myorganization",
            TeamMapping = new List<TeamMappingOptions>
            {
                new TeamMappingOptions
                {
                    OriginalTeamName = "MoneyLogistic",
                    DisplayName = "金流團隊"
                },
                new TeamMappingOptions
                {
                    OriginalTeamName = "DailyResource",
                    DisplayName = "日常資源團隊"
                }
            }
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }
}
