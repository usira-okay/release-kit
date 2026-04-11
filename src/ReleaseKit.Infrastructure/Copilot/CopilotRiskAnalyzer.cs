using System.ComponentModel;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Infrastructure.Copilot;

/// <summary>
/// 使用 GitHub Copilot SDK 實作的風險分析服務（Agentic 模式）
/// </summary>
/// <remarks>
/// 建立 Copilot session 並註冊 run_command 工具，
/// 讓 AI 自主決定要執行什麼 shell 指令來探索 repo 並分析風險。
/// </remarks>
public class CopilotRiskAnalyzer : IRiskAnalyzer
{
    private readonly IOptions<CopilotOptions> _copilotOptions;
    private readonly IOptions<RiskAnalysisOptions> _riskOptions;
    private readonly IShellCommandExecutor _shellExecutor;
    private readonly INow _now;
    private readonly ILogger<CopilotRiskAnalyzer> _logger;

    /// <summary>
    /// Agentic 分析系統提示詞
    /// </summary>
    internal const string AgenticSystemPrompt = """
        你是一位資深軟體架構師，專精於微服務架構風險分析。

        ## 你的任務
        分析指定專案的 commit 變更，識別所有可能影響其他服務的風險。

        ## 可用工具
        你可以使用 `run_command` 工具在 repo 目錄中執行任意 shell 指令。
        - 建議先用 `git log`、`git diff`、`git show` 了解變更範圍
        - 可用 `grep`、`cat`、`find` 等深入檢查特定檔案
        - 每次指令輸出最多 {maxOutputChars} 字元，請自行用 | head、| tail、| grep 控制輸出量

        ## 風險類別
        1. API 契約變更 (ApiContract) — Controller endpoint、Request/Response 模型
        2. DB Schema 變更 (DatabaseSchema) — Migration、SQL、Entity 欄位
        3. DB 資料異動 (DatabaseData)【重點分析】— Seed data、Lookup table、預設值、Stored Procedure
        4. 事件/訊息格式變更 (EventFormat) — Event class
        5. 設定檔變更 (Configuration) — appsettings、環境變數

        ## 【最重要】分析重點
        - 「改 A 壞 B」情境：資料異動可能導致其他服務的 switch/case、LINQ 查詢、硬編碼值失效
        - Lookup table 新增/修改值 → 消費端可能沒有對應處理
        - Stored Procedure 參數變更 → 呼叫端可能傳錯參數

        ## 輸出格式
        你的最終回應必須是純 JSON（禁止 markdown code block），格式如下：
        {
          "riskItems": [
            {
              "category": "ApiContract|DatabaseSchema|DatabaseData|EventFormat|Configuration",
              "level": "High|Medium|Low",
              "changeSummary": "變更摘要（繁體中文）",
              "affectedFiles": ["file1.cs", "file2.cs"],
              "potentiallyAffectedServices": ["ServiceA", "ServiceB"],
              "impactDescription": "影響說明（繁體中文）",
              "suggestedValidationSteps": ["驗證步驟1", "驗證步驟2"]
            }
          ],
          "summary": "整體分析摘要（繁體中文）",
          "analysisLog": "你執行了哪些指令、為什麼執行這些指令的簡要說明（繁體中文）"
        }
        """;

    /// <summary>
    /// 最終報告系統提示詞
    /// </summary>
    internal const string FinalReportSystemPrompt = """
        將以下風險分析結果彙整成一份完整的 Release 風險評估報告（Markdown 格式，繁體中文）。

        報告結構：
        # Release 風險評估報告
        ## 風險摘要
        ## 🔴 高風險項目
        ## 🟡 中風險項目
        ## 🟢 低風險項目
        ## 跨專案影響矩陣
        ## 建議的測試計畫
        ## 附錄
        """;

    /// <summary>
    /// 初始化 <see cref="CopilotRiskAnalyzer"/> 類別的新執行個體
    /// </summary>
    public CopilotRiskAnalyzer(
        IOptions<CopilotOptions> copilotOptions,
        IOptions<RiskAnalysisOptions> riskOptions,
        IShellCommandExecutor shellExecutor,
        INow now,
        ILogger<CopilotRiskAnalyzer> logger)
    {
        _copilotOptions = copilotOptions;
        _riskOptions = riskOptions;
        _shellExecutor = shellExecutor;
        _now = now;
        _logger = logger;
    }

