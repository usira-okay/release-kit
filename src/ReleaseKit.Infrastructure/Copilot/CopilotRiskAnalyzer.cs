using GitHub.Copilot.SDK;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Infrastructure.Copilot;

/// <summary>
/// 使用 GitHub Copilot SDK 實作的風險分析服務
/// </summary>
/// <remarks>
/// 透過 Copilot SDK 建立 session，將 PR diff 資訊送出，
/// 由 AI 模型分析後識別跨服務風險項目。
/// </remarks>
public class CopilotRiskAnalyzer : IRiskAnalyzer
{
    private readonly IOptions<CopilotOptions> _copilotOptions;
    private readonly IOptions<RiskAnalysisOptions> _riskOptions;
    private readonly INow _now;
    private readonly ILogger<CopilotRiskAnalyzer> _logger;

    /// <summary>
    /// Pass 1 系統提示詞：單一專案風險分析
    /// </summary>
    internal const string Pass1SystemPrompt = """
        你是一位資深軟體架構師，專精於微服務架構風險分析。
        分析以下專案的 PR 變更，識別所有可能影響其他服務的風險。

        風險類別：
        1. API 契約變更（Controller endpoint、Request/Response 模型）
        2. DB Schema 變更（Migration、SQL、Entity 欄位）
        3. DB 資料異動（重點分析）（Seed data、Lookup table、預設值、Stored Procedure）
        4. 事件/訊息格式變更（Event class）
        5. 設定檔變更（appsettings、環境變數）

        【最重要】你的回應必須只有純 JSON，禁止包含任何文字說明、解釋、markdown 格式或 code block 標記。

        JSON response format:
        {
          "riskItems": [
            {
              "category": "ApiContract|DatabaseSchema|DatabaseData|EventFormat|Configuration",
              "level": "High|Medium|Low",
              "changeSummary": "變更摘要（繁體中文）",
              "affectedFiles": ["affected/file/path.cs"],
              "potentiallyAffectedServices": ["ServiceName"],
              "sourceProject": "來源專案",
              "affectedProject": "受影響專案",
              "impactDescription": "影響描述（繁體中文）",
              "suggestedValidationSteps": ["驗證步驟"]
            }
          ],
          "summary": "整體分析摘要（繁體中文）"
        }
        """;

