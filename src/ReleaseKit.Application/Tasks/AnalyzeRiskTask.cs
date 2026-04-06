using Microsoft.Extensions.Logging;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 風險分析 Orchestrator，串聯所有子 Task
/// </summary>
public sealed class AnalyzeRiskTask : ITask
{
    private readonly CloneRepositoriesTask _cloneRepositoriesTask;
    private readonly ExtractPrDiffsTask _extractPrDiffsTask;
    private readonly AnalyzeProjectRiskTask _analyzeProjectRiskTask;
    private readonly AnalyzeCrossProjectRiskTask _analyzeCrossProjectRiskTask;
    private readonly GenerateRiskReportTask _generateRiskReportTask;
    private readonly ILogger<AnalyzeRiskTask> _logger;

    /// <summary>
    /// 初始化 <see cref="AnalyzeRiskTask"/> 類別的新執行個體
    /// </summary>
    public AnalyzeRiskTask(
        CloneRepositoriesTask cloneRepositoriesTask,
        ExtractPrDiffsTask extractPrDiffsTask,
        AnalyzeProjectRiskTask analyzeProjectRiskTask,
        AnalyzeCrossProjectRiskTask analyzeCrossProjectRiskTask,
        GenerateRiskReportTask generateRiskReportTask,
        ILogger<AnalyzeRiskTask> logger)
    {
        _cloneRepositoriesTask = cloneRepositoriesTask;
        _extractPrDiffsTask = extractPrDiffsTask;
        _analyzeProjectRiskTask = analyzeProjectRiskTask;
        _analyzeCrossProjectRiskTask = analyzeCrossProjectRiskTask;
        _generateRiskReportTask = generateRiskReportTask;
        _logger = logger;
    }

    /// <summary>執行完整風險分析流程</summary>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始執行完整風險分析流程");

        _logger.LogInformation("Step 1/5: Clone repositories");
        await _cloneRepositoriesTask.ExecuteAsync();

        _logger.LogInformation("Step 2/5: Extract PR diffs");
        await _extractPrDiffsTask.ExecuteAsync();

        _logger.LogInformation("Step 3/5: Per-project AI analysis (Pass 1)");
        await _analyzeProjectRiskTask.ExecuteAsync();

        _logger.LogInformation("Step 4/5: Cross-project dynamic analysis (Pass 2~10)");
        await _analyzeCrossProjectRiskTask.ExecuteAsync();

        _logger.LogInformation("Step 5/5: Generate final risk report");
        await _generateRiskReportTask.ExecuteAsync();

        _logger.LogInformation("Release 風險分析完成");
    }
}
