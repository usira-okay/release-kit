using System.Text.Json;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Infrastructure.Copilot;

/// <summary>
/// 使用 GitHub Copilot SDK 實作的標題增強服務
/// </summary>
/// <remarks>
/// 透過 Copilot SDK 建立 session，將所有候選標題批次送出，
/// 由 AI 模型分析後回傳更有意義的標題。
/// </remarks>
public class CopilotTitleEnhancer : ITitleEnhancer
{
    private readonly IOptions<CopilotOptions> _options;
    private readonly ILogger<CopilotTitleEnhancer> _logger;

    /// <summary>
    /// 系統提示詞，指示 AI 模型從候選標題中選擇最適合的
    /// </summary>
    internal const string SystemPrompt = """
        你是一個 Release Notes 標題選擇助手。
        你的任務是根據提供的候選標題資訊，為每個項目從候選標題中選出一個最適合作為 Release Notes 標題的選項。

        規則：
        1. 你必須從候選標題中選擇一個，不可自行修改或產生新標題
        2. 選擇最能描述該項目實際變更內容的標題
        3. 優先選擇有意義的描述性標題，避免選擇如 "Update README.md"、"Fix typo" 等無意義標題
        4. 僅回傳 JSON 陣列，不要包含任何額外說明或 markdown 格式
        5. JSON 陣列中的每個元素為選出的標題字串，順序與輸入一致

        範例輸入：
        [["Update README.md", "新增登入功能", "feature/VSTS100-add-login"], ["Fix typo", "修正認證錯誤"]]

        範例輸出：
        ["新增登入功能","修正認證錯誤"]
        """;

    /// <summary>
    /// 初始化 <see cref="CopilotTitleEnhancer"/> 類別的新執行個體
    /// </summary>
    /// <param name="options">Copilot 組態選項</param>
    /// <param name="logger">日誌記錄器</param>
    public CopilotTitleEnhancer(
        IOptions<CopilotOptions> options,
        ILogger<CopilotTitleEnhancer> logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// 批次增強標題
    /// </summary>
    /// <param name="titleGroups">每個項目的候選標題清單</param>
    /// <returns>增強後的標題清單</returns>
    public async Task<IReadOnlyList<string>> EnhanceTitlesAsync(IReadOnlyList<IReadOnlyList<string>> titleGroups)
    {
        if (titleGroups.Count == 0)
        {
            return Array.Empty<string>();
        }

        var model = _options.Value.Model;
        _logger.LogInformation("使用 Copilot SDK 增強 {Count} 個標題，模型: {Model}", titleGroups.Count, model);

        // 建立輸入 JSON
        var inputJson = titleGroups.ToJson();
        var prompt = $"請從以下候選標題中，為每個項目選出最適合的標題：\n{inputJson}";

        await using var client = new CopilotClient(new CopilotClientOptions
        {
            AutoStart = true
        });

        await using var session = await client.CreateSessionAsync(new SessionConfig
        {
            Model = model,
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Replace,
                Content = SystemPrompt
            },
            InfiniteSessions = new InfiniteSessionConfig { Enabled = false }
        });

        _logger.LogDebug("Copilot session 已建立，正在發送標題增強請求");

        var response = await session.SendAndWaitAsync(new MessageOptions
        {
            Prompt = prompt
        });

        var responseContent = response?.Data?.Content;
        if (string.IsNullOrWhiteSpace(responseContent))
        {
            _logger.LogWarning("Copilot 回傳空白回應，使用原始標題");
            return GetFallbackTitles(titleGroups);
        }

        _logger.LogDebug("Copilot 回應: {Response}", responseContent);

        // 解析回應 JSON
        var enhancedTitles = ParseResponse(responseContent, titleGroups.Count);

        if (enhancedTitles == null || enhancedTitles.Count != titleGroups.Count)
        {
            _logger.LogWarning(
                "Copilot 回應解析結果數量不符（預期 {Expected}，實際 {Actual}），使用原始標題",
                titleGroups.Count, enhancedTitles?.Count ?? 0);
            return GetFallbackTitles(titleGroups);
        }

        _logger.LogInformation("標題增強完成，共 {Count} 個標題", enhancedTitles.Count);
        return enhancedTitles;
    }

    /// <summary>
    /// 解析 Copilot 回應為標題清單
    /// </summary>
    /// <remarks>
    /// 回應可能包含 markdown code block 標記，需先清理後再解析 JSON。
    /// </remarks>
    internal static List<string>? ParseResponse(string responseContent, int expectedCount)
    {
        // 清理可能的 markdown code block 標記
        var cleaned = responseContent.Trim();
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

        cleaned = cleaned.Trim();

        return cleaned.ToTypedObject<List<string>>();
    }

    /// <summary>
    /// 當 AI 回應無法使用時，取每組候選標題的第一個作為回退值
    /// </summary>
    private static IReadOnlyList<string> GetFallbackTitles(IReadOnlyList<IReadOnlyList<string>> titleGroups)
    {
        return titleGroups
            .Select(group => group.FirstOrDefault() ?? string.Empty)
            .ToList();
    }
}
