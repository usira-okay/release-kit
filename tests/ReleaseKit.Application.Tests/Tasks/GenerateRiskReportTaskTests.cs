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
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly Mock<IRiskAnalyzer> _riskAnalyzerMock;
    private readonly Mock<INow> _nowMock;
    private readonly Mock<ILogger<GenerateRiskReportTask>> _loggerMock;
    private readonly string _reportOutputPath;
    private readonly DateTimeOffset _fixedNow = new(2025, 7, 15, 10, 0, 0, TimeSpan.Zero);

    public GenerateRiskReportTaskTests()
    {
        _redisServiceMock = new Mock<IRedisService>();
        _riskAnalyzerMock = new Mock<IRiskAnalyzer>();
        _nowMock = new Mock<INow>();
        _loggerMock = new Mock<ILogger<GenerateRiskReportTask>>();

        _nowMock.Setup(x => x.UtcNow).Returns(_fixedNow);

        _reportOutputPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            $"test-reports-{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_reportOutputPath))
            Directory.Delete(_reportOutputPath, recursive: true);
    }

    private GenerateRiskReportTask CreateTask()
    {
        var options = Options.Create(new RiskAnalysisOptions
        {
            CloneBasePath = "/clone",
            ReportOutputPath = _reportOutputPath
        });

        return new GenerateRiskReportTask(
            _redisServiceMock.Object,
            _riskAnalyzerMock.Object,
            options,
            _nowMock.Object,
            _loggerMock.Object);
    }

    /// <summary>
    /// 建立測試用 RiskAnalysisReport
    /// </summary>
    private static RiskAnalysisReport CreateReport(int sequence, string? projectName = null) => new()
    {
        Sequence = sequence,
        ProjectName = projectName ?? $"project-{sequence}",
        RiskItems = new List<RiskItem>
        {
            new()
            {
                Category = RiskCategory.ApiContract,
                Level = RiskLevel.High,
                ChangeSummary = "測試變更",
                AffectedFiles = new List<string> { "src/Foo.cs" },
                PotentiallyAffectedServices = new List<string> { "ServiceA" },
                ImpactDescription = "測試影響",
                SuggestedValidationSteps = new List<string> { "步驟一" }
            }
        },
        Summary = "測試摘要",
        AnalyzedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task ExecuteAsync_載入所有中間報告並傳入GenerateFinalReportAsync()
    {
        // Arrange
        var report1 = CreateReport(1, "project-a");
        var report2 = CreateReport(2, "project-b");
        var intermediateData = new Dictionary<string, string>
        {
            [$"{RedisKeys.Fields.IntermediatePrefix}1"] = report1.ToJson(),
            [$"{RedisKeys.Fields.IntermediatePrefix}2"] = report2.ToJson()
        };
        _redisServiceMock.Setup(x => x.HashGetByPrefixAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.IntermediatePrefix))
            .ReturnsAsync(intermediateData);

        IReadOnlyList<RiskAnalysisReport>? capturedReports = null;
        _riskAnalyzerMock.Setup(x => x.GenerateFinalReportAsync(
                It.IsAny<IReadOnlyList<RiskAnalysisReport>>(), It.IsAny<CancellationToken>()))
            .Returns((IReadOnlyList<RiskAnalysisReport> reports, CancellationToken _) =>
            {
                capturedReports = reports;
                return Task.FromResult("# 風險報告");
            });

        _redisServiceMock.Setup(x => x.HashSetAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.FinalReport, It.IsAny<string>()))
            .ReturnsAsync(true);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 確認載入全部中間報告
        Assert.NotNull(capturedReports);
        Assert.Equal(2, capturedReports.Count);

        _riskAnalyzerMock.Verify(x => x.GenerateFinalReportAsync(
            It.IsAny<IReadOnlyList<RiskAnalysisReport>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_報告存入Redis的FinalReport欄位()
    {
        // Arrange
        _redisServiceMock.Setup(x => x.HashGetByPrefixAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.IntermediatePrefix))
            .ReturnsAsync(new Dictionary<string, string>
            {
                [$"{RedisKeys.Fields.IntermediatePrefix}1"] = CreateReport(1).ToJson()
            });

        const string expectedMarkdown = "# 最終風險報告\n\n風險項目...";
        _riskAnalyzerMock.Setup(x => x.GenerateFinalReportAsync(
                It.IsAny<IReadOnlyList<RiskAnalysisReport>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedMarkdown);

        string? capturedMarkdown = null;
        _redisServiceMock.Setup(x => x.HashSetAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.FinalReport, It.IsAny<string>()))
            .Returns((string _, string _, string value) =>
            {
                capturedMarkdown = value;
                return Task.FromResult(true);
            });

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        Assert.Equal(expectedMarkdown, capturedMarkdown);
        _redisServiceMock.Verify(x => x.HashSetAsync(
            RedisKeys.RiskAnalysisHash, RedisKeys.Fields.FinalReport, expectedMarkdown), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_報告寫入檔案包含日期名稱()
    {
        // Arrange
        _redisServiceMock.Setup(x => x.HashGetByPrefixAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.IntermediatePrefix))
            .ReturnsAsync(new Dictionary<string, string>
            {
                [$"{RedisKeys.Fields.IntermediatePrefix}1"] = CreateReport(1).ToJson()
            });

        const string expectedMarkdown = "# 風險報告內容";
        _riskAnalyzerMock.Setup(x => x.GenerateFinalReportAsync(
                It.IsAny<IReadOnlyList<RiskAnalysisReport>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedMarkdown);

        _redisServiceMock.Setup(x => x.HashSetAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.FinalReport, It.IsAny<string>()))
            .ReturnsAsync(true);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 確認檔案已寫入且檔名包含日期
        var expectedPath = Path.Combine(_reportOutputPath, "risk-report-2025-07-15.md");
        Assert.True(File.Exists(expectedPath));
        var content = await File.ReadAllTextAsync(expectedPath);
        Assert.Equal(expectedMarkdown, content);
    }

    [Fact]
    public async Task ExecuteAsync_空中間報告仍呼叫GenerateFinalReportAsync()
    {
        // Arrange — 無任何中間報告
        _redisServiceMock.Setup(x => x.HashGetByPrefixAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.IntermediatePrefix))
            .ReturnsAsync(new Dictionary<string, string>());

        _riskAnalyzerMock.Setup(x => x.GenerateFinalReportAsync(
                It.IsAny<IReadOnlyList<RiskAnalysisReport>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("# 空報告");

        _redisServiceMock.Setup(x => x.HashSetAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.FinalReport, It.IsAny<string>()))
            .ReturnsAsync(true);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 以空清單呼叫 GenerateFinalReportAsync
        _riskAnalyzerMock.Verify(x => x.GenerateFinalReportAsync(
            It.Is<IReadOnlyList<RiskAnalysisReport>>(r => r.Count == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_報告依Sequence排序後傳入GenerateFinalReportAsync()
    {
        // Arrange — 故意以逆序存入 Redis
        var report3 = CreateReport(3, "project-c");
        var report1 = CreateReport(1, "project-a");
        var report2 = CreateReport(2, "project-b");
        var intermediateData = new Dictionary<string, string>
        {
            [$"{RedisKeys.Fields.IntermediatePrefix}3"] = report3.ToJson(),
            [$"{RedisKeys.Fields.IntermediatePrefix}1"] = report1.ToJson(),
            [$"{RedisKeys.Fields.IntermediatePrefix}2"] = report2.ToJson()
        };
        _redisServiceMock.Setup(x => x.HashGetByPrefixAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.IntermediatePrefix))
            .ReturnsAsync(intermediateData);

        IReadOnlyList<RiskAnalysisReport>? capturedReports = null;
        _riskAnalyzerMock.Setup(x => x.GenerateFinalReportAsync(
                It.IsAny<IReadOnlyList<RiskAnalysisReport>>(), It.IsAny<CancellationToken>()))
            .Returns((IReadOnlyList<RiskAnalysisReport> reports, CancellationToken _) =>
            {
                capturedReports = reports;
                return Task.FromResult("# 排序報告");
            });

        _redisServiceMock.Setup(x => x.HashSetAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.FinalReport, It.IsAny<string>()))
            .ReturnsAsync(true);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 確認依 Sequence 升序排列
        Assert.NotNull(capturedReports);
        Assert.Equal(3, capturedReports.Count);
        Assert.Equal(1, capturedReports[0].Sequence);
        Assert.Equal(2, capturedReports[1].Sequence);
        Assert.Equal(3, capturedReports[2].Sequence);
    }
}
