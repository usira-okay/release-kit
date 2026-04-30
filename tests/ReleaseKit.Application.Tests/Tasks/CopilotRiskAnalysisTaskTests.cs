using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Application.Tests.Tasks;

/// <summary>
/// CopilotRiskAnalysisTask 單元測試
/// </summary>
public class CopilotRiskAnalysisTaskTests
{
    private readonly Mock<ICopilotRiskDispatcher> _dispatcherMock;
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly Mock<ILogger<CopilotRiskAnalysisTask>> _loggerMock;

    private const string RunId = "20240315103045";
    private const string ProjectPath = "group/project-a";
    private const string LocalPath = "/repos/group/project-a";

    public CopilotRiskAnalysisTaskTests()
    {
        _dispatcherMock = new Mock<ICopilotRiskDispatcher>();
        _redisServiceMock = new Mock<IRedisService>();
        _loggerMock = new Mock<ILogger<CopilotRiskAnalysisTask>>();

        _redisServiceMock
            .Setup(x => x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        _redisServiceMock
            .Setup(x => x.HashGetAllAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, string>());

        _dispatcherMock
            .Setup(x => x.DispatchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<CommitSummary>>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<RiskScenario>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private CopilotRiskAnalysisTask CreateTask(RiskAnalysisOptions? options = null)
    {
        var effectiveOptions = options ?? new RiskAnalysisOptions();
        return new CopilotRiskAnalysisTask(
            _dispatcherMock.Object,
            _redisServiceMock.Object,
            Options.Create(effectiveOptions),
            _loggerMock.Object);
    }

    private static ProjectDiffResult BuildDiffResult(string projectPath, int count = 1) =>
        new()
        {
            ProjectPath = projectPath,
            CommitSummaries = Enumerable.Range(0, count)
                .Select(i => new CommitSummary
                {
                    CommitSha = $"abc{i:D3}",
                    ChangedFiles = new List<FileDiff>
                    {
                        new() { FilePath = $"src/File{i}.cs", ChangeType = ChangeType.Modified, CommitSha = $"abc{i:D3}" }
                    },
                    TotalFilesChanged = 1,
                    TotalLinesAdded = 10,
                    TotalLinesRemoved = 5
                })
                .ToList()
        };

    private static string BuildStage1Json(string localPath = LocalPath) =>
        new { LocalPath = localPath, Status = "Success" }.ToJson();

    [Fact]
    public async Task ExecuteAsync_RunId不存在時_應提前結束不執行分析()
    {
        // Arrange
        _redisServiceMock
            .Setup(x => x.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey))
            .ReturnsAsync((string?)null);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _dispatcherMock.Verify(
            x => x.DispatchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<CommitSummary>>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<RiskScenario>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _redisServiceMock.Verify(
            x => x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_應讀取Stage1與Stage2的Redis資料()
    {
        // Arrange
        var diffResult = BuildDiffResult(ProjectPath);

        _redisServiceMock
            .Setup(x => x.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey))
            .ReturnsAsync(RunId);
        _redisServiceMock
            .Setup(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage1Hash(RunId)))
            .ReturnsAsync(new Dictionary<string, string> { [ProjectPath] = BuildStage1Json() });
        _redisServiceMock
            .Setup(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage2Hash(RunId)))
            .ReturnsAsync(new Dictionary<string, string> { [ProjectPath] = diffResult.ToJson() });

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _redisServiceMock.Verify(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage1Hash(RunId)), Times.Once);
        _redisServiceMock.Verify(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage2Hash(RunId)), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_應對每個有CommitSummary的專案呼叫DispatchAsync()
    {
        // Arrange
        var diffA = BuildDiffResult("group/project-a");
        var diffB = BuildDiffResult("group/project-b");

        _redisServiceMock
            .Setup(x => x.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey))
            .ReturnsAsync(RunId);
        _redisServiceMock
            .Setup(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage1Hash(RunId)))
            .ReturnsAsync(new Dictionary<string, string>
            {
                ["group/project-a"] = BuildStage1Json("/repos/group/project-a"),
                ["group/project-b"] = BuildStage1Json("/repos/group/project-b")
            });
        _redisServiceMock
            .Setup(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage2Hash(RunId)))
            .ReturnsAsync(new Dictionary<string, string>
            {
                ["group/project-a"] = diffA.ToJson(),
                ["group/project-b"] = diffB.ToJson()
            });

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _dispatcherMock.Verify(
            x => x.DispatchAsync(
                RunId,
                "group/project-a",
                It.IsAny<IReadOnlyList<CommitSummary>>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<RiskScenario>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _dispatcherMock.Verify(
            x => x.DispatchAsync(
                RunId,
                "group/project-b",
                It.IsAny<IReadOnlyList<CommitSummary>>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<RiskScenario>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_無CommitSummary的專案_應跳過不呼叫DispatchAsync()
    {
        // Arrange
        var emptyDiff = new ProjectDiffResult
        {
            ProjectPath = ProjectPath,
            CommitSummaries = new List<CommitSummary>()
        };

        _redisServiceMock
            .Setup(x => x.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey))
            .ReturnsAsync(RunId);
        _redisServiceMock
            .Setup(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage2Hash(RunId)))
            .ReturnsAsync(new Dictionary<string, string> { [ProjectPath] = emptyDiff.ToJson() });

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _dispatcherMock.Verify(
            x => x.DispatchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<CommitSummary>>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<RiskScenario>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_無Stage1記錄的專案_應跳過不呼叫DispatchAsync()
    {
        // Arrange
        var diffResult = BuildDiffResult(ProjectPath);

        _redisServiceMock
            .Setup(x => x.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey))
            .ReturnsAsync(RunId);
        // Stage1 為空（無任何專案記錄）
        _redisServiceMock
            .Setup(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage1Hash(RunId)))
            .ReturnsAsync(new Dictionary<string, string>());
        _redisServiceMock
            .Setup(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage2Hash(RunId)))
            .ReturnsAsync(new Dictionary<string, string> { [ProjectPath] = diffResult.ToJson() });

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _dispatcherMock.Verify(
            x => x.DispatchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<CommitSummary>>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<RiskScenario>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_應將設定的Scenarios傳入DispatchAsync()
    {
        // Arrange
        var customOptions = new RiskAnalysisOptions
        {
            Scenarios = new List<string> { "ApiContractBreak", "DatabaseSchemaChange" }
        };
        var diffResult = BuildDiffResult(ProjectPath);

        _redisServiceMock
            .Setup(x => x.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey))
            .ReturnsAsync(RunId);
        _redisServiceMock
            .Setup(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage1Hash(RunId)))
            .ReturnsAsync(new Dictionary<string, string> { [ProjectPath] = BuildStage1Json() });
        _redisServiceMock
            .Setup(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage2Hash(RunId)))
            .ReturnsAsync(new Dictionary<string, string> { [ProjectPath] = diffResult.ToJson() });

        var task = CreateTask(customOptions);

        // Act
        await task.ExecuteAsync();

        // Assert
        _dispatcherMock.Verify(
            x => x.DispatchAsync(
                RunId,
                ProjectPath,
                It.IsAny<IReadOnlyList<CommitSummary>>(),
                It.IsAny<string>(),
                It.Is<IReadOnlyList<RiskScenario>>(s =>
                    s.Count == 2 &&
                    s.Contains(RiskScenario.ApiContractBreak) &&
                    s.Contains(RiskScenario.DatabaseSchemaChange)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
