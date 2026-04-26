using System.Text.Json;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Infrastructure.Copilot;

/// <summary>
/// Copilot SDK 風險分析封裝
/// </summary>
public class CopilotRiskAnalyzer : ICopilotRiskAnalyzer
{
    private readonly IOptions<CopilotOptions> _options;
    private readonly ILogger<CopilotRiskAnalyzer> _logger;

    /// <summary>
    /// 單一 session 的最大 token 閾值
    /// </summary>
    private const int MaxTokensPerSession = 30000;

    /// <summary>
    /// 初始化 <see cref="CopilotRiskAnalyzer"/> 類別的新執行個體
    /// </summary>
    /// <param name="options">Copilot 組態選項</param>
    /// <param name="logger">日誌記錄器</param>
    public CopilotRiskAnalyzer(
        IOptions<CopilotOptions> options,
        ILogger<CopilotRiskAnalyzer> logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// 分析指定專案的風險
    /// </summary>
    /// <param name="projectPath">專案路徑</param>
    /// <param name="fileDiffs">異動檔案</param>
    /// <param name="projectStructure">專案結構（可為 null）</param>
    /// <param name="scenarios">分析情境</param>
    /// <param name="changedBy">變更者</param>
    /// <returns>風險發現清單與使用的 session 數量</returns>
    public async Task<(List<RiskFinding> Findings, int SessionCount)> AnalyzeAsync(
        string projectPath,
        IReadOnlyList<FileDiff> fileDiffs,
        ProjectStructure? projectStructure,
        IReadOnlyList<RiskScenario> scenarios,
        string changedBy)
    {
        var allFindings = new List<RiskFinding>();
        var sessionCount = 0;

        var userPrompt = RiskAnalysisPromptBuilder.BuildUserPrompt(projectPath, fileDiffs, projectStructure, scenarios);
        var estimatedTokens = RiskAnalysisPromptBuilder.EstimateTokens(userPrompt);

        if (estimatedTokens <= MaxTokensPerSession)
        {
            var findings = await RunSingleSessionAsync(userPrompt, changedBy);
            sessionCount = 1;
            allFindings.AddRange(findings);
        }
        else
        {
            var fileGroups = SplitFileDiffs(fileDiffs, MaxTokensPerSession);
            foreach (var group in fileGroups)
            {
                var groupPrompt = RiskAnalysisPromptBuilder.BuildUserPrompt(projectPath, group, projectStructure, scenarios);
                var findings = await RunSingleSessionAsync(groupPrompt, changedBy);
                sessionCount++;
                allFindings.AddRange(findings);
            }
        }

        _logger.LogInformation("專案 {ProjectPath} 分析完成: {FindingCount} 個風險, {SessionCount} 個 session",
            projectPath, allFindings.Count, sessionCount);

        return (allFindings, sessionCount);
    }

    /// <summary>
    /// 執行單一 Copilot session，逾時時重試一次
    /// </summary>
    private async Task<List<RiskFinding>> RunSingleSessionAsync(string userPrompt, string changedBy)
    {
        var clientOptions = new CopilotClientOptions { AutoStart = true };
        if (!string.IsNullOrWhiteSpace(_options.Value.GitHubToken))
            clientOptions.GitHubToken = _options.Value.GitHubToken;

        await using var client = new CopilotClient(clientOptions);

        var authStatus = await client.GetAuthStatusAsync();
        if (authStatus is not { IsAuthenticated: true })
        {
            _logger.LogError(
                "Copilot SDK 身份驗證失敗（IsAuthenticated: {IsAuthenticated}），無法執行風險分析",
                authStatus?.IsAuthenticated);
            return new List<RiskFinding>();
        }

        var config = new SessionConfig
        {
            Model = _options.Value.Model,
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Replace,
                Content = RiskAnalysisPromptBuilder.BuildSystemPrompt()
            },
            InfiniteSessions = new InfiniteSessionConfig { Enabled = false },
            OnPermissionRequest = PermissionHandler.ApproveAll
        };

        await using var session = await client.CreateSessionAsync(config);

        var timeout = TimeSpan.FromSeconds(_options.Value.TimeoutSeconds);
        var response = await session.SendAndWaitAsync(new MessageOptions { Prompt = userPrompt }, timeout: timeout);
        var content = response?.Data?.Content;

        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("Copilot 風險分析回傳空白回應，重試一次");
            response = await session.SendAndWaitAsync(new MessageOptions { Prompt = userPrompt }, timeout: timeout);
            content = response?.Data?.Content;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("Copilot 風險分析重試後仍為空白，回傳空結果");
            return new List<RiskFinding>();
        }

        return ParseFindings(content, changedBy);
    }

    /// <summary>
    /// 解析 JSON 回應為 RiskFinding 清單
    /// </summary>
    internal List<RiskFinding> ParseFindings(string response, string changedBy)
    {
        var json = response.Trim();

        if (json.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            json = json["```json".Length..];
        else if (json.StartsWith("```"))
            json = json["```".Length..];

        if (json.EndsWith("```"))
            json = json[..^"```".Length];

        json = json.Trim();

        var serializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        List<RiskFindingDto>? items;
        // AI 回應可能包含非合法 JSON，此處需明確捕捉解析例外並記錄原始回應後回傳空結果
        try
        {
            items = JsonSerializer.Deserialize<List<RiskFindingDto>>(json, serializerOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Copilot 風險分析回應解析失敗，原始回應: {Response}", response);
            return new List<RiskFinding>();
        }

        if (items == null)
        {
            _logger.LogWarning("Copilot 風險分析回應解析結果為 null，原始回應: {Response}", response);
            return new List<RiskFinding>();
        }

        return items.Select(dto => new RiskFinding
        {
            Scenario = Enum.Parse<RiskScenario>(dto.Scenario, ignoreCase: true),
            RiskLevel = Enum.Parse<RiskLevel>(dto.RiskLevel, ignoreCase: true),
            Description = dto.Description,
            AffectedFile = dto.AffectedFile,
            DiffSnippet = dto.DiffSnippet,
            PotentiallyAffectedProjects = dto.PotentiallyAffectedProjects ?? new List<string>(),
            RecommendedAction = dto.RecommendedAction,
            ChangedBy = changedBy
        }).ToList();
    }

    /// <summary>
    /// 將 FileDiff 依 token 閾值分組
    /// </summary>
    internal static List<List<FileDiff>> SplitFileDiffs(IReadOnlyList<FileDiff> diffs, int maxTokens)
    {
        var groups = new List<List<FileDiff>>();
        var currentGroup = new List<FileDiff>();
        var currentTokens = 0;

        foreach (var diff in diffs)
        {
            var tokens = RiskAnalysisPromptBuilder.EstimateTokens(diff.DiffContent);
            if (currentTokens + tokens > maxTokens && currentGroup.Count > 0)
            {
                groups.Add(currentGroup);
                currentGroup = new List<FileDiff>();
                currentTokens = 0;
            }
            currentGroup.Add(diff);
            currentTokens += tokens;
        }

        if (currentGroup.Count > 0)
            groups.Add(currentGroup);

        return groups;
    }

    /// <summary>
    /// JSON 反序列化用的 DTO
    /// </summary>
    private sealed record RiskFindingDto
    {
        public string Scenario { get; init; } = "";
        public string RiskLevel { get; init; } = "";
        public string Description { get; init; } = "";
        public string AffectedFile { get; init; } = "";
        public string DiffSnippet { get; init; } = "";
        public List<string>? PotentiallyAffectedProjects { get; init; }
        public string RecommendedAction { get; init; } = "";
    }
}
