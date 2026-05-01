# 情境專家型風險分析設計 (Scenario-Specialized Multi-Agent)

## 問題描述

現有的 `CopilotRiskAnalysisTask`（Stage 4）使用雙層 SubAgent（Dispatcher + Analyzer）進行風險分析。本設計新增一種替代策略：**情境專家型多 Agent 架構**，為每個風險情境配備專屬 AI Expert，提升分析品質與深度。

## 目標

1. 每個風險情境由專精的 Agent 負責分析，提高辨識精度
2. AI 全程驅動——從任務分配、diff 探索到最終判斷都由 AI 自主決策
3. 與現有 `CopilotRiskAnalysis` 方案共存，使用者依場景選用

## 與現有架構的關係

| 元素 | 現有方案 (雙層 SubAgent) | 新方案 (情境專家型) |
|------|------------------------|---------------------|
| TaskType | `CopilotRiskAnalysis` | 新增 `CopilotScenarioAnalysis` |
| CLI 觸發 | `--task CopilotRiskAnalysis` | `--task CopilotScenarioAnalysis` |
| Redis Key | `RiskAnalysis:{runId}:Stage4:{project}` | `RiskAnalysis:{runId}:Stage4Scenario:*` |
| Session 數 | 2-6/project | 7/project |
| 可共存 | ✓ | ✓ |

---

## 整體架構

### 三層 Agent Pipeline

```
Stage 2 (AnalyzePRDiffsTask) 完成後，Redis 中已有:
  - 每個專案的 CommitSummary 清單（含 CommitSha、ChangedFiles、行數統計）

CopilotScenarioAnalysisTask 啟動:

1. Coordinator Agent (每專案 1 個)
   ├─ 輸入: CommitSummary metadata + ChangedFiles 清單
   ├─ 工具: list_files（列出 repo 目錄結構）
   ├─ AI 決策: 哪些 commit 可能跟哪些情境相關
   └─ 輸出: 每個情境的 "待分析 commit 清單"
       ├─ ApiContractBreak     → [sha1, sha3, sha5]
       ├─ DatabaseSchemaChange → [sha2, sha3]
       ├─ MessageQueueFormat   → [sha4]
       ├─ ConfigEnvChange      → [sha1, sha6]
       └─ DataSemanticChange   → [sha2, sha5]

2. Expert Agents (平行執行，每情境 1 個)
   ├─ 輸入: Coordinator 指定的 commit 清單 + 情境專屬 system prompt
   ├─ 工具: get_commit_overview, get_full_diff, get_file_content, search_pattern, list_directory
   ├─ AI 決策: 逐一檢視 → 決定是否看完整 diff → 決定是否搜尋上下文
   └─ 輸出: 該情境的 RiskFinding[]

3. Synthesis Agent (每專案 1 個)
   ├─ 輸入: 所有 Expert findings + 專案靜態分析結果（軟性依賴）
   ├─ 工具: 無（純推理）
   ├─ AI 決策: 去重 → 識別複合風險 → 跨專案影響判斷 → 風險等級校正
   └─ 輸出: 最終 ProjectRiskAnalysis
```

### Session 數量估算

| 層級 | 數量 | 說明 |
|------|------|------|
| Coordinator | 1/project | 分配任務 |
| Expert | 5/project | 每個情境一個 |
| Synthesis | 1/project | 匯總結論 |
| **總計** | **7/project** | 20 個專案 = 140 sessions |

---

## Coordinator Agent 設計

### 角色

任務分配者，**不做風險分析**，只判斷哪些 commit 可能與哪些情境相關。

### 工具集

| 工具 | 用途 |
|------|------|
| `list_files(path)` | 列出 repo 目錄結構，輔助判斷專案類型 |

### 輸入

Coordinator 收到輕量 metadata：

```
CommitSha: abc123
ChangedFiles:
  - Controllers/UserController.cs (Modified, +15/-3)
  - Models/UserDto.cs (Modified, +5/-0)
  - appsettings.json (Modified, +2/-0)
```

### AI 判斷啟發

