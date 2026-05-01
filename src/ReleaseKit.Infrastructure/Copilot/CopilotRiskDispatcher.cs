using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Infrastructure.Copilot;

/// <summary>
/// Copilot SDK 雙層 SubAgent 風險分析調度器
/// </summary>
/// <remarks>
/// SubAgent 1（Dispatcher）：接收各 Commit 的異動量統計，決定分組後呼叫 dispatch_project_analysis 工具。
/// SubAgent 2（Analyzer）：接收一組 CommitSha，逐一呼叫 get_diff 工具取得 diff，完成後直接寫入 Redis。
/// </remarks>
public class CopilotRiskDispatcher : ICopilotRiskDispatcher
{
    private readonly IGitOperationService _gitService;
    private readonly IRedisService _redisService;
    private readonly IOptions<CopilotOptions> _options;
    private readonly ILogger<CopilotRiskDispatcher> _logger;

    /// <summary>
    /// 初始化 <see cref="CopilotRiskDispatcher"/> 類別的新執行個體
    /// </summary>
    public CopilotRiskDispatcher(
        IGitOperationService gitService,
        IRedisService redisService,
        IOptions<CopilotOptions> options,
        ILogger<CopilotRiskDispatcher> logger)
    {
        _gitService = gitService;
        _redisService = redisService;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task DispatchAsync(
        string runId,
        string projectPath,
        IReadOnlyList<CommitSummary> commitSummaries,
        string localPath,
        IReadOnlyList<RiskScenario> scenarios,
        CancellationToken ct = default)
    {
        var clientOptions = BuildClientOptions();

        await using var dispatcherClient = new CopilotClient(clientOptions);

        var authStatus = await dispatcherClient.GetAuthStatusAsync();
        if (authStatus is not { IsAuthenticated: true })
        {
            _logger.LogError("Copilot SDK 身份驗證失敗（IsAuthenticated: {IsAuthenticated}），無法執行風險分析",
                authStatus?.IsAuthenticated);
            return;
        }

        // 定義 dispatch_project_analysis 工具：SubAgent 1 呼叫此工具來派發 SubAgent 2
        var dispatchProjectAnalysis = AIFunctionFactory.Create(
            async ([Description("要分析的 CommitSha 清單")] List<string> commitShas) =>
            {
                return await RunAnalyzerAgentAsync(
                    runId, projectPath, commitShas, localPath, scenarios, clientOptions, ct);
            },
            "dispatch_project_analysis",
            "派發分析任務給 Analyzer SubAgent，分析指定的 CommitSha 清單的程式碼異動風險"
        );

        var dispatcherConfig = new SessionConfig
        {
            Model = _options.Value.Model,
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Replace,
                Content = RiskAnalysisPromptBuilder.BuildDispatcherSystemPrompt()
            },
            Tools = [dispatchProjectAnalysis],
            InfiniteSessions = new InfiniteSessionConfig { Enabled = false },
            OnPermissionRequest = PermissionHandler.ApproveAll
        };

        await using var dispatcherSession = await dispatcherClient.CreateSessionAsync(dispatcherConfig);

        var userPrompt = RiskAnalysisPromptBuilder.BuildDispatcherUserPrompt(projectPath, commitSummaries);
        var timeout = TimeSpan.FromSeconds(_options.Value.TimeoutSeconds);

        await dispatcherSession.SendAndWaitAsync(new MessageOptions { Prompt = userPrompt }, timeout: timeout);

        _logger.LogInformation("專案 {ProjectPath} Dispatcher SubAgent 完成", projectPath);
    }

    /// <summary>
    /// 建立並執行 Analyzer SubAgent（SubAgent 2），分析指定的 CommitSha 清單
    /// </summary>
    private async Task<string> RunAnalyzerAgentAsync(
        string runId,
        string projectPath,
        IReadOnlyList<string> commitShas,
        string localPath,
        IReadOnlyList<RiskScenario> scenarios,
        CopilotClientOptions clientOptions,
        CancellationToken ct)
    {
        // 定義 get_diff 工具：Analyzer SubAgent 呼叫此工具按需取得 commit 的完整 diff
        var getDiff = AIFunctionFactory.Create(
            async ([Description("要取得 diff 的 CommitSha")] string commitSha) =>
            {
                var result = await _gitService.GetCommitRawDiffAsync(localPath, commitSha, ct);
                return result.IsSuccess ? result.Value! : $"錯誤: {result.Error!.Message}";
            },
            "get_diff",
            "取得指定 CommitSha 的完整 diff 內容（unified diff 格式），用於分析具體的程式碼變更"
        );

        await using var analyzerClient = new CopilotClient(clientOptions);

        var analyzerConfig = new SessionConfig
        {
            Model = _options.Value.Model,
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Replace,
                Content = RiskAnalysisPromptBuilder.BuildAnalyzerSystemPrompt()
            },
            Tools = [getDiff],
            InfiniteSessions = new InfiniteSessionConfig { Enabled = false },
            OnPermissionRequest = PermissionHandler.ApproveAll
        };

