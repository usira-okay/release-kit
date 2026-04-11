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

namespace ReleaseKit.Application.Tests.Tasks;

/// <summary>
/// ExtractPrDiffsTask 單元測試
/// </summary>
public class ExtractPrDiffsTaskTests
{
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly Mock<IGitService> _gitServiceMock;
    private readonly Mock<ILogger<ExtractPrDiffsTask>> _loggerMock;
    private string? _capturedRedisJson;

    private const string SampleDiff = """
        diff --git a/src/Foo.cs b/src/Foo.cs
        index 1234567..abcdefg 100644
        --- a/src/Foo.cs
        +++ b/src/Foo.cs
        @@ -1,3 +1,4 @@
        +using System;
         namespace Foo;
        diff --git a/src/Bar.cs b/src/Bar.cs
        index 1111111..2222222 100644
        --- a/src/Bar.cs
        +++ b/src/Bar.cs
        @@ -1,3 +1,4 @@
        +using System;
         namespace Bar;
        """;

    public ExtractPrDiffsTaskTests()
    {
        _redisServiceMock = new Mock<IRedisService>();
        _gitServiceMock = new Mock<IGitService>();
        _loggerMock = new Mock<ILogger<ExtractPrDiffsTask>>();

        // 捕捉寫入 Redis 的 JSON
        _redisServiceMock.Setup(x => x.HashSetAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.PrDiffs, It.IsAny<string>()))
            .Callback<string, string, string>((_, _, json) => _capturedRedisJson = json)
            .ReturnsAsync(true);
    }

    private ExtractPrDiffsTask CreateTask()
    {
        return new ExtractPrDiffsTask(
            _redisServiceMock.Object,
            _gitServiceMock.Object,
            _loggerMock.Object);
    }

    /// <summary>
    /// 建立測試用的 FetchResult
    /// </summary>
    private static FetchResult CreateFetchResult(
        string projectPath,
        SourceControlPlatform platform,
        params MergeRequestOutput[] prs)
    {
        return new FetchResult
        {
            Results = new List<ProjectResult>
            {
                new()
                {
                    ProjectPath = projectPath,
                    Platform = platform,
                    PullRequests = prs.ToList()
                }
            }
        };
    }

    /// <summary>
    /// 建立測試用的 MergeRequestOutput
    /// </summary>
    private static MergeRequestOutput CreatePr(
        string title = "feat: 新功能",
        string sourceBranch = "feature/test",
        string targetBranch = "main",
        string authorName = "developer",
        string prUrl = "https://gitlab.example.com/mr/1")
    {
        return new MergeRequestOutput
        {
            Title = title,
            Description = "測試描述",
            SourceBranch = sourceBranch,
            TargetBranch = targetBranch,
            CreatedAt = DateTimeOffset.UtcNow,
            MergedAt = DateTimeOffset.UtcNow,
            State = "merged",
            AuthorUserId = "user1",
            AuthorName = authorName,
            PrId = "1",
            PRUrl = prUrl,
            WorkItemId = 12345
        };
    }

    /// <summary>
    /// 設定 Redis 回傳 GitLab PR 資料
    /// </summary>
    private void SetupGitLabPrData(FetchResult? fetchResult)
    {
        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.GitLabHash, RedisKeys.Fields.PullRequestsByUser))
            .ReturnsAsync(fetchResult?.ToJson());
    }

    /// <summary>
    /// 設定 Redis 回傳 Bitbucket PR 資料
    /// </summary>
    private void SetupBitbucketPrData(FetchResult? fetchResult)
    {
        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.BitbucketHash, RedisKeys.Fields.PullRequestsByUser))
            .ReturnsAsync(fetchResult?.ToJson());
    }

    /// <summary>
    /// 設定 Redis 回傳 Clone 路徑對照表
    /// </summary>
    private void SetupClonePaths(Dictionary<string, string>? paths)
    {
        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.ClonePaths))
            .ReturnsAsync(paths?.ToJson());
    }

    [Fact]
    public async Task ExecuteAsync_成功取得MergeCommitDiff_應建立正確的PrDiffContext()
    {
        // Arrange
        var pr = CreatePr();
        var fetchResult = CreateFetchResult("group/project-a", SourceControlPlatform.GitLab, pr);
        SetupGitLabPrData(fetchResult);
        SetupBitbucketPrData(null);
        SetupClonePaths(new Dictionary<string, string> { ["group/project-a"] = "/clone/group/project-a" });

        _gitServiceMock.Setup(x => x.FindMergeCommitAsync(
                "/clone/group/project-a", "feature/test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("abc123"));
        _gitServiceMock.Setup(x => x.GetCommitDiffAsync(
                "/clone/group/project-a", "abc123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success(SampleDiff));

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        Assert.NotNull(_capturedRedisJson);
        var result = _capturedRedisJson.ToTypedObject<Dictionary<string, List<PrDiffContext>>>();
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("group/project-a"));

        var diffs = result["group/project-a"];
        Assert.Single(diffs);

        var diff = diffs[0];
        Assert.Equal("feat: 新功能", diff.Title);
        Assert.Equal("測試描述", diff.Description);
        Assert.Equal("feature/test", diff.SourceBranch);
        Assert.Equal("main", diff.TargetBranch);
        Assert.Equal("developer", diff.AuthorName);
        Assert.Equal("https://gitlab.example.com/mr/1", diff.PrUrl);
        Assert.Equal(SampleDiff, diff.DiffContent);
        Assert.Equal(SourceControlPlatform.GitLab, diff.Platform);
    }

    [Fact]
    public async Task ExecuteAsync_CommitDiff失敗時_應跳過PR()
    {
        // Arrange
        var pr = CreatePr();
        var fetchResult = CreateFetchResult("group/project-a", SourceControlPlatform.GitLab, pr);
        SetupGitLabPrData(fetchResult);
        SetupBitbucketPrData(null);
        SetupClonePaths(new Dictionary<string, string> { ["group/project-a"] = "/clone/group/project-a" });

        // FindMergeCommit 成功
        _gitServiceMock.Setup(x => x.FindMergeCommitAsync(
                "/clone/group/project-a", "feature/test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("abc123"));

        // GetCommitDiff 失敗
        _gitServiceMock.Setup(x => x.GetCommitDiffAsync(
                "/clone/group/project-a", "abc123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Failure(
                Error.RiskAnalysis.GitCommandFailed("git show", "commit diff failed")));

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 結果中不包含任何 PrDiffContext
        Assert.NotNull(_capturedRedisJson);
        var result = _capturedRedisJson.ToTypedObject<Dictionary<string, List<PrDiffContext>>>();
        Assert.NotNull(result);
        var totalDiffs = result.Values.SelectMany(x => x).Count();
        Assert.Equal(0, totalDiffs);
    }

    [Fact]
    public async Task ExecuteAsync_FindMergeCommit失敗時_應跳過PR並記錄警告()
    {
        // Arrange
        var pr = CreatePr();
        var fetchResult = CreateFetchResult("group/project-a", SourceControlPlatform.GitLab, pr);
        SetupGitLabPrData(fetchResult);
        SetupBitbucketPrData(null);
        SetupClonePaths(new Dictionary<string, string> { ["group/project-a"] = "/clone/group/project-a" });

        // Merge commit 失敗
        _gitServiceMock.Setup(x => x.FindMergeCommitAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Failure(
                Error.RiskAnalysis.GitCommandFailed("git log", "not found")));

        var task = CreateTask();

        // Act — 不應擲出例外
        await task.ExecuteAsync();

        // Assert — 結果中不包含任何 PrDiffContext
        Assert.NotNull(_capturedRedisJson);
        var result = _capturedRedisJson.ToTypedObject<Dictionary<string, List<PrDiffContext>>>();
        Assert.NotNull(result);

        var totalDiffs = result.Values.SelectMany(x => x).Count();
        Assert.Equal(0, totalDiffs);
    }

    [Fact]
    public async Task ExecuteAsync_應正確從Diff解析檔案清單()
    {
        // Arrange
        var pr = CreatePr();
        var fetchResult = CreateFetchResult("group/project-a", SourceControlPlatform.GitLab, pr);
        SetupGitLabPrData(fetchResult);
        SetupBitbucketPrData(null);
        SetupClonePaths(new Dictionary<string, string> { ["group/project-a"] = "/clone/group/project-a" });

        _gitServiceMock.Setup(x => x.FindMergeCommitAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("abc123"));
        _gitServiceMock.Setup(x => x.GetCommitDiffAsync(
                It.IsAny<string>(), "abc123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success(SampleDiff));

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        Assert.NotNull(_capturedRedisJson);
        var result = _capturedRedisJson.ToTypedObject<Dictionary<string, List<PrDiffContext>>>();
        Assert.NotNull(result);

        var diff = result["group/project-a"][0];
        Assert.Equal(2, diff.ChangedFiles.Count);
        Assert.Contains("src/Foo.cs", diff.ChangedFiles);
        Assert.Contains("src/Bar.cs", diff.ChangedFiles);
    }

    [Fact]
    public async Task ExecuteAsync_混合GitLab與Bitbucket_應處理所有PR()
    {
        // Arrange
        var gitLabPr = CreatePr(title: "feat: GitLab 功能", prUrl: "https://gitlab.example.com/mr/1");
        var gitLabFetch = CreateFetchResult("group/project-a", SourceControlPlatform.GitLab, gitLabPr);

        var bbPr = CreatePr(title: "feat: Bitbucket 功能", prUrl: "https://bitbucket.org/pr/2");
        var bbFetch = CreateFetchResult("team/repo-b", SourceControlPlatform.Bitbucket, bbPr);

        SetupGitLabPrData(gitLabFetch);
        SetupBitbucketPrData(bbFetch);
        SetupClonePaths(new Dictionary<string, string>
        {
            ["group/project-a"] = "/clone/group/project-a",
            ["team/repo-b"] = "/clone/team/repo-b"
        });

        _gitServiceMock.Setup(x => x.FindMergeCommitAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("abc123"));
        _gitServiceMock.Setup(x => x.GetCommitDiffAsync(
                It.IsAny<string>(), "abc123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success(SampleDiff));

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        Assert.NotNull(_capturedRedisJson);
        var result = _capturedRedisJson.ToTypedObject<Dictionary<string, List<PrDiffContext>>>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey("group/project-a"));
        Assert.True(result.ContainsKey("team/repo-b"));

        // 驗證平台標記正確
        Assert.Equal(SourceControlPlatform.GitLab, result["group/project-a"][0].Platform);
        Assert.Equal(SourceControlPlatform.Bitbucket, result["team/repo-b"][0].Platform);
    }

    [Fact]
    public async Task ExecuteAsync_無PR資料時_應正常完成並存入空字典()
    {
        // Arrange
        SetupGitLabPrData(null);
        SetupBitbucketPrData(null);
        SetupClonePaths(null);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        Assert.NotNull(_capturedRedisJson);
        var result = _capturedRedisJson.ToTypedObject<Dictionary<string, List<PrDiffContext>>>();
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ExecuteAsync_Clone路徑不存在時_應跳過該專案()
    {
        // Arrange
        var pr = CreatePr();
        var fetchResult = CreateFetchResult("group/project-a", SourceControlPlatform.GitLab, pr);
        SetupGitLabPrData(fetchResult);
        SetupBitbucketPrData(null);

        // Clone 路徑不包含 "group/project-a"
        SetupClonePaths(new Dictionary<string, string> { ["other/project"] = "/clone/other/project" });

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 不應呼叫任何 Git 操作
        _gitServiceMock.Verify(x => x.FindMergeCommitAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        Assert.NotNull(_capturedRedisJson);
        var result = _capturedRedisJson.ToTypedObject<Dictionary<string, List<PrDiffContext>>>();
        Assert.NotNull(result);
        var totalDiffs = result.Values.SelectMany(x => x).Count();
        Assert.Equal(0, totalDiffs);
    }

    [Fact]
    public async Task ExecuteAsync_應以正確的Key存入Redis()
    {
        // Arrange
        SetupGitLabPrData(null);
        SetupBitbucketPrData(null);
        SetupClonePaths(null);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 驗證 HashSetAsync 以正確參數被呼叫
        _redisServiceMock.Verify(x => x.HashSetAsync(
            RedisKeys.RiskAnalysisHash,
            RedisKeys.Fields.PrDiffs,
            It.IsAny<string>()), Times.Once);
    }
}
