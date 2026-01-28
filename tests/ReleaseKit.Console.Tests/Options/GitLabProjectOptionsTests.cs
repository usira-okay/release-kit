using FluentAssertions;
using ReleaseKit.Console.Options;

namespace ReleaseKit.Console.Tests.Options;

/// <summary>
/// GitLabProjectOptions 單元測試
/// </summary>
public class GitLabProjectOptionsTests
{
    [Fact]
    public void Validate_WhenProjectPathIsEmpty_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var options = new GitLabProjectOptions
        {
            ProjectPath = "",
            TargetBranch = "main"
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("GitLab:Projects:ProjectPath 組態設定不得為空");
    }

    [Fact]
    public void Validate_WhenTargetBranchIsEmpty_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var options = new GitLabProjectOptions
        {
            ProjectPath = "mygroup/backend-api",
            TargetBranch = ""
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("GitLab:Projects:TargetBranch 組態設定不得為空");
    }

    [Fact]
    public void Validate_WhenFetchModeIsBranchDiffButSourceBranchIsEmpty_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var options = new GitLabProjectOptions
        {
            ProjectPath = "mygroup/backend-api",
            TargetBranch = "main",
            FetchMode = FetchMode.BranchDiff,
            SourceBranch = ""
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("當 FetchMode 為 BranchDiff 時，SourceBranch 不得為空");
    }

    [Fact]
    public void Validate_WhenFetchModeIsDateTimeRangeButStartDateTimeIsNull_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var options = new GitLabProjectOptions
        {
            ProjectPath = "mygroup/backend-api",
            TargetBranch = "main",
            FetchMode = FetchMode.DateTimeRange,
            StartDateTime = null,
            EndDateTime = DateTimeOffset.Now
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("當 FetchMode 為 DateTimeRange 時，StartDateTime 不得為空");
    }

    [Fact]
    public void Validate_WhenFetchModeIsDateTimeRangeButEndDateTimeIsNull_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var options = new GitLabProjectOptions
        {
            ProjectPath = "mygroup/backend-api",
            TargetBranch = "main",
            FetchMode = FetchMode.DateTimeRange,
            StartDateTime = DateTimeOffset.Now,
            EndDateTime = null
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("當 FetchMode 為 DateTimeRange 時，EndDateTime 不得為空");
    }

    [Fact]
    public void Validate_WhenFetchModeIsDateTimeRangeButStartDateTimeIsAfterEndDateTime_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var options = new GitLabProjectOptions
        {
            ProjectPath = "mygroup/backend-api",
            TargetBranch = "main",
            FetchMode = FetchMode.DateTimeRange,
            StartDateTime = new DateTimeOffset(2025, 2, 1, 0, 0, 0, TimeSpan.FromHours(8)),
            EndDateTime = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.FromHours(8))
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("StartDateTime 必須早於 EndDateTime");
    }

    [Fact]
    public void Validate_WhenFetchModeIsNotSet_ShouldNotThrow()
    {
        // Arrange
        var options = new GitLabProjectOptions
        {
            ProjectPath = "mygroup/backend-api",
            TargetBranch = "main",
            FetchMode = null
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WhenFetchModeIsBranchDiffAndSourceBranchIsSet_ShouldNotThrow()
    {
        // Arrange
        var options = new GitLabProjectOptions
        {
            ProjectPath = "mygroup/backend-api",
            TargetBranch = "main",
            FetchMode = FetchMode.BranchDiff,
            SourceBranch = "release/20260128"
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WhenFetchModeIsDateTimeRangeAndDateTimesAreValid_ShouldNotThrow()
    {
        // Arrange
        var options = new GitLabProjectOptions
        {
            ProjectPath = "mygroup/backend-api",
            TargetBranch = "main",
            FetchMode = FetchMode.DateTimeRange,
            StartDateTime = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.FromHours(8)),
            EndDateTime = new DateTimeOffset(2025, 1, 31, 23, 59, 59, TimeSpan.FromHours(8))
        };

        // Act
        var act = () => options.Validate();

        // Assert
        act.Should().NotThrow();
    }
}
