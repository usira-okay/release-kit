using Microsoft.Extensions.Logging;
using ReleaseKit.Application.Common;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Entities;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// Stage 3：靜態專案分析
/// </summary>
public class StaticProjectAnalysisTask : ITask
{
    private readonly IProjectStructureScanner _scanner;
    private readonly IDependencyInferrer _inferrer;
    private readonly IRedisService _redisService;
    private readonly ILogger<StaticProjectAnalysisTask> _logger;

    /// <summary>
    /// 初始化 <see cref="StaticProjectAnalysisTask"/> 類別的新執行個體
    /// </summary>
    /// <param name="scanner">專案結構掃描器</param>
    /// <param name="inferrer">跨專案相依性推斷器</param>
    /// <param name="redisService">Redis 快取服務</param>
    /// <param name="logger">日誌記錄器</param>
    public StaticProjectAnalysisTask(
        IProjectStructureScanner scanner,
        IDependencyInferrer inferrer,
        IRedisService redisService,
        ILogger<StaticProjectAnalysisTask> logger)
    {
        _scanner = scanner;
        _inferrer = inferrer;
        _redisService = redisService;
        _logger = logger;
    }

    /// <summary>
    /// 執行 Stage 3 靜態專案分析
    /// </summary>
    public async Task ExecuteAsync()
    {
        var runId = await _redisService.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey);
        if (string.IsNullOrEmpty(runId))
        {
            _logger.LogWarning("找不到 RunId，請先執行 Stage 1");
            return;
        }
        _logger.LogInformation("開始 Stage 3: 靜態專案分析, RunId={RunId}", runId);

        var stage1Results = await _redisService.HashGetAllAsync(RiskAnalysisRedisKeys.Stage1Hash(runId));
        var projectStructures = new List<ProjectStructure>();

        // Phase 1: 掃描每個成功 clone 的專案
        foreach (var (projectPath, cloneJson) in stage1Results)
        {
            var cloneResult = cloneJson.ToTypedObject<CloneStageResult>();
            if (cloneResult?.Status != "Success")
            {
                _logger.LogWarning("專案 {ProjectPath} clone 失敗，跳過靜態分析", projectPath);
                continue;
            }

            var structure = _scanner.Scan(projectPath, cloneResult.LocalPath);
            projectStructures.Add(structure);
        }

        // Phase 2: 推斷跨專案相依性
        var enrichedStructures = _inferrer.InferDependencies(projectStructures);

        // Phase 3: 儲存分析結果
        foreach (var structure in enrichedStructures)
        {
            await _redisService.HashSetAsync(
                RiskAnalysisRedisKeys.Stage3Hash(runId),
                structure.ProjectPath,
                structure.ToJson());
        }

        _logger.LogInformation("Stage 3 完成: {Count} 個專案分析完畢, RunId={RunId}",
            enrichedStructures.Count, runId);
    }

    /// <summary>
    /// Stage 1 clone 結果的反序列化模型
    /// </summary>
    private sealed record CloneStageResult
    {
        public string LocalPath { get; init; } = "";
        public string Status { get; init; } = "";
    }
}
