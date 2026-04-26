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
/// AnalyzePRDiffsTask 單元測試
/// </summary>
public class AnalyzePRDiffsTaskTests
{
    private readonly Mock<IGitOperationService> _gitServiceMock;
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly Mock<ILogger<AnalyzePRDiffsTask>> _loggerMock;

    private const string RunId = "20240315103045";
    private const string ProjectPath = "group/project-a";
    private const string LocalPath = "/repos/group/project-a";
    private const string CommitSha = "abc123def456";

    public AnalyzePRDiffsTaskTests()
    {
        _gitServiceMock = new Mock<IGitOperationService>();
        _redisServiceMock = new Mock<IRedisService>();
        _loggerMock = new Mock<ILogger<AnalyzePRDiffsTask>>();

        _redisServiceMock
            .Setup(x => x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
    }

    private AnalyzePRDiffsTask CreateTask() =>
        new(_gitServiceMock.Object, _redisServiceMock.Object, _loggerMock.Object);

    private static MergeRequestOutput BuildMergeRequestOutput(string? mergeCommitSha = CommitSha) =>
        new()
        {
            Title = "feat: add login",
            SourceBranch = "feature/login",
            TargetBranch = "main",
            AuthorName = "Alice",
            AuthorUserId = "alice",
            MergeCommitSha = mergeCommitSha,
            PrId = "42",
            PRUrl = "https://gitlab.example.com/group/project-a/-/merge_requests/42",
            State = "merged",
            CreatedAt = DateTimeOffset.UtcNow,
            MergedAt = DateTimeOffset.UtcNow
        };

    private static FileDiff BuildFileDiff(string path = "src/Login.cs") =>
        new()
        {
            FilePath = path,
            ChangeType = ChangeType.Modified,
            DiffContent = "@@ -1,3 +1,4 @@",
            CommitSha = CommitSha
        };

    private void SetupRunId(string? runId = RunId) =>
        _redisServiceMock
            .Setup(x => x.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey))
            .ReturnsAsync(runId);

    private void SetupStage1(string projectPath, string localPath, string status = "Success") =>
        _redisServiceMock
            .Setup(x => x.HashGetAsync(RiskAnalysisRedisKeys.Stage1Hash(RunId), projectPath))
            .ReturnsAsync(new { LocalPath = localPath, Status = status }.ToJson());

    private void SetupGitLabPrs(Dictionary<string, List<MergeRequestOutput>> data) =>
        _redisServiceMock
            .Setup(x => x.HashGetAsync(RedisKeys.GitLabHash, RedisKeys.Fields.PullRequests))
            .ReturnsAsync(ToFetchResultJson(data, SourceControlPlatform.GitLab));

    private void SetupBitbucketPrs(Dictionary<string, List<MergeRequestOutput>> data) =>
        _redisServiceMock
            .Setup(x => x.HashGetAsync(RedisKeys.BitbucketHash, RedisKeys.Fields.PullRequests))
            .ReturnsAsync(ToFetchResultJson(data, SourceControlPlatform.Bitbucket));

    /// <summary>
    /// 將測試資料轉換為 FetchResult JSON，模擬 BaseFetchPullRequestsTask 的實際輸出格式
    /// </summary>
    private static string ToFetchResultJson(
        Dictionary<string, List<MergeRequestOutput>> data,
        SourceControlPlatform platform)
    {
        var fetchResult = new FetchResult
        {
            Results = data.Select(kvp => new ProjectResult
            {
                ProjectPath = kvp.Key,
                Platform = platform,
                PullRequests = kvp.Value
            }).ToList()
        };
        return fetchResult.ToJson();
    }