    /// <summary>
    /// 分析單一專案的變更風險（Agentic 模式）
    /// </summary>
    public async Task<RiskAnalysisReport> AnalyzeProjectRiskAsync(
        ProjectAnalysisContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("開始 agentic 分析專案 {ProjectName}，共 {Count} 個 commit",
            context.ProjectName, context.CommitShas.Count);

        var systemPrompt = AgenticSystemPrompt
            .Replace("{maxOutputChars}", _riskOptions.Value.MaxOutputCharacters.ToString());

        var userPrompt = BuildUserPrompt(context);
        var timeout = TimeSpan.FromSeconds(_riskOptions.Value.CommandTimeoutSeconds);

        // 例外處理：外部 AI 呼叫需要明確的錯誤恢復邏輯
        try
        {
            var responseContent = await SendAgenticRequestAsync(
                systemPrompt, userPrompt, context.RepoPath, timeout, cancellationToken);

            if (string.IsNullOrWhiteSpace(responseContent))
            {
                _logger.LogWarning("Copilot 回傳空白回應，專案: {ProjectName}", context.ProjectName);
                return CreateEmptyReport(context.ProjectName);
            }

            var report = ParseProjectRiskResponse(responseContent, context.ProjectName, _now.UtcNow);
            if (report is null)
            {
                _logger.LogWarning("無法解析 Copilot 回應，專案: {ProjectName}", context.ProjectName);
                return CreateEmptyReport(context.ProjectName);
            }

            _logger.LogInformation("專案 {ProjectName} agentic 分析完成，識別到 {Count} 項風險",
                context.ProjectName, report.RiskItems.Count);
            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Copilot agentic 分析失敗，專案: {ProjectName}", context.ProjectName);
            return CreateEmptyReport(context.ProjectName);
        }
    }

