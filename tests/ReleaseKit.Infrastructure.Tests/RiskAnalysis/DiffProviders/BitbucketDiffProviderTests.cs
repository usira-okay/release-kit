using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReleaseKit.Domain.Common;
using ReleaseKit.Infrastructure.RiskAnalysis.DiffProviders;
using ReleaseKit.Infrastructure.RiskAnalysis.DiffProviders.Models;
using ReleaseKit.Infrastructure.SourceControl.Bitbucket;

namespace ReleaseKit.Infrastructure.Tests.RiskAnalysis.DiffProviders;

/// <summary>
/// BitbucketDiffProvider 單元測試
/// </summary>
public class BitbucketDiffProviderTests
{
    private readonly Mock<IBitbucketRepository> _bitbucketRepositoryMock = new();
    private readonly Mock<ILogger<BitbucketDiffProvider>> _loggerMock = new();

    private const string ProjectPath = "my-workspace/my-repo";
    private const string PrId = "42";

    private BitbucketDiffProvider CreateSut()
    {
        return new BitbucketDiffProvider(_bitbucketRepositoryMock.Object, _loggerMock.Object);
    }

    private void SetupDiffStatSuccess(BitbucketRiskDiffStatResponse response)
    {
        _bitbucketRepositoryMock
            .Setup(r => r.GetPullRequestDiffStatAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Result<BitbucketRiskDiffStatResponse>.Success(response));
    }

    private void SetupRawDiffSuccess(string rawDiff)
    {
        _bitbucketRepositoryMock
            .Setup(r => r.GetPullRequestRawDiffAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Result<string>.Success(rawDiff));
    }

    private void SetupDiffStatFailure(Error error)
    {
        _bitbucketRepositoryMock
            .Setup(r => r.GetPullRequestDiffStatAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Result<BitbucketRiskDiffStatResponse>.Failure(error));
    }

