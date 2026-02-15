using ReleaseKit.Application.Common;
using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Common.Configuration;

namespace ReleaseKit.Application.Tests.Tasks;

/// <summary>
/// 多專案批次執行的整合測試
/// </summary>
/// <remarks>
/// 測試多專案設定合併與批次執行邏輯
/// </remarks>
public sealed class MultiProjectExecutionTests
{
    [Fact]
    public void PrepareProjectConfiguration_WithMultipleProjects_ShouldMergeCorrectly()
    {
        // Arrange
        var rootFetchMode = new FetchModeOptions
        {
            FetchMode = FetchMode.DateTimeRange,
            StartDateTime = DateTimeOffset.Parse("2024-01-01T00:00:00Z"),
            EndDateTime = DateTimeOffset.Parse("2024-01-31T23:59:59Z"),
            TargetBranch = "main"
        };

        var projects = new List<GitLabProjectOptions>
        {
            new() { ProjectPath = "group/project1" }, // 全用根層級設定
            new() { ProjectPath = "group/project2", TargetBranch = "production" }, // 覆蓋 TargetBranch
            new() { ProjectPath = "group/project3", FetchMode = FetchMode.BranchDiff, SourceBranch = "release/v1", TargetBranch = "develop" } // 完全覆蓋
        };

        // Act
        var mergedProject1 = ConfigurationHelper.MergeGitLabProjectOptions(rootFetchMode, projects[0]);
        var mergedProject2 = ConfigurationHelper.MergeGitLabProjectOptions(rootFetchMode, projects[1]);
        var mergedProject3 = ConfigurationHelper.MergeGitLabProjectOptions(rootFetchMode, projects[2]);

        // Assert
        // Project 1: 全用根層級
        Assert.Equal(FetchMode.DateTimeRange, mergedProject1.FetchMode);
        Assert.Equal("main", mergedProject1.TargetBranch);
        Assert.Equal(rootFetchMode.StartDateTime, mergedProject1.StartDateTime);

        // Project 2: 覆蓋 TargetBranch
        Assert.Equal(FetchMode.DateTimeRange, mergedProject2.FetchMode);
        Assert.Equal("production", mergedProject2.TargetBranch);
        Assert.Equal(rootFetchMode.StartDateTime, mergedProject2.StartDateTime);

        // Project 3: 完全覆蓋
        Assert.Equal(FetchMode.BranchDiff, mergedProject3.FetchMode);
        Assert.Equal("release/v1", mergedProject3.SourceBranch);
        Assert.Equal("develop", mergedProject3.TargetBranch);
    }

    [Fact]
    public void MapMergeRequestsToOutput_ShouldTransformCorrectly()
    {
        // Arrange
        var mergeRequests = new List<Domain.Entities.MergeRequest>
        {
            new()
            {
                PullRequestId = 1,
                Title = "feat: Add new feature",
                Description = "This is a test MR",
                SourceBranch = "feature/test",
                TargetBranch = "main",
                CreatedAt = DateTimeOffset.Parse("2024-01-15T10:00:00Z"),
                MergedAt = DateTimeOffset.Parse("2024-01-16T14:00:00Z"),
                State = "merged",
                AuthorUserId = "user123",
                AuthorName = "John Doe",
                PRUrl = "https://gitlab.example.com/group/project/-/merge_requests/1",
                Platform = Domain.ValueObjects.SourceControlPlatform.GitLab,
                ProjectPath = "group/project"
            }
        };

        // Act
        var outputs = mergeRequests.Select(mr => new MergeRequestOutput
        {
            Title = mr.Title,
            Description = mr.Description,
            SourceBranch = mr.SourceBranch,
            TargetBranch = mr.TargetBranch,
            CreatedAt = mr.CreatedAt,
            MergedAt = mr.MergedAt,
            State = mr.State,
            AuthorUserId = mr.AuthorUserId,
            AuthorName = mr.AuthorName,
            PRUrl = mr.PRUrl
        }).ToList();

        // Assert
        Assert.Single(outputs);
        var output = outputs[0];
        Assert.Equal("feat: Add new feature", output.Title);
        Assert.Equal("feature/test", output.SourceBranch);
        Assert.Equal("main", output.TargetBranch);
        Assert.Equal("user123", output.AuthorUserId);
        Assert.Equal("John Doe", output.AuthorName);
    }

    [Fact]
    public void AggregateMultiProjectResults_ShouldCreateFetchResult()
    {
        // Arrange
        var projectResults = new List<ProjectResult>
        {
            new()
            {
                ProjectPath = "group/backend",
                Platform = SourceControlPlatform.GitLab,
                PullRequests = new List<MergeRequestOutput>
                {
                    new() { Title = "Backend MR 1", SourceBranch = "feature/backend", TargetBranch = "main" }
                }
            },
            new()
            {
                ProjectPath = "group/frontend",
                Platform = SourceControlPlatform.GitLab,
                PullRequests = new List<MergeRequestOutput>
                {
                    new() { Title = "Frontend MR 1", SourceBranch = "feature/frontend", TargetBranch = "main" }
                }
            }
        };

        // Act
        var fetchResult = new FetchResult
        {
            Results = projectResults
        };

        // Assert
        Assert.Equal(2, fetchResult.Results.Count);
        Assert.Equal("group/backend", fetchResult.Results[0].ProjectPath);
        Assert.Equal("group/frontend", fetchResult.Results[1].ProjectPath);
        Assert.Single(fetchResult.Results[0].PullRequests);
        Assert.Single(fetchResult.Results[1].PullRequests);
    }

    [Fact]
    public void ProjectResult_WithError_ShouldHaveEmptyPullRequestsAndErrorMessage()
    {
        // Arrange & Act
        var projectResult = new ProjectResult
        {
            ProjectPath = "group/failed-project",
            Platform = SourceControlPlatform.GitLab,
            PullRequests = new List<MergeRequestOutput>(),
            Error = "API 驗證失敗: 401 Unauthorized"
        };

        // Assert
        Assert.Empty(projectResult.PullRequests);
        Assert.NotNull(projectResult.Error);
        Assert.Contains("401 Unauthorized", projectResult.Error);
    }

    [Fact]
    public void PrepareProjectConfiguration_ForBitbucket_ShouldMergeCorrectly()
    {
        // Arrange
        var rootFetchMode = new FetchModeOptions
        {
            FetchMode = FetchMode.DateTimeRange,
            StartDateTime = DateTimeOffset.Parse("2024-01-01T00:00:00Z"),
            EndDateTime = DateTimeOffset.Parse("2024-01-31T23:59:59Z"),
            TargetBranch = "master"
        };

        var bitbucketProject = new BitbucketProjectOptions
        {
            ProjectPath = "workspace/repo1",
            TargetBranch = "main"
        };

        // Act
        var merged = ConfigurationHelper.MergeBitbucketProjectOptions(rootFetchMode, bitbucketProject);

        // Assert
        Assert.Equal(FetchMode.DateTimeRange, merged.FetchMode);
        Assert.Equal("main", merged.TargetBranch);
        Assert.Equal(rootFetchMode.StartDateTime, merged.StartDateTime);
        Assert.Equal(rootFetchMode.EndDateTime, merged.EndDateTime);
    }
}
