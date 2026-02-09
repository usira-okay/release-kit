using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Domain.Abstractions;
using AppTaskFactory = ReleaseKit.Application.Tasks.TaskFactory;

namespace ReleaseKit.Application.Tests.Tasks;

/// <summary>
/// TaskFactory 單元測試
/// </summary>
public class TaskFactoryTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AppTaskFactory _factory;

    public TaskFactoryTests()
    {
        // 建立測試用的 DI 容器
        var services = new ServiceCollection();
        
        // 註冊必要的配置選項
        services.AddSingleton(Options.Create(new GitLabOptions()));
        services.AddSingleton(Options.Create(new BitbucketOptions()));
        services.AddSingleton(Options.Create(new FetchModeOptions()));
        
        // 註冊 Logger mocks
        services.AddSingleton(new Mock<ILogger<FetchGitLabPullRequestsTask>>().Object);
        services.AddSingleton(new Mock<ILogger<FetchBitbucketPullRequestsTask>>().Object);
        
        // 註冊 ISourceControlRepository mock with keyed services
        var mockGitLabRepository = new Mock<ISourceControlRepository>();
        var mockBitbucketRepository = new Mock<ISourceControlRepository>();
        services.AddKeyedSingleton<ISourceControlRepository>("GitLab", mockGitLabRepository.Object);
        services.AddKeyedSingleton<ISourceControlRepository>("Bitbucket", mockBitbucketRepository.Object);
        
        // 註冊 IRedisService mock
        var mockRedisService = new Mock<IRedisService>();
        mockRedisService.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        mockRedisService.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>())).ReturnsAsync(true);
        services.AddSingleton(mockRedisService.Object);
        
        // 註冊 Tasks
        services.AddTransient<FetchGitLabPullRequestsTask>();
        services.AddTransient<FetchBitbucketPullRequestsTask>();
        services.AddTransient<FetchAzureDevOpsWorkItemsTask>();
        services.AddTransient<UpdateGoogleSheetsTask>();

        _serviceProvider = services.BuildServiceProvider();
        _factory = new AppTaskFactory(_serviceProvider);
    }

    [Fact]
    public void Constructor_WithNullServiceProvider_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new AppTaskFactory(null!));
        Assert.Equal("serviceProvider", exception.ParamName);
    }

    [Fact]
    public void CreateTask_WithFetchGitLabPullRequests_ShouldReturnCorrectTaskType()
    {
        // Act
        var task = _factory.CreateTask(TaskType.FetchGitLabPullRequests);

        // Assert
        Assert.NotNull(task);
        Assert.IsType<FetchGitLabPullRequestsTask>(task);
    }

    [Fact]
    public void CreateTask_WithFetchBitbucketPullRequests_ShouldReturnCorrectTaskType()
    {
        // Act
        var task = _factory.CreateTask(TaskType.FetchBitbucketPullRequests);

        // Assert
        Assert.NotNull(task);
        Assert.IsType<FetchBitbucketPullRequestsTask>(task);
    }

    [Fact]
    public void CreateTask_WithFetchAzureDevOpsWorkItems_ShouldReturnCorrectTaskType()
    {
        // Act
        var task = _factory.CreateTask(TaskType.FetchAzureDevOpsWorkItems);

        // Assert
        Assert.NotNull(task);
        Assert.IsType<FetchAzureDevOpsWorkItemsTask>(task);
    }

    [Fact]
    public void CreateTask_WithUpdateGoogleSheets_ShouldReturnCorrectTaskType()
    {
        // Act
        var task = _factory.CreateTask(TaskType.UpdateGoogleSheets);

        // Assert
        Assert.NotNull(task);
        Assert.IsType<UpdateGoogleSheetsTask>(task);
    }

    [Fact]
    public void CreateTask_WithInvalidTaskType_ShouldThrowArgumentException()
    {
        // Arrange
        var invalidTaskType = (TaskType)999;

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => _factory.CreateTask(invalidTaskType));
        Assert.Contains("不支援的任務類型", exception.Message);
    }

    [Fact]
    public void CreateTask_ShouldCreateNewInstanceEachTime()
    {
        // Act
        var task1 = _factory.CreateTask(TaskType.FetchGitLabPullRequests);
        var task2 = _factory.CreateTask(TaskType.FetchGitLabPullRequests);

        // Assert
        Assert.NotNull(task1);
        Assert.NotNull(task2);
        Assert.NotSame(task1, task2); // 確保每次都建立新的實例
    }
}