    /// <summary>
    /// 產生最終整合報告 Markdown
    /// </summary>
    public async Task<string> GenerateFinalReportAsync(
        IReadOnlyList<RiskAnalysisReport> reports,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("開始產生最終風險報告，共 {Count} 份中間報告", reports.Count);

        var userPrompt = BuildFinalReportUserPrompt(reports);

        // 例外處理：外部 AI 呼叫需要明確的錯誤恢復邏輯
        try
        {
            var responseContent = await SendSimpleRequestAsync(
                FinalReportSystemPrompt, userPrompt, cancellationToken);

            return responseContent ?? "# 風險報告產生失敗\n\n無法從 AI 取得分析結果。";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "最終報告產生失敗");
            return "# 風險報告產生失敗\n\n產生過程發生錯誤。";
        }
    }

    /// <summary>
    /// 解析 Copilot 回應為 RiskAnalysisReport
    /// </summary>
    internal static RiskAnalysisReport? ParseProjectRiskResponse(
        string content,
        string projectName,
        DateTimeOffset analyzedAt)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        var cleaned = CleanMarkdownWrapper(content);

        try
        {
            var dto = cleaned.ToTypedObject<ProjectRiskResponseDto>();
            if (dto is null)
                return null;

            return new RiskAnalysisReport
            {
                Sequence = 0,
                ProjectName = projectName,
                RiskItems = dto.RiskItems ?? [],
                Summary = dto.Summary ?? string.Empty,
                AnalysisLog = dto.AnalysisLog,
                AnalyzedAt = analyzedAt
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 建構 agentic 分析使用者提示詞
    /// </summary>
    internal static string BuildUserPrompt(ProjectAnalysisContext context)
    {
        var shas = string.Join("\n", context.CommitShas.Select(s => $"  - {s}"));
        return $"""
            請分析專案 "{context.ProjectName}" 的以下 commit 變更：

            Repo 路徑: {context.RepoPath}
            Commit SHAs:
            {shas}

            請使用 run_command 工具探索 repo，分析這些 commit 的變更風險。
            工作目錄請使用: {context.RepoPath}
            """;
    }

    /// <summary>
    /// 清除 Markdown 代碼區塊包裝
    /// </summary>
    internal static string CleanMarkdownWrapper(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.StartsWith("```"))
        {
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0)
                trimmed = trimmed[(firstNewline + 1)..];

            if (trimmed.EndsWith("```"))
                trimmed = trimmed[..^3];

            return trimmed.Trim();
        }

        return trimmed;
    }

    /// <summary>
    /// 發送 agentic Copilot 請求（含 run_command 工具）
    /// </summary>
    private async Task<string?> SendAgenticRequestAsync(
        string systemPrompt,
        string userPrompt,
        string repoPath,
        TimeSpan commandTimeout,
        CancellationToken cancellationToken)
    {
        var model = _copilotOptions.Value.Model;
        var token = _copilotOptions.Value.GitHubToken;

        var clientOptions = new CopilotClientOptions { AutoStart = true };
        if (!string.IsNullOrWhiteSpace(token))
            clientOptions.GitHubToken = token;

        await using var client = new CopilotClient(clientOptions);

        var authStatus = await client.GetAuthStatusAsync();
        if (authStatus is not { IsAuthenticated: true })
        {
            _logger.LogError("Copilot SDK 身份驗證失敗");
            return null;
        }

        var maxOutputChars = _riskOptions.Value.MaxOutputCharacters;
        var runCommandTool = AIFunctionFactory.Create(
            async ([Description("要執行的 shell 指令")] string command,
                   [Description("工作目錄路徑")] string workingDirectory) =>
            {
                _logger.LogDebug("run_command: {Command} in {Dir}", command, workingDirectory);
                var result = await _shellExecutor.ExecuteAsync(
                    command, workingDirectory, commandTimeout, cancellationToken);

                var output = result.StandardOutput;
                if (output.Length > maxOutputChars)
                    output = output[..maxOutputChars] +
                             $"\n\n[輸出已截斷，共 {output.Length} 字元，僅顯示前 {maxOutputChars} 字元]";

                if (result.TimedOut)
                    return $"[指令超時] stderr: {result.StandardError}";

                if (result.ExitCode != 0)
                    return $"[exit code: {result.ExitCode}]\nstdout:\n{output}\nstderr:\n{result.StandardError}";

                return output;
            },
            name: "run_command",
            description: "在指定的 repo 目錄中執行 shell 指令，回傳 stdout 與 stderr");

        await using var session = await client.CreateSessionAsync(new SessionConfig
        {
            Model = model,
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Replace,
                Content = systemPrompt
            },
            Tools = [runCommandTool],
            InfiniteSessions = new InfiniteSessionConfig { Enabled = false },
            OnPermissionRequest = PermissionHandler.ApproveAll
        });

        _logger.LogDebug("Agentic session 已建立，發送分析請求");

        var response = await session.SendAndWaitAsync(new MessageOptions
        {
            Prompt = userPrompt
        }, timeout: TimeSpan.FromSeconds(_copilotOptions.Value.TimeoutSeconds));

        return response?.Data?.Content;
    }

    /// <summary>
    /// 發送簡單 Copilot 請求（不含工具）
    /// </summary>
    private async Task<string?> SendSimpleRequestAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken)
    {
        var model = _copilotOptions.Value.Model;
        var token = _copilotOptions.Value.GitHubToken;

        var clientOptions = new CopilotClientOptions { AutoStart = true };
        if (!string.IsNullOrWhiteSpace(token))
            clientOptions.GitHubToken = token;

        await using var client = new CopilotClient(clientOptions);

        var authStatus = await client.GetAuthStatusAsync();
        if (authStatus is not { IsAuthenticated: true })
        {
            _logger.LogError("Copilot SDK 身份驗證失敗");
            return null;
        }

        await using var session = await client.CreateSessionAsync(new SessionConfig
        {
            Model = model,
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Replace,
                Content = systemPrompt
            },
            InfiniteSessions = new InfiniteSessionConfig { Enabled = false },
            OnPermissionRequest = PermissionHandler.ApproveAll
        });

        var response = await session.SendAndWaitAsync(new MessageOptions
        {
            Prompt = userPrompt
        }, timeout: TimeSpan.FromSeconds(_copilotOptions.Value.TimeoutSeconds));

        return response?.Data?.Content;
    }

    /// <summary>建構最終報告使用者提示詞</summary>
    private static string BuildFinalReportUserPrompt(IReadOnlyList<RiskAnalysisReport> reports)
    {
        var reportsJson = reports.ToJson();
        return $"請根據以下風險分析結果產生最終報告：\n{reportsJson}";
    }

    /// <summary>建立空白報告（回退值）</summary>
    private RiskAnalysisReport CreateEmptyReport(string projectName)
    {
        return new RiskAnalysisReport
        {
            Sequence = 0,
            ProjectName = projectName,
            RiskItems = [],
            Summary = "AI 分析失敗，未產生風險報告",
            AnalyzedAt = _now.UtcNow
        };
    }

    /// <summary>回應 DTO</summary>
    private sealed record ProjectRiskResponseDto
    {
        public IReadOnlyList<RiskItem>? RiskItems { get; init; }
        public string? Summary { get; init; }
        public string? AnalysisLog { get; init; }
    }
}
