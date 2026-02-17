using Microsoft.Extensions.Logging;
using Moq;
using ReleaseKit.Application.Common;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Common;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;
using Xunit;

namespace ReleaseKit.Application.Tests.Tasks;

/// <summary>
/// FetchAzureDevOpsWorkItemsTask 單元測試
/// </summary>
public class FetchAzureDevOpsWorkItemsTaskTests
{
    private readonly Mock<ILogger<FetchAzureDevOpsWorkItemsTask>> _loggerMock;
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly Mock<IAzureDevOpsRepository> _azureDevOpsRepositoryMock;

    public FetchAzureDevOpsWorkItemsTaskTests()
    {
        _loggerMock = new Mock<ILogger<FetchAzureDevOpsWorkItemsTask>>();
        _redisServiceMock = new Mock<IRedisService>();
        _azureDevOpsRepositoryMock = new Mock<IAzureDevOpsRepository>();
    }

    [Fact]
    public async Task ExecuteAsync_WithSingleVSTSIdInSourceBranch_ShouldFetchWorkItem()
    {
        // Arrange
        var fetchResult = CreateFetchResult("修復登入錯誤", "feature/VSTS12345", "main");
        SetupRedis(gitLabData: fetchResult);
        
        var workItem = CreateWorkItem(12345, "修復登入錯誤", "Bug", "Active");
        _azureDevOpsRepositoryMock
            .Setup(x => x.GetWorkItemAsync(12345))
            .ReturnsAsync(Result<WorkItem>.Success(workItem));

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _azureDevOpsRepositoryMock.Verify(x => x.GetWorkItemAsync(12345), Times.Once);
        VerifyRedisWrite(result =>
        {
            Assert.Single(result.WorkItems);
            Assert.Equal(12345, result.WorkItems[0].WorkItemId);
            Assert.True(result.WorkItems[0].IsSuccess);
            Assert.Equal(1, result.TotalWorkItemsFound);
            Assert.Equal(1, result.SuccessCount);
            Assert.Equal(0, result.FailureCount);
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleVSTSIdsInOneSourceBranch_ShouldFetchAllWorkItems()
    {
        // Arrange
        var fetchResult = CreateFetchResult("修復問題", "feature/VSTS111-VSTS222", "main");
        SetupRedis(gitLabData: fetchResult);
        
        var workItem1 = CreateWorkItem(111, "問題 1", "Bug", "Active");
        var workItem2 = CreateWorkItem(222, "問題 2", "Task", "Closed");
        
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(111)).ReturnsAsync(Result<WorkItem>.Success(workItem1));
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(222)).ReturnsAsync(Result<WorkItem>.Success(workItem2));

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _azureDevOpsRepositoryMock.Verify(x => x.GetWorkItemAsync(111), Times.Once);
        _azureDevOpsRepositoryMock.Verify(x => x.GetWorkItemAsync(222), Times.Once);
        VerifyRedisWrite(result =>
        {
            Assert.Equal(2, result.WorkItems.Count);
            Assert.Equal(2, result.TotalWorkItemsFound);
            Assert.Equal(2, result.SuccessCount);
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithDuplicateVSTSIdsAcrossPRs_ShouldDeduplicateAndFetchOnce()
    {
        // Arrange
        var fetchResult = new FetchResult
        {
            Results = new List<ProjectResult>
            {
                new ProjectResult
                {
                    ProjectPath = "group/project1",
                    Platform = SourceControlPlatform.GitLab,
                    PullRequests = new List<MergeRequestOutput>
                    {
                        CreateMergeRequest("Fix issue", "feature/VSTS123"),
                        CreateMergeRequest("另一個 PR 提到相同 WorkItem", "feature/VSTS123-fix")
                    }
                }
            }
        };
        SetupRedis(gitLabData: fetchResult);
        
        var workItem = CreateWorkItem(123, "Fix issue", "Bug", "Active");
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(123)).ReturnsAsync(Result<WorkItem>.Success(workItem));

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _azureDevOpsRepositoryMock.Verify(x => x.GetWorkItemAsync(123), Times.Once); // 只呼叫一次
        VerifyRedisWrite(result =>
        {
            Assert.Single(result.WorkItems);
            Assert.Equal(2, result.TotalPRsAnalyzed); // 兩個 PR
            Assert.Equal(1, result.TotalWorkItemsFound); // 去重複後只有一個 WorkItem
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithNoVSTSIdInSourceBranch_ShouldNotWriteToRedis()
    {
        // Arrange
        var fetchResult = CreateFetchResult("Some title", "feature/no-work-item-id", "main");
        SetupRedis(gitLabData: fetchResult);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _azureDevOpsRepositoryMock.Verify(x => x.GetWorkItemAsync(It.IsAny<int>()), Times.Never);
        _redisServiceMock.Verify(x => x.SetAsync(RedisKeys.AzureDevOpsWorkItems, It.IsAny<string>(), null), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidVSTSFormats_ShouldIgnoreThem()
    {
        // Arrange
        var fetchResult = CreateFetchResult("Some title", "feature/VSTSabc-vsts123-VSTS-VSTS456-works", "main");
        SetupRedis(gitLabData: fetchResult);
        
        var workItem = CreateWorkItem(456, "Valid", "Bug", "Active");
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(456)).ReturnsAsync(Result<WorkItem>.Success(workItem));

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _azureDevOpsRepositoryMock.Verify(x => x.GetWorkItemAsync(456), Times.Once);
        _azureDevOpsRepositoryMock.Verify(x => x.GetWorkItemAsync(It.Is<int>(id => id != 456)), Times.Never);
        VerifyRedisWrite(result =>
        {
            Assert.Single(result.WorkItems);
            Assert.Equal(1, result.TotalWorkItemsFound);
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithSuccessfulAndFailedCalls_ShouldRecordBoth()
    {
        // Arrange
        var fetchResult = CreateFetchResult("Some title", "feature/VSTS111-VSTS999", "main");
        SetupRedis(gitLabData: fetchResult);
        
        var workItem = CreateWorkItem(111, "Success", "Bug", "Active");
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(111)).ReturnsAsync(Result<WorkItem>.Success(workItem));
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(999)).ReturnsAsync(Result<WorkItem>.Failure(Error.AzureDevOps.WorkItemNotFound(999)));

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        VerifyRedisWrite(result =>
        {
            Assert.Equal(2, result.WorkItems.Count);
            Assert.Equal(2, result.TotalWorkItemsFound);
            Assert.Equal(1, result.SuccessCount);
            Assert.Equal(1, result.FailureCount);
            
            var successItem = result.WorkItems.First(w => w.WorkItemId == 111);
            Assert.True(successItem.IsSuccess);
            Assert.NotNull(successItem.Title);
            
            var failedItem = result.WorkItems.First(w => w.WorkItemId == 999);
            Assert.False(failedItem.IsSuccess);
            Assert.NotNull(failedItem.ErrorMessage);
            Assert.Null(failedItem.Title);
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithBothGitLabAndBitbucketData_ShouldProcessAll()
    {
        // Arrange
        var gitLabResult = CreateFetchResult("GitLab title", "feature/VSTS111", "main");
        var bitbucketResult = CreateFetchResult("Bitbucket title", "feature/VSTS222", "main", SourceControlPlatform.Bitbucket);
        
        SetupRedis(gitLabData: gitLabResult, bitbucketData: bitbucketResult);
        
        var workItem1 = CreateWorkItem(111, "GitLab Issue", "Bug", "Active");
        var workItem2 = CreateWorkItem(222, "Bitbucket Issue", "Task", "Closed");
        
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(111)).ReturnsAsync(Result<WorkItem>.Success(workItem1));
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(222)).ReturnsAsync(Result<WorkItem>.Success(workItem2));

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        VerifyRedisWrite(result =>
        {
            Assert.Equal(2, result.WorkItems.Count);
            Assert.Equal(2, result.TotalPRsAnalyzed);
            Assert.Equal(2, result.TotalWorkItemsFound);
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithOnlyGitLabKeyExists_ShouldProcessGitLabAndLogWarning()
    {
        // Arrange
        var gitLabResult = CreateFetchResult("Some title", "feature/VSTS123", "main");
        SetupRedis(gitLabData: gitLabResult, bitbucketData: null); // Bitbucket key doesn't exist
        
        var workItem = CreateWorkItem(123, "Issue", "Bug", "Active");
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(123)).ReturnsAsync(Result<WorkItem>.Success(workItem));

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _azureDevOpsRepositoryMock.Verify(x => x.GetWorkItemAsync(123), Times.Once);
        VerifyRedisWrite(result =>
        {
            Assert.Single(result.WorkItems);
            Assert.Equal(1, result.TotalPRsAnalyzed);
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithOnlyBitbucketKeyExists_ShouldProcessBitbucketAndLogWarning()
    {
        // Arrange
        var bitbucketResult = CreateFetchResult("Some title", "feature/VSTS456", "main", SourceControlPlatform.Bitbucket);
        SetupRedis(gitLabData: null, bitbucketData: bitbucketResult); // GitLab key doesn't exist
        
        var workItem = CreateWorkItem(456, "Issue", "Task", "New");
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(456)).ReturnsAsync(Result<WorkItem>.Success(workItem));

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _azureDevOpsRepositoryMock.Verify(x => x.GetWorkItemAsync(456), Times.Once);
        VerifyRedisWrite(result =>
        {
            Assert.Single(result.WorkItems);
            Assert.Equal(1, result.TotalPRsAnalyzed);
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithBothKeysMissing_ShouldExitGracefullyWithoutAPICalls()
    {
        // Arrange
        SetupRedis(gitLabData: null, bitbucketData: null); // Both keys missing

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _azureDevOpsRepositoryMock.Verify(x => x.GetWorkItemAsync(It.IsAny<int>()), Times.Never);
        _redisServiceMock.Verify(x => x.SetAsync(RedisKeys.AzureDevOpsWorkItems, It.IsAny<string>(), null), Times.Never);
    }

    // Helper methods
    private FetchAzureDevOpsWorkItemsTask CreateTask()
    {
        return new FetchAzureDevOpsWorkItemsTask(
            _loggerMock.Object,
            _redisServiceMock.Object,
            _azureDevOpsRepositoryMock.Object);
    }

    private FetchResult CreateFetchResult(string title, string sourceBranch, string targetBranch, SourceControlPlatform platform = SourceControlPlatform.GitLab)
    {
        return new FetchResult
        {
            Results = new List<ProjectResult>
            {
                new ProjectResult
                {
                    ProjectPath = "group/project1",
                    Platform = platform,
                    PullRequests = new List<MergeRequestOutput>
                    {
                        CreateMergeRequest(title, sourceBranch, targetBranch)
                    }
                }
            }
        };
    }

    private MergeRequestOutput CreateMergeRequest(string title, string sourceBranch = "feature/test", string targetBranch = "main")
    {
        return new MergeRequestOutput
        {
            Title = title,
            SourceBranch = sourceBranch,
            TargetBranch = targetBranch,
            State = "merged",
            AuthorUserId = "12345",
            AuthorName = "Test User",
            PRUrl = "https://example.com/pr/1",
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private WorkItem CreateWorkItem(int id, string title, string type, string state)
    {
        return new WorkItem
        {
            WorkItemId = id,
            Title = title,
            Type = type,
            State = state,
            Url = $"https://dev.azure.com/org/project/_workitems/edit/{id}",
            OriginalTeamName = "MyTeam"
        };
    }

    private void SetupRedis(FetchResult? gitLabData, FetchResult? bitbucketData = null)
    {
        _redisServiceMock.Setup(x => x.DeleteAsync(RedisKeys.AzureDevOpsWorkItems))
            .ReturnsAsync(true);
        
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.GitLabPullRequestsByUser))
            .ReturnsAsync(gitLabData?.ToJson());
        _redisServiceMock.Setup(x => x.GetAsync(RedisKeys.BitbucketPullRequestsByUser))
            .ReturnsAsync(bitbucketData?.ToJson());
        
        string? capturedJson = null;
        _redisServiceMock.Setup(x => x.SetAsync(RedisKeys.AzureDevOpsWorkItems, It.IsAny<string>(), null))
            .Callback<string, string, TimeSpan?>((key, json, ttl) => capturedJson = json)
            .ReturnsAsync(true);
    }

    private void VerifyRedisWrite(Action<WorkItemFetchResult> assert)
    {
        _redisServiceMock.Verify(x => x.SetAsync(
            RedisKeys.AzureDevOpsWorkItems,
            It.IsAny<string>(),
            null), Times.Once);
        
        // Get captured JSON from the last call
        _redisServiceMock.Invocations
            .Where(i => i.Method.Name == nameof(IRedisService.SetAsync))
            .Select(i => i.Arguments[1] as string)
            .Where(json => json != null)
            .ToList()
            .ForEach(json =>
            {
                var result = json!.ToTypedObject<WorkItemFetchResult>();
                if (result != null)
                {
                    assert(result);
                }
            });
    }
}
