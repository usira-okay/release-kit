using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ReleaseKit.Application.Common;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Application.Tests.Tasks;

/// <summary>
/// 過濾 PR 依使用者測試
/// </summary>
public class FilterPullRequestsByUserTaskTests
{
    /// <summary>
    /// T003: GitLab 過濾測試 - Redis 中有 PR 資料且使用者清單有匹配項，驗證過濾後僅保留匹配使用者的 PR
    /// </summary>
    [Fact]
    public async Task FilterGitLabPullRequestsByUser_ShouldFilterByUserIds_WhenUserIdsMatch()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<FilterGitLabPullRequestsByUserTask>>();
        var redisServiceMock = new Mock<IRedisService>();
        
        // 準備 Redis 中的 PR 資料
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
                            Title = "PR 1",
                            AuthorUserId = "12345",
                            AuthorName = "John Doe",
                            SourceBranch = "feature/test",
                            TargetBranch = "main",
                            State = "merged",
                            PRUrl = "https://gitlab.com/group/project1/merge_requests/1",
                            CreatedAt = DateTimeOffset.UtcNow
                        },
                        new MergeRequestOutput
                        {
                            Title = "PR 2",
                            AuthorUserId = "67890",
                            AuthorName = "Jane Smith",
                            SourceBranch = "feature/test2",
                            TargetBranch = "main",
                            State = "merged",
                            PRUrl = "https://gitlab.com/group/project1/merge_requests/2",
                            CreatedAt = DateTimeOffset.UtcNow
                        },
                        new MergeRequestOutput
                        {
                            Title = "PR 3",
                            AuthorUserId = "99999",
                            AuthorName = "Other User",
                            SourceBranch = "feature/test3",
                            TargetBranch = "main",
                            State = "merged",
                            PRUrl = "https://gitlab.com/group/project1/merge_requests/3",
                            CreatedAt = DateTimeOffset.UtcNow
                        }
                    }
                }
            }
        };
        
        redisServiceMock.Setup(x => x.GetAsync(RedisKeys.GitLabPullRequests))
            .ReturnsAsync(fetchResult.ToJson());
        redisServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(true);
        
        // 準備使用者對應設定
        var userMappingOptions = Options.Create(new UserMappingOptions
        {
            Mappings = new List<UserMapping>
            {
                new UserMapping { GitLabUserId = "12345", DisplayName = "John Doe" },
                new UserMapping { GitLabUserId = "67890", DisplayName = "Jane Smith" }
            }
        });
        
        var task = new FilterGitLabPullRequestsByUserTask(
            loggerMock.Object,
            redisServiceMock.Object,
            userMappingOptions);
        
        string? capturedJson = null;
        redisServiceMock.Setup(x => x.SetAsync(RedisKeys.GitLabPullRequestsByUser, It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .Callback<string, string, TimeSpan?>((key, json, ttl) => capturedJson = json)
            .ReturnsAsync(true);
        
        // Act
        await task.ExecuteAsync();
        
        // Assert
        redisServiceMock.Verify(x => x.GetAsync(RedisKeys.GitLabPullRequests), Times.Once);
        redisServiceMock.Verify(x => x.SetAsync(RedisKeys.GitLabPullRequestsByUser, It.IsAny<string>(), It.IsAny<TimeSpan?>()), Times.Once);
        
        // 驗證寫入的資料僅包含匹配的 PR
        Assert.NotNull(capturedJson);
        var result = capturedJson.ToTypedObject<FetchResult>();
        Assert.NotNull(result);
        Assert.Single(result.Results);
        Assert.Equal(2, result.Results[0].PullRequests.Count);
        Assert.All(result.Results[0].PullRequests, pr => 
            Assert.True(pr.AuthorUserId == "12345" || pr.AuthorUserId == "67890"));
    }
    
    /// <summary>
    /// T004: GitLab 多專案過濾測試 - 驗證多個 ProjectResult 各自獨立過濾
    /// </summary>
    [Fact]
    public async Task FilterGitLabPullRequestsByUser_ShouldFilterEachProjectIndependently_WhenMultipleProjects()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<FilterGitLabPullRequestsByUserTask>>();
        var redisServiceMock = new Mock<IRedisService>();
        
        // 準備多個專案的 PR 資料
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
                        new MergeRequestOutput { Title = "PR 1-1", AuthorUserId = "12345", AuthorName = "John", SourceBranch = "feature1", TargetBranch = "main", State = "merged", PRUrl = "url1", CreatedAt = DateTimeOffset.UtcNow },
                        new MergeRequestOutput { Title = "PR 1-2", AuthorUserId = "99999", AuthorName = "Other", SourceBranch = "feature2", TargetBranch = "main", State = "merged", PRUrl = "url2", CreatedAt = DateTimeOffset.UtcNow }
                    }
                },
                new ProjectResult
                {
                    ProjectPath = "group/project2",
                    Platform = SourceControlPlatform.GitLab,
                    PullRequests = new List<MergeRequestOutput>
                    {
                        new MergeRequestOutput { Title = "PR 2-1", AuthorUserId = "67890", AuthorName = "Jane", SourceBranch = "feature3", TargetBranch = "main", State = "merged", PRUrl = "url3", CreatedAt = DateTimeOffset.UtcNow },
                        new MergeRequestOutput { Title = "PR 2-2", AuthorUserId = "88888", AuthorName = "Someone", SourceBranch = "feature4", TargetBranch = "main", State = "merged", PRUrl = "url4", CreatedAt = DateTimeOffset.UtcNow }
                    }
                }
            }
        };
        
        redisServiceMock.Setup(x => x.GetAsync(RedisKeys.GitLabPullRequests))
            .ReturnsAsync(fetchResult.ToJson());
        redisServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(true);
        
        var userMappingOptions = Options.Create(new UserMappingOptions
        {
            Mappings = new List<UserMapping>
            {
                new UserMapping { GitLabUserId = "12345", DisplayName = "John Doe" },
                new UserMapping { GitLabUserId = "67890", DisplayName = "Jane Smith" }
            }
        });
        
        var task = new FilterGitLabPullRequestsByUserTask(
            loggerMock.Object,
            redisServiceMock.Object,
            userMappingOptions);
        
        string? capturedJson = null;
        redisServiceMock.Setup(x => x.SetAsync(RedisKeys.GitLabPullRequestsByUser, It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .Callback<string, string, TimeSpan?>((key, json, ttl) => capturedJson = json)
            .ReturnsAsync(true);
        
        // Act
        await task.ExecuteAsync();
        
        // Assert
        redisServiceMock.Verify(x => x.SetAsync(RedisKeys.GitLabPullRequestsByUser, It.IsAny<string>(), It.IsAny<TimeSpan?>()), Times.Once);
        
        Assert.NotNull(capturedJson);
        var result = capturedJson.ToTypedObject<FetchResult>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Results.Count);
        Assert.Single(result.Results[0].PullRequests);
        Assert.Equal("12345", result.Results[0].PullRequests[0].AuthorUserId);
        Assert.Single(result.Results[1].PullRequests);
        Assert.Equal("67890", result.Results[1].PullRequests[0].AuthorUserId);
    }
    
    /// <summary>
    /// T005: GitLab 過濾後寫入 Redis 測試 - 驗證結果寫入 GitLab:PullRequests:ByUser 且格式為 FetchResult
    /// </summary>
    [Fact]
    public async Task FilterGitLabPullRequestsByUser_ShouldWriteToCorrectRedisKey_WhenFilterComplete()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<FilterGitLabPullRequestsByUserTask>>();
        var redisServiceMock = new Mock<IRedisService>();
        
        var fetchResult = new FetchResult
        {
            Results = new List<ProjectResult>
            {
                new ProjectResult
                {
                    ProjectPath = "group/project",
                    Platform = SourceControlPlatform.GitLab,
                    PullRequests = new List<MergeRequestOutput>
                    {
                        new MergeRequestOutput { Title = "PR 1", AuthorUserId = "12345", AuthorName = "John", SourceBranch = "feature", TargetBranch = "main", State = "merged", PRUrl = "url", CreatedAt = DateTimeOffset.UtcNow }
                    }
                }
            }
        };
        
        redisServiceMock.Setup(x => x.GetAsync(RedisKeys.GitLabPullRequests))
            .ReturnsAsync(fetchResult.ToJson());
        
        string? capturedKey = null;
        string? capturedJson = null;
        
        redisServiceMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .Callback<string, string, TimeSpan?>((key, json, ttl) =>
            {
                capturedKey = key;
                capturedJson = json;
            })
            .ReturnsAsync(true);
        
        var userMappingOptions = Options.Create(new UserMappingOptions
        {
            Mappings = new List<UserMapping>
            {
                new UserMapping { GitLabUserId = "12345", DisplayName = "John Doe" }
            }
        });
        
        var task = new FilterGitLabPullRequestsByUserTask(
            loggerMock.Object,
            redisServiceMock.Object,
            userMappingOptions);
        
        // Act
        await task.ExecuteAsync();
        
        // Assert
        Assert.Equal(RedisKeys.GitLabPullRequestsByUser, capturedKey);
        Assert.NotNull(capturedJson);
        
        var result = capturedJson.ToTypedObject<FetchResult>();
        Assert.NotNull(result);
        Assert.NotNull(result.Results);
        Assert.Single(result.Results);
        Assert.Equal(SourceControlPlatform.GitLab, result.Results[0].Platform);
    }
}