| 檔案模式 | 可能相關情境 |
|---------|------------|
| `*Controller.cs`, `*Endpoint.cs`, `Swagger*` | ApiContractBreak |
| `*Migration*.cs`, `*DbContext.cs`, `*Entity*.cs` | DatabaseSchemaChange |
| `*Event.cs`, `*Message.cs`, `*Command.cs` (MQ) | MessageQueueFormat |
| `appsettings*.json`, `*.env`, `*Options.cs` | ConfigEnvChange |
| `*Repository.cs`, `*Query*.cs`, `*Specification*.cs` | DataSemanticChange |

> Coordinator 是 AI 驅動，以上僅為啟發，AI 可自行判斷。一個 commit 可以分配給多個 Expert。

### 輸出格式

```json
{
  "assignments": {
    "ApiContractBreak": ["abc123", "def456"],
    "DatabaseSchemaChange": ["ghi789"],
    "MessageQueueFormat": [],
    "ConfigEnvChange": ["abc123", "jkl012"],
    "DataSemanticChange": ["ghi789", "def456"]
  },
  "reasoning": "abc123 修改了 Controller 和 appsettings，分配給 API 和 Config..."
}
```

### System Prompt

```
你是一位任務分配 AI。根據每個 Commit 的異動檔案路徑與行數統計，
判斷哪些 Commit 可能與哪些風險情境相關，並分配給對應的專家分析。

規則：
1. 一個 Commit 可以同時分配給多個情境
2. 若某個 Commit 的檔案完全不符合任何情境，可以不分配
3. 寧可多分配也不要遺漏（false positive 比 false negative 好）
4. 你的判斷基於檔案名稱模式，不是 100% 準確，專家會做最終判斷
5. 你可以使用 list_files 工具探索專案目錄結構來輔助判斷
```

---

## Expert Agents 設計

### 共通工具集

| 工具 | 簽名 | 用途 |
|------|------|------|
| `get_commit_overview` | `(commitSha) → string` | 取得 commit 的 shortstat + 檔案清單（輕量） |
| `get_full_diff` | `(commitSha) → string` | 取得完整 unified diff（AI 按需呼叫） |
| `get_file_content` | `(filePath) → string` | 取得檔案完整內容 |
| `search_pattern` | `(pattern, fileGlob?) → string` | 使用 git grep 搜尋程式碼 |
| `list_directory` | `(path) → string` | 列出目錄結構 |

### 兩階段 Diff 策略

Expert 先呼叫 `get_commit_overview` 看摘要，再自行決定是否呼叫 `get_full_diff`：

- 若摘要顯示只改了 1-2 行且是註解 → 可能不需要完整 diff
- 若摘要顯示改了 Controller + DTO → 立即呼叫 get_full_diff

### AI 決策自主性

Expert 不是被動「看完所有 diff 就結束」，它會：

1. **選擇性深入**：diff 只改了註解 → 直接跳過
2. **主動探索上下文**：看到 Controller 改了回傳型別 → 搜尋呼叫端
3. **跨檔追蹤**：看到 Entity 加了欄位 → 搜尋 Migration 是否有對應
4. **自行判斷終止**：所有 commit 都是低風險 → 快速產出空結果

### 各 Expert 專屬 System Prompt

#### 1. API Contract Expert

```
你是 API 契約分析專家。專注於識別可能破壞 API 呼叫端的變更。

你要特別注意：
- Route path 變更（路徑參數名稱、結構）
- HTTP Method 變更
- Request/Response DTO 的欄位增減（特別是必填欄位）
- 回傳型別變更（如 IActionResult → ActionResult<T>）
- API 版本號變更
- Authorization 屬性變更

判斷策略：
- 先用 get_commit_overview 快速評估
- 看完 diff 後，用 get_file_content 看完整的 Controller 確認前後差異
- 用 search_pattern 搜尋 HttpClient 或 RestSharp 呼叫，找到呼叫此 API 的地方
```

#### 2. Database Schema Expert

```
你是資料庫 Schema 分析專家。專注於識別可能影響共用資料庫的變更。

你要特別注意：
- Entity 類別的屬性增減（尤其是非 nullable 新增）
- Migration 檔案中的 Column 新增/刪除/改名
- DbContext 的 DbSet 增減
- Index 的新增或移除
- Seed Data 的變更

判斷策略：
- 先用 get_commit_overview 快速評估
- 看 diff 後，用 get_file_content 看完整 Entity 確認哪些欄位是新增的
- 搜尋其他地方是否有直接 SQL 查詢存取同一資料表
```

