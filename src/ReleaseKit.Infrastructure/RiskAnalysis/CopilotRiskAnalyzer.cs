using GitHub.Copilot.SDK;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Infrastructure.RiskAnalysis.Models;

namespace ReleaseKit.Infrastructure.RiskAnalysis;

/// <summary>
/// 使用 GitHub Copilot SDK 實作的風險分析服務
/// </summary>
/// <remarks>
/// 透過 Copilot SDK 建立 session，依序執行初篩、深度分析與跨服務影響分析，
/// 由 AI 模型分析後回傳結構化的風險評估結果。
/// </remarks>
public class CopilotRiskAnalyzer : IRiskAnalyzer
{
    private readonly IOptions<CopilotOptions> _options;
    private readonly ILogger<CopilotRiskAnalyzer> _logger;

    /// <summary>
    /// Phase 2 初篩系統提示詞，指示 AI 模型分析 PR diff 摘要並辨識潛在風險
    /// </summary>
    internal const string ScreeningSystemPrompt = """
        你是一個程式碼風險分析助手。你的任務是分析 Pull Request 的 diff 摘要，辨識潛在風險。

        【最重要】你的回應必須只有純 JSON 陣列，禁止包含任何文字說明、解釋、markdown 格式或 code block 標記。

        對每個 PR，回傳一個 JSON 物件，包含以下欄位：
        - prId: string (PR 識別碼)
        - repositoryName: string (Repository 名稱)
        - riskLevel: string ("None" | "Low" | "Medium" | "High" | "Critical")
        - riskCategories: string[] (從以下選擇: "CrossServiceApiBreaking", "SharedLibraryChange", "DatabaseSchemaChange", "DatabaseDataChange", "ConfigurationChange", "SecurityChange", "PerformanceChange", "CoreBusinessLogicChange")
        - riskDescription: string (風險描述，繁體中文)
        - needsDeepAnalysis: boolean (是否需要深度分析)
        - affectedComponents: string[] (受影響的元件)
        - suggestedAction: string (建議行動，繁體中文)

        風險判斷規則：
        1. API endpoint 修改（路由、request/response 格式）→ CrossServiceApiBreaking, High
        2. 共用套件版本升級 → SharedLibraryChange, Medium
        3. Migration/Schema 變更 → DatabaseSchemaChange, High
        4. INSERT/UPDATE/DELETE/SaveChanges 邏輯變更 → DatabaseDataChange, High
        5. appsettings/環境變數異動 → ConfigurationChange, Medium
        6. 認證/授權邏輯修改 → SecurityChange, Critical
        7. 快取/查詢效能相關 → PerformanceChange, Medium
        8. 金流/訂單/核心計算邏輯 → CoreBusinessLogicChange, Critical
        9. 純文件/測試/格式化 → None
        """;

    /// <summary>
    /// Phase 3 深度分析系統提示詞，指示 AI 模型結合完整程式碼上下文進行精確風險評估
    /// </summary>
    internal const string DeepAnalysisSystemPrompt = """
        你是一個資深程式碼審查員。你的任務是深度分析高風險的程式碼變更，結合完整的程式碼上下文進行評估。

        【最重要】你的回應必須只有純 JSON 陣列，禁止包含任何文字說明。

        對每個 PR，回傳一個 JSON 物件（與 Phase 2 格式相同），但基於完整的程式碼上下文進行更精確的風險評估。

        深度分析重點：
        1. 變更是否影響公開 API 契約
        2. 資料庫操作是否有潛在的資料一致性問題
        3. 是否有未處理的 null reference 或型別轉換風險
        4. 商業邏輯變更是否可能產生計算錯誤
        5. 安全性漏洞（SQL injection、XSS、權限繞過）
        6. 效能退化（N+1 查詢、缺少索引、記憶體洩漏）
        """;

