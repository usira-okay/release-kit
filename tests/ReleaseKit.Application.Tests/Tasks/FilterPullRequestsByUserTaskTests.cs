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
    
    /// <summary>
    /// T008: Bitbucket 過濾測試 - Redis 中有 PR 資料且使用者清單有匹配項，驗證過濾後僅保留匹配使用者的 PR
    /// </summary>
    [Fact]
    public async Task FilterBitbucketPullRequestsByUser_ShouldFilterByUserIds_WhenUserIdsMatch()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<FilterBitbucketPullRequestsByUserTask>>();
        var redisServiceMock = new Mock<IRedisService>();
        
        // 準備 Redis 中的 PR 資料
        var fetchResult = new FetchResult
        {
            Results = new List<ProjectResult>
            {
                new ProjectResult
                {
                    ProjectPath = "workspace/repo1",
                    Platform = SourceControlPlatform.Bitbucket,
                    PullRequests = new List<MergeRequestOutput>
                    {
                        new MergeRequestOutput
                        {
                            Title = "PR 1",
                            AuthorUserId = "{abc-def-123}",
                            AuthorName = "John Doe",
                            SourceBranch = "feature/test",
                            TargetBranch = "main",
                            State = "merged",
                            PRUrl = "https://bitbucket.org/workspace/repo1/pull-requests/1",
                            CreatedAt = DateTimeOffset.UtcNow
                        },
                        new MergeRequestOutput
                        {
                            Title = "PR 2",
                            AuthorUserId = "{xyz-789}",
                            AuthorName = "Jane Smith",
                            SourceBranch = "feature/test2",
                            TargetBranch = "main",
                            State = "merged",
                            PRUrl = "https://bitbucket.org/workspace/repo1/pull-requests/2",
                            CreatedAt = DateTimeOffset.UtcNow
                        },
                        new MergeRequestOutput
                        {
                            Title = "PR 3",
                            AuthorUserId = "{other-999}",
                            AuthorName = "Other User",
                            SourceBranch = "feature/test3",
                            TargetBranch = "main",
                            State = "merged",
                            PRUrl = "https://bitbucket.org/workspace/repo1/pull-requests/3",
                            CreatedAt = DateTimeOffset.UtcNow
                        }
                    }
                }
            }
        };
        
        redisServiceMock.Setup(x => x.GetAsync(RedisKeys.BitbucketPullRequests))
            .ReturnsAsync(fetchResult.ToJson());
        
        var userMappingOptions = Options.Create(new UserMappingOptions
        {
            Mappings = new List<UserMapping>
            {
                new UserMapping { BitbucketUserId = "{abc-def-123}", DisplayName = "John Doe" },
                new UserMapping { BitbucketUserId = "{xyz-789}", DisplayName = "Jane Smith" }
            }
        });
        
        var task = new FilterBitbucketPullRequestsByUserTask(
            loggerMock.Object,
            redisServiceMock.Object,
            userMappingOptions);
        
        string? capturedJson = null;
        redisServiceMock.Setup(x => x.SetAsync(RedisKeys.BitbucketPullRequestsByUser, It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .Callback<string, string, TimeSpan?>((key, json, ttl) => capturedJson = json)
            .ReturnsAsync(true);
        
        // Act
        await task.ExecuteAsync();
        
        // Assert
        redisServiceMock.Verify(x => x.GetAsync(RedisKeys.BitbucketPullRequests), Times.Once);
        redisServiceMock.Verify(x => x.SetAsync(RedisKeys.BitbucketPullRequestsByUser, It.IsAny<string>(), It.IsAny<TimeSpan?>()), Times.Once);
        
        // 驗證寫入的資料僅包含匹配的 PR
        Assert.NotNull(capturedJson);
        var result = capturedJson.ToTypedObject<FetchResult>();
        Assert.NotNull(result);
        Assert.Single(result.Results);
        Assert.Equal(2, result.Results[0].PullRequests.Count);
        Assert.All(result.Results[0].PullRequests, pr => 
            Assert.True(pr.AuthorUserId == "{abc-def-123}" || pr.AuthorUserId == "{xyz-789}"));
    }
    
    /// <summary>
    /// T009: Bitbucket 過濾後寫入 Redis 測試 - 驗證結果寫入 Bitbucket:PullRequests:ByUser 且格式為 FetchResult
    /// </summary>
    [Fact]
    public async Task FilterBitbucketPullRequestsByUser_ShouldWriteToCorrectRedisKey_WhenFilterComplete()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<FilterBitbucketPullRequestsByUserTask>>();
        var redisServiceMock = new Mock<IRedisService>();
        
        var fetchResult = new FetchResult
        {
            Results = new List<ProjectResult>
            {
                new ProjectResult
                {
                    ProjectPath = "workspace/repo",
                    Platform = SourceControlPlatform.Bitbucket,
                    PullRequests = new List<MergeRequestOutput>
                    {
                        new MergeRequestOutput { Title = "PR 1", AuthorUserId = "{abc-def-123}", AuthorName = "John", SourceBranch = "feature", TargetBranch = "main", State = "merged", PRUrl = "url", CreatedAt = DateTimeOffset.UtcNow }
                    }
                }
            }
        };
        
        redisServiceMock.Setup(x => x.GetAsync(RedisKeys.BitbucketPullRequests))
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
                new UserMapping { BitbucketUserId = "{abc-def-123}", DisplayName = "John Doe" }
            }
        });
        
        var task = new FilterBitbucketPullRequestsByUserTask(
            loggerMock.Object,
            redisServiceMock.Object,
            userMappingOptions);
        
        // Act
        await task.ExecuteAsync();
        
        // Assert
        Assert.Equal(RedisKeys.BitbucketPullRequestsByUser, capturedKey);
        Assert.NotNull(capturedJson);
        
        var result = capturedJson.ToTypedObject<FetchResult>();
        Assert.NotNull(result);
        Assert.NotNull(result.Results);
        Assert.Single(result.Results);
        Assert.Equal(SourceControlPlatform.Bitbucket, result.Results[0].Platform);
    }
    
    /// <summary>
    /// T011: 無 PR 資料測試 - Redis 中不存在 PR 資料時，驗證記錄警告日誌且不寫入新 Redis Key
    /// </summary>
    [Fact]
    public async Task FilterGitLabPullRequestsByUser_ShouldLogWarningAndNotWriteRedis_WhenNoPRData()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<FilterGitLabPullRequestsByUserTask>>();
        var redisServiceMock = new Mock<IRedisService>();
        
        // Redis 中無 PR 資料
        redisServiceMock.Setup(x => x.GetAsync(RedisKeys.GitLabPullRequests))
            .ReturnsAsync((string?)null);
        
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
        redisServiceMock.Verify(x => x.GetAsync(RedisKeys.GitLabPullRequests), Times.Once);
        redisServiceMock.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()), Times.Never);
    }
    
    /// <summary>
    /// T012: 空使用者清單測試 - UserMapping.Mappings 為空時，驗證記錄警告日誌且不寫入新 Redis Key
    /// </summary>
    [Fact]
    public async Task FilterGitLabPullRequestsByUser_ShouldLogWarningAndNotWriteRedis_WhenEmptyUserList()
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
        
        // 空的使用者清單
        var userMappingOptions = Options.Create(new UserMappingOptions
        {
            Mappings = new List<UserMapping>()
        });
        
        var task = new FilterGitLabPullRequestsByUserTask(
            loggerMock.Object,
            redisServiceMock.Object,
            userMappingOptions);
        
        // Act
        await task.ExecuteAsync();
        
        // Assert
        redisServiceMock.Verify(x => x.GetAsync(RedisKeys.GitLabPullRequests), Times.Once);
        redisServiceMock.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()), Times.Never);
    }
    
    /// <summary>
    /// T013: 含 Error 的 ProjectResult 測試 - 驗證含 Error 的 ProjectResult 保留原樣不進行 PR 過濾
    /// </summary>
    [Fact]
    public async Task FilterGitLabPullRequestsByUser_ShouldPreserveErrorResults_WhenProjectHasError()
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
                    ProjectPath = "group/project1",
                    Platform = SourceControlPlatform.GitLab,
                    Error = "Failed to fetch PRs",
                    PullRequests = new List<MergeRequestOutput>()
                },
                new ProjectResult
                {
                    ProjectPath = "group/project2",
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
        
        string? capturedJson = null;
        redisServiceMock.Setup(x => x.SetAsync(RedisKeys.GitLabPullRequestsByUser, It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .Callback<string, string, TimeSpan?>((key, json, ttl) => capturedJson = json)
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
        Assert.NotNull(capturedJson);
        var result = capturedJson.ToTypedObject<FetchResult>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Results.Count);
        
        // 第一個 ProjectResult 含 Error，應保留原樣
        Assert.Equal("Failed to fetch PRs", result.Results[0].Error);
        Assert.Empty(result.Results[0].PullRequests);
        
        // 第二個 ProjectResult 正常過濾
        Assert.Null(result.Results[1].Error);
        Assert.Single(result.Results[1].PullRequests);
    }
    
    /// <summary>
    /// T014: 空 UserId 過濾測試 - UserMapping 中某 UserId 為空字串時，驗證該項目不參與過濾比對
    /// </summary>
    [Fact]
    public async Task FilterGitLabPullRequestsByUser_ShouldIgnoreEmptyUserIds_WhenMappingHasEmptyIds()
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
                        new MergeRequestOutput { Title = "PR 1", AuthorUserId = "12345", AuthorName = "John", SourceBranch = "feature", TargetBranch = "main", State = "merged", PRUrl = "url1", CreatedAt = DateTimeOffset.UtcNow },
                        new MergeRequestOutput { Title = "PR 2", AuthorUserId = "", AuthorName = "Anonymous", SourceBranch = "feature2", TargetBranch = "main", State = "merged", PRUrl = "url2", CreatedAt = DateTimeOffset.UtcNow },
                        new MergeRequestOutput { Title = "PR 3", AuthorUserId = "67890", AuthorName = "Jane", SourceBranch = "feature3", TargetBranch = "main", State = "merged", PRUrl = "url3", CreatedAt = DateTimeOffset.UtcNow }
                    }
                }
            }
        };
        
        redisServiceMock.Setup(x => x.GetAsync(RedisKeys.GitLabPullRequests))
            .ReturnsAsync(fetchResult.ToJson());
        
        string? capturedJson = null;
        redisServiceMock.Setup(x => x.SetAsync(RedisKeys.GitLabPullRequestsByUser, It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .Callback<string, string, TimeSpan?>((key, json, ttl) => capturedJson = json)
            .ReturnsAsync(true);
        
        // UserMapping 包含空字串的 UserId
        var userMappingOptions = Options.Create(new UserMappingOptions
        {
            Mappings = new List<UserMapping>
            {
                new UserMapping { GitLabUserId = "12345", DisplayName = "John Doe" },
                new UserMapping { GitLabUserId = "", DisplayName = "Empty User" },
                new UserMapping { GitLabUserId = "67890", DisplayName = "Jane Smith" }
            }
        });
        
        var task = new FilterGitLabPullRequestsByUserTask(
            loggerMock.Object,
            redisServiceMock.Object,
            userMappingOptions);
        
        // Act
        await task.ExecuteAsync();
        
        // Assert
        Assert.NotNull(capturedJson);
        var result = capturedJson.ToTypedObject<FetchResult>();
        Assert.NotNull(result);
        Assert.Single(result.Results);
        Assert.Equal(2, result.Results[0].PullRequests.Count);
        
        // 應僅包含 UserId 為 "12345" 和 "67890" 的 PR，不包含空 UserId 的 PR
        Assert.DoesNotContain(result.Results[0].PullRequests, pr => pr.AuthorUserId == "");
        Assert.Contains(result.Results[0].PullRequests, pr => pr.AuthorUserId == "12345");
        Assert.Contains(result.Results[0].PullRequests, pr => pr.AuthorUserId == "67890");
    }
}