#### 3. Message Queue Expert

```
你是訊息佇列格式分析專家。專注於識別事件/訊息結構變更。

你要特別注意：
- Event/Message/Command 類別的屬性增減
- 序列化格式變更
- Topic/Queue 名稱變更
- 消費端處理邏輯的變更

判斷策略：
- 先用 get_commit_overview 快速評估
- 看 diff 後，用 search_pattern 搜尋該 Event 類別在其他地方的使用
- 確認是否有消費端使用舊版屬性
```

#### 4. Config/Environment Expert

```
你是設定檔分析專家。專注於識別設定檔與環境變數的變更。

你要特別注意：
- appsettings.json 的 key 新增/刪除/改名
- Options 類別的屬性變更
- 環境變數名稱變更
- Connection String 格式變更
- Feature Flag 新增或移除

判斷策略：
- 先用 get_commit_overview 快速評估
- 看 diff 後，用 search_pattern 找 IOptions<T> 或 Configuration["key"] 的使用
- 確認新增的 key 是否為必填且無預設值
```

#### 5. Data Semantic Expert

```
你是資料語意分析專家。專注於識別資料查詢邏輯與語意的變更。

你要特別注意：
- LINQ 查詢條件變更（Where 條件、OrderBy、Select 投影）
- SQL 查詢邏輯修改
- 資料轉換/映射邏輯改變
- 計算公式變更（如折扣計算、金額計算）
- 快取 key 產生邏輯改變

判斷策略：
- 先用 get_commit_overview 快速評估
- 看 diff 後，用 get_file_content 看完整的 Repository/Service 理解上下文
- 搜尋是否有其他 Service 依賴相同的查詢結果
```

### Expert 輸出格式

```json
[
  {
    "Scenario": "ApiContractBreak",
    "RiskLevel": "High",
    "Description": "UserController.GetUser 新增必填參數 tenantId",
    "AffectedFile": "Controllers/UserController.cs",
    "DiffSnippet": "- GetUser(int id)\n+ GetUser(int id, string tenantId)",
    "PotentiallyAffectedProjects": ["team-b/user-portal"],
    "RecommendedAction": "通知 user-portal 團隊更新 API 呼叫"
  }
]
```

---

## Synthesis Agent 設計

### 角色

最終綜合判斷者，負責去重、識別複合風險、跨專案影響推斷。

### 工具集

無工具（純推理），輸入已包含所有必要資訊。

### 輸入

```json
{
  "projectPath": "team-a/order-service",
  "expertFindings": {
    "ApiContractBreak": [/* findings */],
    "DatabaseSchemaChange": [/* findings */],
    "MessageQueueFormat": [/* findings */],
    "ConfigEnvChange": [/* findings */],
    "DataSemanticChange": [/* findings */]
  },
  "projectStructure": {
    /* Stage 3 結果，軟性依賴，可能為 null */
  },
  "otherProjectsSummary": [
    /* 其他專案的簡要資訊 */
  ]
}
```

### 職責

1. **去重**：多個 Expert 對同一變更報出不同角度的風險 → 合併為一筆
2. **複合風險識別**：API 變更 + DB Schema 變更在同一 commit → 提升風險等級
3. **跨專案影響**：結合靜態分析結果（若有），推斷受影響的其他專案
4. **風險等級校正**：Expert 可能過度保守或激進，做最終裁決

### Stage 3 軟性依賴

- 有 Stage 3 結果：利用 `InferredDependencies` 做精確的跨專案推斷
- 無 Stage 3 結果：僅根據 Expert findings 中的推斷合併

### 輸出格式

```json
[
  {
    "Scenario": "ApiContractBreak",
    "RiskLevel": "High",
    "Description": "...",
    "AffectedFile": "...",
    "DiffSnippet": "...",
    "PotentiallyAffectedProjects": ["..."],
    "RecommendedAction": "...",
    "CompositeRisk": "與 DatabaseSchemaChange 相關：Users table 同時新增 TenantId 欄位"
  }
]
```

---

## 實作架構

### 新增類別

