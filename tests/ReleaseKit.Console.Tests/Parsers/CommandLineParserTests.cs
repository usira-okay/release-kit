using ReleaseKit.Application.Tasks;
using ReleaseKit.Console.Parsers;

namespace ReleaseKit.Console.Tests.Parsers;

/// <summary>
/// CommandLineParser 單元測試
/// </summary>
public class CommandLineParserTests
{
    private readonly CommandLineParser _parser;

    public CommandLineParserTests()
    {
        _parser = new CommandLineParser();
    }

    [Fact]
    public void Parse_WithNoArgs_ShouldReturnFailure()
    {
        // Arrange
        var args = Array.Empty<string>();

        // Act
        var result = _parser.Parse(args);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("請指定要執行的任務", result.ErrorMessage);
    }

    [Fact]
    public void Parse_WithNullArgs_ShouldReturnFailure()
    {
        // Arrange
        string[]? args = null;

        // Act
        var result = _parser.Parse(args!);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("請指定要執行的任務", result.ErrorMessage);
    }

    [Fact]
    public void Parse_WithMultipleArgs_ShouldReturnFailure()
    {
        // Arrange
        var args = new[] { "fetch-gitlab-pr", "fetch-bitbucket-pr" };

        // Act
        var result = _parser.Parse(args);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("每次只允許執行單一任務", result.ErrorMessage);
    }

    [Theory]
    [InlineData("fetch-gitlab-pr", TaskType.FetchGitLabPullRequests)]
    [InlineData("fetch-bitbucket-pr", TaskType.FetchBitbucketPullRequests)]
    [InlineData("fetch-azure-workitems", TaskType.FetchAzureDevOpsWorkItems)]
    [InlineData("update-googlesheet", TaskType.UpdateGoogleSheets)]
    [InlineData("fetch-gitlab-release-branch", TaskType.FetchGitLabReleaseBranch)]
    [InlineData("fetch-bitbucket-release-branch", TaskType.FetchBitbucketReleaseBranch)]
    [InlineData("filter-gitlab-pr-by-user", TaskType.FilterGitLabPullRequestsByUser)]
    [InlineData("filter-bitbucket-pr-by-user", TaskType.FilterBitbucketPullRequestsByUser)]
    [InlineData("get-user-story", TaskType.GetUserStory)]
    public void Parse_WithValidTaskName_ShouldReturnSuccessWithCorrectTaskType(string taskName, TaskType expectedTaskType)
    {
        // Arrange
        var args = new[] { taskName };

        // Act
        var result = _parser.Parse(args);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedTaskType, result.TaskType);
        Assert.Null(result.ErrorMessage);
    }

    [Theory]
    [InlineData("FETCH-GITLAB-PR", TaskType.FetchGitLabPullRequests)]
    [InlineData("FeTcH-BiTbUcKeT-pR", TaskType.FetchBitbucketPullRequests)]
    [InlineData("FETCH-AZURE-WORKITEMS", TaskType.FetchAzureDevOpsWorkItems)]
    [InlineData("UPDATE-GOOGLESHEET", TaskType.UpdateGoogleSheets)]
    [InlineData("FETCH-GITLAB-RELEASE-BRANCH", TaskType.FetchGitLabReleaseBranch)]
    [InlineData("FETCH-BITBUCKET-RELEASE-BRANCH", TaskType.FetchBitbucketReleaseBranch)]
    [InlineData("FILTER-GITLAB-PR-BY-USER", TaskType.FilterGitLabPullRequestsByUser)]
    [InlineData("FiLtEr-BiTbUcKeT-pR-bY-uSeR", TaskType.FilterBitbucketPullRequestsByUser)]
    [InlineData("GET-USER-STORY", TaskType.GetUserStory)]
    public void Parse_WithValidTaskName_ShouldBeCaseInsensitive(string taskName, TaskType expectedTaskType)
    {
        // Arrange
        var args = new[] { taskName };

        // Act
        var result = _parser.Parse(args);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedTaskType, result.TaskType);
    }

    [Fact]
    public void Parse_WithInvalidTaskName_ShouldReturnFailure()
    {
        // Arrange
        var args = new[] { "invalid-task" };

        // Act
        var result = _parser.Parse(args);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("不支援的任務", result.ErrorMessage);
        Assert.Contains("invalid-task", result.ErrorMessage);
    }

    [Fact]
    public void Parse_WithInvalidTaskName_ShouldShowValidTasks()
    {
        // Arrange
        var args = new[] { "unknown" };

        // Act
        var result = _parser.Parse(args);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("fetch-gitlab-pr", result.ErrorMessage);
        Assert.Contains("fetch-bitbucket-pr", result.ErrorMessage);
        Assert.Contains("fetch-azure-workitems", result.ErrorMessage);
        Assert.Contains("update-googlesheet", result.ErrorMessage);
        Assert.Contains("fetch-gitlab-release-branch", result.ErrorMessage);
        Assert.Contains("fetch-bitbucket-release-branch", result.ErrorMessage);
        Assert.Contains("filter-gitlab-pr-by-user", result.ErrorMessage);
        Assert.Contains("filter-bitbucket-pr-by-user", result.ErrorMessage);
    }
}
