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
/// GenerateRiskReportTask 單元測試
/// </summary>
public class GenerateRiskReportTaskTests : IDisposable
{
    private readonly Mock<IMarkdownReportGenerator> _reportGeneratorMock;
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly Mock<INow> _nowMock;
    private readonly Mock<ILogger<GenerateRiskReportTask>> _loggerMock;
    private readonly string _testOutputPath;

    private const string RunId = "20240315103045";
    private static readonly DateTimeOffset FakeNow = new(2024, 3, 15, 10, 30, 45, TimeSpan.Zero);

    public GenerateRiskReportTaskTests()
    {
        _reportGeneratorMock = new Mock<IMarkdownReportGenerator>();
        _redisServiceMock = new Mock<IRedisService>();
        _nowMock = new Mock<INow>();
        _loggerMock = new Mock<ILogger<GenerateRiskReportTask>>();

        _testOutputPath = Path.Combine(AppContext.BaseDirectory, "test-reports", Guid.NewGuid().ToString());

        _nowMock.Setup(x => x.UtcNow).Returns(FakeNow);

        _reportGeneratorMock
            .Setup(x => x.Generate(It.IsAny<RiskReport>()))
            .Returns("# 風險報告\n測試內容");

        _redisServiceMock
            .Setup(x => x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        _redisServiceMock
            .Setup(x => x.HashGetAllAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, string>());

        _redisServiceMock
            .Setup(x => x.HashGetAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string?)null);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testOutputPath))
            Directory.Delete(_testOutputPath, recursive: true);
    }

    private GenerateRiskReportTask CreateTask(string? outputPath = null) =>
        new(
            _reportGeneratorMock.Object,
            _redisServiceMock.Object,
            _nowMock.Object,
            Options.Create(new RiskAnalysisOptions { ReportOutputPath = outputPath ?? _testOutputPath }),
            _loggerMock.Object);

    private static ProjectRiskAnalysis BuildAnalysis(string projectPath) =>
        new()
        {
            ProjectPath = projectPath,
            Findings = new List<RiskFinding>(),
            SessionCount = 1
        };

    private static CrossProjectCorrelation BuildCorrelation() =>
        new()
        {
            DependencyEdges = new List<DependencyEdge>(),
            CorrelatedFindings = new List<CorrelatedRiskFinding>(),
            NotificationTargets = new List<NotificationTarget>()
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
        _reportGeneratorMock.Verify(x => x.Generate(It.IsAny<RiskReport>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_應從Stage4與Stage5讀取資料()
    {
        // Arrange
        _redisServiceMock
            .Setup(x => x.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey))
            .ReturnsAsync(RunId);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _redisServiceMock.Verify(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage4Hash(RunId)), Times.Once);
        _redisServiceMock.Verify(
            x => x.HashGetAsync(RiskAnalysisRedisKeys.Stage5Hash(RunId), RiskAnalysisRedisKeys.CorrelationField),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_應使用正確RunId與時間戳組裝RiskReport()
    {
        // Arrange
        _redisServiceMock
            .Setup(x => x.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey))
            .ReturnsAsync(RunId);

        RiskReport? capturedReport = null;
        _reportGeneratorMock
            .Setup(x => x.Generate(It.IsAny<RiskReport>()))
            .Callback<RiskReport>(r => capturedReport = r)
            .Returns("# 報告");

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        Assert.NotNull(capturedReport);
        Assert.Equal(RunId, capturedReport.RunId);
        Assert.Equal(FakeNow, capturedReport.ExecutedAt);
    }

    [Fact]
    public async Task ExecuteAsync_應使用Stage4資料填入ProjectAnalyses()
    {
        // Arrange
        _redisServiceMock
            .Setup(x => x.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey))
            .ReturnsAsync(RunId);

        var analysis = BuildAnalysis("project-a");
        _redisServiceMock
            .Setup(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage4Hash(RunId)))
            .ReturnsAsync(new Dictionary<string, string>
            {
                ["project-a"] = analysis.ToJson()
            });

        RiskReport? capturedReport = null;
        _reportGeneratorMock
            .Setup(x => x.Generate(It.IsAny<RiskReport>()))
            .Callback<RiskReport>(r => capturedReport = r)
            .Returns("# 報告");

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        Assert.NotNull(capturedReport);
        Assert.Single(capturedReport.ProjectAnalyses);
        Assert.Equal("project-a", capturedReport.ProjectAnalyses[0].ProjectPath);
    }

    [Fact]
    public async Task ExecuteAsync_應呼叫Generate並傳入組裝後的RiskReport()
    {
        // Arrange
        _redisServiceMock
            .Setup(x => x.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey))
            .ReturnsAsync(RunId);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _reportGeneratorMock.Verify(x => x.Generate(It.IsAny<RiskReport>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_應將報告儲存至Stage6Hash()
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
                RiskAnalysisRedisKeys.Stage6Hash(RunId),
                RiskAnalysisRedisKeys.ReportField,
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_應將Stage6Redis儲存包含MarkdownContent()
    {
        // Arrange
        _redisServiceMock
            .Setup(x => x.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey))
            .ReturnsAsync(RunId);

        var expectedMarkdown = "# 風險報告\n完整內容";
        _reportGeneratorMock
            .Setup(x => x.Generate(It.IsAny<RiskReport>()))
            .Returns(expectedMarkdown);

        string? capturedJson = null;
        _redisServiceMock
            .Setup(x => x.HashSetAsync(
                RiskAnalysisRedisKeys.Stage6Hash(RunId),
                RiskAnalysisRedisKeys.ReportField,
                It.IsAny<string>()))
            .Callback<string, string, string>((_, _, json) => capturedJson = json)
            .ReturnsAsync(true);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        Assert.NotNull(capturedJson);
        Assert.Contains("markdownContent", capturedJson);
        Assert.Contains("風險報告", capturedJson);
    }

    [Fact]
    public async Task ExecuteAsync_應將Markdown寫入本機檔案()
    {
        // Arrange
        _redisServiceMock
            .Setup(x => x.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey))
            .ReturnsAsync(RunId);

        var expectedMarkdown = "# 風險報告\n測試內容";
        _reportGeneratorMock
            .Setup(x => x.Generate(It.IsAny<RiskReport>()))
            .Returns(expectedMarkdown);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        var expectedFilePath = Path.Combine(_testOutputPath, $"{RunId}-risk-report.md");
        Assert.True(File.Exists(expectedFilePath));
        var fileContent = await File.ReadAllTextAsync(expectedFilePath);
        Assert.Equal(expectedMarkdown, fileContent);
    }

    [Fact]
    public async Task ExecuteAsync_Stage4與Stage5皆無資料_應正常完成並儲存空報告()
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
                RiskAnalysisRedisKeys.Stage6Hash(RunId),
                RiskAnalysisRedisKeys.ReportField,
                It.IsAny<string>()),
            Times.Once);

        var reportFilePath = Path.Combine(_testOutputPath, $"{RunId}-risk-report.md");
        Assert.True(File.Exists(reportFilePath));
    }

    [Fact]
    public async Task ExecuteAsync_Stage5資料有效_應正確填入Correlation()
    {
        // Arrange
        _redisServiceMock
            .Setup(x => x.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey))
            .ReturnsAsync(RunId);

        var correlation = BuildCorrelation();
        _redisServiceMock
            .Setup(x => x.HashGetAsync(
                RiskAnalysisRedisKeys.Stage5Hash(RunId),
                RiskAnalysisRedisKeys.CorrelationField))
            .ReturnsAsync(correlation.ToJson());

        RiskReport? capturedReport = null;
        _reportGeneratorMock
            .Setup(x => x.Generate(It.IsAny<RiskReport>()))
            .Callback<RiskReport>(r => capturedReport = r)
            .Returns("# 報告");

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        Assert.NotNull(capturedReport);
        Assert.NotNull(capturedReport.Correlation);
        Assert.Empty(capturedReport.Correlation.DependencyEdges);
        Assert.Empty(capturedReport.Correlation.CorrelatedFindings);
    }
}
