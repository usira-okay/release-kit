using System.Text;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis.Models;

namespace ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis;

/// <summary>
/// 情境專家型風險分析 Prompt 建構器
/// </summary>
public static class ScenarioPromptBuilder
{
    /// <summary>
    /// 建構 Coordinator Agent 的系統提示詞
    /// </summary>
    public static string BuildCoordinatorSystemPrompt()
    {
        return """
            你是一位任務分配 AI。根據每個 Commit 的異動檔案路徑與行數統計，
            判斷哪些 Commit 可能與哪些風險情境相關，並分配給對應的專家分析。

            【風險情境】
            - ApiContractBreak: API 契約破壞（Controller、Endpoint、Route、DTO 變更）
            - DatabaseSchemaChange: 資料庫 Schema 變更（Entity、Migration、DbContext）
            - MessageQueueFormat: 訊息佇列格式變更（Event、Message、Command 類別）
            - ConfigEnvChange: 設定檔變更（appsettings、Options、環境變數）
            - DataSemanticChange: 資料語意變更（Repository、Query、計算邏輯）

            【分配規則】
            1. 一個 Commit 可以同時分配給多個情境
            2. 若某個 Commit 的檔案完全不符合任何情境，可以不分配
            3. 寧可多分配也不要遺漏（false positive 比 false negative 好）
            4. 你的判斷基於檔案名稱模式，不是 100% 準確，專家會做最終判斷
            5. 你可以使用 list_files 工具探索專案目錄結構來輔助判斷

            【輸出格式】
            回應必須是純 JSON，格式如下：
            ```json
            {
              "assignments": {
                "ApiContractBreak": ["commitSha1", "commitSha2"],
                "DatabaseSchemaChange": ["commitSha3"],
                "MessageQueueFormat": [],
                "ConfigEnvChange": ["commitSha1"],
                "DataSemanticChange": []
              },
              "reasoning": "簡要說明分配理由"
            }
            ```
            """;
    }

    /// <summary>
    /// 建構 Coordinator Agent 的使用者提示詞
    /// </summary>
    public static string BuildCoordinatorUserPrompt(string projectPath, IReadOnlyList<CommitSummary> commitSummaries)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## 待分配專案：{projectPath}");
        sb.AppendLine();
        sb.AppendLine("### Commit 異動清單");
        sb.AppendLine();

