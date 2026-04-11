using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ReleaseKit.Application.Common;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Application.Tests.Tasks;

/// <summary>
/// AnalyzeRiskTask 單元測試（Agentic 模式）
/// </summary>
public class AnalyzeRiskTaskTests
{
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly Mock<IRiskAnalyzer> _riskAnalyzerMock;
    private readonly Mock<ILogger<AnalyzeRiskTask>> _loggerMock;
    private readonly RiskAnalysisOptions _riskOptions;

    public AnalyzeRiskTaskTests()
    {
        _redisServiceMock = new Mock<IRedisService>();
        _riskAnalyzerMock = new Mock<IRiskAnalyzer>();
        _loggerMock = new Mock<ILogger<AnalyzeRiskTask>>();
        _riskOptions = new RiskAnalysisOptions
        {
            CloneBasePath = "/clone",
            ReportOutputPath = "/reports",
            MaxConcurrentAnalysis = 2
        };
    }

    private AnalyzeRiskTask CreateTask()
    {
        return new AnalyzeRiskTask(
            _redisServiceMock.Object,
            _riskAnalyzerMock.Object,
            Options.Create(_riskOptions),
            _loggerMock.Object);
    }

    private static FetchResult CreateFetchResult(params ProjectResult[] projects)
    {
        return new FetchResult { Results = projects.ToList() };
    }

    private static ProjectResult CreateProjectResult(
        string projectPath,
        params MergeRequestOutput[] prs)
    {
        return new ProjectResult
        {
            ProjectPath = projectPath,
            PullRequests = prs.ToList()
        };
    }

    private static MergeRequestOutput CreatePr(
        string? mergeCommitSha = "abc123",
        string title = "feat: 測試",
        string sourceBranch = "feature/test",
        string targetBranch = "main",
        string authorName = "dev1",
        string prUrl = "https://example.com/pr/1")
    {
        return new MergeRequestOutput
        {
            MergeCommitSha = mergeCommitSha,
            Title = title,
            SourceBranch = sourceBranch,
            TargetBranch = targetBranch,
            AuthorName = authorName,
            PRUrl = prUrl
        };
    }