    [Fact]
    public async Task ExecuteAsync_找不到RunId時_應提早結束不分析()
    {
        // Arrange
        SetupRunId(null);
        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _gitServiceMock.Verify(
            x => x.GetCommitDiffAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _redisServiceMock.Verify(
            x => x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_Clone失敗的專案_應跳過不分析()
    {
        // Arrange
        SetupRunId();
        SetupGitLabPrs(new Dictionary<string, List<MergeRequestOutput>>
        {
            [ProjectPath] = new() { BuildMergeRequestOutput() }
        });
        SetupStage1(ProjectPath, LocalPath, "Failed: connection refused");
        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _gitServiceMock.Verify(
            x => x.GetCommitDiffAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _redisServiceMock.Verify(
            x => x.HashSetAsync(RiskAnalysisRedisKeys.Stage2Hash(RunId), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_無PR資料的專案_應跳過不分析()
    {
        // Arrange
        SetupRunId();
        // 空 PR 資料
        SetupGitLabPrs(new Dictionary<string, List<MergeRequestOutput>>());
        _redisServiceMock
            .Setup(x => x.HashGetAsync(RedisKeys.BitbucketHash, RedisKeys.Fields.PullRequests))
            .ReturnsAsync((string?)null);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _gitServiceMock.Verify(
            x => x.GetCommitDiffAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_PR無MergeCommitSha_應跳過該PR()
    {
        // Arrange
        SetupRunId();
        SetupGitLabPrs(new Dictionary<string, List<MergeRequestOutput>>
        {
            [ProjectPath] = new() { BuildMergeRequestOutput(mergeCommitSha: null) }
        });
        SetupStage1(ProjectPath, LocalPath);
        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _gitServiceMock.Verify(
            x => x.GetCommitDiffAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        // 仍寫入空 diff 結果
        _redisServiceMock.Verify(
            x => x.HashSetAsync(RiskAnalysisRedisKeys.Stage2Hash(RunId), ProjectPath, It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_成功取得Diff_應呼叫GetCommitDiffAsync並儲存結果()
    {
        // Arrange
        var fileDiff = BuildFileDiff();
        SetupRunId();
        SetupGitLabPrs(new Dictionary<string, List<MergeRequestOutput>>
        {
            [ProjectPath] = new() { BuildMergeRequestOutput() }
        });
        SetupStage1(ProjectPath, LocalPath);
        _gitServiceMock
            .Setup(x => x.GetCommitDiffAsync(LocalPath, CommitSha, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<FileDiff>>.Success(new List<FileDiff> { fileDiff }));
        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _gitServiceMock.Verify(
            x => x.GetCommitDiffAsync(LocalPath, CommitSha, It.IsAny<CancellationToken>()),
            Times.Once);
        _redisServiceMock.Verify(
            x => x.HashSetAsync(
                RiskAnalysisRedisKeys.Stage2Hash(RunId),
                ProjectPath,
                It.Is<string>(v => v.Contains("src/Login.cs"))),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Diff取得失敗_應記錄警告並繼續不拋出例外()
    {
        // Arrange
        SetupRunId();
        SetupGitLabPrs(new Dictionary<string, List<MergeRequestOutput>>
        {
            [ProjectPath] = new() { BuildMergeRequestOutput() }
        });
        SetupStage1(ProjectPath, LocalPath);
        _gitServiceMock
            .Setup(x => x.GetCommitDiffAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<FileDiff>>.Failure(Error.Git.DiffFailed(CommitSha, "not found")));
        var task = CreateTask();

        // Act — 不應拋出例外
        await task.ExecuteAsync();

        // Assert — 仍寫入空 diff 結果
        _redisServiceMock.Verify(
            x => x.HashSetAsync(RiskAnalysisRedisKeys.Stage2Hash(RunId), ProjectPath, It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_多個PR有MergeCommitSha_應對每個CommitSha呼叫GetCommitDiffAsync()
    {
        // Arrange
        const string sha1 = "aaa111";
        const string sha2 = "bbb222";
        var mr1 = BuildMergeRequestOutput(sha1);
        var mr2 = BuildMergeRequestOutput(sha2);
        SetupRunId();
        SetupGitLabPrs(new Dictionary<string, List<MergeRequestOutput>>
        {
            [ProjectPath] = new() { mr1, mr2 }
        });
        SetupStage1(ProjectPath, LocalPath);
        _gitServiceMock
            .Setup(x => x.GetCommitDiffAsync(LocalPath, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<FileDiff>>.Success(new List<FileDiff> { BuildFileDiff() }));
        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _gitServiceMock.Verify(
            x => x.GetCommitDiffAsync(LocalPath, sha1, It.IsAny<CancellationToken>()),
            Times.Once);
        _gitServiceMock.Verify(
            x => x.GetCommitDiffAsync(LocalPath, sha2, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_GitLab與Bitbucket的PR合併後_應分析全部專案()
    {
        // Arrange
        const string bbProject = "workspace/repo-b";
        const string bbLocal = "/repos/workspace/repo-b";
        const string bbSha = "cccc4444";

        SetupRunId();
        SetupGitLabPrs(new Dictionary<string, List<MergeRequestOutput>>
        {
            [ProjectPath] = new() { BuildMergeRequestOutput() }
        });
        SetupBitbucketPrs(new Dictionary<string, List<MergeRequestOutput>>
        {
            [bbProject] = new()
            {
                new MergeRequestOutput
                {
                    Title = "bb-pr",
                    SourceBranch = "feature/x",
                    TargetBranch = "main",
                    AuthorName = "Bob",
                    AuthorUserId = "bob",
                    MergeCommitSha = bbSha,
                    PrId = "7",
                    PRUrl = "https://bitbucket.org/workspace/repo-b/pull-requests/7",
                    State = "merged",
                    CreatedAt = DateTimeOffset.UtcNow,
                    MergedAt = DateTimeOffset.UtcNow
                }
            }
        });
        SetupStage1(ProjectPath, LocalPath);
        _redisServiceMock
            .Setup(x => x.HashGetAsync(RiskAnalysisRedisKeys.Stage1Hash(RunId), bbProject))
            .ReturnsAsync(new { LocalPath = bbLocal, Status = "Success" }.ToJson());
        _gitServiceMock
            .Setup(x => x.GetCommitDiffAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<FileDiff>>.Success(new List<FileDiff>()));
        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 兩個專案都寫入 Stage 2
        _redisServiceMock.Verify(
            x => x.HashSetAsync(RiskAnalysisRedisKeys.Stage2Hash(RunId), ProjectPath, It.IsAny<string>()),
            Times.Once);
        _redisServiceMock.Verify(
            x => x.HashSetAsync(RiskAnalysisRedisKeys.Stage2Hash(RunId), bbProject, It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Stage1無記錄的專案_應跳過()
    {
        // Arrange
        SetupRunId();
        SetupGitLabPrs(new Dictionary<string, List<MergeRequestOutput>>
        {
            [ProjectPath] = new() { BuildMergeRequestOutput() }
        });
        // Stage1 中無此專案的記錄
        _redisServiceMock
            .Setup(x => x.HashGetAsync(RiskAnalysisRedisKeys.Stage1Hash(RunId), ProjectPath))
            .ReturnsAsync((string?)null);
        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _gitServiceMock.Verify(
            x => x.GetCommitDiffAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _redisServiceMock.Verify(
            x => x.HashSetAsync(RiskAnalysisRedisKeys.Stage2Hash(RunId), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }
}
