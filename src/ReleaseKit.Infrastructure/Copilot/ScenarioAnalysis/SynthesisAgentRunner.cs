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
/// Synthesis Agent 執行器：匯總各 Expert 結果，去重、識別複合風險
/// </summary>
public class SynthesisAgentRunner
{
    private readonly IOptions<CopilotOptions> _options;
    private readonly ILogger<SynthesisAgentRunner> _logger;

    /// <summary>
    /// 初始化 <see cref="SynthesisAgentRunner"/>
    /// </summary>
    public SynthesisAgentRunner(
        IOptions<CopilotOptions> options,
        ILogger<SynthesisAgentRunner> logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// 執行 Synthesis Agent，回傳最終合併結果
    /// </summary>
    public async Task<IReadOnlyList<RiskFinding>> RunAsync(
        SynthesisInput input,
        CopilotClientOptions clientOptions,
        CancellationToken ct = default)
    {
        var totalFindings = input.ExpertResults.Values
            .Where(e => !e.Failed)
            .Sum(e => e.Findings.Count);

        if (totalFindings == 0)
        {
            _logger.LogInformation("所有 Expert 均無風險發現，跳過 Synthesis");
            return [];
        }

        await using var client = new CopilotClient(clientOptions);
        var config = new SessionConfig
        {
            Model = _options.Value.Model,
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Replace,
                Content = ScenarioPromptBuilder.BuildSynthesisSystemPrompt()
            },
            Tools = [],
            InfiniteSessions = new InfiniteSessionConfig { Enabled = false },
            OnPermissionRequest = PermissionHandler.ApproveAll
        };

        await using var session = await client.CreateSessionAsync(config);
        var userPrompt = ScenarioPromptBuilder.BuildSynthesisUserPrompt(input);
        var timeout = TimeSpan.FromSeconds(_options.Value.TimeoutSeconds);

        var response = await session.SendAndWaitAsync(
            new MessageOptions { Prompt = userPrompt }, timeout: timeout);

        var content = response?.Data?.Content;

        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("Synthesis Agent 回傳空白，直接合併 Expert 結果");
            return MergeExpertFindings(input.ExpertResults);
        }

        var findings = ParseSynthesisFindings(content);
        if (findings.Count == 0 && totalFindings > 0)
        {
            _logger.LogWarning("Synthesis 解析結果為空但 Expert 有發現，使用 fallback 合併");
            return MergeExpertFindings(input.ExpertResults);
        }

        _logger.LogInformation("Synthesis 完成: {FindingCount} 個最終風險", findings.Count);
        return findings;
    }

    /// <summary>
    /// 解析 Synthesis 回傳的 JSON（格式同 Expert，可能含 CompositeRisk）
    /// </summary>
    internal static List<RiskFinding> ParseSynthesisFindings(string response)
    {
        var json = ExtractJsonBlock(response);
        if (string.IsNullOrWhiteSpace(json))
            json = response.Trim();

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var items = JsonSerializer.Deserialize<List<SynthesisFindingDto>>(json, options);
            if (items == null) return [];

            return items.Select(dto => new RiskFinding
            {
                Scenario = Enum.TryParse<RiskScenario>(dto.Scenario, ignoreCase: true, out var s) ? s : RiskScenario.ApiContractBreak,
                RiskLevel = Enum.TryParse<RiskLevel>(dto.RiskLevel, ignoreCase: true, out var r) ? r : RiskLevel.Medium,
                Description = BuildDescription(dto.Description, dto.CompositeRisk),
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

    /// <summary>
    /// Fallback：直接合併所有 Expert 的 findings
    /// </summary>
    internal static List<RiskFinding> MergeExpertFindings(
        IReadOnlyDictionary<RiskScenario, ExpertFindings> expertResults)
    {
        return expertResults.Values
            .Where(e => !e.Failed)
            .SelectMany(e => e.Findings)
            .ToList();
    }

    private static string BuildDescription(string? description, string? compositeRisk)
    {
        if (string.IsNullOrEmpty(compositeRisk))
            return description ?? string.Empty;
        return $"{description} [複合風險: {compositeRisk}]";
    }

    private static string ExtractJsonBlock(string response)
    {
        var match = Regex.Match(response, @"```(?:json)?\s*\n?(.*?)\n?```",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private sealed record SynthesisFindingDto
    {
        public string? Scenario { get; init; }
        public string? RiskLevel { get; init; }
        public string? Description { get; init; }
        public string? AffectedFile { get; init; }
        public string? DiffSnippet { get; init; }
        public List<string>? PotentiallyAffectedProjects { get; init; }
        public string? RecommendedAction { get; init; }
        public string? CompositeRisk { get; init; }
    }
}