    /// <summary>
    /// Phase 4 跨服務影響分析系統提示詞，指示 AI 模型找出跨服務的風險關聯
    /// </summary>
    internal const string CrossServiceSystemPrompt = """
        你是一個微服務架構分析師。你的任務是分析多個服務的風險評估結果，找出跨服務的風險關聯。

        【最重要】你的回應必須只有純 JSON 陣列，禁止包含任何文字說明。

        回傳的 JSON 陣列中每個物件包含：
        - sourceService: string (發起變更的服務名稱)
        - affectedServices: string[] (受影響的服務)
        - riskLevel: string ("None" | "Low" | "Medium" | "High" | "Critical")
        - impactDescription: string (影響描述，繁體中文)
        - suggestedAction: string (建議行動，繁體中文)
        - relatedPrIds: string[] (相關的 PR ID)

        分析重點：
        1. 服務 A 修改了 API，服務 B 是否有呼叫該 API
        2. 共用 NuGet 套件版本是否一致
        3. 資料庫 Schema 變更是否影響多個服務
        4. 設定檔變更是否需要跨服務同步部署
        """;

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
    /// Phase 2：批次初篩 PR 風險分類
    /// </summary>
    /// <param name="inputs">待初篩的 PR 資訊清單</param>
    /// <returns>每個 PR 的風險評估結果</returns>
    public async Task<IReadOnlyList<PullRequestRisk>> ScreenRisksAsync(
        IReadOnlyList<ScreenRiskInput> inputs)
    {
        if (inputs.Count == 0)
        {
            return Array.Empty<PullRequestRisk>();
        }

        _logger.LogInformation("使用 Copilot SDK 初篩 {Count} 個 PR 的風險，模型: {Model}",
            inputs.Count, _options.Value.Model);

        var promptData = inputs.Select(i => new
        {
            i.PrId,
            i.RepositoryName,
            i.PrTitle,
            i.DiffSummary
        }).ToJson();
        var prompt = $"請分析以下 PR 的風險：\n{promptData}";

        var responseContent = await SendCopilotRequestAsync(ScreeningSystemPrompt, prompt);
        if (string.IsNullOrWhiteSpace(responseContent))
        {
            _logger.LogWarning("Copilot 初篩回傳空白回應，使用回退結果");
            return inputs.Select(CreateScreenFallback).ToList();
        }

        var parsed = ParseScreenRiskResponse(responseContent);
        if (parsed == null || parsed.Count == 0)
        {
            _logger.LogWarning("Copilot 初篩回應解析失敗，使用回退結果");
            return inputs.Select(CreateScreenFallback).ToList();
        }

        return inputs.Select(input =>
        {
            var match = parsed.FirstOrDefault(r =>
                r.PrId == input.PrId && r.RepositoryName == input.RepositoryName);
            return match != null
                ? MapToRisk(match, input)
                : CreateScreenFallback(input);
        }).ToList();
    }

    /// <summary>
    /// Phase 3：深度分析高風險 PR
    /// </summary>
    /// <param name="inputs">待深度分析的 PR 資訊清單</param>
    /// <returns>每個 PR 的深度風險評估結果</returns>
    public async Task<IReadOnlyList<PullRequestRisk>> DeepAnalyzeAsync(
        IReadOnlyList<DeepAnalyzeInput> inputs)
    {
        if (inputs.Count == 0)
        {
            return Array.Empty<PullRequestRisk>();
        }

        _logger.LogInformation("使用 Copilot SDK 深度分析 {Count} 個 PR 的風險，模型: {Model}",
            inputs.Count, _options.Value.Model);

        var promptData = inputs.Select(i => new
        {
            i.PrId,
            i.RepositoryName,
            i.InitialRiskSummary,
            i.FullContext
        }).ToJson();
        var prompt = $"請深度分析以下 PR 的風險：\n{promptData}";

        var responseContent = await SendCopilotRequestAsync(DeepAnalysisSystemPrompt, prompt);
        if (string.IsNullOrWhiteSpace(responseContent))
        {
            _logger.LogWarning("Copilot 深度分析回傳空白回應，使用回退結果");
            return inputs.Select(CreateDeepFallback).ToList();
        }

        var parsed = ParseScreenRiskResponse(responseContent);
        if (parsed == null || parsed.Count == 0)
        {
            _logger.LogWarning("Copilot 深度分析回應解析失敗，使用回退結果");
            return inputs.Select(CreateDeepFallback).ToList();
        }

        return inputs.Select(input =>
        {
            var match = parsed.FirstOrDefault(r =>
                r.PrId == input.PrId && r.RepositoryName == input.RepositoryName);
            return match != null
                ? MapToDeepRisk(match, input)
                : CreateDeepFallback(input);
        }).ToList();
    }

