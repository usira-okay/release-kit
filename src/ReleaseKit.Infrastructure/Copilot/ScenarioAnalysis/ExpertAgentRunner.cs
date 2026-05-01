using System.Text.Json;
using System.Text.RegularExpressions;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis.Models;

namespace ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis;

/// <summary>
/// Expert Agent 執行器：依情境切換 prompt，使用工具集自主分析
/// </summary>
public class ExpertAgentRunner
{
    private readonly ExpertToolFactory _toolFactory;
    private readonly IOptions<CopilotOptions> _options;
    private readonly ILogger<ExpertAgentRunner> _logger;

    /// <summary>
    /// 初始化 <see cref="ExpertAgentRunner"/>
    /// </summary>
    public ExpertAgentRunner(
        ExpertToolFactory toolFactory,
        IOptions<CopilotOptions> options,
        ILogger<ExpertAgentRunner> logger)
    {
        _toolFactory = toolFactory;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// 執行 Expert Agent，回傳該情境的分析結果
    /// </summary>
    public async Task<ExpertFindings> RunAsync(
        RiskScenario scenario,
        IReadOnlyList<string> commitShas,
        string localPath,
        string projectPath,
        CopilotClientOptions clientOptions,
        CancellationToken ct = default)
    {
        if (commitShas.Count == 0)
        {
            return new ExpertFindings
            {
                Scenario = scenario,
                Findings = []
            };
        }

        var tools = _toolFactory.CreateTools(localPath, ct);

        await using var client = new CopilotClient(clientOptions);
        var config = new SessionConfig
        {
            Model = _options.Value.Model,
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Replace,
                Content = ScenarioPromptBuilder.BuildExpertSystemPrompt(scenario)
            },
            Tools = tools.ToList(),
            InfiniteSessions = new InfiniteSessionConfig { Enabled = false },
            OnPermissionRequest = PermissionHandler.ApproveAll
        };

        await using var session = await client.CreateSessionAsync(config);
        var userPrompt = ScenarioPromptBuilder.BuildExpertUserPrompt(projectPath, commitShas, scenario);
        var timeout = TimeSpan.FromSeconds(_options.Value.TimeoutSeconds);

        var response = await session.SendAndWaitAsync(
            new MessageOptions { Prompt = userPrompt }, timeout: timeout);

        var content = response?.Data?.Content;

        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("{Scenario} Expert 回傳空白，重試一次", scenario);
            response = await session.SendAndWaitAsync(
                new MessageOptions { Prompt = "請重新分析並以 JSON 格式回傳結果。" },
                timeout: timeout);
            content = response?.Data?.Content;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("{Scenario} Expert 重試後仍為空白", scenario);
            return new ExpertFindings
            {
                Scenario = scenario,
                Findings = [],
                Failed = true,
                FailureReason = "Agent 回應為空"
            };
        }

        var findings = ParseFindings(content, scenario);

        _logger.LogInformation("{Scenario} Expert 完成: {FindingCount} 個風險, {CommitCount} 個 commit",
            scenario, findings.Count, commitShas.Count);

        return new ExpertFindings
        {
            Scenario = scenario,
            Findings = findings
        };
    }

    /// <summary>
    /// 解析 Expert 回傳的 JSON 為 RiskFinding 清單
    /// </summary>
    internal static List<RiskFinding> ParseFindings(string response, RiskScenario expectedScenario)
    {
        var json = ExtractJsonBlock(response);
        if (string.IsNullOrWhiteSpace(json))
        {
            json = response.Trim();
        }

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var items = JsonSerializer.Deserialize<List<RiskFindingDto>>(json, options);
            if (items == null) return [];

            return items.Select(dto => new RiskFinding
            {
                Scenario = Enum.TryParse<RiskScenario>(dto.Scenario, ignoreCase: true, out var s) ? s : expectedScenario,
                RiskLevel = Enum.TryParse<RiskLevel>(dto.RiskLevel, ignoreCase: true, out var r) ? r : RiskLevel.Medium,
                Description = dto.Description ?? string.Empty,
                AffectedFile = dto.AffectedFile ?? string.Empty,
                DiffSnippet = dto.DiffSnippet ?? string.Empty,
                PotentiallyAffectedProjects = dto.PotentiallyAffectedProjects ?? [],
                RecommendedAction = dto.RecommendedAction ?? string.Empty,
                ChangedBy = string.Empty
            }).ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string ExtractJsonBlock(string response)
    {
        var match = Regex.Match(response, @"```(?:json)?\s*\n?(.*?)\n?```",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private sealed record RiskFindingDto
    {
        public string? Scenario { get; init; }
        public string? RiskLevel { get; init; }
        public string? Description { get; init; }
        public string? AffectedFile { get; init; }
        public string? DiffSnippet { get; init; }
        public List<string>? PotentiallyAffectedProjects { get; init; }
        public string? RecommendedAction { get; init; }
    }
}
