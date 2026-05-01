using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis.Models;

namespace ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis;

/// <summary>
/// Coordinator Agent 執行器：分配 commit 至各情境專家
/// </summary>
public class CoordinatorAgentRunner
{
    private readonly IOptions<CopilotOptions> _options;
    private readonly ILogger<CoordinatorAgentRunner> _logger;

    /// <summary>
    /// 初始化 <see cref="CoordinatorAgentRunner"/>
    /// </summary>
    public CoordinatorAgentRunner(
        IOptions<CopilotOptions> options,
        ILogger<CoordinatorAgentRunner> logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// 執行 Coordinator Agent，回傳分配結果
    /// </summary>
    public async Task<CoordinatorAssignment> RunAsync(
        string projectPath,
        IReadOnlyList<CommitSummary> commitSummaries,
        string localPath,
        IReadOnlyList<RiskScenario> scenarios,
        CopilotClientOptions clientOptions,
        CancellationToken ct = default)
    {
        var listFiles = AIFunctionFactory.Create(
            ([Description("目錄的相對路徑")] string path) =>
            {
                var fullPath = Path.Combine(localPath, path ?? string.Empty);
                if (!Directory.Exists(fullPath))
                    return $"目錄不存在: {path}";
                var entries = Directory.GetFileSystemEntries(fullPath)
                    .Select(e => Path.GetRelativePath(localPath, e))
                    .OrderBy(e => e);
                return string.Join("\n", entries);
            },
            "list_files",
            "列出指定目錄的檔案與子目錄");

        await using var client = new CopilotClient(clientOptions);
        var config = new SessionConfig
        {
            Model = _options.Value.Model,
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Replace,
                Content = ScenarioPromptBuilder.BuildCoordinatorSystemPrompt()
            },
            Tools = [listFiles],
            InfiniteSessions = new InfiniteSessionConfig { Enabled = false },
            OnPermissionRequest = PermissionHandler.ApproveAll
        };

        await using var session = await client.CreateSessionAsync(config);
        var userPrompt = ScenarioPromptBuilder.BuildCoordinatorUserPrompt(projectPath, commitSummaries);
        var timeout = TimeSpan.FromSeconds(_options.Value.TimeoutSeconds);

        var response = await session.SendAndWaitAsync(
            new MessageOptions { Prompt = userPrompt }, timeout: timeout);

        var content = response?.Data?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("Coordinator Agent 回傳空白，使用 fallback 分配所有 commit");
            return BuildFallbackAssignment(
                commitSummaries.Select(c => c.CommitSha).ToList(), scenarios);
        }

        var assignment = ParseAssignment(content);
        if (assignment == null)
        {
            _logger.LogWarning("Coordinator Agent 回傳格式錯誤，使用 fallback: {Content}", content);
            return BuildFallbackAssignment(
                commitSummaries.Select(c => c.CommitSha).ToList(), scenarios);
        }

        _logger.LogInformation("Coordinator 分配完成: {Reasoning}", assignment.Reasoning);
        return assignment;
    }

    /// <summary>
    /// 解析 Coordinator 回傳的 JSON 為 CoordinatorAssignment
    /// </summary>
    internal static CoordinatorAssignment? ParseAssignment(string response)
    {
        var json = ExtractJsonBlock(response);
        if (string.IsNullOrWhiteSpace(json))
        {
            json = response.Trim();
        }

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var dto = JsonSerializer.Deserialize<CoordinatorAssignmentDto>(json, options);
            if (dto?.Assignments == null) return null;

            var assignments = new Dictionary<RiskScenario, List<string>>();
            foreach (var (key, value) in dto.Assignments)
            {
                if (Enum.TryParse<RiskScenario>(key, ignoreCase: true, out var scenario))
                {
                    assignments[scenario] = value ?? [];
                }
            }

            return new CoordinatorAssignment
            {
                Assignments = assignments,
                Reasoning = dto.Reasoning ?? string.Empty
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Fallback：將所有 commit 分配給每個情境（不篩選）
    /// </summary>
    internal static CoordinatorAssignment BuildFallbackAssignment(
        IReadOnlyList<string> commitShas,
        IReadOnlyList<RiskScenario> scenarios)
    {
        var assignments = scenarios.ToDictionary(
            s => s,
            _ => commitShas.ToList());

        return new CoordinatorAssignment
        {
            Assignments = assignments,
            Reasoning = "Fallback: Coordinator 回應解析失敗，全部 commit 分配給所有情境"
        };
    }

    private static string ExtractJsonBlock(string response)
    {
        var match = Regex.Match(response, @"```(?:json)?\s*\n?(.*?)\n?```",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private sealed record CoordinatorAssignmentDto
    {
        public Dictionary<string, List<string>>? Assignments { get; init; }
        public string? Reasoning { get; init; }
    }
}