    /// <summary>
    /// Phase 4：跨服務影響分析
    /// </summary>
    /// <param name="input">跨服務分析的輸入資料</param>
    /// <returns>跨服務風險關聯清單</returns>
    public async Task<IReadOnlyList<CrossServiceRisk>> AnalyzeCrossServiceImpactAsync(
        CrossServiceAnalysisInput input)
    {
        if (input.AllRisks.Count == 0)
        {
            return Array.Empty<CrossServiceRisk>();
        }

        _logger.LogInformation("使用 Copilot SDK 分析 {Count} 個 PR 的跨服務風險，模型: {Model}",
            input.AllRisks.Count, _options.Value.Model);

        var promptData = new
        {
            risks = input.AllRisks.Select(r => new
            {
                r.PrId,
                r.RepositoryName,
                RiskLevel = r.RiskLevel.ToString(),
                RiskCategories = r.RiskCategories.Select(c => c.ToString()).ToList(),
                r.RiskDescription,
                r.AffectedComponents
            }),
            input.ServiceDependencyContext
        }.ToJson();
        var prompt = $"請分析以下各服務 PR 的跨服務風險關聯：\n{promptData}";

        var responseContent = await SendCopilotRequestAsync(CrossServiceSystemPrompt, prompt);
        if (string.IsNullOrWhiteSpace(responseContent))
        {
            _logger.LogWarning("Copilot 跨服務分析回傳空白回應，回傳空結果");
            return Array.Empty<CrossServiceRisk>();
        }

        var parsed = ParseCrossServiceResponse(responseContent);
        if (parsed == null || parsed.Count == 0)
        {
            _logger.LogWarning("Copilot 跨服務分析回應解析失敗，回傳空結果");
            return Array.Empty<CrossServiceRisk>();
        }

        return parsed.Select(MapToCrossServiceRisk).ToList();
    }

    /// <summary>
    /// 發送請求至 Copilot SDK 並取得回應內容
    /// </summary>
    private async Task<string?> SendCopilotRequestAsync(string systemPrompt, string prompt)
    {
        var token = _options.Value.GitHubToken;
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

        _logger.LogInformation("Copilot SDK 身份驗證成功（登入帳號: {Login}）", authStatus.Login);

        await using var session = await client.CreateSessionAsync(new SessionConfig
        {
            Model = _options.Value.Model,
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
            Prompt = prompt
        }, timeout: TimeSpan.FromSeconds(_options.Value.TimeoutSeconds));

        var responseContent = response?.Data?.Content;
        if (string.IsNullOrWhiteSpace(responseContent))
        {
            return null;
        }

        _logger.LogDebug("Copilot 回應: {Response}", responseContent);
        return responseContent;
    }

