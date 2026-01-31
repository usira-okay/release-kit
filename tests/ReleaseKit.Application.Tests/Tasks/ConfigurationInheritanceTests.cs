using ReleaseKit.Application.Common;
using ReleaseKit.Common.Configuration;

namespace ReleaseKit.Application.Tests.Tasks;

/// <summary>
/// 設定繼承邏輯的單元測試
/// </summary>
public sealed class ConfigurationInheritanceTests
{
    [Fact]
    public void MergeGitLabProjectOptions_WhenProjectOverridesTargetBranch_ShouldUseProjectValue()
    {
        // Arrange
        var rootFetchMode = new FetchModeOptions
        {
            FetchMode = FetchMode.DateTimeRange,
            StartDateTime = DateTimeOffset.Parse("2024-01-01T00:00:00Z"),
            EndDateTime = DateTimeOffset.Parse("2024-01-31T23:59:59Z"),
            TargetBranch = "main"
        };

        var projectOptions = new GitLabProjectOptions
        {
            ProjectPath = "group/project1",
            TargetBranch = "production" // 覆蓋
        };

        // Act
        var merged = ConfigurationHelper.MergeGitLabProjectOptions(rootFetchMode, projectOptions);

        // Assert
        Assert.Equal("production", merged.TargetBranch);
        Assert.Equal(rootFetchMode.StartDateTime, merged.StartDateTime);
        Assert.Equal(rootFetchMode.EndDateTime, merged.EndDateTime);
        Assert.Equal(rootFetchMode.FetchMode, merged.FetchMode);
    }

    [Fact]
    public void MergeGitLabProjectOptions_WhenProjectOverridesAllFields_ShouldUseAllProjectValues()
    {
        // Arrange
        var rootFetchMode = new FetchModeOptions
        {
            FetchMode = FetchMode.DateTimeRange,
            StartDateTime = DateTimeOffset.Parse("2024-01-01T00:00:00Z"),
            EndDateTime = DateTimeOffset.Parse("2024-01-31T23:59:59Z"),
            TargetBranch = "main"
        };

        var projectOptions = new GitLabProjectOptions
        {
            ProjectPath = "group/project1",
            FetchMode = FetchMode.BranchDiff,
            SourceBranch = "release/20240101",
            TargetBranch = "develop"
        };

        // Act
        var merged = ConfigurationHelper.MergeGitLabProjectOptions(rootFetchMode, projectOptions);

        // Assert
        Assert.Equal(FetchMode.BranchDiff, merged.FetchMode);
        Assert.Equal("release/20240101", merged.SourceBranch);
        Assert.Equal("develop", merged.TargetBranch);
    }

    [Fact]
    public void MergeGitLabProjectOptions_WhenProjectHasNoOverrides_ShouldUseRootValues()
    {
        // Arrange
        var rootFetchMode = new FetchModeOptions
        {
            FetchMode = FetchMode.DateTimeRange,
            StartDateTime = DateTimeOffset.Parse("2024-01-01T00:00:00Z"),
            EndDateTime = DateTimeOffset.Parse("2024-01-31T23:59:59Z"),
            TargetBranch = "main"
        };

        var projectOptions = new GitLabProjectOptions
        {
            ProjectPath = "group/project1"
        };

        // Act
        var merged = ConfigurationHelper.MergeGitLabProjectOptions(rootFetchMode, projectOptions);

        // Assert
        Assert.Equal(FetchMode.DateTimeRange, merged.FetchMode);
        Assert.Equal("main", merged.TargetBranch);
        Assert.Equal(rootFetchMode.StartDateTime, merged.StartDateTime);
        Assert.Equal(rootFetchMode.EndDateTime, merged.EndDateTime);
    }

    [Fact]
    public void MergeBitbucketProjectOptions_WhenProjectOverridesFields_ShouldUseProjectValues()
    {
        // Arrange
        var rootFetchMode = new FetchModeOptions
        {
            FetchMode = FetchMode.DateTimeRange,
            StartDateTime = DateTimeOffset.Parse("2024-01-01T00:00:00Z"),
            EndDateTime = DateTimeOffset.Parse("2024-01-31T23:59:59Z"),
            TargetBranch = "master"
        };

        var projectOptions = new BitbucketProjectOptions
        {
            ProjectPath = "workspace/repo",
            TargetBranch = "main"
        };

        // Act
        var merged = ConfigurationHelper.MergeBitbucketProjectOptions(rootFetchMode, projectOptions);

        // Assert
        Assert.Equal("main", merged.TargetBranch);
        Assert.Equal(rootFetchMode.StartDateTime, merged.StartDateTime);
    }

    [Fact]
    public void MergeGitLabProjectOptions_WhenProjectOnlyOverridesTargetBranch_ShouldKeepOtherRootValues()
    {
        // Arrange
        var rootFetchMode = new FetchModeOptions
        {
            FetchMode = FetchMode.DateTimeRange,
            StartDateTime = DateTimeOffset.Parse("2024-01-01T00:00:00Z"),
            EndDateTime = DateTimeOffset.Parse("2024-01-31T23:59:59Z"),
            TargetBranch = "main"
        };

        var projectOptions = new GitLabProjectOptions
        {
            ProjectPath = "group/project1",
            TargetBranch = "production"
        };

        // Act
        var merged = ConfigurationHelper.MergeGitLabProjectOptions(rootFetchMode, projectOptions);

        // Assert
        Assert.Equal(FetchMode.DateTimeRange, merged.FetchMode);
        Assert.Equal("production", merged.TargetBranch);
        Assert.Equal(rootFetchMode.StartDateTime, merged.StartDateTime);
        Assert.Equal(rootFetchMode.EndDateTime, merged.EndDateTime);
    }

    [Fact]
    public void MergeGitLabProjectOptions_WhenProjectHasEmptyTargetBranch_ShouldUseRootValue()
    {
        // Arrange
        var rootFetchMode = new FetchModeOptions
        {
            TargetBranch = "main"
        };

        var projectOptions = new GitLabProjectOptions
        {
            ProjectPath = "group/project1",
            TargetBranch = "" // 空字串應該使用根層級
        };

        // Act
        var merged = ConfigurationHelper.MergeGitLabProjectOptions(rootFetchMode, projectOptions);

        // Assert
        Assert.Equal("main", merged.TargetBranch);
    }
}
