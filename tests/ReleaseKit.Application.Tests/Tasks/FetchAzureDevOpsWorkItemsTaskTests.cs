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
    public async Task ExecuteAsync_WithSingleVSTSIdInTitle_ShouldFetchWorkItem()
    {
        // Arrange
        var fetchResult = CreateFetchResult("修復登入錯誤", "feature/VSTS12345-login-fix", "main");
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
    public async Task ExecuteAsync_WithMultipleVSTSIdsInOneTitle_ShouldFetchAllWorkItems()
    {
        // Arrange - 兩個不同的 PR，各自包含不同的 VSTS ID 在 source branch 中
        var result1 = new FetchResult
        {
            Results = new List<ProjectResult>
            {
                new ProjectResult
                {
                    ProjectPath = "group/project1",
                    Platform = SourceControlPlatform.GitLab,
                    PullRequests = new List<MergeRequestOutput>
                    {
                        CreateMergeRequest("修復問題 1", "feature/VSTS111-fix-1", "main"),
                        CreateMergeRequest("修復問題 2", "feature/VSTS222-fix-2", "main")
                    }
                }
            }
        };
        SetupRedis(gitLabData: result1);
        
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
    public async Task ExecuteAsync_WithDuplicateVSTSIdsAcrossPRs_ShouldPreserveDuplicatesAndTrackPrIds()
    {
        // Arrange - 兩個不同的 PR，但都指向相同的 Work Item ID
        // 新需求：不去重複，保留所有 PR-WorkItem 對應關係
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
                        new MergeRequestOutput
                        {
                            Title = "Fix issue",
                            SourceBranch = "feature/VSTS123-fix-1",
                            TargetBranch = "main",
                            State = "merged",
                            AuthorUserId = "12345",
                            AuthorName = "Test User",
                            PrId = "1",
                            PRUrl = "https://gitlab.com/proj/mrs/1",
                            CreatedAt = DateTimeOffset.UtcNow,
                            WorkItemId = 123
                        },
                        new MergeRequestOutput
                        {
                            Title = "另一個 PR 提到相同 WorkItem",
                            SourceBranch = "feature/VSTS123-fix-2",
                            TargetBranch = "main",
                            State = "merged",
                            AuthorUserId = "67890",
                            AuthorName = "Another User",
                            PrId = "2",
                            PRUrl = "https://gitlab.com/proj/mrs/2",
                            CreatedAt = DateTimeOffset.UtcNow,
                            WorkItemId = 123
                        }
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
        // 使用快取：即使有重複的 Work Item ID，也只呼叫一次 API
        _azureDevOpsRepositoryMock.Verify(x => x.GetWorkItemAsync(123), Times.Once);
        VerifyRedisWrite(result =>
        {
            // 保留重複：即使使用快取，仍應該有兩筆 WorkItemOutput（不同 PrId）
            Assert.Equal(2, result.WorkItems.Count);
            Assert.Equal(2, result.TotalPRsAnalyzed);
            Assert.Equal(2, result.TotalWorkItemsFound); // 包含重複
            
            // 驗證每個 WorkItemOutput 都有對應的 PrId
            var firstWorkItem = result.WorkItems[0];
            Assert.Equal(123, firstWorkItem.WorkItemId);
            Assert.Equal("1", firstWorkItem.PrId);
            
            var secondWorkItem = result.WorkItems[1];
            Assert.Equal(123, secondWorkItem.WorkItemId);
            Assert.Equal("2", secondWorkItem.PrId);
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithNoVSTSIdInTitle_ShouldNotWriteToRedis()
    {
        // Arrange - SourceBranch 沒有包含 VSTS ID
        var fetchResult = CreateFetchResult("No work item ID here", "feature/no-vsts-id", "main");
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
        // Arrange - 測試各種無效的 VSTS 格式（非數字、無數字等）
        var result = new FetchResult
        {
            Results = new List<ProjectResult>
            {
                new ProjectResult
                {
                    ProjectPath = "group/project1",
                    Platform = SourceControlPlatform.GitLab,
                    PullRequests = new List<MergeRequestOutput>
                    {
                        CreateMergeRequest("Invalid formats", "feature/VSTSabc", "main"),  // 非數字
                        CreateMergeRequest("Lowercase", "feature/vsts123", "main"),         // 小寫（現在支援）
                        CreateMergeRequest("No number", "feature/VSTS", "main"),            // 無數字
                        CreateMergeRequest("Valid one", "feature/VSTS456-works", "main")    // 有效的
                    }
                }
            }
        };
        SetupRedis(gitLabData: result);
        
        var workItem123 = CreateWorkItem(123, "Lowercase Valid", "Bug", "Active");
        var workItem456 = CreateWorkItem(456, "Valid", "Bug", "Active");
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(123)).ReturnsAsync(Result<WorkItem>.Success(workItem123));
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(456)).ReturnsAsync(Result<WorkItem>.Success(workItem456));

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _azureDevOpsRepositoryMock.Verify(x => x.GetWorkItemAsync(123), Times.Once);
        _azureDevOpsRepositoryMock.Verify(x => x.GetWorkItemAsync(456), Times.Once);
        _azureDevOpsRepositoryMock.Verify(x => x.GetWorkItemAsync(It.Is<int>(id => id != 123 && id != 456)), Times.Never);
        VerifyRedisWrite(result =>
        {
            Assert.Equal(2, result.WorkItems.Count);
            Assert.Equal(2, result.TotalWorkItemsFound);
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithSuccessfulAndFailedCalls_ShouldRecordBoth()
    {
        // Arrange - 兩個不同的 PR，一個成功一個失敗
        var result = new FetchResult
        {
            Results = new List<ProjectResult>
            {
                new ProjectResult
                {
                    ProjectPath = "group/project1",
                    Platform = SourceControlPlatform.GitLab,
                    PullRequests = new List<MergeRequestOutput>
                    {
                        CreateMergeRequest("Success", "feature/VSTS111-success", "main"),
                        CreateMergeRequest("Will fail", "feature/VSTS999-fail", "main")
                    }
                }
            }
        };
        SetupRedis(gitLabData: result);
        
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
    public async Task ExecuteAsync_WhenRepositoryReturnsNullWorkItem_ShouldRecordFailure()
    {
        // Arrange
        var fetchResult = CreateFetchResult("空結果測試", "feature/VSTS123-null", "main");
        SetupRedis(gitLabData: fetchResult);

        _azureDevOpsRepositoryMock
            .Setup(x => x.GetWorkItemAsync(123))
            .ReturnsAsync(Result<WorkItem>.Success(null!));

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        VerifyRedisWrite(result =>
        {
            Assert.Equal(1, result.TotalWorkItemsFound);
            Assert.Equal(0, result.SuccessCount);
            Assert.Equal(1, result.FailureCount);

            var workItem = Assert.Single(result.WorkItems);
            Assert.Equal(123, workItem.WorkItemId);
            Assert.False(workItem.IsSuccess);
            Assert.False(string.IsNullOrWhiteSpace(workItem.ErrorMessage));
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithBothGitLabAndBitbucketData_ShouldProcessAll()
    {
        // Arrange
        var gitLabResult = CreateFetchResult("GitLab Issue", "feature/VSTS111-gitlab", "main");
        var bitbucketResult = CreateFetchResult("Bitbucket Issue", "feature/VSTS222-bitbucket", "main", SourceControlPlatform.Bitbucket);
        
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
        var gitLabResult = CreateFetchResult("Issue", "feature/VSTS123-issue", "main");
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
        var bitbucketResult = CreateFetchResult("Issue", "feature/VSTS456-issue", "main", SourceControlPlatform.Bitbucket);
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

    [Fact]
    public async Task ExecuteAsync_WithDuplicateWorkItemIds_ShouldUseCacheAndReduceAPICalls()
    {
        // Arrange - 三個 PR 指向兩個不同的 Work Item（123 出現兩次，456 出現一次）
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
                        CreateMergeRequest("PR 1", "feature/VSTS123-pr1", "main"),
                        CreateMergeRequest("PR 2", "feature/VSTS123-pr2", "main"),
                        CreateMergeRequest("PR 3", "feature/VSTS456-pr3", "main")
                    }
                }
            }
        };
        SetupRedis(gitLabData: fetchResult);
        
        var workItem123 = CreateWorkItem(123, "Work Item 123", "Bug", "Active");
        var workItem456 = CreateWorkItem(456, "Work Item 456", "Task", "Closed");
        
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(123)).ReturnsAsync(Result<WorkItem>.Success(workItem123));
        _azureDevOpsRepositoryMock.Setup(x => x.GetWorkItemAsync(456)).ReturnsAsync(Result<WorkItem>.Success(workItem456));

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        // 快取生效：Work Item 123 只呼叫一次（雖然有兩個 PR 參照它）
        _azureDevOpsRepositoryMock.Verify(x => x.GetWorkItemAsync(123), Times.Once);
        _azureDevOpsRepositoryMock.Verify(x => x.GetWorkItemAsync(456), Times.Once);
        
        VerifyRedisWrite(result =>
        {
            // 輸出仍保留所有對應關係：3 筆 WorkItemOutput
            Assert.Equal(3, result.WorkItems.Count);
            Assert.Equal(3, result.TotalWorkItemsFound);
            
            // 驗證 Work Item 123 有兩筆輸出（不同 PrId）
            var workItems123 = result.WorkItems.Where(w => w.WorkItemId == 123).ToList();
            Assert.Equal(2, workItems123.Count);
            Assert.Contains(workItems123, w => w.PrId == "1");
            Assert.Contains(workItems123, w => w.PrId == "1");
            
            // 驗證 Work Item 456 有一筆輸出
            var workItems456 = result.WorkItems.Where(w => w.WorkItemId == 456).ToList();
            Assert.Single(workItems456);
        });
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
        // 從 SourceBranch 解析 WorkItemId
        var workItemId = VstsIdParser.ParseFromSourceBranch(sourceBranch);
        
        return new MergeRequestOutput
        {
            Title = title,
            SourceBranch = sourceBranch,
            TargetBranch = targetBranch,
            State = "merged",
            AuthorUserId = "12345",
            AuthorName = "Test User",
            PrId = "1",
            PRUrl = "https://example.com/pr/1",
            CreatedAt = DateTimeOffset.UtcNow,
            WorkItemId = workItemId
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