```
Domain/
  Abstractions/
    ICopilotScenarioDispatcher.cs

Infrastructure/Copilot/
  ScenarioAnalysis/
    CopilotScenarioDispatcher.cs      ← 主協調器
    CoordinatorAgentRunner.cs         ← Coordinator Agent 執行器
    ExpertAgentRunner.cs              ← Expert Agent 執行器（依 scenario 切換 prompt）
    SynthesisAgentRunner.cs           ← Synthesis Agent 執行器
    ScenarioPromptBuilder.cs          ← 各角色的 prompt 模板
    ExpertToolFactory.cs              ← 建立 Expert 工具集

Application/Tasks/
    CopilotScenarioAnalysisTask.cs    ← 新 TaskType 入口

Domain/Abstractions/
    IGitOperationService.cs           ← 新增 SearchPatternAsync 方法
```

### 新增 IGitOperationService 方法

```csharp
/// <summary>
/// 使用 git grep 搜尋程式碼庫中符合模式的內容
/// </summary>
Task<Result<string>> SearchPatternAsync(
    string repoPath,
    string pattern,
    string? fileGlob = null,
    CancellationToken cancellationToken = default);
```

### Redis Key 結構

```
RiskAnalysis:{runId}:Stage4Scenario:Coordinator:{projectPath}
RiskAnalysis:{runId}:Stage4Scenario:Expert:{projectPath}:{scenario}
RiskAnalysis:{runId}:Stage4Scenario:Synthesis:{projectPath}
```

### 執行序列

```csharp
// CopilotScenarioDispatcher.DispatchAsync 內部流程
public async Task DispatchAsync(...)
{
    // 1. Coordinator 分配
    var assignment = await _coordinatorRunner.RunAsync(commitSummaries, localPath);

    // 2. Expert 平行分析
    var expertTasks = scenarios.Select(scenario =>
        _expertRunner.RunAsync(scenario, assignment.GetShas(scenario), localPath));
    var expertResults = await Task.WhenAll(expertTasks);

    // 3. Synthesis 匯總
    var projectStructure = await TryLoadStage3Async(runId, projectPath);
    var synthesis = await _synthesisRunner.RunAsync(expertResults, projectStructure);

    // 4. 寫入 Redis
    await SaveToRedisAsync(runId, projectPath, synthesis);
}
```

---

## 錯誤處理與容錯

| 情境 | 處理方式 |
|------|---------|
| Coordinator 回傳格式錯誤 | 退回所有 commit 給每個 Expert（不篩選） |
| Expert session 逾時 | 記錄逾時的 scenario，不影響其他 Expert |
| Expert 回傳空白 | 重試一次，仍失敗標記為「需人工檢視」 |
| Synthesis 回傳格式錯誤 | 直接合併所有 Expert findings（不去重） |
| Copilot 認證失敗 | 整個任務中止，log error |

---

## 技術決策

| 決策 | 選擇 | 理由 |
|------|------|------|
| Copilot SDK 介入程度 | 全程 AI 驅動 | 追求最佳分析品質 |
| Expert 並行策略 | `Task.WhenAll` | 5 個 Expert 互相獨立 |
| Diff 取得策略 | 兩階段（overview → full diff） | AI 自主決定是否需要完整 diff |
| 搜尋工具 | `git grep` | 不需額外安裝 |
| Stage 3 依賴 | 軟性依賴 | 有就用，沒有也能運作 |
| 與現有方案關係 | 共存 | 新增 TaskType，使用者選擇 |
| Session 成本 | 不限制 | 追求品質優先 |

---

## 開發階段建議

### Phase 1：Domain & Infrastructure 基礎

- 新增 `ICopilotScenarioDispatcher` 介面
- 擴充 `IGitOperationService` 新增 `SearchPatternAsync`
- 新增 `TaskType.CopilotScenarioAnalysis`
- 實作 `GitOperationService.SearchPatternAsync`

### Phase 2：Agent Runners

- 實作 `CoordinatorAgentRunner`
- 實作 `ExpertAgentRunner`（含 ExpertToolFactory）
- 實作 `SynthesisAgentRunner`
- 實作 `ScenarioPromptBuilder`

### Phase 3：整合與任務入口

- 實作 `CopilotScenarioDispatcher`
- 實作 `CopilotScenarioAnalysisTask`
- DI 註冊與 CLI 命令支援

### Phase 4：測試

- 各 Agent Runner 的單元測試
- Prompt Builder 的測試
- 整合測試（mock Copilot SDK）
