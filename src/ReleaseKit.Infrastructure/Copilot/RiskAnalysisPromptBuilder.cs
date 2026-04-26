using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Infrastructure.Copilot;

/// <summary>
/// 風險分析 Copilot Prompt 建構器
/// </summary>
public static class RiskAnalysisPromptBuilder
{
    /// <summary>
    /// 建構系統提示詞
    /// </summary>
    public static string BuildSystemPrompt()
    {
        return """
            你是一位資深的微服務架構安全分析師。你的任務是分析程式碼變更並識別跨專案的潛在風險。

            【分析情境】
            1. ApiContractBreak - API 契約破壞：Route 變更、參數增減、回傳格式改變、HTTP Method 改變
            2. DatabaseSchemaChange - 資料庫 Schema 變更：Entity 欄位增減、Migration 新增、DbContext 修改
            3. MessageQueueFormat - 訊息佇列格式變更：Event/Message/Command 類別的屬性變更
            4. ConfigEnvChange - 設定檔變更：appsettings key 增減、環境變數改變
            5. DataSemanticChange - 資料語意變更：查詢邏輯修改、資料轉換邏輯改變、欄位含義變更

            【風險等級】
            - High: 破壞性變更（刪除欄位、移除 API、改變必要格式）
            - Medium: 可能影響（新增必填欄位、修改回傳格式、條件邏輯變更）
            - Low: 輕微影響（新增選填欄位、新增 API、新增設定項）

            【最重要】你的回應必須是純 JSON 陣列，禁止包含任何文字說明或 markdown 格式。

            JSON 格式：
            [
              {
                "Scenario": "ApiContractBreak",
                "RiskLevel": "High",
                "Description": "描述風險內容",
                "AffectedFile": "檔案路徑",
                "DiffSnippet": "相關 diff 片段",
                "PotentiallyAffectedProjects": ["project-a", "project-b"],
                "RecommendedAction": "建議動作"
              }
            ]

            若無風險發現，回傳空陣列 []
            """;
    }

    /// <summary>
    /// 建構使用者提示詞
    /// </summary>
    /// <param name="projectPath">專案路徑</param>
    /// <param name="fileDiffs">異動檔案清單</param>
    /// <param name="projectStructure">專案結構</param>
    /// <param name="scenarios">要分析的情境清單</param>
    public static string BuildUserPrompt(
        string projectPath,
        IReadOnlyList<FileDiff> fileDiffs,
        ProjectStructure? projectStructure,
        IReadOnlyList<RiskScenario> scenarios)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"## 分析專案: {projectPath}");
        sb.AppendLine();
        sb.AppendLine($"### 需分析的情境: {string.Join(", ", scenarios)}");
        sb.AppendLine();

        if (projectStructure != null)
        {
            sb.AppendLine("### 專案結構摘要");
            if (projectStructure.ApiEndpoints.Count > 0)
            {
                sb.AppendLine($"- API 端點數: {projectStructure.ApiEndpoints.Count}");
                foreach (var ep in projectStructure.ApiEndpoints.Take(20))
                    sb.AppendLine($"  - {ep.HttpMethod} {ep.Route} ({ep.ControllerName}.{ep.ActionName})");
            }
            if (projectStructure.DbContextFiles.Count > 0)
                sb.AppendLine($"- DbContext 檔案: {string.Join(", ", projectStructure.DbContextFiles)}");
            if (projectStructure.MessageContracts.Count > 0)
                sb.AppendLine($"- 訊息契約: {string.Join(", ", projectStructure.MessageContracts)}");
            sb.AppendLine();
        }

        sb.AppendLine($"### 異動檔案 ({fileDiffs.Count} 個)");
        foreach (var diff in fileDiffs)
        {
            sb.AppendLine($"\n#### {diff.ChangeType}: {diff.FilePath}");
            sb.AppendLine("```diff");
            var content = diff.DiffContent.Length > 2000
                ? diff.DiffContent[..2000] + "\n... (已截斷)"
                : diff.DiffContent;
            sb.AppendLine(content);
            sb.AppendLine("```");
        }

        sb.AppendLine("\n請分析以上變更的風險，以 JSON 陣列格式回傳。");
        return sb.ToString();
    }

    /// <summary>
    /// 估算文字的 token 數量（字元數 / 4 近似）
    /// </summary>
    public static int EstimateTokens(string text) => text.Length / 4;
}
