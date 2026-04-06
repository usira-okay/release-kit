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
/// AnalyzeCrossProjectRiskTask 單元測試
/// </summary>
public class AnalyzeCrossProjectRiskTaskTests
{
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly Mock<IRiskAnalyzer> _riskAnalyzerMock;
    private readonly Mock<INow> _nowMock;
    private readonly Mock<ILogger<AnalyzeCrossProjectRiskTask>> _loggerMock;
    private readonly RiskAnalysisOptions _options;
    private readonly DateTimeOffset _fixedNow = new(2025, 7, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 捕捉所有寫入 Redis 的欄位
    /// </summary>
    private readonly Dictionary<string, string> _capturedRedisFields = new();

    public AnalyzeCrossProjectRiskTaskTests()
    {
        _redisServiceMock = new Mock<IRedisService>();
        _riskAnalyzerMock = new Mock<IRiskAnalyzer>();
        _nowMock = new Mock<INow>();
        _loggerMock = new Mock<ILogger<AnalyzeCrossProjectRiskTask>>();

        _options = new RiskAnalysisOptions
        {
            CloneBasePath = "/clone-base",
            ReportOutputPath = "/reports",
            MaxAnalysisPasses = 10
        };

        _nowMock.Setup(x => x.UtcNow).Returns(_fixedNow);

        // 捕捉所有寫入 RiskAnalysisHash 的欄位
        _redisServiceMock.Setup(x => x.HashSetAsync(
                RedisKeys.RiskAnalysisHash, It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, field, json) => _capturedRedisFields[field] = json)
            .ReturnsAsync(true);
    }

    private AnalyzeCrossProjectRiskTask CreateTask()
    {
        return new AnalyzeCrossProjectRiskTask(
            _redisServiceMock.Object,
            _riskAnalyzerMock.Object,
            Options.Create(_options),
            _nowMock.Object,
            _loggerMock.Object);
    }

    /// <summary>
    /// 建立測試用的 RiskAnalysisReport
    /// </summary>
    private RiskAnalysisReport CreateReport(
        int pass,
        int sequence,
        string? projectName = null,
        string summary = "測試摘要")
    {
        return new RiskAnalysisReport
        {
            PassKey = new AnalysisPassKey { Pass = pass, Sequence = sequence },
            ProjectName = projectName,
            RiskItems = new List<RiskItem>
            {
                new()
                {
                    Category = RiskCategory.ApiContract,
                    Level = RiskLevel.Medium,
                    ChangeSummary = "API 變更",
                    AffectedFiles = new List<string> { "test.cs" },
                    PotentiallyAffectedServices = new List<string> { "TestService" },
                    ImpactDescription = "影響測試服務",
                    SuggestedValidationSteps = new List<string> { "執行整合測試" }
                }
            },
            Summary = summary,
            AnalyzedAt = _fixedNow
        };
    }

    /// <summary>
    /// 設定 Redis 回傳 Pass 1 中間報告
    /// </summary>
    private void SetupPass1Reports(params RiskAnalysisReport[] reports)
    {
        var dict = new Dictionary<string, string>();
        foreach (var report in reports)
        {
            dict[report.PassKey.ToRedisField()] = report.ToJson();
        }

        _redisServiceMock.Setup(x => x.HashGetByPrefixAsync(
                RedisKeys.RiskAnalysisHash, "Intermediate:1-"))
            .ReturnsAsync(dict);
    }

    /// <summary>
    /// 建立 DynamicAnalysisResult
    /// </summary>
    private DynamicAnalysisResult CreateDynamicResult(
        int pass,
        bool continueAnalysis,
        string? continueReason = null,
        int reportCount = 1)
    {
        var reports = Enumerable.Range(1, reportCount)
            .Select(seq => CreateReport(pass, seq, summary: $"Pass {pass} 摘要"))
            .ToList();

        return new DynamicAnalysisResult
        {
            Reports = reports,
            ContinueAnalysis = continueAnalysis,
            ContinueReason = continueReason,
            AnalysisStrategy = $"Pass {pass} 策略"
        };
    }

    [Fact]
    public async Task ExecuteAsync_載入Pass1報告並呼叫AnalyzeDeepAsync_應傳入正確參數()
    {
        // Arrange
        var pass1Report1 = CreateReport(1, 1, projectName: "project-a");
        var pass1Report2 = CreateReport(1, 2, projectName: "project-b");
        SetupPass1Reports(pass1Report1, pass1Report2);

        var pass2Result = CreateDynamicResult(2, continueAnalysis: false);

        _riskAnalyzerMock.Setup(x => x.AnalyzeDeepAsync(
                2, It.IsAny<IReadOnlyList<RiskAnalysisReport>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pass2Result);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 驗證呼叫 AnalyzeDeepAsync 時 Pass=2 且傳入 2 份 Pass 1 報告
        _riskAnalyzerMock.Verify(x => x.AnalyzeDeepAsync(
            2,
            It.Is<IReadOnlyList<RiskAnalysisReport>>(reports => reports.Count == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_AI回傳ContinueAnalysisFalse_應在Pass2終止()
    {
        // Arrange
        SetupPass1Reports(CreateReport(1, 1, projectName: "project-a"));

        var pass2Result = CreateDynamicResult(2, continueAnalysis: false);

        _riskAnalyzerMock.Setup(x => x.AnalyzeDeepAsync(
                2, It.IsAny<IReadOnlyList<RiskAnalysisReport>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pass2Result);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 僅呼叫一次 AnalyzeDeepAsync（Pass 2）
        _riskAnalyzerMock.Verify(x => x.AnalyzeDeepAsync(
            It.IsAny<int>(), It.IsAny<IReadOnlyList<RiskAnalysisReport>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_多層分析Pass234_應連續呼叫直到停止()
    {
        // Arrange
        SetupPass1Reports(CreateReport(1, 1, projectName: "project-a"));

        // Pass 2 → 繼續
        _riskAnalyzerMock.Setup(x => x.AnalyzeDeepAsync(
                2, It.IsAny<IReadOnlyList<RiskAnalysisReport>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDynamicResult(2, continueAnalysis: true, continueReason: "需要更深入分析"));

        // Pass 3 → 繼續
        _riskAnalyzerMock.Setup(x => x.AnalyzeDeepAsync(
                3, It.IsAny<IReadOnlyList<RiskAnalysisReport>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDynamicResult(3, continueAnalysis: true, continueReason: "仍有未覆蓋風險"));

        // Pass 4 → 停止
        _riskAnalyzerMock.Setup(x => x.AnalyzeDeepAsync(
                4, It.IsAny<IReadOnlyList<RiskAnalysisReport>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDynamicResult(4, continueAnalysis: false));

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 共呼叫 3 次（Pass 2, 3, 4）
        _riskAnalyzerMock.Verify(x => x.AnalyzeDeepAsync(
            It.IsAny<int>(), It.IsAny<IReadOnlyList<RiskAnalysisReport>>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));

        _riskAnalyzerMock.Verify(x => x.AnalyzeDeepAsync(
            2, It.IsAny<IReadOnlyList<RiskAnalysisReport>>(), It.IsAny<CancellationToken>()), Times.Once);
        _riskAnalyzerMock.Verify(x => x.AnalyzeDeepAsync(
            3, It.IsAny<IReadOnlyList<RiskAnalysisReport>>(), It.IsAny<CancellationToken>()), Times.Once);
        _riskAnalyzerMock.Verify(x => x.AnalyzeDeepAsync(
            4, It.IsAny<IReadOnlyList<RiskAnalysisReport>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_硬上限10層_即使AI持續要求也應停止()
    {
        // Arrange
        SetupPass1Reports(CreateReport(1, 1, projectName: "project-a"));

        // 所有 Pass 都回傳 continueAnalysis=true
        _riskAnalyzerMock.Setup(x => x.AnalyzeDeepAsync(
                It.IsAny<int>(), It.IsAny<IReadOnlyList<RiskAnalysisReport>>(), It.IsAny<CancellationToken>()))
            .Returns((int pass, IReadOnlyList<RiskAnalysisReport> _, CancellationToken _) =>
                Task.FromResult(CreateDynamicResult(pass, continueAnalysis: true, continueReason: "持續分析")));

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 最多呼叫 9 次（Pass 2~10）
        _riskAnalyzerMock.Verify(x => x.AnalyzeDeepAsync(
            It.IsAny<int>(), It.IsAny<IReadOnlyList<RiskAnalysisReport>>(), It.IsAny<CancellationToken>()),
            Times.Exactly(9));
    }

    [Fact]
    public async Task ExecuteAsync_空報告正常完成_無Pass1報告時不呼叫AI()
    {
        // Arrange — 無 Pass 1 報告
        _redisServiceMock.Setup(x => x.HashGetByPrefixAsync(
                RedisKeys.RiskAnalysisHash, "Intermediate:1-"))
            .ReturnsAsync(new Dictionary<string, string>());

        var task = CreateTask();

        // Act — 不應擲出例外
        await task.ExecuteAsync();

        // Assert
        _riskAnalyzerMock.Verify(x => x.AnalyzeDeepAsync(
            It.IsAny<int>(), It.IsAny<IReadOnlyList<RiskAnalysisReport>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Empty(_capturedRedisFields);
    }

    [Fact]
    public async Task ExecuteAsync_PassMetadata正確存入Redis_應包含策略與報告數()
    {
        // Arrange
        SetupPass1Reports(CreateReport(1, 1, projectName: "project-a"));

        var pass2Result = new DynamicAnalysisResult
        {
            Reports = new List<RiskAnalysisReport> { CreateReport(2, 1) },
            ContinueAnalysis = false,
            ContinueReason = null,
            AnalysisStrategy = "跨專案關聯分析"
        };

        _riskAnalyzerMock.Setup(x => x.AnalyzeDeepAsync(
                2, It.IsAny<IReadOnlyList<RiskAnalysisReport>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pass2Result);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 驗證 PassMetadata:2 已存入
        Assert.True(_capturedRedisFields.ContainsKey("PassMetadata:2"));
        var metadataJson = _capturedRedisFields["PassMetadata:2"];
        Assert.Contains("跨專案關聯分析", metadataJson);
    }

    [Fact]
    public async Task ExecuteAsync_中間報告正確存入Redis_應使用正確欄位名稱()
    {
        // Arrange
        SetupPass1Reports(CreateReport(1, 1, projectName: "project-a"));

        var pass2Reports = new List<RiskAnalysisReport>
        {
            CreateReport(2, 1, summary: "跨專案報告1"),
            CreateReport(2, 2, summary: "跨專案報告2")
        };

        var pass2Result = new DynamicAnalysisResult
        {
            Reports = pass2Reports,
            ContinueAnalysis = false,
            AnalysisStrategy = "關聯分析"
        };

        _riskAnalyzerMock.Setup(x => x.AnalyzeDeepAsync(
                2, It.IsAny<IReadOnlyList<RiskAnalysisReport>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pass2Result);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 驗證中間報告以 Intermediate:{pass}-{seq} 格式存入
        Assert.True(_capturedRedisFields.ContainsKey("Intermediate:2-1"));
        Assert.True(_capturedRedisFields.ContainsKey("Intermediate:2-2"));

        var report1 = _capturedRedisFields["Intermediate:2-1"].ToTypedObject<RiskAnalysisReport>();
        Assert.NotNull(report1);
        Assert.Equal(2, report1.PassKey.Pass);
        Assert.Equal(1, report1.PassKey.Sequence);
    }
}
