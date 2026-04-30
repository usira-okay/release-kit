using System.Text;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Infrastructure.Copilot;

/// <summary>
/// 風險分析 Copilot Prompt 建構器
/// </summary>
public static class RiskAnalysisPromptBuilder
{
    /// <summary>
    /// 建構 SubAgent 1（Dispatcher）的系統提示詞
    /// </summary>
    public static string BuildDispatcherSystemPrompt()
    {
        return """
            你是一個任務調度 AI。你的工作是分析各 Commit 的異動量，決定如何將 Commit 分組，然後對每組呼叫 dispatch_project_analysis 工具進行風險分析。

            【分組原則】
            - 每組的總異動行數（linesAdded + linesRemoved）建議不超過 3000 行
            - 若某個 Commit 的異動量超過 3000 行，單獨成一組
            - 最多可啟動 5 組；若分組後仍超過 5 組，優先合併異動量較小的 Commit
            - 每組至少包含 1 個 CommitSha

            【重要】你只負責調度任務，不負責分析風險。
            全部 dispatch_project_analysis 呼叫完成後，回傳「調度完成，共處理 N 個 Commit，啟動 M 組分析」。
            """;
    }

    /// <summary>
    /// 建構 SubAgent 1（Dispatcher）的使用者提示詞
    /// </summary>
    /// <param name="projectPath">專案路徑</param>
    /// <param name="commitSummaries">各 Commit 的異動摘要</param>
    public static string BuildDispatcherUserPrompt(string projectPath, IReadOnlyList<CommitSummary> commitSummaries)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## 待調度專案：{projectPath}");
        sb.AppendLine();
        sb.AppendLine("### Commit 異動統計");
        sb.AppendLine();
        sb.AppendLine("| CommitSha | 異動檔案數 | 新增行數 | 刪除行數 | 總異動量 |");
        sb.AppendLine("|-----------|-----------|---------|---------|---------|");

        foreach (var commit in commitSummaries)
        {
            var total = commit.TotalLinesAdded + commit.TotalLinesRemoved;
            sb.AppendLine($"| {commit.CommitSha[..Math.Min(8, commit.CommitSha.Length)]}... | {commit.TotalFilesChanged} | {commit.TotalLinesAdded} | {commit.TotalLinesRemoved} | {total} |");
        }

        sb.AppendLine();
        var totalLines = commitSummaries.Sum(c => c.TotalLinesAdded + c.TotalLinesRemoved);
        sb.AppendLine($"共 {commitSummaries.Count} 個 Commit，總異動量 {totalLines} 行。");
        sb.AppendLine();
        sb.AppendLine("請根據以上統計依照分組原則分組，並呼叫 dispatch_project_analysis 工具分析各組的風險。");

        return sb.ToString();
    }

    /// <summary>
    /// 建構 SubAgent 2（Analyzer）的系統提示詞
    /// </summary>
    public static string BuildAnalyzerSystemPrompt()
    {
        return """
            你是一位資深的微服務架構安全分析師。你的任務是分析程式碼變更並識別跨專案的潛在風險。

            【工作流程】
            1. 使用 get_diff 工具逐一取得每個 CommitSha 的 diff 內容
            2. 分析所有取得的 diff 內容，識別以下情境的風險

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

            【最重要】分析完所有 Commit 的 diff 後，你的最終回應必須是純 JSON 陣列，禁止包含任何文字說明或 markdown 格式。

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
    /// 建構 SubAgent 2（Analyzer）的使用者提示詞
    /// </summary>
    /// <param name="projectPath">專案路徑</param>
    /// <param name="commitShas">要分析的 CommitSha 清單</param>
    /// <param name="scenarios">要分析的情境清單</param>
    public static string BuildAnalyzerUserPrompt(
        string projectPath,
        IReadOnlyList<string> commitShas,
        IReadOnlyList<RiskScenario> scenarios)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## 分析專案：{projectPath}");
        sb.AppendLine();
        sb.AppendLine($"### 需分析的情境：{string.Join(", ", scenarios)}");
        sb.AppendLine();
        sb.AppendLine("### 待分析的 Commit SHA 清單：");
        foreach (var sha in commitShas)
            sb.AppendLine($"- {sha}");
        sb.AppendLine();
        sb.AppendLine("請依序對每個 CommitSha 呼叫 get_diff 工具取得 diff 內容，分析所有變更後以 JSON 陣列格式回傳所有風險發現。");

        return sb.ToString();
    }

    /// <summary>
    /// 估算文字的 token 數量（字元數 / 4 近似）
    /// </summary>
    public static int EstimateTokens(string text) => text.Length / 4;
}
