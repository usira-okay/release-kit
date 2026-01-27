using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.ValueObjects;
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
        
        // 註冊 Mock 依賴
        var mockGitLabRepository = new Mock<IGitLabRepository>();
        var mockNow = new Mock<INow>();
        mockNow.Setup(x => x.UtcNow).Returns(new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero));
        
        var gitLabSettings = new GitLabSettings
        {
            Domain = "https://gitlab.com",
            AccessToken = "test-token",
            Projects = new List<GitLabProjectSettings>()
        };
        
        services.AddSingleton(mockGitLabRepository.Object);
        services.AddSingleton(mockNow.Object);
        services.AddSingleton(gitLabSettings);
        services.AddSingleton(Mock.Of<ILogger<FetchGitLabPullRequestsTask>>());
        
        // 註冊任務
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
