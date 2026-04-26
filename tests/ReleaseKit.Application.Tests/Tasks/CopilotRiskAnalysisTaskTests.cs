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
    private readonly Mock<ICopilotRiskAnalyzer> _analyzerMock;
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly Mock<ILogger<CopilotRiskAnalysisTask>> _loggerMock;

    private const string RunId = "20240315103045";
    private const string ProjectPath = "group/project-a";

    public CopilotRiskAnalysisTaskTests()
    {
        _analyzerMock = new Mock<ICopilotRiskAnalyzer>();
        _redisServiceMock = new Mock<IRedisService>();
        _loggerMock = new Mock<ILogger<CopilotRiskAnalysisTask>>();

        _redisServiceMock
            .Setup(x => x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        _analyzerMock
            .Setup(x => x.AnalyzeAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<FileDiff>>(),
                It.IsAny<ProjectStructure?>(),
                It.IsAny<IReadOnlyList<RiskScenario>>(),
                It.IsAny<string>()))
            .ReturnsAsync((new List<RiskFinding>(), 1));
    }

    private CopilotRiskAnalysisTask CreateTask(RiskAnalysisOptions? options = null)
    {
        var effectiveOptions = options ?? new RiskAnalysisOptions();
        return new CopilotRiskAnalysisTask(
            _analyzerMock.Object,
            _redisServiceMock.Object,
            Options.Create(effectiveOptions),
            _loggerMock.Object);
    }

    private static ProjectDiffResult BuildDiffResult(string projectPath, int diffCount = 1) =>
        new()
        {
            ProjectPath = projectPath,
            FileDiffs = Enumerable.Range(0, diffCount)
                .Select(i => new FileDiff
                {
                    FilePath = $"src/File{i}.cs",
                    DiffContent = $"+added line {i}",
                    ChangeType = ChangeType.Modified,
                    CommitSha = $"abc{i:D3}"
                })
                .ToList()
        };

    private static ProjectStructure BuildProjectStructure(string projectPath) =>
        new()
        {
            ProjectPath = projectPath,
            ApiEndpoints = new List<ApiEndpoint>(),
            NuGetPackages = new List<string>(),
            DbContextFiles = new List<string>(),
            MigrationFiles = new List<string>(),
            MessageContracts = new List<string>(),
            ConfigKeys = new List<string>(),
            InferredDependencies = new List<ServiceDependency>()
        };

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
        _analyzerMock.Verify(
            x => x.AnalyzeAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<FileDiff>>(),
                It.IsAny<ProjectStructure?>(),
                It.IsAny<IReadOnlyList<RiskScenario>>(),
                It.IsAny<string>()),
            Times.Never);
        _redisServiceMock.Verify(
            x => x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_應讀取Stage2與Stage3的Redis資料()
    {
        // Arrange
        var diffResult = BuildDiffResult(ProjectPath);
        var structure = BuildProjectStructure(ProjectPath);

        _redisServiceMock
            .Setup(x => x.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey))
            .ReturnsAsync(RunId);
        _redisServiceMock
            .Setup(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage2Hash(RunId)))
            .ReturnsAsync(new Dictionary<string, string> { [ProjectPath] = diffResult.ToJson() });
        _redisServiceMock
            .Setup(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage3Hash(RunId)))
            .ReturnsAsync(new Dictionary<string, string> { [ProjectPath] = structure.ToJson() });

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _redisServiceMock.Verify(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage2Hash(RunId)), Times.Once);
        _redisServiceMock.Verify(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage3Hash(RunId)), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_應對每個有Diff的專案呼叫AnalyzeAsync()
    {
        // Arrange
        var diffA = BuildDiffResult("group/project-a");
        var diffB = BuildDiffResult("group/project-b");

        _redisServiceMock
            .Setup(x => x.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey))
            .ReturnsAsync(RunId);
        _redisServiceMock
            .Setup(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage2Hash(RunId)))
            .ReturnsAsync(new Dictionary<string, string>
            {
                ["group/project-a"] = diffA.ToJson(),
                ["group/project-b"] = diffB.ToJson()
            });
        _redisServiceMock
            .Setup(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage3Hash(RunId)))
            .ReturnsAsync(new Dictionary<string, string>());

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _analyzerMock.Verify(
            x => x.AnalyzeAsync(
                "group/project-a",
                It.IsAny<IReadOnlyList<FileDiff>>(),
                It.IsAny<ProjectStructure?>(),
                It.IsAny<IReadOnlyList<RiskScenario>>(),
                It.IsAny<string>()),
            Times.Once);
        _analyzerMock.Verify(
            x => x.AnalyzeAsync(
                "group/project-b",
                It.IsAny<IReadOnlyList<FileDiff>>(),
                It.IsAny<ProjectStructure?>(),
                It.IsAny<IReadOnlyList<RiskScenario>>(),
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_應將ProjectRiskAnalysis儲存至Stage4Hash()
    {
        // Arrange
        var diffResult = BuildDiffResult(ProjectPath);
        var findings = new List<RiskFinding>
        {
            new()
            {
                Scenario = RiskScenario.ApiContractBreak,
                RiskLevel = RiskLevel.High,
                Description = "API 破壞性變更",
                AffectedFile = "src/Controller.cs",
                DiffSnippet = "-old method",
                PotentiallyAffectedProjects = new List<string>(),
                RecommendedAction = "Review API changes",
                ChangedBy = string.Empty
            }
        };

        _redisServiceMock
            .Setup(x => x.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey))
            .ReturnsAsync(RunId);
        _redisServiceMock
            .Setup(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage2Hash(RunId)))
            .ReturnsAsync(new Dictionary<string, string> { [ProjectPath] = diffResult.ToJson() });
        _redisServiceMock
            .Setup(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage3Hash(RunId)))
            .ReturnsAsync(new Dictionary<string, string>());
        _analyzerMock
            .Setup(x => x.AnalyzeAsync(
                ProjectPath,
                It.IsAny<IReadOnlyList<FileDiff>>(),
                null,
                It.IsAny<IReadOnlyList<RiskScenario>>(),
                It.IsAny<string>()))
            .ReturnsAsync((findings, 1));

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _redisServiceMock.Verify(
            x => x.HashSetAsync(
                RiskAnalysisRedisKeys.Stage4Hash(RunId),
                ProjectPath,
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_無Diff的專案_應跳過不呼叫AnalyzeAsync()
    {
        // Arrange
        var emptyDiff = new ProjectDiffResult { ProjectPath = ProjectPath, FileDiffs = new List<FileDiff>() };

        _redisServiceMock
            .Setup(x => x.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey))
            .ReturnsAsync(RunId);
        _redisServiceMock
            .Setup(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage2Hash(RunId)))
            .ReturnsAsync(new Dictionary<string, string> { [ProjectPath] = emptyDiff.ToJson() });
        _redisServiceMock
            .Setup(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage3Hash(RunId)))
            .ReturnsAsync(new Dictionary<string, string>());

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _analyzerMock.Verify(
            x => x.AnalyzeAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<FileDiff>>(),
                It.IsAny<ProjectStructure?>(),
                It.IsAny<IReadOnlyList<RiskScenario>>(),
                It.IsAny<string>()),
            Times.Never);
        _redisServiceMock.Verify(
            x => x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_應將設定的Scenarios傳入AnalyzeAsync()
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
            .Setup(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage2Hash(RunId)))
            .ReturnsAsync(new Dictionary<string, string> { [ProjectPath] = diffResult.ToJson() });
        _redisServiceMock
            .Setup(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage3Hash(RunId)))
            .ReturnsAsync(new Dictionary<string, string>());

        var task = CreateTask(customOptions);

        // Act
        await task.ExecuteAsync();

        // Assert
        _analyzerMock.Verify(
            x => x.AnalyzeAsync(
                ProjectPath,
                It.IsAny<IReadOnlyList<FileDiff>>(),
                It.IsAny<ProjectStructure?>(),
                It.Is<IReadOnlyList<RiskScenario>>(s =>
                    s.Count == 2 &&
                    s.Contains(RiskScenario.ApiContractBreak) &&
                    s.Contains(RiskScenario.DatabaseSchemaChange)),
                It.IsAny<string>()),
            Times.Once);
    }
}
