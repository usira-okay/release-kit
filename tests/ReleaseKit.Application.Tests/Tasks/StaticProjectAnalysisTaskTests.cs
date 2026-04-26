using Microsoft.Extensions.Logging;
using Moq;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Entities;

namespace ReleaseKit.Application.Tests.Tasks;

/// <summary>
/// StaticProjectAnalysisTask 單元測試
/// </summary>
public class StaticProjectAnalysisTaskTests
{
    private readonly Mock<IProjectStructureScanner> _scannerMock;
    private readonly Mock<IDependencyInferrer> _inferrerMock;
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly Mock<ILogger<StaticProjectAnalysisTask>> _loggerMock;

    private const string RunId = "20240315103045";

    public StaticProjectAnalysisTaskTests()
    {
        _scannerMock = new Mock<IProjectStructureScanner>();
        _inferrerMock = new Mock<IDependencyInferrer>();
        _redisServiceMock = new Mock<IRedisService>();
        _loggerMock = new Mock<ILogger<StaticProjectAnalysisTask>>();

        _redisServiceMock
            .Setup(x => x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        _inferrerMock
            .Setup(x => x.InferDependencies(It.IsAny<IReadOnlyList<ProjectStructure>>()))
            .Returns<IReadOnlyList<ProjectStructure>>(structures => structures);
    }

    private StaticProjectAnalysisTask CreateTask() =>
        new StaticProjectAnalysisTask(
            _scannerMock.Object,
            _inferrerMock.Object,
            _redisServiceMock.Object,
            _loggerMock.Object);

    private static ProjectStructure CreateProjectStructure(string projectPath) =>
        new ProjectStructure
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
        _scannerMock.Verify(x => x.Scan(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _inferrerMock.Verify(x => x.InferDependencies(It.IsAny<IReadOnlyList<ProjectStructure>>()), Times.Never);
        _redisServiceMock.Verify(x => x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_應掃描每個成功Clone的專案()
    {
        // Arrange
        var stage1Data = new Dictionary<string, string>
        {
            ["group/project-a"] = new { LocalPath = "/repos/group/project-a", Status = "Success" }.ToJson(),
            ["group/project-b"] = new { LocalPath = "/repos/group/project-b", Status = "Success" }.ToJson()
        };

        _redisServiceMock
            .Setup(x => x.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey))
            .ReturnsAsync(RunId);
        _redisServiceMock
            .Setup(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage1Hash(RunId)))
            .ReturnsAsync(stage1Data);

        _scannerMock
            .Setup(x => x.Scan(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((path, _) => CreateProjectStructure(path));

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _scannerMock.Verify(x => x.Scan("group/project-a", "/repos/group/project-a"), Times.Once);
        _scannerMock.Verify(x => x.Scan("group/project-b", "/repos/group/project-b"), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Clone失敗的專案_應跳過不掃描()
    {
        // Arrange
        var stage1Data = new Dictionary<string, string>
        {
            ["group/project-ok"] = new { LocalPath = "/repos/group/project-ok", Status = "Success" }.ToJson(),
            ["group/project-fail"] = new { LocalPath = "/repos/group/project-fail", Status = "Failed: connection refused" }.ToJson()
        };

        _redisServiceMock
            .Setup(x => x.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey))
            .ReturnsAsync(RunId);
        _redisServiceMock
            .Setup(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage1Hash(RunId)))
            .ReturnsAsync(stage1Data);

        _scannerMock
            .Setup(x => x.Scan(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((path, _) => CreateProjectStructure(path));

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _scannerMock.Verify(x => x.Scan("group/project-ok", It.IsAny<string>()), Times.Once);
        _scannerMock.Verify(x => x.Scan("group/project-fail", It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_應以所有掃描結果呼叫InferDependencies()
    {
        // Arrange
        var stage1Data = new Dictionary<string, string>
        {
            ["group/project-a"] = new { LocalPath = "/repos/group/project-a", Status = "Success" }.ToJson(),
            ["group/project-b"] = new { LocalPath = "/repos/group/project-b", Status = "Success" }.ToJson()
        };

        _redisServiceMock
            .Setup(x => x.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey))
            .ReturnsAsync(RunId);
        _redisServiceMock
            .Setup(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage1Hash(RunId)))
            .ReturnsAsync(stage1Data);

        _scannerMock
            .Setup(x => x.Scan(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((path, _) => CreateProjectStructure(path));

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _inferrerMock.Verify(x =>
            x.InferDependencies(It.Is<IReadOnlyList<ProjectStructure>>(list => list.Count == 2)),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_應將豐富化結果儲存至Stage3_Redis_Hash()
    {
        // Arrange
        var structureA = CreateProjectStructure("group/project-a");
        var structureB = CreateProjectStructure("group/project-b");

        var stage1Data = new Dictionary<string, string>
        {
            ["group/project-a"] = new { LocalPath = "/repos/group/project-a", Status = "Success" }.ToJson(),
            ["group/project-b"] = new { LocalPath = "/repos/group/project-b", Status = "Success" }.ToJson()
        };

        _redisServiceMock
            .Setup(x => x.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey))
            .ReturnsAsync(RunId);
        _redisServiceMock
            .Setup(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage1Hash(RunId)))
            .ReturnsAsync(stage1Data);

        _scannerMock
            .Setup(x => x.Scan("group/project-a", It.IsAny<string>()))
            .Returns(structureA);
        _scannerMock
            .Setup(x => x.Scan("group/project-b", It.IsAny<string>()))
            .Returns(structureB);

        _inferrerMock
            .Setup(x => x.InferDependencies(It.IsAny<IReadOnlyList<ProjectStructure>>()))
            .Returns(new List<ProjectStructure> { structureA, structureB });

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _redisServiceMock.Verify(x =>
            x.HashSetAsync(
                RiskAnalysisRedisKeys.Stage3Hash(RunId),
                "group/project-a",
                It.IsAny<string>()),
            Times.Once);
        _redisServiceMock.Verify(x =>
            x.HashSetAsync(
                RiskAnalysisRedisKeys.Stage3Hash(RunId),
                "group/project-b",
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_無專案時_應呼叫InferDependencies並儲存零筆結果()
    {
        // Arrange
        _redisServiceMock
            .Setup(x => x.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey))
            .ReturnsAsync(RunId);
        _redisServiceMock
            .Setup(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage1Hash(RunId)))
            .ReturnsAsync(new Dictionary<string, string>());

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _inferrerMock.Verify(x =>
            x.InferDependencies(It.Is<IReadOnlyList<ProjectStructure>>(list => list.Count == 0)),
            Times.Once);
        _redisServiceMock.Verify(x =>
            x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }
}
