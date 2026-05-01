using GitHub.Copilot.SDK;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis.Models;

namespace ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis;

/// <summary>
/// 情境專家型 Copilot 風險分析主協調器
/// </summary>
/// <remarks>
/// 協調三層 Agent Pipeline：Coordinator → Expert × N → Synthesis
/// </remarks>
public class CopilotScenarioDispatcher : ICopilotScenarioDispatcher
{
    private readonly CoordinatorAgentRunner _coordinatorRunner;
    private readonly ExpertAgentRunner _expertRunner;
    private readonly SynthesisAgentRunner _synthesisRunner;
    private readonly ILogger<CopilotScenarioDispatcher> _logger;
    private readonly IOptions<CopilotOptions> _options;

    /// <summary>
    /// 初始化 <see cref="CopilotScenarioDispatcher"/>
    /// </summary>
    public CopilotScenarioDispatcher(
        CoordinatorAgentRunner coordinatorRunner,
        ExpertAgentRunner expertRunner,
        SynthesisAgentRunner synthesisRunner,
        ILogger<CopilotScenarioDispatcher> logger,
        IOptions<CopilotOptions> options)
    {
        _coordinatorRunner = coordinatorRunner;
        _expertRunner = expertRunner;
        _synthesisRunner = synthesisRunner;
        _logger = logger;
        _options = options;
    }

    /// <inheritdoc />
    public async Task<ProjectRiskAnalysis> DispatchAsync(
        string runId,
        string projectPath,
        IReadOnlyList<CommitSummary> commitSummaries,
        string localPath,
        IReadOnlyList<RiskScenario> scenarios,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "開始情境專家分析: {ProjectPath}, {CommitCount} 個 commit, {ScenarioCount} 個情境",
            projectPath, commitSummaries.Count, scenarios.Count);

        var clientOptions = BuildClientOptions();

        // 1. Coordinator 分配
        var assignment = await _coordinatorRunner.RunAsync(
            projectPath, commitSummaries, localPath, scenarios, clientOptions, ct);

        // 2. Expert 平行分析
        var expertTasks = scenarios.Select(scenario =>
            _expertRunner.RunAsync(scenario, assignment.GetShas(scenario), localPath, projectPath, clientOptions, ct));
        var expertResults = await Task.WhenAll(expertTasks);

        var expertDict = expertResults.ToDictionary(e => e.Scenario);

        // 3. Synthesis 匯總
        var synthesisInput = new SynthesisInput
        {
            ProjectPath = projectPath,
            ExpertResults = expertDict
        };

        var finalFindings = await _synthesisRunner.RunAsync(synthesisInput, clientOptions, ct);

        var sessionCount = 1 + scenarios.Count + 1; // Coordinator + Experts + Synthesis

        _logger.LogInformation(
            "情境專家分析完成: {ProjectPath}, {FindingCount} 個風險, {SessionCount} 個 session",
            projectPath, finalFindings.Count, sessionCount);

        return new ProjectRiskAnalysis
        {
            ProjectPath = projectPath,
            Findings = finalFindings.ToList(),
            SessionCount = sessionCount
        };
    }

    /// <summary>
    /// 建立 CopilotClient 選項
    /// </summary>
    internal CopilotClientOptions BuildClientOptions()
    {
        var clientOptions = new CopilotClientOptions { AutoStart = true };
        if (!string.IsNullOrWhiteSpace(_options.Value.GitHubToken))
            clientOptions.GitHubToken = _options.Value.GitHubToken;
        return clientOptions;
    }
}