    /// <summary>
    /// Pass 2~10 動態分析系統提示詞
    /// </summary>
    /// <remarks>
    /// 包含 {currentPass} 佔位符，使用時需替換為實際層數。
    /// </remarks>
    internal const string DynamicAnalysisSystemPrompt = """
        你是一位資深軟體架構師，專精於微服務架構風險分析。
        當前是第 {currentPass} 層分析（上限 10 層）。

        根據前一層分析結果，進行更深入的跨專案影響分析。
        請決定是否需要繼續更深層分析，並說明理由。

        【最重要】你的回應必須只有純 JSON，禁止包含任何文字說明、解釋、markdown 格式或 code block 標記。

        JSON response format:
        {
          "analysisStrategy": "本層使用的分析策略描述",
          "continueAnalysis": true|false,
          "continueReason": "繼續分析的理由（若 continueAnalysis 為 false 可省略）",
          "riskItems": [
            {
              "category": "ApiContract|DatabaseSchema|DatabaseData|EventFormat|Configuration",
              "level": "High|Medium|Low",
              "changeSummary": "變更摘要（繁體中文）",
              "affectedFiles": ["affected/file/path.cs"],
              "potentiallyAffectedServices": ["ServiceName"],
              "sourceProject": "來源專案",
              "affectedProject": "受影響專案",
              "impactDescription": "影響描述（繁體中文）",
              "suggestedValidationSteps": ["驗證步驟"]
            }
          ],
          "summary": "本層分析摘要（繁體中文）"
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
    /// <param name="copilotOptions">Copilot 組態選項</param>
    /// <param name="riskOptions">風險分析組態選項</param>
    /// <param name="now">時間服務</param>
    /// <param name="logger">日誌記錄器</param>
    public CopilotRiskAnalyzer(
        IOptions<CopilotOptions> copilotOptions,
        IOptions<RiskAnalysisOptions> riskOptions,
        INow now,
        ILogger<CopilotRiskAnalyzer> logger)
    {
        _copilotOptions = copilotOptions;
        _riskOptions = riskOptions;
        _now = now;
        _logger = logger;
    }

    /// <summary>
    /// 分析單一專案的 PR 變更風險（Pass 1）
    /// </summary>
    /// <param name="projectName">專案名稱</param>
    /// <param name="diffs">PR diff 上下文清單</param>
    /// <param name="cancellationToken">取消權杖</param>
    /// <returns>風險分析報告</returns>
    public async Task<RiskAnalysisReport> AnalyzeProjectRiskAsync(
        string projectName,
        IReadOnlyList<PrDiffContext> diffs,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("開始分析專案 {ProjectName} 的風險，共 {Count} 個 PR diff", projectName, diffs.Count);

        var systemPrompt = Pass1SystemPrompt.Replace("{projectName}", projectName);
        var userPrompt = BuildPass1UserPrompt(projectName, diffs);

        // 例外處理：外部 AI 呼叫需要明確的錯誤恢復邏輯
        try
        {
            var responseContent = await SendCopilotRequestAsync(systemPrompt, userPrompt, cancellationToken);

            if (string.IsNullOrWhiteSpace(responseContent))
            {
                _logger.LogWarning("Copilot 回傳空白回應，專案: {ProjectName}", projectName);
                return CreateEmptyReport(projectName);
            }

            var report = ParseProjectRiskResponse(responseContent, projectName, _now.UtcNow, 1);
            if (report is null)
            {
                _logger.LogWarning("無法解析 Copilot 回應，專案: {ProjectName}", projectName);
                return CreateEmptyReport(projectName);
            }

            _logger.LogInformation("專案 {ProjectName} 風險分析完成，識別到 {Count} 項風險",
                projectName, report.RiskItems.Count);
            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Copilot 風險分析失敗，專案: {ProjectName}", projectName);
            return CreateEmptyReport(projectName);
        }
    }

    /// <summary>
    /// 動態深度分析：接收前一層報告，產出下一層分析（Pass 2~10）
    /// </summary>
    /// <param name="currentPass">當前分析層數</param>
    /// <param name="previousPassReports">前一層的分析報告</param>
    /// <param name="cancellationToken">取消權杖</param>
    /// <returns>動態分析結果</returns>
    public async Task<DynamicAnalysisResult> AnalyzeDeepAsync(
        int currentPass,
        IReadOnlyList<RiskAnalysisReport> previousPassReports,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("開始第 {Pass} 層深度分析，前一層報告數: {Count}",
            currentPass, previousPassReports.Count);

        var systemPrompt = DynamicAnalysisSystemPrompt.Replace("{currentPass}", currentPass.ToString());
        var userPrompt = BuildDynamicAnalysisUserPrompt(previousPassReports);

        // 例外處理：外部 AI 呼叫需要明確的錯誤恢復邏輯
        try
        {
            var responseContent = await SendCopilotRequestAsync(systemPrompt, userPrompt, cancellationToken);

            if (string.IsNullOrWhiteSpace(responseContent))
            {
                _logger.LogWarning("Copilot 回傳空白回應，第 {Pass} 層分析", currentPass);
                return CreateEmptyDynamicResult();
            }

            var result = ParseDynamicAnalysisResponse(responseContent, _now.UtcNow, currentPass);
            if (result is null)
            {
                _logger.LogWarning("無法解析 Copilot 回應，第 {Pass} 層分析", currentPass);
                return CreateEmptyDynamicResult();
            }

            _logger.LogInformation("第 {Pass} 層深度分析完成，ContinueAnalysis: {Continue}",
                currentPass, result.ContinueAnalysis);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Copilot 深度分析失敗，第 {Pass} 層", currentPass);
            return CreateEmptyDynamicResult();
        }
    }

    /// <summary>
    /// 產生最終整合報告 Markdown
    /// </summary>
    /// <param name="lastPassReports">最後一層的分析報告</param>
    /// <param name="cancellationToken">取消權杖</param>
    /// <returns>Markdown 格式的最終報告</returns>
    public async Task<string> GenerateFinalReportAsync(
        IReadOnlyList<RiskAnalysisReport> lastPassReports,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("開始產生最終風險評估報告，報告數: {Count}", lastPassReports.Count);

        var userPrompt = BuildFinalReportUserPrompt(lastPassReports);

        // 例外處理：外部 AI 呼叫需要明確的錯誤恢復邏輯
        try
        {
            var responseContent = await SendCopilotRequestAsync(FinalReportSystemPrompt, userPrompt, cancellationToken);

            if (string.IsNullOrWhiteSpace(responseContent))
            {
                _logger.LogWarning("Copilot 回傳空白回應，無法產生最終報告");
                return "## 報告產生失敗\n\nCopilot 回傳空白回應，請重新嘗試。";
            }

            _logger.LogInformation("最終風險評估報告產生完成");
            return responseContent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Copilot 最終報告產生失敗");
            return $"## 報告產生失敗\n\n錯誤訊息：{ex.Message}";
        }
    }

    /// <summary>
    /// 解析 Pass 1 專案風險分析回應
    /// </summary>
    /// <param name="content">AI 回應內容</param>
    /// <param name="projectName">專案名稱</param>
    /// <param name="analyzedAt">分析時間</param>
    /// <param name="sequence">序號</param>
    /// <returns>風險分析報告，解析失敗時回傳 null</returns>
    internal static RiskAnalysisReport? ParseProjectRiskResponse(
        string content,
        string projectName,
        DateTimeOffset analyzedAt,
        int sequence)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var cleaned = CleanMarkdownWrapper(content);

        var parsed = ParseJsonSafe<ProjectRiskResponseDto>(cleaned);
        if (parsed is null)
        {
            return null;
        }

        return new RiskAnalysisReport
        {
            PassKey = new AnalysisPassKey { Pass = 1, Sequence = sequence },
            ProjectName = projectName,
            RiskItems = parsed.RiskItems ?? [],
            Summary = parsed.Summary ?? string.Empty,
            AnalyzedAt = analyzedAt
        };
    }

    /// <summary>
    /// 解析 Pass 2~10 動態分析回應
    /// </summary>
    /// <param name="content">AI 回應內容</param>
    /// <param name="analyzedAt">分析時間</param>
    /// <param name="currentPass">當前分析層數</param>
    /// <returns>動態分析結果，解析失敗時回傳 null</returns>
    internal static DynamicAnalysisResult? ParseDynamicAnalysisResponse(
        string content,
        DateTimeOffset analyzedAt,
        int currentPass)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var cleaned = CleanMarkdownWrapper(content);

        var parsed = ParseJsonSafe<DynamicAnalysisResponseDto>(cleaned);
        if (parsed is null)
        {
            return null;
        }

        var report = new RiskAnalysisReport
        {
            PassKey = new AnalysisPassKey { Pass = currentPass, Sequence = 1 },
            RiskItems = parsed.RiskItems ?? [],
            Summary = parsed.Summary ?? string.Empty,
            AnalyzedAt = analyzedAt
        };

        return new DynamicAnalysisResult
        {
            Reports = [report],
            ContinueAnalysis = parsed.ContinueAnalysis,
            ContinueReason = parsed.ContinueReason,
            AnalysisStrategy = parsed.AnalysisStrategy ?? string.Empty
        };
    }

    /// <summary>
    /// 清除 AI 回應中的 markdown 代碼區塊包裝
    /// </summary>
    /// <param name="content">原始回應內容</param>
    /// <returns>清理後的純 JSON 字串</returns>
    internal static string CleanMarkdownWrapper(string content)
    {
        var cleaned = content.Trim();

        if (cleaned.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned["```json".Length..];
        }
        else if (cleaned.StartsWith("```"))
        {
            cleaned = cleaned["```".Length..];
        }

        if (cleaned.EndsWith("```"))
        {
            cleaned = cleaned[..^"```".Length];
        }

        return cleaned.Trim();
    }

    /// <summary>
    /// 安全解析 JSON，失敗時回傳 null
    /// </summary>
    private static T? ParseJsonSafe<T>(string json) where T : class
    {
        try
        {
            return json.ToTypedObject<T>();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 發送 Copilot SDK 請求
    /// </summary>
    private async Task<string?> SendCopilotRequestAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken)
    {
        var model = _copilotOptions.Value.Model;
        var token = _copilotOptions.Value.GitHubToken;

        var clientOptions = new CopilotClientOptions { AutoStart = true };
        if (!string.IsNullOrWhiteSpace(token))
        {
            clientOptions.GitHubToken = token;
        }

        await using var client = new CopilotClient(clientOptions);

        var authStatus = await client.GetAuthStatusAsync();
        if (authStatus is not { IsAuthenticated: true })
        {
            _logger.LogError(
                "Copilot SDK 身份驗證失敗（IsAuthenticated: {IsAuthenticated}，AuthType: {AuthType}）",
                authStatus?.IsAuthenticated,
                authStatus?.AuthType ?? "unknown");
            return null;
        }

        _logger.LogDebug("Copilot SDK 身份驗證成功（登入帳號: {Login}）", authStatus.Login);

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

        _logger.LogDebug("Copilot session 已建立，正在發送風險分析請求");

        var response = await session.SendAndWaitAsync(new MessageOptions
        {
            Prompt = userPrompt
        }, timeout: TimeSpan.FromSeconds(_copilotOptions.Value.TimeoutSeconds));

        return response?.Data?.Content;
    }

    /// <summary>
    /// 建構 Pass 1 使用者提示詞
    /// </summary>
    private static string BuildPass1UserPrompt(string projectName, IReadOnlyList<PrDiffContext> diffs)
    {
        var diffsSummary = diffs.Select(d => new
        {
            d.Title,
            d.Description,
            d.SourceBranch,
            d.TargetBranch,
            d.AuthorName,
            d.PrUrl,
            d.ChangedFiles,
            d.DiffContent
        }).ToJson();

        return $"請分析專案 \"{projectName}\" 的以下 PR 變更：\n{diffsSummary}";
    }

    /// <summary>
    /// 建構動態分析使用者提示詞
    /// </summary>
    private static string BuildDynamicAnalysisUserPrompt(IReadOnlyList<RiskAnalysisReport> previousReports)
    {
        var reportsJson = previousReports.ToJson();
        return $"以下是前一層的分析結果，請進行更深入的跨專案影響分析：\n{reportsJson}";
    }

    /// <summary>
    /// 建構最終報告使用者提示詞
    /// </summary>
    private static string BuildFinalReportUserPrompt(IReadOnlyList<RiskAnalysisReport> reports)
    {
        var reportsJson = reports.ToJson();
        return $"請根據以下風險分析結果產生最終報告：\n{reportsJson}";
    }

    /// <summary>
    /// 建立空白風險分析報告（作為回退值）
    /// </summary>
    private RiskAnalysisReport CreateEmptyReport(string projectName)
    {
        return new RiskAnalysisReport
        {
            PassKey = new AnalysisPassKey { Pass = 1, Sequence = 1 },
            ProjectName = projectName,
            RiskItems = [],
            Summary = "AI 分析失敗，未產生風險報告",
            AnalyzedAt = _now.UtcNow
        };
    }

    /// <summary>
    /// 建立空白動態分析結果（作為回退值，停止繼續分析）
    /// </summary>
    private static DynamicAnalysisResult CreateEmptyDynamicResult()
    {
        return new DynamicAnalysisResult
        {
            Reports = [],
            ContinueAnalysis = false,
            ContinueReason = "AI 分析失敗，停止繼續分析",
            AnalysisStrategy = string.Empty
        };
    }

    /// <summary>
    /// Pass 1 回應的 DTO（用於 JSON 反序列化）
    /// </summary>
    private sealed record ProjectRiskResponseDto
    {
        public IReadOnlyList<RiskItem>? RiskItems { get; init; }
        public string? Summary { get; init; }
    }

    /// <summary>
    /// 動態分析回應的 DTO（用於 JSON 反序列化）
    /// </summary>
    private sealed record DynamicAnalysisResponseDto
    {
        public string? AnalysisStrategy { get; init; }
        public bool ContinueAnalysis { get; init; }
        public string? ContinueReason { get; init; }
        public IReadOnlyList<RiskItem>? RiskItems { get; init; }
        public string? Summary { get; init; }
    }
}