        await using var analyzerSession = await analyzerClient.CreateSessionAsync(analyzerConfig);

        var userPrompt = RiskAnalysisPromptBuilder.BuildAnalyzerUserPrompt(projectPath, commitShas, scenarios);
        var timeout = TimeSpan.FromSeconds(_options.Value.TimeoutSeconds);

        var response = await analyzerSession.SendAndWaitAsync(
            new MessageOptions { Prompt = userPrompt }, timeout: timeout);

        var content = response?.Data?.Content;

        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("Analyzer SubAgent 回傳空白，重試一次: {ProjectPath}", projectPath);
            response = await analyzerSession.SendAndWaitAsync(
                new MessageOptions { Prompt = userPrompt }, timeout: timeout);
            content = response?.Data?.Content;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("Analyzer SubAgent 重試後仍為空白: {ProjectPath}", projectPath);
            return "分析失敗：回應為空";
        }

        var newFindings = ParseFindings(content, projectPath);

        // 累積寫入 Redis Stage 4（多次 tool 呼叫結果合併至同一 key）
        var existingJson = await _redisService.HashGetAsync(
            RiskAnalysisRedisKeys.Stage4Hash(runId), projectPath);

        var allFindings = new List<RiskFinding>();
        var sessionCount = 1;

        if (!string.IsNullOrEmpty(existingJson))
        {
            var existing = existingJson.ToTypedObject<ProjectRiskAnalysis>();
            if (existing != null)
            {
                allFindings.AddRange(existing.Findings);
                sessionCount += existing.SessionCount;
            }
        }

        allFindings.AddRange(newFindings);

        var analysis = new ProjectRiskAnalysis
        {
            ProjectPath = projectPath,
            Findings = allFindings,
            SessionCount = sessionCount
        };

        await _redisService.HashSetAsync(
            RiskAnalysisRedisKeys.Stage4Hash(runId),
            projectPath,
            analysis.ToJson());

        _logger.LogInformation(
            "Analyzer SubAgent 完成: {ProjectPath}, {FindingCount} 個風險, {CommitCount} 個 commit",
            projectPath, newFindings.Count, commitShas.Count);

        return $"分析完成：{newFindings.Count} 個風險發現";
    }

    /// <summary>
    /// 解析 AI 回傳的 JSON 為 RiskFinding 清單
    /// </summary>
    internal List<RiskFinding> ParseFindings(string response, string projectPath)
    {
        var json = ExtractJsonBlock(response);

        if (string.IsNullOrWhiteSpace(json))
        {
            _logger.LogWarning("無法擷取 JSON 代碼塊，專案 {ProjectPath}，原始回應: {Response}",
                projectPath, response);
            return [];
        }

        var serializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        List<RiskFindingDto>? items;
        try
        {
            items = JsonSerializer.Deserialize<List<RiskFindingDto>>(json, serializerOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Copilot 風險分析回應解析失敗，專案 {ProjectPath}，原始回應: {Response}",
                projectPath, response);
            return [];
        }

        if (items == null)
        {
            _logger.LogWarning("Copilot 風險分析回應解析結果為 null，專案 {ProjectPath}", projectPath);
            return [];
        }

        return items.Select(dto => new RiskFinding
        {
            Scenario = Enum.Parse<RiskScenario>(dto.Scenario, ignoreCase: true),
            RiskLevel = Enum.Parse<RiskLevel>(dto.RiskLevel, ignoreCase: true),
            Description = dto.Description,
            AffectedFile = dto.AffectedFile,
            DiffSnippet = dto.DiffSnippet,
            PotentiallyAffectedProjects = dto.PotentiallyAffectedProjects ?? [],
            RecommendedAction = dto.RecommendedAction,
            ChangedBy = string.Empty
        }).ToList();
    }

    /// <summary>
    /// 從回應中擷取 ```json...``` 代碼塊的 JSON 內容
    /// </summary>
    private static string ExtractJsonBlock(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return string.Empty;

        var pattern = @"```(?:json)?\s*\n?(.*?)\n?```";
        var match = Regex.Match(response, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    /// <summary>
    /// 建立 CopilotClient 選項
    /// </summary>
    private CopilotClientOptions BuildClientOptions()
    {
        var clientOptions = new CopilotClientOptions { AutoStart = true };
        if (!string.IsNullOrWhiteSpace(_options.Value.GitHubToken))
            clientOptions.GitHubToken = _options.Value.GitHubToken;
        return clientOptions;
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