    [Fact]
    public async Task ExecuteAsync_無PR資料時跳過分析()
    {
        // Arrange — GitLab 與 Bitbucket 都沒有 PR 資料
        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.GitLabHash, RedisKeys.Fields.PullRequestsByUser))
            .ReturnsAsync((string?)null);
        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.BitbucketHash, RedisKeys.Fields.PullRequestsByUser))
            .ReturnsAsync((string?)null);
        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.ClonePaths))
            .ReturnsAsync((string?)null);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — IRiskAnalyzer 不應被呼叫
        _riskAnalyzerMock.Verify(x => x.AnalyzeProjectRiskAsync(
            It.IsAny<ProjectAnalysisContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_無ClonePaths時跳過分析()
    {
        // Arrange — 有 PR 資料但無 ClonePaths
        var fetchResult = CreateFetchResult(
            CreateProjectResult("group/project-a", CreatePr("sha1")));
        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.GitLabHash, RedisKeys.Fields.PullRequestsByUser))
            .ReturnsAsync(fetchResult.ToJson());
        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.BitbucketHash, RedisKeys.Fields.PullRequestsByUser))
            .ReturnsAsync((string?)null);
        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.ClonePaths))
            .ReturnsAsync((string?)null);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — IRiskAnalyzer 不應被呼叫
        _riskAnalyzerMock.Verify(x => x.AnalyzeProjectRiskAsync(
            It.IsAny<ProjectAnalysisContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_專案無MergeCommitSha時跳過該專案()
    {
        // Arrange — 專案的 PR 都沒有 MergeCommitSha
        var fetchResult = CreateFetchResult(
            CreateProjectResult("group/project-a",
                CreatePr(mergeCommitSha: null),
                CreatePr(mergeCommitSha: "")));

        var clonePaths = new Dictionary<string, string>
        {
            ["group/project-a"] = "/clone/project-a"
        };

        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.GitLabHash, RedisKeys.Fields.PullRequestsByUser))
            .ReturnsAsync(fetchResult.ToJson());
        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.BitbucketHash, RedisKeys.Fields.PullRequestsByUser))
            .ReturnsAsync((string?)null);
        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.ClonePaths))
            .ReturnsAsync(clonePaths.ToJson());

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 該專案被跳過，IRiskAnalyzer 不應被呼叫
        _riskAnalyzerMock.Verify(x => x.AnalyzeProjectRiskAsync(
            It.IsAny<ProjectAnalysisContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_正常專案建立正確的ProjectAnalysisContext()
    {
        // Arrange
        var fetchResult = CreateFetchResult(
            CreateProjectResult("group/project-a",
                CreatePr("sha1", title: "feat: 功能A"),
                CreatePr("sha2", title: "fix: 修復B")));

        var clonePaths = new Dictionary<string, string>
        {
            ["group/project-a"] = "/clone/project-a"
        };

        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.GitLabHash, RedisKeys.Fields.PullRequestsByUser))
            .ReturnsAsync(fetchResult.ToJson());
        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.BitbucketHash, RedisKeys.Fields.PullRequestsByUser))
            .ReturnsAsync((string?)null);
        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.ClonePaths))
            .ReturnsAsync(clonePaths.ToJson());

        ProjectAnalysisContext? capturedContext = null;
        _riskAnalyzerMock.Setup(x => x.AnalyzeProjectRiskAsync(
                It.IsAny<ProjectAnalysisContext>(), It.IsAny<CancellationToken>()))
            .Returns((ProjectAnalysisContext ctx, CancellationToken _) =>
            {
                capturedContext = ctx;
                return Task.FromResult(new RiskAnalysisReport
                {
                    Sequence = 0,
                    ProjectName = ctx.ProjectName,
                    RiskItems = new List<RiskItem>(),
                    Summary = "測試摘要",
                    AnalyzedAt = DateTimeOffset.UtcNow
                });
            });

        _redisServiceMock.Setup(x => x.HashSetAsync(
                RedisKeys.RiskAnalysisHash, It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 驗證傳入 IRiskAnalyzer 的上下文正確
        Assert.NotNull(capturedContext);
        Assert.Equal("group/project-a", capturedContext.ProjectName);
        Assert.Equal("/clone/project-a", capturedContext.RepoPath);
        Assert.Equal(2, capturedContext.CommitShas.Count);
        Assert.Contains("sha1", capturedContext.CommitShas);
        Assert.Contains("sha2", capturedContext.CommitShas);
    }

    [Fact]
    public async Task ExecuteAsync_結果存入Redis的Intermediate與AnalysisContext欄位()
    {
        // Arrange
        var fetchResult = CreateFetchResult(
            CreateProjectResult("group/project-a", CreatePr("sha1")));

        var clonePaths = new Dictionary<string, string>
        {
            ["group/project-a"] = "/clone/project-a"
        };

        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.GitLabHash, RedisKeys.Fields.PullRequestsByUser))
            .ReturnsAsync(fetchResult.ToJson());
        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.BitbucketHash, RedisKeys.Fields.PullRequestsByUser))
            .ReturnsAsync((string?)null);
        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.ClonePaths))
            .ReturnsAsync(clonePaths.ToJson());

        _riskAnalyzerMock.Setup(x => x.AnalyzeProjectRiskAsync(
                It.IsAny<ProjectAnalysisContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RiskAnalysisReport
            {
                Sequence = 0,
                ProjectName = "group/project-a",
                RiskItems = new List<RiskItem>(),
                Summary = "摘要",
                AnalyzedAt = DateTimeOffset.UtcNow
            });

        var storedFields = new Dictionary<string, string>();
        _redisServiceMock.Setup(x => x.HashSetAsync(
                RedisKeys.RiskAnalysisHash, It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string _, string field, string value) =>
            {
                storedFields[field] = value;
                return Task.FromResult(true);
            });

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 驗證 Intermediate 與 AnalysisContext 都有存入
        Assert.Contains(storedFields.Keys,
            k => k.StartsWith(RedisKeys.Fields.IntermediatePrefix));
        Assert.Contains(storedFields.Keys,
            k => k.StartsWith(RedisKeys.Fields.AnalysisContextPrefix));
    }

    [Fact]
    public async Task ExecuteAsync_重複CommitSha應去重()
    {
        // Arrange — 兩個 PR 有相同的 MergeCommitSha
        var fetchResult = CreateFetchResult(
            CreateProjectResult("group/project-a",
                CreatePr("same-sha"),
                CreatePr("same-sha"),
                CreatePr("different-sha")));

        var clonePaths = new Dictionary<string, string>
        {
            ["group/project-a"] = "/clone/project-a"
        };

        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.GitLabHash, RedisKeys.Fields.PullRequestsByUser))
            .ReturnsAsync(fetchResult.ToJson());
        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.BitbucketHash, RedisKeys.Fields.PullRequestsByUser))
            .ReturnsAsync((string?)null);
        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.ClonePaths))
            .ReturnsAsync(clonePaths.ToJson());

        ProjectAnalysisContext? capturedContext = null;
        _riskAnalyzerMock.Setup(x => x.AnalyzeProjectRiskAsync(
                It.IsAny<ProjectAnalysisContext>(), It.IsAny<CancellationToken>()))
            .Returns((ProjectAnalysisContext ctx, CancellationToken _) =>
            {
                capturedContext = ctx;
                return Task.FromResult(new RiskAnalysisReport
                {
                    Sequence = 0,
                    ProjectName = ctx.ProjectName,
                    RiskItems = new List<RiskItem>(),
                    Summary = "摘要",
                    AnalyzedAt = DateTimeOffset.UtcNow
                });
            });

        _redisServiceMock.Setup(x => x.HashSetAsync(
                RedisKeys.RiskAnalysisHash, It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — CommitShas 應去重，只有 2 個
        Assert.NotNull(capturedContext);
        Assert.Equal(2, capturedContext.CommitShas.Count);
        Assert.Contains("same-sha", capturedContext.CommitShas);
        Assert.Contains("different-sha", capturedContext.CommitShas);
    }
}
