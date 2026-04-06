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
            Path.GetTempPath(),
            $"release-kit-test-{Guid.NewGuid():N}");
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
    private static RiskAnalysisReport CreateReport(int pass, int sequence, string? projectName = null)
    {
        return new RiskAnalysisReport
        {
            PassKey = new AnalysisPassKey { Pass = pass, Sequence = sequence },
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
    }

    [Fact]
    public async Task ExecuteAsync_載入最後一層報告並產生報告()
    {
        // Arrange — PassMetadata:2 存在，表示最後一層為 Pass 2
        var fields = new List<string>
        {
            "Intermediate:1-1", "Intermediate:1-2",
            "PassMetadata:2",
            "Intermediate:2-1", "Intermediate:2-2"
        };
        _redisServiceMock.Setup(x => x.HashFieldsAsync(RedisKeys.RiskAnalysisHash))
            .ReturnsAsync(fields);

        var report1 = CreateReport(2, 1);
        var report2 = CreateReport(2, 2);
        var intermediateData = new Dictionary<string, string>
        {
            ["Intermediate:2-1"] = report1.ToJson(),
            ["Intermediate:2-2"] = report2.ToJson()
        };
        _redisServiceMock.Setup(x => x.HashGetByPrefixAsync(
                RedisKeys.RiskAnalysisHash, "Intermediate:2-"))
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

        // Assert — 確認載入 Pass 2 的報告
        Assert.NotNull(capturedReports);
        Assert.Equal(2, capturedReports.Count);

        _riskAnalyzerMock.Verify(x => x.GenerateFinalReportAsync(
            It.IsAny<IReadOnlyList<RiskAnalysisReport>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_僅有Pass1時正確處理()
    {
        // Arrange — 無 PassMetadata 欄位，預設使用 Pass 1
        var fields = new List<string>
        {
            "Intermediate:1-1", "Intermediate:1-2",
            RedisKeys.Fields.ClonePaths
        };
        _redisServiceMock.Setup(x => x.HashFieldsAsync(RedisKeys.RiskAnalysisHash))
            .ReturnsAsync(fields);

        var report1 = CreateReport(1, 1, "project-a");
        var intermediateData = new Dictionary<string, string>
        {
            ["Intermediate:1-1"] = report1.ToJson(),
            ["Intermediate:1-2"] = CreateReport(1, 2, "project-b").ToJson()
        };
        _redisServiceMock.Setup(x => x.HashGetByPrefixAsync(
                RedisKeys.RiskAnalysisHash, "Intermediate:1-"))
            .ReturnsAsync(intermediateData);

        _riskAnalyzerMock.Setup(x => x.GenerateFinalReportAsync(
                It.IsAny<IReadOnlyList<RiskAnalysisReport>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("# Pass 1 報告");

        _redisServiceMock.Setup(x => x.HashSetAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.FinalReport, It.IsAny<string>()))
            .ReturnsAsync(true);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 確認使用 Intermediate:1- 前綴查詢
        _redisServiceMock.Verify(x => x.HashGetByPrefixAsync(
            RedisKeys.RiskAnalysisHash, "Intermediate:1-"), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_報告存入Redis()
    {
        // Arrange
        var fields = new List<string> { "Intermediate:1-1" };
        _redisServiceMock.Setup(x => x.HashFieldsAsync(RedisKeys.RiskAnalysisHash))
            .ReturnsAsync(fields);

        _redisServiceMock.Setup(x => x.HashGetByPrefixAsync(
                RedisKeys.RiskAnalysisHash, "Intermediate:1-"))
            .ReturnsAsync(new Dictionary<string, string>
            {
                ["Intermediate:1-1"] = CreateReport(1, 1).ToJson()
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
    public async Task ExecuteAsync_報告寫入檔案()
    {
        // Arrange
        var fields = new List<string> { "Intermediate:1-1" };
        _redisServiceMock.Setup(x => x.HashFieldsAsync(RedisKeys.RiskAnalysisHash))
            .ReturnsAsync(fields);

        _redisServiceMock.Setup(x => x.HashGetByPrefixAsync(
                RedisKeys.RiskAnalysisHash, "Intermediate:1-"))
            .ReturnsAsync(new Dictionary<string, string>
            {
                ["Intermediate:1-1"] = CreateReport(1, 1).ToJson()
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

        // Assert — 確認檔案已寫入
        var expectedPath = Path.Combine(_reportOutputPath, "risk-report-2025-07-15.md");
        Assert.True(File.Exists(expectedPath));
        var content = await File.ReadAllTextAsync(expectedPath);
        Assert.Equal(expectedMarkdown, content);
    }

    [Fact]
    public async Task ExecuteAsync_空報告正常完成()
    {
        // Arrange — 無任何中間報告
        var fields = new List<string>();
        _redisServiceMock.Setup(x => x.HashFieldsAsync(RedisKeys.RiskAnalysisHash))
            .ReturnsAsync(fields);

        _redisServiceMock.Setup(x => x.HashGetByPrefixAsync(
                RedisKeys.RiskAnalysisHash, "Intermediate:1-"))
            .ReturnsAsync(new Dictionary<string, string>());

        _riskAnalyzerMock.Setup(x => x.GenerateFinalReportAsync(
                It.IsAny<IReadOnlyList<RiskAnalysisReport>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("# 空報告");

        _redisServiceMock.Setup(x => x.HashSetAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.FinalReport, It.IsAny<string>()))
            .ReturnsAsync(true);

        var task = CreateTask();

        // Act — 不應擲出例外
        await task.ExecuteAsync();

        // Assert
        _riskAnalyzerMock.Verify(x => x.GenerateFinalReportAsync(
            It.Is<IReadOnlyList<RiskAnalysisReport>>(r => r.Count == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_檔案路徑包含日期()
    {
        // Arrange
        var fields = new List<string>();
        _redisServiceMock.Setup(x => x.HashFieldsAsync(RedisKeys.RiskAnalysisHash))
            .ReturnsAsync(fields);

        _redisServiceMock.Setup(x => x.HashGetByPrefixAsync(
                RedisKeys.RiskAnalysisHash, "Intermediate:1-"))
            .ReturnsAsync(new Dictionary<string, string>());

        _riskAnalyzerMock.Setup(x => x.GenerateFinalReportAsync(
                It.IsAny<IReadOnlyList<RiskAnalysisReport>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("# 報告");

        _redisServiceMock.Setup(x => x.HashSetAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.FinalReport, It.IsAny<string>()))
            .ReturnsAsync(true);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 檢查檔案名稱格式
        var expectedFileName = $"risk-report-{_fixedNow:yyyy-MM-dd}.md";
        var expectedPath = Path.Combine(_reportOutputPath, expectedFileName);
        Assert.True(File.Exists(expectedPath),
            $"預期檔案 {expectedPath} 應存在");
        Assert.Equal("risk-report-2025-07-15.md", expectedFileName);
    }
}
