using Microsoft.Extensions.Logging;
using Moq;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Application.Tests.Tasks;

/// <summary>
/// CrossProjectCorrelationTask 單元測試
/// </summary>
public class CrossProjectCorrelationTaskTests
{
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly Mock<ILogger<CrossProjectCorrelationTask>> _loggerMock;

    private const string RunId = "20240315103045";

    public CrossProjectCorrelationTaskTests()
    {
        _redisServiceMock = new Mock<IRedisService>();
        _loggerMock = new Mock<ILogger<CrossProjectCorrelationTask>>();

        _redisServiceMock
            .Setup(x => x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        _redisServiceMock
            .Setup(x => x.HashGetAllAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, string>());
    }

    private CrossProjectCorrelationTask CreateTask() =>
        new(_redisServiceMock.Object, _loggerMock.Object);

    private static ProjectRiskAnalysis BuildAnalysis(string projectPath, IReadOnlyList<RiskFinding>? findings = null) =>
        new()
        {
            ProjectPath = projectPath,
            Findings = findings ?? new List<RiskFinding>(),
            SessionCount = 1
        };

    private static RiskFinding BuildFinding(
        RiskLevel level = RiskLevel.Medium,
        IReadOnlyList<string>? potentiallyAffected = null,
        string changedBy = "dev@example.com",
        string description = "風險描述") =>
        new()
        {
            Scenario = RiskScenario.ApiContractBreak,
            RiskLevel = level,
            Description = description,
            AffectedFile = "src/Controller.cs",
            DiffSnippet = "-old method",
            PotentiallyAffectedProjects = potentiallyAffected ?? new List<string>(),
            RecommendedAction = "請審查 API 變更",
            ChangedBy = changedBy
        };

    [Fact]
    public async Task ExecuteAsync_RunId不存在時_應提前結束不執行儲存()
    {
        // Arrange
        _redisServiceMock
            .Setup(x => x.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey))
            .ReturnsAsync((string?)null);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _redisServiceMock.Verify(
            x => x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_應只從Stage4讀取資料()
    {
        // Arrange
        _redisServiceMock
            .Setup(x => x.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey))
            .ReturnsAsync(RunId);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 只讀取 Stage4，不讀取 Stage3
        _redisServiceMock.Verify(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage4Hash(RunId)), Times.Once);
        _redisServiceMock.Verify(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage3Hash(RunId)), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_應將CrossProjectCorrelation儲存至Stage5Hash()
    {
        // Arrange
        _redisServiceMock
            .Setup(x => x.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey))
            .ReturnsAsync(RunId);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _redisServiceMock.Verify(
            x => x.HashSetAsync(
                RiskAnalysisRedisKeys.Stage5Hash(RunId),
                RiskAnalysisRedisKeys.CorrelationField,
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public void BuildDependencyEdges_有PotentiallyAffectedProjects_應建立相依邊()
    {
        // Arrange
        var finding = BuildFinding(potentiallyAffected: new[] { "project-b" });
        var analysis = BuildAnalysis("project-a", new[] { finding });

        // Act
        var edges = CrossProjectCorrelationTask.BuildDependencyEdges(new[] { analysis });

        // Assert
        Assert.Single(edges);
        Assert.Equal("project-a", edges[0].SourceProject);
        Assert.Equal("project-b", edges[0].TargetProject);
    }

    [Fact]
    public void BuildDependencyEdges_無PotentiallyAffectedProjects_應回傳空清單()
    {
        // Arrange
        var finding = BuildFinding(potentiallyAffected: new List<string>());
        var analysis = BuildAnalysis("project-a", new[] { finding });

        // Act
        var edges = CrossProjectCorrelationTask.BuildDependencyEdges(new[] { analysis });

        // Assert
        Assert.Empty(edges);
    }

    [Fact]
    public void BuildDependencyEdges_相同目標重複出現_不應重複建立相同邊()
    {
        // Arrange — 兩個 finding 都指向 project-b，且情境相同（ApiContractBreak → HttpCall）
        var finding1 = BuildFinding(potentiallyAffected: new[] { "project-b" });
        var finding2 = BuildFinding(potentiallyAffected: new[] { "project-b" });
        var analysis = BuildAnalysis("project-a", new[] { finding1, finding2 });

        // Act
        var edges = CrossProjectCorrelationTask.BuildDependencyEdges(new[] { analysis });

        // Assert — 相同的 source/target/type 只建立一條邊
        Assert.Single(edges);
    }

    [Fact]
    public void CorrelateFindings_PotentiallyAffectedProjects出現在相依圖中_應確認受影響專案()
    {
        // Arrange
        var finding = BuildFinding(
            level: RiskLevel.Low,
            potentiallyAffected: new[] { "project-b" });

        var analysis = BuildAnalysis("project-a", new[] { finding });
        var edges = new List<DependencyEdge>
        {
            new() { SourceProject = "project-a", TargetProject = "project-b", DependencyType = DependencyType.SharedDb, Target = "orders-db" }
        };

        // Act
        var result = CrossProjectCorrelationTask.CorrelateFindings(new[] { analysis }, edges);

        // Assert — PotentiallyAffectedProjects 直接成為 ConfirmedAffectedProjects
        Assert.Single(result);
        Assert.Contains("project-b", result[0].ConfirmedAffectedProjects);
    }

    [Fact]
    public void CorrelateFindings_PotentiallyAffectedProjects不在相依圖中_仍應確認受影響專案()
    {
        // Arrange — AI 推斷的受影響專案無論是否在靜態相依圖中，皆直接作為確認結果
        var finding = BuildFinding(
            level: RiskLevel.High,
            potentiallyAffected: new[] { "project-c" });

        var analysis = BuildAnalysis("project-a", new[] { finding });
        var edges = new List<DependencyEdge>
        {
            new() { SourceProject = "project-a", TargetProject = "project-b", DependencyType = DependencyType.SharedDb, Target = "orders-db" }
        };

        // Act
        var result = CrossProjectCorrelationTask.CorrelateFindings(new[] { analysis }, edges);

        // Assert — project-c 雖不在靜態相依圖中，仍應被確認
        Assert.Single(result);
        Assert.Contains("project-c", result[0].ConfirmedAffectedProjects);
    }

    [Fact]
    public void CorrelateFindings_Medium等級且有確認受影響專案_應提升為High()
    {
        // Arrange
        var finding = BuildFinding(
            level: RiskLevel.Medium,
            potentiallyAffected: new[] { "project-b" });

        var analysis = BuildAnalysis("project-a", new[] { finding });
        var edges = new List<DependencyEdge>
        {
            new() { SourceProject = "project-a", TargetProject = "project-b", DependencyType = DependencyType.SharedMQ, Target = "events-topic" }
        };

        // Act
        var result = CrossProjectCorrelationTask.CorrelateFindings(new[] { analysis }, edges);

        // Assert
        Assert.Single(result);
        Assert.Equal(RiskLevel.High, result[0].FinalRiskLevel);
    }

    [Fact]
    public void CorrelateFindings_High等級且有確認受影響專案_風險等級應維持High()
    {
        // Arrange
        var finding = BuildFinding(
            level: RiskLevel.High,
            potentiallyAffected: new[] { "project-b" });

        var analysis = BuildAnalysis("project-a", new[] { finding });
        var edges = new List<DependencyEdge>
        {
            new() { SourceProject = "project-a", TargetProject = "project-b", DependencyType = DependencyType.HttpCall, Target = "/api/orders" }
        };

        // Act
        var result = CrossProjectCorrelationTask.CorrelateFindings(new[] { analysis }, edges);

        // Assert
        Assert.Single(result);
        Assert.Equal(RiskLevel.High, result[0].FinalRiskLevel);
    }

    [Fact]
    public void CorrelateFindings_Medium等級且無PotentiallyAffectedProjects_應維持Medium()
    {
        // Arrange — PotentiallyAffectedProjects 為空時不升級風險等級
        var finding = BuildFinding(
            level: RiskLevel.Medium,
            potentiallyAffected: new List<string>());

        var analysis = BuildAnalysis("project-a", new[] { finding });

        // Act
        var result = CrossProjectCorrelationTask.CorrelateFindings(new[] { analysis }, new List<DependencyEdge>());

        // Assert
        Assert.Single(result);
        Assert.Equal(RiskLevel.Medium, result[0].FinalRiskLevel);
        Assert.Empty(result[0].ConfirmedAffectedProjects);
    }

    [Fact]
    public void BuildNotificationTargets_有確認受影響專案_應建立通知對象()
    {
        // Arrange
        var finding = BuildFinding(
            changedBy: "alice@example.com",
            description: "API 破壞性變更",
            potentiallyAffected: new[] { "project-b" });

        var correlated = new CorrelatedRiskFinding
        {
            OriginalFinding = finding,
            ConfirmedAffectedProjects = new[] { "project-b" },
            FinalRiskLevel = RiskLevel.High
        };

        // Act
        var targets = CrossProjectCorrelationTask.BuildNotificationTargets(new[] { correlated });

        // Assert
        Assert.Single(targets);
        Assert.Equal("alice@example.com", targets[0].PersonName);
        Assert.Equal("API 破壞性變更", targets[0].RiskDescription);
        Assert.Equal("project-b", targets[0].RelatedProject);
    }

    [Fact]
    public void BuildNotificationTargets_無確認受影響專案_應回傳空清單()
    {
        // Arrange
        var finding = BuildFinding();
        var correlated = new CorrelatedRiskFinding
        {
            OriginalFinding = finding,
            ConfirmedAffectedProjects = new List<string>(),
            FinalRiskLevel = RiskLevel.Medium
        };

        // Act
        var targets = CrossProjectCorrelationTask.BuildNotificationTargets(new[] { correlated });

        // Assert
        Assert.Empty(targets);
    }

    [Fact]
    public async Task ExecuteAsync_無Stage4資料_應正常完成並儲存空結果()
    {
        // Arrange
        _redisServiceMock
            .Setup(x => x.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey))
            .ReturnsAsync(RunId);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert - should store an empty correlation without throwing
        _redisServiceMock.Verify(
            x => x.HashSetAsync(
                RiskAnalysisRedisKeys.Stage5Hash(RunId),
                RiskAnalysisRedisKeys.CorrelationField,
                It.Is<string>(json =>
                    json.Contains("dependencyEdges") &&
                    json.Contains("correlatedFindings") &&
                    json.Contains("notificationTargets"))),
            Times.Once);
    }
}
