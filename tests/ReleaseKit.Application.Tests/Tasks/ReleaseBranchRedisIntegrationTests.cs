using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Common;

namespace ReleaseKit.Application.Tests.Tasks;

/// <summary>
/// Release Branch 資料傳遞整合測試
/// </summary>
public class ReleaseBranchDataTransferIntegrationTests
{
    #region GitLab Tests

    [Fact]
    public async Task FetchGitLabReleaseBranchTask_ShouldClearOldData_WhenDataExists()
    {
        // Arrange
        var services = new ServiceCollection();
        var gitLabOptions = Options.Create(new GitLabOptions 
        { 
            Projects = new List<GitLabProjectOptions>()
        });
        
        var loggerMock = new Mock<ILogger<FetchGitLabReleaseBranchTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        
        // Mock 資料傳遞服務 - 設定有舊資料存在
        var dataTransferServiceMock = new Mock<IDataTransferService>();
        dataTransferServiceMock.Setup(x => x.GroupExistsAsync(DataTransferKeys.GitLabHash, DataTransferKeys.Fields.ReleaseBranches))
            .ReturnsAsync(true);
        dataTransferServiceMock.Setup(x => x.GroupDeleteAsync(DataTransferKeys.GitLabHash, DataTransferKeys.Fields.ReleaseBranches))
            .ReturnsAsync(true);
        dataTransferServiceMock.Setup(x => x.GroupSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        
        services.AddKeyedSingleton("GitLab", repositoryMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var task = new FetchGitLabReleaseBranchTask(
            serviceProvider,
            loggerMock.Object,
            dataTransferServiceMock.Object,
            gitLabOptions);

        // Act
        await task.ExecuteAsync();

        // Assert - 驗證檢查並刪除舊資料
        dataTransferServiceMock.Verify(
            x => x.GroupExistsAsync(DataTransferKeys.GitLabHash, DataTransferKeys.Fields.ReleaseBranches),
            Times.Once);
        dataTransferServiceMock.Verify(
            x => x.GroupDeleteAsync(DataTransferKeys.GitLabHash, DataTransferKeys.Fields.ReleaseBranches),
            Times.Once);
    }

    [Fact]
    public async Task FetchGitLabReleaseBranchTask_ShouldNotDeleteData_WhenNoDataExists()
    {
        // Arrange
        var services = new ServiceCollection();
        var gitLabOptions = Options.Create(new GitLabOptions 
        { 
            Projects = new List<GitLabProjectOptions>()
        });
        
        var loggerMock = new Mock<ILogger<FetchGitLabReleaseBranchTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        
        // Mock 資料傳遞服務 - 設定沒有舊資料
        var dataTransferServiceMock = new Mock<IDataTransferService>();
        dataTransferServiceMock.Setup(x => x.GroupExistsAsync(DataTransferKeys.GitLabHash, DataTransferKeys.Fields.ReleaseBranches))
            .ReturnsAsync(false);
        dataTransferServiceMock.Setup(x => x.GroupSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        
        services.AddKeyedSingleton("GitLab", repositoryMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var task = new FetchGitLabReleaseBranchTask(
            serviceProvider,
            loggerMock.Object,
            dataTransferServiceMock.Object,
            gitLabOptions);

        // Act
        await task.ExecuteAsync();

        // Assert - 驗證沒有嘗試刪除資料
        dataTransferServiceMock.Verify(
            x => x.GroupExistsAsync(DataTransferKeys.GitLabHash, DataTransferKeys.Fields.ReleaseBranches),
            Times.Once);
        dataTransferServiceMock.Verify(
            x => x.GroupDeleteAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task FetchGitLabReleaseBranchTask_ShouldSaveData_AfterFetch()
    {
        // Arrange
        var services = new ServiceCollection();
        var gitLabOptions = Options.Create(new GitLabOptions 
        { 
            Projects = new List<GitLabProjectOptions>
            {
                new GitLabProjectOptions
                {
                    ProjectPath = "test/project",
                    TargetBranch = "main"
                }
            }
        });
        
        var loggerMock = new Mock<ILogger<FetchGitLabReleaseBranchTask>>();
        
        // Mock repository - 回傳一個 release branch
        var repositoryMock = new Mock<ISourceControlRepository>();
        repositoryMock
            .Setup(x => x.GetBranchesAsync("test/project", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "release/20260210" }.AsReadOnly()));
        
        // Mock 資料傳遞服務
        var dataTransferServiceMock = new Mock<IDataTransferService>();
        dataTransferServiceMock.Setup(x => x.GroupExistsAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);
        dataTransferServiceMock.Setup(x => x.GroupSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        
        services.AddKeyedSingleton("GitLab", repositoryMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var task = new FetchGitLabReleaseBranchTask(
            serviceProvider,
            loggerMock.Object,
            dataTransferServiceMock.Object,
            gitLabOptions);

        // Act
        await task.ExecuteAsync();

        // Assert - 驗證儲存到資料傳遞存放區
        dataTransferServiceMock.Verify(
            x => x.GroupSetAsync(
                DataTransferKeys.GitLabHash,
                DataTransferKeys.Fields.ReleaseBranches,
                It.Is<string>(json => json.Contains("release/20260210") && json.Contains("test/project"))),
            Times.Once);
    }

    [Fact]
    public async Task FetchGitLabReleaseBranchTask_ShouldUseCorrectKey()
    {
        // Arrange
        var services = new ServiceCollection();
        var gitLabOptions = Options.Create(new GitLabOptions 
        { 
            Projects = new List<GitLabProjectOptions>()
        });
        
        var loggerMock = new Mock<ILogger<FetchGitLabReleaseBranchTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        var dataTransferServiceMock = new Mock<IDataTransferService>();
        
        dataTransferServiceMock.Setup(x => x.GroupExistsAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);
        dataTransferServiceMock.Setup(x => x.GroupSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        
        services.AddKeyedSingleton("GitLab", repositoryMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var task = new FetchGitLabReleaseBranchTask(
            serviceProvider,
            loggerMock.Object,
            dataTransferServiceMock.Object,
            gitLabOptions);

        // Act
        await task.ExecuteAsync();

        // Assert - 驗證使用正確的 資料傳遞存放區 Key 與 Field
        dataTransferServiceMock.Verify(
            x => x.GroupExistsAsync(DataTransferKeys.GitLabHash, DataTransferKeys.Fields.ReleaseBranches),
            Times.Once);
        dataTransferServiceMock.Verify(
            x => x.GroupSetAsync(DataTransferKeys.GitLabHash, DataTransferKeys.Fields.ReleaseBranches, It.IsAny<string>()),
            Times.Once);
        
        // 驗證不應該使用 Bitbucket 的 Hash Key
        dataTransferServiceMock.Verify(
            x => x.GroupExistsAsync(DataTransferKeys.BitbucketHash, DataTransferKeys.Fields.ReleaseBranches),
            Times.Never);
        dataTransferServiceMock.Verify(
            x => x.GroupSetAsync(DataTransferKeys.BitbucketHash, DataTransferKeys.Fields.ReleaseBranches, It.IsAny<string>()),
            Times.Never);
    }

    #endregion

    #region Bitbucket Tests

    [Fact]
    public async Task FetchBitbucketReleaseBranchTask_ShouldClearOldData_WhenDataExists()
    {
        // Arrange
        var services = new ServiceCollection();
        var bitbucketOptions = Options.Create(new BitbucketOptions 
        { 
            Projects = new List<BitbucketProjectOptions>()
        });
        
        var loggerMock = new Mock<ILogger<FetchBitbucketReleaseBranchTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        
        // Mock 資料傳遞服務 - 設定有舊資料存在
        var dataTransferServiceMock = new Mock<IDataTransferService>();
        dataTransferServiceMock.Setup(x => x.GroupExistsAsync(DataTransferKeys.BitbucketHash, DataTransferKeys.Fields.ReleaseBranches))
            .ReturnsAsync(true);
        dataTransferServiceMock.Setup(x => x.GroupDeleteAsync(DataTransferKeys.BitbucketHash, DataTransferKeys.Fields.ReleaseBranches))
            .ReturnsAsync(true);
        dataTransferServiceMock.Setup(x => x.GroupSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        
        services.AddKeyedSingleton("Bitbucket", repositoryMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var task = new FetchBitbucketReleaseBranchTask(
            serviceProvider,
            loggerMock.Object,
            dataTransferServiceMock.Object,
            bitbucketOptions);

        // Act
        await task.ExecuteAsync();

        // Assert - 驗證檢查並刪除舊資料
        dataTransferServiceMock.Verify(
            x => x.GroupExistsAsync(DataTransferKeys.BitbucketHash, DataTransferKeys.Fields.ReleaseBranches),
            Times.Once);
        dataTransferServiceMock.Verify(
            x => x.GroupDeleteAsync(DataTransferKeys.BitbucketHash, DataTransferKeys.Fields.ReleaseBranches),
            Times.Once);
    }

    [Fact]
    public async Task FetchBitbucketReleaseBranchTask_ShouldNotDeleteData_WhenNoDataExists()
    {
        // Arrange
        var services = new ServiceCollection();
        var bitbucketOptions = Options.Create(new BitbucketOptions 
        { 
            Projects = new List<BitbucketProjectOptions>()
        });
        
        var loggerMock = new Mock<ILogger<FetchBitbucketReleaseBranchTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        
        // Mock 資料傳遞服務 - 設定沒有舊資料
        var dataTransferServiceMock = new Mock<IDataTransferService>();
        dataTransferServiceMock.Setup(x => x.GroupExistsAsync(DataTransferKeys.BitbucketHash, DataTransferKeys.Fields.ReleaseBranches))
            .ReturnsAsync(false);
        dataTransferServiceMock.Setup(x => x.GroupSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        
        services.AddKeyedSingleton("Bitbucket", repositoryMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var task = new FetchBitbucketReleaseBranchTask(
            serviceProvider,
            loggerMock.Object,
            dataTransferServiceMock.Object,
            bitbucketOptions);

        // Act
        await task.ExecuteAsync();

        // Assert - 驗證沒有嘗試刪除資料
        dataTransferServiceMock.Verify(
            x => x.GroupExistsAsync(DataTransferKeys.BitbucketHash, DataTransferKeys.Fields.ReleaseBranches),
            Times.Once);
        dataTransferServiceMock.Verify(
            x => x.GroupDeleteAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task FetchBitbucketReleaseBranchTask_ShouldSaveData_AfterFetch()
    {
        // Arrange
        var services = new ServiceCollection();
        var bitbucketOptions = Options.Create(new BitbucketOptions 
        { 
            Projects = new List<BitbucketProjectOptions>
            {
                new BitbucketProjectOptions
                {
                    ProjectPath = "test/project",
                    TargetBranch = "main"
                }
            }
        });
        
        var loggerMock = new Mock<ILogger<FetchBitbucketReleaseBranchTask>>();
        
        // Mock repository - 回傳一個 release branch
        var repositoryMock = new Mock<ISourceControlRepository>();
        repositoryMock
            .Setup(x => x.GetBranchesAsync("test/project", "release/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<string>>.Success(new List<string> { "release/20260210" }.AsReadOnly()));
        
        // Mock 資料傳遞服務
        var dataTransferServiceMock = new Mock<IDataTransferService>();
        dataTransferServiceMock.Setup(x => x.GroupExistsAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);
        dataTransferServiceMock.Setup(x => x.GroupSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        
        services.AddKeyedSingleton("Bitbucket", repositoryMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var task = new FetchBitbucketReleaseBranchTask(
            serviceProvider,
            loggerMock.Object,
            dataTransferServiceMock.Object,
            bitbucketOptions);

        // Act
        await task.ExecuteAsync();

        // Assert - 驗證儲存到資料傳遞存放區
        dataTransferServiceMock.Verify(
            x => x.GroupSetAsync(
                DataTransferKeys.BitbucketHash,
                DataTransferKeys.Fields.ReleaseBranches,
                It.Is<string>(json => json.Contains("release/20260210") && json.Contains("test/project"))),
            Times.Once);
    }

    [Fact]
    public async Task FetchBitbucketReleaseBranchTask_ShouldUseCorrectKey()
    {
        // Arrange
        var services = new ServiceCollection();
        var bitbucketOptions = Options.Create(new BitbucketOptions 
        { 
            Projects = new List<BitbucketProjectOptions>()
        });
        
        var loggerMock = new Mock<ILogger<FetchBitbucketReleaseBranchTask>>();
        var repositoryMock = new Mock<ISourceControlRepository>();
        var dataTransferServiceMock = new Mock<IDataTransferService>();
        
        dataTransferServiceMock.Setup(x => x.GroupExistsAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);
        dataTransferServiceMock.Setup(x => x.GroupSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        
        services.AddKeyedSingleton("Bitbucket", repositoryMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var task = new FetchBitbucketReleaseBranchTask(
            serviceProvider,
            loggerMock.Object,
            dataTransferServiceMock.Object,
            bitbucketOptions);

        // Act
        await task.ExecuteAsync();

        // Assert - 驗證使用正確的 資料傳遞存放區 Key 與 Field
        dataTransferServiceMock.Verify(
            x => x.GroupExistsAsync(DataTransferKeys.BitbucketHash, DataTransferKeys.Fields.ReleaseBranches),
            Times.Once);
        dataTransferServiceMock.Verify(
            x => x.GroupSetAsync(DataTransferKeys.BitbucketHash, DataTransferKeys.Fields.ReleaseBranches, It.IsAny<string>()),
            Times.Once);
        
        // 驗證不應該使用 GitLab 的 Hash Key
        dataTransferServiceMock.Verify(
            x => x.GroupExistsAsync(DataTransferKeys.GitLabHash, DataTransferKeys.Fields.ReleaseBranches),
            Times.Never);
        dataTransferServiceMock.Verify(
            x => x.GroupSetAsync(DataTransferKeys.GitLabHash, DataTransferKeys.Fields.ReleaseBranches, It.IsAny<string>()),
            Times.Never);
    }

    #endregion
}
