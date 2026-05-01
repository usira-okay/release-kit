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
/// CopilotScenarioAnalysisTask 單元測試
/// </summary>
public class CopilotScenarioAnalysisTaskTests
{
    private readonly Mock<ICopilotScenarioDispatcher> _dispatcherMock;
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly Mock<ILogger<CopilotScenarioAnalysisTask>> _loggerMock;

    private const string RunId = "20240315103045";
    private const string ProjectPath = "group/project-a";
    private const string LocalPath = "/repos/group/project-a";

    public CopilotScenarioAnalysisTaskTests()
    {
        _dispatcherMock = new Mock<ICopilotScenarioDispatcher>();
        _redisServiceMock = new Mock<IRedisService>();
        _loggerMock = new Mock<ILogger<CopilotScenarioAnalysisTask>>();

        _redisServiceMock
            .Setup(x => x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
    }

    private CopilotScenarioAnalysisTask CreateTask(RiskAnalysisOptions? options = null)
    {
        var effectiveOptions = options ?? new RiskAnalysisOptions();
        return new CopilotScenarioAnalysisTask(
            _dispatcherMock.Object,
            _redisServiceMock.Object,
            Options.Create(effectiveOptions),
            _loggerMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_無RunId_應跳過不執行()
    {
        _redisServiceMock.Setup(x => x.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey))
            .ReturnsAsync((string?)null);

        var task = CreateTask();
        await task.ExecuteAsync();

        _dispatcherMock.Verify(
            x => x.DispatchAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyList<CommitSummary>>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyList<RiskScenario>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_有Stage2資料_應呼叫Dispatcher()
    {
        _redisServiceMock.Setup(x => x.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey))
            .ReturnsAsync(RunId);

        var diffResult = new ProjectDiffResult
        {
            ProjectPath = ProjectPath,
            CommitSummaries = new List<CommitSummary>
            {
                new()
                {
                    CommitSha = "abc123",
                    ChangedFiles = new List<FileDiff>(),
                    TotalFilesChanged = 3,
                    TotalLinesAdded = 50,
                    TotalLinesRemoved = 10
                }
            }
        };

        _redisServiceMock.Setup(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage2Hash(RunId)))
            .ReturnsAsync(new Dictionary<string, string> { [ProjectPath] = diffResult.ToJson() });

        var cloneResult = new { LocalPath = LocalPath, Status = "Success" };
        _redisServiceMock.Setup(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage1Hash(RunId)))
            .ReturnsAsync(new Dictionary<string, string> { [ProjectPath] = cloneResult.ToJson() });

        _dispatcherMock
            .Setup(x => x.DispatchAsync(RunId, ProjectPath,
                It.IsAny<IReadOnlyList<CommitSummary>>(), LocalPath,
                It.IsAny<IReadOnlyList<RiskScenario>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProjectRiskAnalysis
            {
                ProjectPath = ProjectPath,
                Findings = [],
                SessionCount = 7
            });

        var task = CreateTask();
        await task.ExecuteAsync();

        _dispatcherMock.Verify(
            x => x.DispatchAsync(RunId, ProjectPath,
                It.IsAny<IReadOnlyList<CommitSummary>>(), LocalPath,
                It.IsAny<IReadOnlyList<RiskScenario>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Stage2無CommitSummary_應跳過()
    {
        _redisServiceMock.Setup(x => x.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey))
            .ReturnsAsync(RunId);

        var emptyDiffResult = new ProjectDiffResult
        {
            ProjectPath = ProjectPath,
            CommitSummaries = new List<CommitSummary>()
        };

        _redisServiceMock.Setup(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage2Hash(RunId)))
            .ReturnsAsync(new Dictionary<string, string> { [ProjectPath] = emptyDiffResult.ToJson() });

        _redisServiceMock.Setup(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage1Hash(RunId)))
            .ReturnsAsync(new Dictionary<string, string>());

        var task = CreateTask();
        await task.ExecuteAsync();

        _dispatcherMock.Verify(
            x => x.DispatchAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyList<CommitSummary>>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyList<RiskScenario>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