    [Fact]
    public async Task GetDiffAsync_WithValidResponse_ShouldReturnPullRequestDiff()
    {
        // Arrange
        var diffStatResponse = new BitbucketRiskDiffStatResponse
        {
            Values =
            [
                new BitbucketRiskDiffStatEntry
                {
                    Type = "diffstat",
                    Status = "modified",
                    LinesAdded = 5,
                    LinesRemoved = 2,
                    Old = new BitbucketRiskFileRef { Path = "src/Service.cs" },
                    New = new BitbucketRiskFileRef { Path = "src/Service.cs" }
                },
                new BitbucketRiskDiffStatEntry
                {
                    Type = "diffstat",
                    Status = "added",
                    LinesAdded = 10,
                    LinesRemoved = 0,
                    Old = null,
                    New = new BitbucketRiskFileRef { Path = "src/NewFile.cs" }
                }
            ]
        };

        var rawDiff = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc1234..def5678 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,5 +1,8 @@
             using System;
            +using System.Linq;
             
             public class Service
             {
            +    public void DoWork() { }
            +    public void DoMore() { }
            -    public void OldMethod() { }
            -    public void OldMethod2() { }
            +    public void NewMethod() { }
             }
            diff --git a/src/NewFile.cs b/src/NewFile.cs
            new file mode 100644
            index 0000000..abc1234
            --- /dev/null
            +++ b/src/NewFile.cs
            @@ -0,0 +1,10 @@
            +namespace MyApp;
            +
            +public class NewFile
            +{
            +    public void Method1() { }
            +    public void Method2() { }
            +    public void Method3() { }
            +    public void Method4() { }
            +    public void Method5() { }
            +}
            """;

        SetupDiffStatSuccess(diffStatResponse);
        SetupRawDiffSuccess(rawDiff);
        var sut = CreateSut();

        // Act
        var result = await sut.GetDiffAsync(ProjectPath, PrId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var diff = result.Value!;
        diff.Platform.Should().Be("Bitbucket");
        diff.RepositoryName.Should().Be("my-repo");
        diff.Files.Should().HaveCount(2);

        var modifiedFile = diff.Files.First(f => f.FilePath == "src/Service.cs");
        modifiedFile.AddedLines.Should().Be(5);
        modifiedFile.DeletedLines.Should().Be(2);
        modifiedFile.IsNewFile.Should().BeFalse();
        modifiedFile.IsDeletedFile.Should().BeFalse();
        modifiedFile.DiffContent.Should().NotBeEmpty();

        var newFile = diff.Files.First(f => f.FilePath == "src/NewFile.cs");
        newFile.AddedLines.Should().Be(10);
        newFile.DeletedLines.Should().Be(0);
        newFile.IsNewFile.Should().BeTrue();
        newFile.IsDeletedFile.Should().BeFalse();
        newFile.DiffContent.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetDiffAsync_WithUnauthorized_ShouldReturnError()
    {
        // Arrange
        SetupDiffStatFailure(Error.SourceControl.Unauthorized);
        var sut = CreateSut();

        // Act
        var result = await sut.GetDiffAsync(ProjectPath, PrId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("SourceControl.Unauthorized");
    }

    [Fact]
    public async Task GetDiffAsync_WithApiError_ShouldReturnError()
    {
        // Arrange
        SetupDiffStatFailure(Error.SourceControl.ApiError("HTTP 500"));
        var sut = CreateSut();

        // Act
        var result = await sut.GetDiffAsync(ProjectPath, PrId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("SourceControl.ApiError");
    }

    [Fact]
    public async Task GetDiffAsync_WithEmptyDiffstat_ShouldReturnEmptyFileList()
    {
        // Arrange
        var diffStatResponse = new BitbucketRiskDiffStatResponse { Values = [] };
        SetupDiffStatSuccess(diffStatResponse);
        var sut = CreateSut();

        // Act
        var result = await sut.GetDiffAsync(ProjectPath, PrId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Files.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDiffAsync_ShouldParseRawDiffCorrectly()
    {
        // Arrange
        var diffStatResponse = new BitbucketRiskDiffStatResponse
        {
            Values =
            [
                new BitbucketRiskDiffStatEntry
                {
                    Type = "diffstat",
                    Status = "modified",
                    LinesAdded = 3,
                    LinesRemoved = 1,
                    Old = new BitbucketRiskFileRef { Path = "src/App.cs" },
                    New = new BitbucketRiskFileRef { Path = "src/App.cs" }
                },
                new BitbucketRiskDiffStatEntry
                {
                    Type = "diffstat",
                    Status = "removed",
                    LinesAdded = 0,
                    LinesRemoved = 15,
                    Old = new BitbucketRiskFileRef { Path = "src/Obsolete.cs" },
                    New = null
                }
            ]
        };

        var rawDiff = """
            diff --git a/src/App.cs b/src/App.cs
            index 1111111..2222222 100644
            --- a/src/App.cs
            +++ b/src/App.cs
            @@ -1,4 +1,6 @@
             namespace MyApp;
             
             public class App
             {
            +    public string Name { get; set; }
            +    public int Version { get; set; }
            +
            -    // old comment
             }
            diff --git a/src/Obsolete.cs b/src/Obsolete.cs
            deleted file mode 100644
            index 3333333..0000000
            --- a/src/Obsolete.cs
            +++ /dev/null
            @@ -1,15 +0,0 @@
            -namespace MyApp;
            -public class Obsolete { }
            """;

        SetupDiffStatSuccess(diffStatResponse);
        SetupRawDiffSuccess(rawDiff);
        var sut = CreateSut();

        // Act
        var result = await sut.GetDiffAsync(ProjectPath, PrId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var files = result.Value!.Files;
        files.Should().HaveCount(2);

        var modifiedFile = files.First(f => f.FilePath == "src/App.cs");
        modifiedFile.AddedLines.Should().Be(3);
        modifiedFile.DeletedLines.Should().Be(1);
        modifiedFile.IsNewFile.Should().BeFalse();
        modifiedFile.IsDeletedFile.Should().BeFalse();
        modifiedFile.DiffContent.Should().Contain("public string Name");

        var deletedFile = files.First(f => f.FilePath == "src/Obsolete.cs");
        deletedFile.AddedLines.Should().Be(0);
        deletedFile.DeletedLines.Should().Be(15);
        deletedFile.IsNewFile.Should().BeFalse();
        deletedFile.IsDeletedFile.Should().BeTrue();
        deletedFile.DiffContent.Should().Contain("Obsolete");
    }

    [Fact]
    public async Task GetDiffAsync_WhenDiffEndpointFails_ShouldReturnError()
    {
        // Arrange
        var diffStatResponse = new BitbucketRiskDiffStatResponse
        {
            Values =
            [
                new BitbucketRiskDiffStatEntry
                {
                    Type = "diffstat",
                    Status = "modified",
                    LinesAdded = 1,
                    LinesRemoved = 0,
                    Old = new BitbucketRiskFileRef { Path = "src/File.cs" },
                    New = new BitbucketRiskFileRef { Path = "src/File.cs" }
                }
            ]
        };
        SetupDiffStatSuccess(diffStatResponse);
        _bitbucketRepositoryMock
            .Setup(r => r.GetPullRequestRawDiffAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Result<string>.Failure(Error.SourceControl.ApiError("HTTP 500")));

        var sut = CreateSut();

        // Act
        var result = await sut.GetDiffAsync(ProjectPath, PrId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("SourceControl.ApiError");
    }
}
