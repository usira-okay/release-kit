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
/// AnalyzeProjectRiskTask 單元測試
/// </summary>
public class AnalyzeProjectRiskTaskTests
{
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly Mock<IRiskAnalyzer> _riskAnalyzerMock;
    private readonly Mock<INow> _nowMock;
    private readonly Mock<ILogger<AnalyzeProjectRiskTask>> _loggerMock;
    private readonly RiskAnalysisOptions _options;
    private readonly DateTimeOffset _fixedNow = new(2025, 7, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 捕捉所有寫入 Redis 的中間報告
    /// </summary>
    private readonly Dictionary<string, string> _capturedRedisFields = new();

    public AnalyzeProjectRiskTaskTests()
    {
        _redisServiceMock = new Mock<IRedisService>();
        _riskAnalyzerMock = new Mock<IRiskAnalyzer>();
        _nowMock = new Mock<INow>();
        _loggerMock = new Mock<ILogger<AnalyzeProjectRiskTask>>();

        _options = new RiskAnalysisOptions
        {
            CloneBasePath = "/clone-base",
            ReportOutputPath = "/reports",
            MaxConcurrentClones = 5,
            MaxTokensPerAiCall = 100
        };

        _nowMock.Setup(x => x.UtcNow).Returns(_fixedNow);

        // 捕捉所有寫入 RiskAnalysisHash 的欄位
        _redisServiceMock.Setup(x => x.HashSetAsync(
                RedisKeys.RiskAnalysisHash, It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, field, json) => _capturedRedisFields[field] = json)
            .ReturnsAsync(true);
    }

    private AnalyzeProjectRiskTask CreateTask()
    {
        return new AnalyzeProjectRiskTask(
            _redisServiceMock.Object,
            _riskAnalyzerMock.Object,
            Options.Create(_options),
            _nowMock.Object,
            _loggerMock.Object);
    }

    /// <summary>
    /// 設定 Redis 回傳 PrDiffs 資料
    /// </summary>
    private void SetupPrDiffs(Dictionary<string, List<PrDiffContext>>? diffs)
    {
        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.PrDiffs))
            .ReturnsAsync(diffs?.ToJson());
    }

    /// <summary>
    /// 建立測試用的 PrDiffContext
    /// </summary>
    private static PrDiffContext CreateDiff(
        string title = "feat: 測試變更",
        string diffContent = "diff --git a/test.cs b/test.cs",
        IReadOnlyList<string>? changedFiles = null)
    {
        return new PrDiffContext
        {
            Title = title,
            Description = "測試描述",
            SourceBranch = "feature/test",
            TargetBranch = "main",
            AuthorName = "developer",
            PrUrl = "https://gitlab.example.com/mr/1",
            DiffContent = diffContent,
            ChangedFiles = changedFiles ?? new List<string> { "test.cs" },
            Platform = SourceControlPlatform.GitLab
        };
    }

    /// <summary>
    /// 建立測試用的 RiskAnalysisReport
    /// </summary>
    private RiskAnalysisReport CreateReport(
        AnalysisPassKey passKey,
        string? projectName = null,
        IReadOnlyList<RiskItem>? riskItems = null,
        string summary = "測試摘要")
    {
        return new RiskAnalysisReport
        {
            PassKey = passKey,
            ProjectName = projectName,
            RiskItems = riskItems ?? new List<RiskItem>
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

    [Fact]
    public async Task ExecuteAsync_單一專案正常分析_應呼叫AI並存入報告()
    {
        // Arrange
        var diff = CreateDiff();
        var diffs = new Dictionary<string, List<PrDiffContext>>
        {
            ["group/project-a"] = new List<PrDiffContext> { diff }
        };
        SetupPrDiffs(diffs);

        var expectedReport = CreateReport(
            new AnalysisPassKey { Pass = 1, Sequence = 1 },
            projectName: "group/project-a");

        _riskAnalyzerMock.Setup(x => x.AnalyzeProjectRiskAsync(
                "group/project-a", It.IsAny<IReadOnlyList<PrDiffContext>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReport);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _riskAnalyzerMock.Verify(x => x.AnalyzeProjectRiskAsync(
            "group/project-a", It.IsAny<IReadOnlyList<PrDiffContext>>(), It.IsAny<CancellationToken>()), Times.Once);

        Assert.Single(_capturedRedisFields);
        Assert.True(_capturedRedisFields.ContainsKey("Intermediate:1-1"));

        var storedReport = _capturedRedisFields["Intermediate:1-1"].ToTypedObject<RiskAnalysisReport>();
        Assert.NotNull(storedReport);
        Assert.Equal("group/project-a", storedReport.ProjectName);
    }

    [Fact]
    public async Task ExecuteAsync_多專案平行分析_應處理所有專案()
    {
        // Arrange
        var diffs = new Dictionary<string, List<PrDiffContext>>
        {
            ["group/project-a"] = new List<PrDiffContext> { CreateDiff() },
            ["team/repo-b"] = new List<PrDiffContext> { CreateDiff() }
        };
        SetupPrDiffs(diffs);

        _riskAnalyzerMock.Setup(x => x.AnalyzeProjectRiskAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<PrDiffContext>>(), It.IsAny<CancellationToken>()))
            .Returns((string name, IReadOnlyList<PrDiffContext> _, CancellationToken _) =>
            {
                var report = CreateReport(
                    new AnalysisPassKey { Pass = 1, Sequence = 1 },
                    projectName: name);
                return Task.FromResult(report);
            });

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        Assert.Equal(2, _capturedRedisFields.Count);
        _riskAnalyzerMock.Verify(x => x.AnalyzeProjectRiskAsync(
            It.IsAny<string>(), It.IsAny<IReadOnlyList<PrDiffContext>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ExecuteAsync_大Diff拆分子代理_應拆分並合併結果()
    {
        // Arrange — MaxTokensPerAiCall = 100，建立超過限制的 diff
        var largeDiff1 = CreateDiff(
            title: "feat: 大型變更1",
            diffContent: new string('x', 60),
            changedFiles: new List<string> { "file1.cs", "file2.cs" });
        var largeDiff2 = CreateDiff(
            title: "feat: 大型變更2",
            diffContent: new string('y', 60),
            changedFiles: new List<string> { "file3.cs" });

        var diffs = new Dictionary<string, List<PrDiffContext>>
        {
            ["group/project-a"] = new List<PrDiffContext> { largeDiff1, largeDiff2 }
        };
        SetupPrDiffs(diffs);

        // 每次 AI 呼叫回傳不同的 RiskItems
        var callCount = 0;
        _riskAnalyzerMock.Setup(x => x.AnalyzeProjectRiskAsync(
                "group/project-a", It.IsAny<IReadOnlyList<PrDiffContext>>(), It.IsAny<CancellationToken>()))
            .Returns((string name, IReadOnlyList<PrDiffContext> chunkDiffs, CancellationToken _) =>
            {
                var currentCall = Interlocked.Increment(ref callCount);
                var report = CreateReport(
                    new AnalysisPassKey { Pass = 1, Sequence = 1, SubSequence = currentCall == 1 ? "a" : "b" },
                    projectName: name,
                    riskItems: new List<RiskItem>
                    {
                        new()
                        {
                            Category = RiskCategory.ApiContract,
                            Level = RiskLevel.High,
                            ChangeSummary = $"變更 {currentCall}",
                            AffectedFiles = chunkDiffs.SelectMany(d => d.ChangedFiles).ToList(),
                            PotentiallyAffectedServices = new List<string> { "Service" },
                            ImpactDescription = $"影響 {currentCall}",
                            SuggestedValidationSteps = new List<string> { "驗證" }
                        }
                    },
                    summary: $"摘要 {currentCall}");
                return Task.FromResult(report);
            });

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 應呼叫 AI 兩次（拆分為兩個 chunk）
        _riskAnalyzerMock.Verify(x => x.AnalyzeProjectRiskAsync(
            "group/project-a", It.IsAny<IReadOnlyList<PrDiffContext>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));

        // 合併後的報告存入 Redis
        Assert.True(_capturedRedisFields.ContainsKey("Intermediate:1-1"));
        var mergedReport = _capturedRedisFields["Intermediate:1-1"].ToTypedObject<RiskAnalysisReport>();
        Assert.NotNull(mergedReport);

        // 合併的 RiskItems 應包含兩個 chunk 的項目
        Assert.Equal(2, mergedReport.RiskItems.Count);
    }

    [Fact]
    public async Task ExecuteAsync_空PrDiffs_應正常完成無錯誤()
    {
        // Arrange — Redis 回傳 null
        SetupPrDiffs(null);

        var task = CreateTask();

        // Act — 不應擲出例外
        await task.ExecuteAsync();

        // Assert — 不應呼叫 AI
        _riskAnalyzerMock.Verify(x => x.AnalyzeProjectRiskAsync(
            It.IsAny<string>(), It.IsAny<IReadOnlyList<PrDiffContext>>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(_capturedRedisFields);
    }

    [Fact]
    public async Task ExecuteAsync_報告正確存入Redis_應使用正確的欄位名稱()
    {
        // Arrange
        var diffs = new Dictionary<string, List<PrDiffContext>>
        {
            ["group/project-a"] = new List<PrDiffContext> { CreateDiff() }
        };
        SetupPrDiffs(diffs);

        var report = CreateReport(
            new AnalysisPassKey { Pass = 1, Sequence = 1 },
            projectName: "group/project-a");

        _riskAnalyzerMock.Setup(x => x.AnalyzeProjectRiskAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<PrDiffContext>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 驗證 Redis 寫入使用正確的 hash key 與 field
        _redisServiceMock.Verify(x => x.HashSetAsync(
            RedisKeys.RiskAnalysisHash,
            "Intermediate:1-1",
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_PassKey序號正確_應按專案名稱排序後遞增()
    {
        // Arrange — 兩個專案，排序後 a-project 在前
        var diffs = new Dictionary<string, List<PrDiffContext>>
        {
            ["z-project"] = new List<PrDiffContext> { CreateDiff() },
            ["a-project"] = new List<PrDiffContext> { CreateDiff() }
        };
        SetupPrDiffs(diffs);

        _riskAnalyzerMock.Setup(x => x.AnalyzeProjectRiskAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<PrDiffContext>>(), It.IsAny<CancellationToken>()))
            .Returns((string name, IReadOnlyList<PrDiffContext> _, CancellationToken _) =>
            {
                var report = CreateReport(
                    new AnalysisPassKey { Pass = 1, Sequence = 1 },
                    projectName: name);
                return Task.FromResult(report);
            });

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — a-project 應為 Sequence 1，z-project 應為 Sequence 2
        Assert.True(_capturedRedisFields.ContainsKey("Intermediate:1-1"));
        Assert.True(_capturedRedisFields.ContainsKey("Intermediate:1-2"));

        var report1 = _capturedRedisFields["Intermediate:1-1"].ToTypedObject<RiskAnalysisReport>();
        var report2 = _capturedRedisFields["Intermediate:1-2"].ToTypedObject<RiskAnalysisReport>();

        Assert.Equal("a-project", report1!.ProjectName);
        Assert.Equal("z-project", report2!.ProjectName);
    }

    [Fact]
    public async Task ExecuteAsync_拆分後覆蓋所有檔案_合併報告應包含所有異動檔案()
    {
        // Arrange — 兩個 diff 各有不同的檔案，超過 token 限制
        var diff1 = CreateDiff(
            diffContent: new string('x', 60),
            changedFiles: new List<string> { "src/ServiceA.cs", "src/ModelA.cs" });
        var diff2 = CreateDiff(
            diffContent: new string('y', 60),
            changedFiles: new List<string> { "src/ServiceB.cs" });

        var diffs = new Dictionary<string, List<PrDiffContext>>
        {
            ["group/project-a"] = new List<PrDiffContext> { diff1, diff2 }
        };
        SetupPrDiffs(diffs);

        _riskAnalyzerMock.Setup(x => x.AnalyzeProjectRiskAsync(
                "group/project-a", It.IsAny<IReadOnlyList<PrDiffContext>>(), It.IsAny<CancellationToken>()))
            .Returns((string name, IReadOnlyList<PrDiffContext> chunkDiffs, CancellationToken _) =>
            {
                var affectedFiles = chunkDiffs.SelectMany(d => d.ChangedFiles).ToList();
                var report = CreateReport(
                    new AnalysisPassKey { Pass = 1, Sequence = 1 },
                    projectName: name,
                    riskItems: new List<RiskItem>
                    {
                        new()
                        {
                            Category = RiskCategory.DatabaseSchema,
                            Level = RiskLevel.Low,
                            ChangeSummary = "Schema 變更",
                            AffectedFiles = affectedFiles,
                            PotentiallyAffectedServices = new List<string>(),
                            ImpactDescription = "影響",
                            SuggestedValidationSteps = new List<string>()
                        }
                    });
                return Task.FromResult(report);
            });

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 合併報告中的 RiskItems 應涵蓋所有原始檔案
        var mergedReport = _capturedRedisFields["Intermediate:1-1"].ToTypedObject<RiskAnalysisReport>();
        Assert.NotNull(mergedReport);

        var allAffectedFiles = mergedReport.RiskItems
            .SelectMany(r => r.AffectedFiles)
            .Distinct()
            .ToList();

        Assert.Contains("src/ServiceA.cs", allAffectedFiles);
        Assert.Contains("src/ModelA.cs", allAffectedFiles);
        Assert.Contains("src/ServiceB.cs", allAffectedFiles);
    }
}
