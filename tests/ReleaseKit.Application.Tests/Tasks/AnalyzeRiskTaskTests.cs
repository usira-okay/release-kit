using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Entities;

namespace ReleaseKit.Application.Tests.Tasks;

/// <summary>
/// AnalyzeRiskTask Orchestrator 單元測試
/// </summary>
public class AnalyzeRiskTaskTests : IDisposable
{
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly Mock<IGitService> _gitServiceMock;
    private readonly Mock<IRiskAnalyzer> _riskAnalyzerMock;
    private readonly Mock<INow> _nowMock;
    private readonly List<string> _callOrder;
    private readonly string _reportOutputPath;

    public AnalyzeRiskTaskTests()
    {
        _redisServiceMock = new Mock<IRedisService>();
        _gitServiceMock = new Mock<IGitService>();
        _riskAnalyzerMock = new Mock<IRiskAnalyzer>();
        _nowMock = new Mock<INow>();
        _callOrder = new List<string>();

        _nowMock.Setup(x => x.UtcNow)
            .Returns(new DateTimeOffset(2025, 7, 15, 10, 0, 0, TimeSpan.Zero));

        _reportOutputPath = Path.Combine(
            Path.GetTempPath(),
            $"release-kit-orchestrator-test-{Guid.NewGuid():N}");

        SetupDefaultRedisResponses();
    }

    public void Dispose()
    {
        if (Directory.Exists(_reportOutputPath))
            Directory.Delete(_reportOutputPath, recursive: true);
    }

    /// <summary>
    /// 設定 Redis 預設回應，讓所有子 Task 能正常完成
    /// </summary>
    private void SetupDefaultRedisResponses()
    {
        // CloneRepositoriesTask 最後呼叫 — 追蹤順序
        _redisServiceMock.Setup(x => x.HashSetAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.ClonePaths, It.IsAny<string>()))
            .Callback(() => _callOrder.Add("CloneRepositories"))
            .ReturnsAsync(true);

        // ExtractPrDiffsTask 讀取 PR 資料
        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.GitLabHash, RedisKeys.Fields.PullRequestsByUser))
            .ReturnsAsync((string?)null);

        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.BitbucketHash, RedisKeys.Fields.PullRequestsByUser))
            .ReturnsAsync((string?)null);

        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.ClonePaths))
            .ReturnsAsync((string?)null);

        // ExtractPrDiffsTask 最後呼叫 — 追蹤順序
        _redisServiceMock.Setup(x => x.HashSetAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.PrDiffs, It.IsAny<string>()))
            .Callback(() => _callOrder.Add("ExtractPrDiffs"))
            .ReturnsAsync(true);

        // AnalyzeProjectRiskTask 讀取 PrDiffs
        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.PrDiffs))
            .Callback(() => _callOrder.Add("AnalyzeProjectRisk"))
            .ReturnsAsync((string?)null);

        // GenerateRiskReportTask 讀取欄位清單
        _redisServiceMock.Setup(x => x.HashFieldsAsync(RedisKeys.RiskAnalysisHash))
            .Callback(() => _callOrder.Add("GenerateRiskReport"))
            .ReturnsAsync(new List<string>());

        _redisServiceMock.Setup(x => x.HashGetByPrefixAsync(
                RedisKeys.RiskAnalysisHash, It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, string>());

        // GenerateRiskReportTask 存入最終報告
        _redisServiceMock.Setup(x => x.HashSetAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.FinalReport, It.IsAny<string>()))
            .ReturnsAsync(true);

        // IRiskAnalyzer
        _riskAnalyzerMock.Setup(x => x.GenerateFinalReportAsync(
                It.IsAny<IReadOnlyList<RiskAnalysisReport>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("# 測試報告");
    }

    /// <summary>
    /// 建立 AnalyzeRiskTask 與所有子 Task
    /// </summary>
    private AnalyzeRiskTask CreateTask()
    {
        var riskAnalysisOptions = Options.Create(new RiskAnalysisOptions
        {
            CloneBasePath = "/clone",
            ReportOutputPath = _reportOutputPath
        });

        var gitLabOptions = Options.Create(new GitLabOptions());
        var bitbucketOptions = Options.Create(new BitbucketOptions());

        var cloneTask = new CloneRepositoriesTask(
            _redisServiceMock.Object,
            _gitServiceMock.Object,
            gitLabOptions,
            bitbucketOptions,
            riskAnalysisOptions,
            new Mock<ILogger<CloneRepositoriesTask>>().Object);

        var extractTask = new ExtractPrDiffsTask(
            _redisServiceMock.Object,
            _gitServiceMock.Object,
            new Mock<ILogger<ExtractPrDiffsTask>>().Object);

        var analyzeProjectTask = new AnalyzeProjectRiskTask(
            _redisServiceMock.Object,
            _riskAnalyzerMock.Object,
            riskAnalysisOptions,
            _nowMock.Object,
            new Mock<ILogger<AnalyzeProjectRiskTask>>().Object);

        var analyzeCrossProjectTask = new AnalyzeCrossProjectRiskTask(
            _redisServiceMock.Object,
            _riskAnalyzerMock.Object,
            riskAnalysisOptions,
            _nowMock.Object,
            new Mock<ILogger<AnalyzeCrossProjectRiskTask>>().Object);

        var generateReportTask = new GenerateRiskReportTask(
            _redisServiceMock.Object,
            _riskAnalyzerMock.Object,
            riskAnalysisOptions,
            _nowMock.Object,
            new Mock<ILogger<GenerateRiskReportTask>>().Object);

        return new AnalyzeRiskTask(
            cloneTask,
            extractTask,
            analyzeProjectTask,
            analyzeCrossProjectTask,
            generateReportTask,
            new Mock<ILogger<AnalyzeRiskTask>>().Object);
    }

    [Fact]
    public async Task ExecuteAsync_五個子Task依序執行()
    {
        // Arrange
        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 透過 Redis 呼叫順序驗證子 Task 依序執行
        Assert.Equal(4, _callOrder.Count);
        Assert.Equal("CloneRepositories", _callOrder[0]);
        Assert.Equal("ExtractPrDiffs", _callOrder[1]);
        Assert.Equal("AnalyzeProjectRisk", _callOrder[2]);
        Assert.Equal("GenerateRiskReport", _callOrder[3]);

        // 額外驗證 GenerateFinalReportAsync 被呼叫（表示 GenerateRiskReportTask 有執行）
        _riskAnalyzerMock.Verify(x => x.GenerateFinalReportAsync(
            It.IsAny<IReadOnlyList<RiskAnalysisReport>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_第一個子Task失敗時不繼續()
    {
        // Arrange — 讓 CloneRepositoriesTask 的 Redis 寫入拋出例外
        _redisServiceMock.Setup(x => x.HashSetAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.ClonePaths, It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Redis 連線失敗"));

        var task = CreateTask();

        // Act & Assert — 預期例外向上傳遞
        await Assert.ThrowsAsync<InvalidOperationException>(task.ExecuteAsync);

        // 後續子 Task 的 Redis 呼叫不應發生
        _redisServiceMock.Verify(x => x.HashGetAsync(
            RedisKeys.GitLabHash, RedisKeys.Fields.PullRequestsByUser), Times.Never);

        _redisServiceMock.Verify(x => x.HashFieldsAsync(
            RedisKeys.RiskAnalysisHash), Times.Never);

        // IRiskAnalyzer 也不應被呼叫
        _riskAnalyzerMock.Verify(x => x.GenerateFinalReportAsync(
            It.IsAny<IReadOnlyList<RiskAnalysisReport>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
