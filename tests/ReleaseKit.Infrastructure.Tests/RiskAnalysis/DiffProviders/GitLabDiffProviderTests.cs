using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReleaseKit.Domain.Common;
using ReleaseKit.Infrastructure.RiskAnalysis.DiffProviders;
using ReleaseKit.Infrastructure.SourceControl.GitLab;

namespace ReleaseKit.Infrastructure.Tests.RiskAnalysis.DiffProviders;

/// <summary>
/// GitLabDiffProvider 的單元測試
/// </summary>
public class GitLabDiffProviderTests
{
    private readonly Mock<GitLabRepository> _gitLabRepositoryMock;
    private readonly Mock<ILogger<GitLabDiffProvider>> _loggerMock;

    public GitLabDiffProviderTests()
    {
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        var repoLoggerMock = new Mock<ILogger<GitLabRepository>>();
        _gitLabRepositoryMock = new Mock<GitLabRepository>(httpClientFactoryMock.Object, repoLoggerMock.Object);
        _loggerMock = new Mock<ILogger<GitLabDiffProvider>>();
    }

    [Fact]
    public async Task GetDiffAsync_WithValidResponse_ShouldReturnPullRequestDiff()
    {
        // Arrange
        var changesResponse = new GitLabMrChangesResponse
        {
            Changes = new List<GitLabMrChangeItem>
            {
                new()
                {
                    OldPath = "src/Service.cs",
                    NewPath = "src/Service.cs",
                    NewFile = false,
                    DeletedFile = false,
                    Diff = "@@ -1,3 +1,5 @@\n context\n+added1\n+added2\n context\n-removed1"
                },
                new()
                {
                    OldPath = "src/Controller.cs",
                    NewPath = "src/Controller.cs",
                    NewFile = false,
                    DeletedFile = false,
                    Diff = "@@ -10,3 +10,4 @@\n context\n+newline\n context"
                }
            }
        };
        _gitLabRepositoryMock
            .Setup(r => r.GetMergeRequestChangesAsync("my-group/my-project", "42"))
            .ReturnsAsync(Result<GitLabMrChangesResponse>.Success(changesResponse));
        var sut = CreateSut();

        // Act
        var result = await sut.GetDiffAsync("my-group/my-project", "42");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Files.Should().HaveCount(2);
        result.Value.RepositoryName.Should().Be("my-group/my-project");
        result.Value.Platform.Should().Be("GitLab");

        result.Value.Files[0].FilePath.Should().Be("src/Service.cs");
        result.Value.Files[0].AddedLines.Should().Be(2);
        result.Value.Files[0].DeletedLines.Should().Be(1);

        result.Value.Files[1].FilePath.Should().Be("src/Controller.cs");
        result.Value.Files[1].AddedLines.Should().Be(1);
        result.Value.Files[1].DeletedLines.Should().Be(0);
    }

    [Fact]
    public async Task GetDiffAsync_WithUnauthorized_ShouldReturnUnauthorizedError()
    {
        // Arrange
        _gitLabRepositoryMock
            .Setup(r => r.GetMergeRequestChangesAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Result<GitLabMrChangesResponse>.Failure(Error.SourceControl.Unauthorized));
        var sut = CreateSut();

        // Act
        var result = await sut.GetDiffAsync("my-group/my-project", "42");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("SourceControl.Unauthorized");
    }

    [Fact]
    public async Task GetDiffAsync_WithApiError_ShouldReturnDiffFetchFailedError()
    {
        // Arrange
        _gitLabRepositoryMock
            .Setup(r => r.GetMergeRequestChangesAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Result<GitLabMrChangesResponse>.Failure(
                Error.RiskAnalysis.DiffFetchFailed("my-group/my-project", "42")));
        var sut = CreateSut();

        // Act
        var result = await sut.GetDiffAsync("my-group/my-project", "42");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("RiskAnalysis.DiffFetchFailed");
    }

    [Fact]
    public async Task GetDiffAsync_WithEmptyChanges_ShouldReturnEmptyFileList()
    {
        // Arrange
        var changesResponse = new GitLabMrChangesResponse
        {
            Changes = new List<GitLabMrChangeItem>()
        };
        _gitLabRepositoryMock
            .Setup(r => r.GetMergeRequestChangesAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Result<GitLabMrChangesResponse>.Success(changesResponse));
        var sut = CreateSut();

        // Act
        var result = await sut.GetDiffAsync("my-group/my-project", "42");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Files.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDiffAsync_WithNewAndDeletedFiles_ShouldSetFlags()
    {
        // Arrange
        var changesResponse = new GitLabMrChangesResponse
        {
            Changes = new List<GitLabMrChangeItem>
            {
                new()
                {
                    OldPath = "src/NewFile.cs",
                    NewPath = "src/NewFile.cs",
                    NewFile = true,
                    DeletedFile = false,
                    Diff = "@@ -0,0 +1,3 @@\n+line1\n+line2\n+line3"
                },
                new()
                {
                    OldPath = "src/OldFile.cs",
                    NewPath = "src/OldFile.cs",
                    NewFile = false,
                    DeletedFile = true,
                    Diff = "@@ -1,2 +0,0 @@\n-line1\n-line2"
                }
            }
        };
        _gitLabRepositoryMock
            .Setup(r => r.GetMergeRequestChangesAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Result<GitLabMrChangesResponse>.Success(changesResponse));
        var sut = CreateSut();

        // Act
        var result = await sut.GetDiffAsync("my-group/my-project", "42");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Files.Should().HaveCount(2);

        result.Value.Files[0].IsNewFile.Should().BeTrue();
        result.Value.Files[0].IsDeletedFile.Should().BeFalse();
        result.Value.Files[0].AddedLines.Should().Be(3);

        result.Value.Files[1].IsNewFile.Should().BeFalse();
        result.Value.Files[1].IsDeletedFile.Should().BeTrue();
        result.Value.Files[1].DeletedLines.Should().Be(2);
    }

    [Theory]
    [InlineData("@@ -1,3 +1,5 @@\n context\n+added1\n+added2\n context\n-removed1", '+', 2)]
    [InlineData("@@ -1,3 +1,5 @@\n context\n+added1\n+added2\n context\n-removed1", '-', 1)]
    [InlineData("", '+', 0)]
    [InlineData("--- a/file.cs\n+++ b/file.cs\n+real add", '+', 1)]
    [InlineData("--- a/file.cs\n+++ b/file.cs\n-real remove", '-', 1)]
    public void CountLines_ShouldCountAddedAndDeletedLines(string diff, char prefix, int expected)
    {
        // Act
        var result = GitLabDiffProvider.CountLines(diff, prefix);

        // Assert
        result.Should().Be(expected);
    }

    private GitLabDiffProvider CreateSut()
    {
        return new GitLabDiffProvider(_gitLabRepositoryMock.Object, _loggerMock.Object);
    }
}