        foreach (var commit in commitSummaries)
        {
            sb.AppendLine($"#### CommitSha: {commit.CommitSha}");
            sb.AppendLine($"統計: {commit.TotalFilesChanged} 檔案, +{commit.TotalLinesAdded}/-{commit.TotalLinesRemoved}");
            sb.AppendLine("異動檔案:");
            foreach (var file in commit.ChangedFiles)
            {
                sb.AppendLine($"  - [{file.ChangeType}] {file.FilePath}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("請根據以上資訊，將各 Commit 分配至對應的風險情境專家。");
        return sb.ToString();
    }

    /// <summary>
    /// 建構 Expert Agent 的系統提示詞（依情境切換）
    /// </summary>
    public static string BuildExpertSystemPrompt(RiskScenario scenario)
    {
        var basePrompt = """
            你是一位資深的微服務架構安全分析師。

            【工作流程】
            1. 先使用 get_commit_overview 快速評估每個 Commit 的異動
            2. 對有風險的 Commit，使用 get_full_diff 取得完整 diff
            3. 若需要理解上下文，使用 get_file_content 取得完整檔案
            4. 若需要搜尋呼叫端或消費端，使用 search_pattern
            5. 分析完成後，以 JSON 陣列格式回傳風險發現

            【風險等級】
            - High: 破壞性變更（刪除欄位、移除 API、改變必要格式）
            - Medium: 可能影響（新增必填欄位、修改回傳格式、條件邏輯變更）
            - Low: 輕微影響（新增選填欄位、新增 API、新增設定項）

            【自主決策】
            - 若 commit 只改了註解或格式化，直接跳過
            - 若需要確認變更的影響範圍，主動搜尋呼叫端
            - 若 diff 過大（超過 500 行），優先看關鍵檔案而非全部

            """;

        var scenarioPrompt = scenario switch
        {
            RiskScenario.ApiContractBreak => """
                【你的專長：API 契約分析】
                你要特別注意：
                - Route path 變更（路徑參數名稱、結構）
                - HTTP Method 變更
                - Request/Response DTO 的欄位增減（特別是必填欄位）
                - 回傳型別變更（如 IActionResult → ActionResult<T>）
                - API 版本號變更
                - Authorization 屬性變更
                - Controller 命名空間或路由前綴變更

                【判斷策略】
                - 看完 diff 後，用 get_file_content 看完整的 Controller 確認前後差異
                - 用 search_pattern 搜尋 HttpClient、RestSharp、Refit 呼叫，找到呼叫此 API 的地方
                """,

            RiskScenario.DatabaseSchemaChange => """
                【你的專長：資料庫 Schema 分析】
                你要特別注意：
                - Entity 類別的屬性增減（尤其是非 nullable 新增）
                - Migration 檔案中的 Column 新增/刪除/改名
                - DbContext 的 DbSet 增減
                - Index 的新增或移除
                - Seed Data 的變更
                - Fluent API 設定變更

                【判斷策略】
                - 看 diff 後，用 get_file_content 看完整 Entity 確認哪些欄位是新增的
                - 搜尋其他地方是否有直接 SQL 查詢存取同一資料表
                - 搜尋 DbContext 確認是否有多個專案共用同一資料庫
                """,

            RiskScenario.MessageQueueFormat => """
                【你的專長：訊息佇列格式分析】
                你要特別注意：
                - Event/Message/Command 類別的屬性增減
                - 序列化格式或命名策略變更
                - Topic/Queue/Exchange 名稱變更
                - 消費端處理邏輯的變更
                - 新增或移除的事件訂閱

                【判斷策略】
                - 看 diff 後，用 search_pattern 搜尋該 Event 類別在其他地方的使用
                - 確認是否有消費端使用舊版屬性
                - 搜尋 MQ 設定檔確認 Topic 綁定
                """,

            RiskScenario.ConfigEnvChange => """
                【你的專長：設定檔分析】
                你要特別注意：
                - appsettings.json 的 key 新增/刪除/改名
                - Options 類別的屬性變更（尤其是 required 屬性）
                - 環境變數名稱變更
                - Connection String 格式變更
                - Feature Flag 新增或移除
                - 設定驗證邏輯變更

                【判斷策略】
                - 看 diff 後，用 search_pattern 找 IOptions<T> 或 Configuration["key"] 的使用
                - 確認新增的 key 是否為必填且無預設值
                - 搜尋 Docker 或部署設定是否需要同步更新
                """,

            RiskScenario.DataSemanticChange => """
                【你的專長：資料語意分析】
                你要特別注意：
                - LINQ 查詢條件變更（Where 條件、OrderBy、Select 投影）
                - SQL 查詢邏輯修改
                - 資料轉換/映射邏輯改變（AutoMapper 設定等）
                - 計算公式變更（折扣計算、金額計算、統計邏輯）
                - 快取 key 產生邏輯改變
                - 分頁或排序邏輯變更

                【判斷策略】
                - 看 diff 後，用 get_file_content 看完整的 Repository/Service 理解上下文
                - 搜尋是否有其他 Service 依賴相同的查詢結果
                - 確認快取失效策略是否需要同步調整
                """,

            _ => string.Empty
        };

        var outputFormat = """

            【最重要】分析完所有 Commit 後，你的最終回應必須是以下格式：
            ```json
            [
              {
                "Scenario": "情境名稱",
                "RiskLevel": "High|Medium|Low",
                "Description": "風險描述",
                "AffectedFile": "檔案路徑",
                "DiffSnippet": "相關 diff 片段",
                "PotentiallyAffectedProjects": ["可能受影響的專案"],
                "RecommendedAction": "建議動作"
              }
            ]
            ```
            若無風險發現，回傳空陣列 []
            """;

        return basePrompt + scenarioPrompt + outputFormat;
    }

    /// <summary>
    /// 建構 Expert Agent 的使用者提示詞
    /// </summary>
    public static string BuildExpertUserPrompt(
        string projectPath,
        IReadOnlyList<string> commitShas,
        RiskScenario scenario)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## 分析專案：{projectPath}");
        sb.AppendLine($"## 分析情境：{scenario}");
        sb.AppendLine();
        sb.AppendLine("### 待分析的 Commit SHA 清單：");
        foreach (var sha in commitShas)
            sb.AppendLine($"- {sha}");
        sb.AppendLine();
        sb.AppendLine("請依序對每個 Commit 先使用 get_commit_overview 快速評估，再決定是否需要 get_full_diff 深入分析。");
        sb.AppendLine("分析完成後以 JSON 陣列格式回傳所有風險發現。");
        return sb.ToString();
    }

    /// <summary>
    /// 建構 Synthesis Agent 的系統提示詞
    /// </summary>
    public static string BuildSynthesisSystemPrompt()
    {
        return """
            你是風險分析綜合判斷 AI。你接收多位情境專家的分析結果，進行以下工作：

            【去重與合併】
            - 若多位專家對同一檔案、同一變更報出風險，合併為一筆
            - 保留所有不同角度的觀點在 Description 中

            【複合風險識別】
            - 若 API 變更 + DB Schema 變更出現在同一個 commit，這通常是高風險
            - 若 Config 新增 + 新 API endpoint 出現在同一個 commit，這可能是新功能上線
            - 標記複合風險並適當提升風險等級

            【跨專案影響】
            - 若提供了其他專案的摘要資訊，利用它推斷受影響的專案
            - 若無其他專案資訊，僅根據 Expert findings 中的推斷

            【風險等級校正】
            - Expert 可能過於保守或激進，你做最終判斷
            - 只有確實會導致 runtime error 的才是 High
            - 可能導致邏輯錯誤但不會 crash 的是 Medium
            - 新增功能但向下相容的是 Low

            【輸出格式】
            回應必須是純 JSON 陣列：
            ```json
            [
              {
                "Scenario": "情境名稱",
                "RiskLevel": "High|Medium|Low",
                "Description": "綜合描述",
                "AffectedFile": "檔案路徑",
                "DiffSnippet": "相關 diff 片段",
                "PotentiallyAffectedProjects": ["受影響專案"],
                "RecommendedAction": "建議動作",
                "CompositeRisk": "與其他情境的關聯（若有）"
              }
            ]
            ```
            若所有 Expert 皆無風險發現，回傳空陣列 []
            """;
    }

    /// <summary>
    /// 建構 Synthesis Agent 的使用者提示詞
    /// </summary>
    public static string BuildSynthesisUserPrompt(SynthesisInput input)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## 專案：{input.ProjectPath}");
        sb.AppendLine();
        sb.AppendLine("### 各情境專家分析結果");
        sb.AppendLine();

        foreach (var (scenario, findings) in input.ExpertResults)
        {
            sb.AppendLine($"#### {scenario}");
            if (findings.Failed)
            {
                sb.AppendLine($"  ⚠️ 分析失敗：{findings.FailureReason}");
            }
            else if (findings.Findings.Count == 0)
            {
                sb.AppendLine("  無風險發現");
            }
            else
            {
                foreach (var f in findings.Findings)
                {
                    sb.AppendLine($"  - [{f.RiskLevel}] {f.Description} ({f.AffectedFile})");
                }
            }
            sb.AppendLine();
        }

        if (input.OtherProjectsSummary is { Count: > 0 })
        {
            sb.AppendLine("### 其他專案摘要（供跨專案推斷參考）");
            foreach (var summary in input.OtherProjectsSummary)
                sb.AppendLine($"  - {summary}");
            sb.AppendLine();
        }

        sb.AppendLine("請綜合以上分析結果，進行去重、複合風險識別、跨專案推斷，產出最終的風險判斷。");
        return sb.ToString();
    }
}