    /// <summary>
    /// 解析 AI 風險初篩/深度分析回應為 ScreenRiskResponse 清單
    /// </summary>
    internal static List<ScreenRiskResponse>? ParseScreenRiskResponse(string responseContent)
    {
        var cleaned = CleanMarkdownCodeBlock(responseContent);
        try
        {
            return cleaned.ToTypedObject<List<ScreenRiskResponse>>();
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// 解析 AI 跨服務風險分析回應為 CrossServiceRiskResponse 清單
    /// </summary>
    internal static List<CrossServiceRiskResponse>? ParseCrossServiceResponse(string responseContent)
    {
        var cleaned = CleanMarkdownCodeBlock(responseContent);
        try
        {
            return cleaned.ToTypedObject<List<CrossServiceRiskResponse>>();
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// 清理回應中的 markdown code block 標記
    /// </summary>
    internal static string CleanMarkdownCodeBlock(string content)
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
    /// 將 AI 回應對應至 PullRequestRisk 領域實體（初篩用）
    /// </summary>
    internal static PullRequestRisk MapToRisk(ScreenRiskResponse response, ScreenRiskInput input)
    {
        return new PullRequestRisk
        {
            PrId = input.PrId,
            RepositoryName = input.RepositoryName,
            PrTitle = input.PrTitle,
            PrUrl = input.PrUrl,
            RiskLevel = Enum.TryParse<RiskLevel>(response.RiskLevel, out var level)
                ? level
                : RiskLevel.Medium,
            RiskCategories = response.RiskCategories
                .Select(c => Enum.TryParse<RiskCategory>(c, out var cat) ? cat : (RiskCategory?)null)
                .Where(c => c.HasValue)
                .Select(c => c!.Value)
                .ToList(),
            RiskDescription = response.RiskDescription,
            NeedsDeepAnalysis = response.NeedsDeepAnalysis,
            AffectedComponents = response.AffectedComponents,
            SuggestedAction = response.SuggestedAction
        };
    }

    /// <summary>
    /// 將 AI 回應對應至 PullRequestRisk 領域實體（深度分析用）
    /// </summary>
    internal static PullRequestRisk MapToDeepRisk(ScreenRiskResponse response, DeepAnalyzeInput input)
    {
        return new PullRequestRisk
        {
            PrId = input.PrId,
            RepositoryName = input.RepositoryName,
            RiskLevel = Enum.TryParse<RiskLevel>(response.RiskLevel, out var level)
                ? level
                : RiskLevel.Medium,
            RiskCategories = response.RiskCategories
                .Select(c => Enum.TryParse<RiskCategory>(c, out var cat) ? cat : (RiskCategory?)null)
                .Where(c => c.HasValue)
                .Select(c => c!.Value)
                .ToList(),
            RiskDescription = response.RiskDescription,
            NeedsDeepAnalysis = response.NeedsDeepAnalysis,
            AffectedComponents = response.AffectedComponents,
            SuggestedAction = response.SuggestedAction
        };
    }

    /// <summary>
    /// 將 AI 回應對應至 CrossServiceRisk 領域實體
    /// </summary>
    internal static CrossServiceRisk MapToCrossServiceRisk(CrossServiceRiskResponse response)
    {
        return new CrossServiceRisk
        {
            SourceService = response.SourceService,
            AffectedServices = response.AffectedServices,
            RiskLevel = Enum.TryParse<RiskLevel>(response.RiskLevel, out var level)
                ? level
                : RiskLevel.Medium,
            ImpactDescription = response.ImpactDescription,
            SuggestedAction = response.SuggestedAction,
            RelatedPrIds = response.RelatedPrIds
        };
    }

    /// <summary>
    /// 初篩失敗時的回退結果
    /// </summary>
    internal static PullRequestRisk CreateScreenFallback(ScreenRiskInput input)
    {
        return new PullRequestRisk
        {
            PrId = input.PrId,
            RepositoryName = input.RepositoryName,
            PrTitle = input.PrTitle,
            PrUrl = input.PrUrl,
            RiskLevel = RiskLevel.Medium,
            RiskCategories = new List<RiskCategory>(),
            RiskDescription = "AI 分析失敗，建議人工審查",
            NeedsDeepAnalysis = true,
            AffectedComponents = new List<string>(),
            SuggestedAction = "建議人工審查此 PR 的變更內容"
        };
    }

    /// <summary>
    /// 深度分析失敗時的回退結果
    /// </summary>
    internal static PullRequestRisk CreateDeepFallback(DeepAnalyzeInput input)
    {
        return new PullRequestRisk
        {
            PrId = input.PrId,
            RepositoryName = input.RepositoryName,
            RiskLevel = RiskLevel.Medium,
            RiskCategories = new List<RiskCategory>(),
            RiskDescription = "AI 分析失敗，建議人工審查",
            NeedsDeepAnalysis = true,
            AffectedComponents = new List<string>(),
            SuggestedAction = "建議人工審查此 PR 的變更內容"
        };
    }
}
