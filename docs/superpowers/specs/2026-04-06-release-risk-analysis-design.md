# Release 風險分析功能設計文件

**日期**: 2026-04-06  
**狀態**: 設計中  
**版本**: 1.0.0

---

## 1. 問題描述

公司採用微服務架構（30-50 個 repo，分佈於 GitLab 與 Bitbucket），目前 Release-Kit 已能收集特定週期的 PR/MR 資訊。然而，缺乏跨專案「改 A 壞 B」的風險偵測能力。

### 目標

利用 PR 異動的程式碼 diff 與公司現有程式邏輯，透過 AI（GitHub Copilot SDK）進行全自動分析，識別跨專案的潛在風險變動，並產出風險分級報告。

### 受眾

- 開發團隊（技術導向）
- PM / 專案經理（業務導向）
- QA 團隊（測試範圍導向）

---

## 2. 架構設計

### 2.1 Multi-Pass Map-Reduce 分析流程

> **動態多層分析**：中間報告不限於固定兩層。AI Agent 可自行判斷是否需要繼續深入分析，
> 產生更多層的中間報告。上限硬編碼為 **10 層**。當 AI 判斷不需要更多分析、
> 或達到 10 層上限時，進入最終報告產出階段。

```
Pass 0: 資料準備（固定）
├─ 從 Redis 取得已篩選的 PR 資訊（GitLab + Bitbucket）
├─ 完整 Clone 所有相關 repo（git clone + git fetch --all）
└─ 擷取每個 PR 的 diff → 存入 Redis

Pass 1: Per-Project Analysis（固定，Map 階段）
├─ 對每個專案的 PR diffs 平行派發 AI Sub-Agent
├─ AI 識別：變更類型、影響範圍、對外暴露的介面變動
├─ 大型 diff 拆分為多個 Sub-Agent 呼叫，確保全覆蓋
└─ 中間報告 1-1, 1-2, ..., 1-N → 存入 Redis

Pass 2~9: 動態深度分析（AI 決定是否繼續）
├─ 每一層接收前一層的中間報告作為輸入
├─ AI Agent 決定分析策略（如：按風險類別分組、按影響範圍分組、交叉比對等）
├─ AI Agent 在每層分析完後回報：是否需要更深入的分析
├─ 若 AI 判斷已充分 → 進入 Final Report
├─ 若 AI 判斷需繼續 → 進入下一層
├─ 硬上限：第 10 層（Pass 10）強制結束，進入 Final Report
└─ 每層中間報告 {pass}-1, {pass}-2, ... → 存入 Redis

Final Report Generation（固定）
├─ 彙整最後一層的所有中間報告
├─ AI 產生最終風險摘要 + 風險等級分類
└─ 最終 Markdown 報告 → 存入 Redis + 輸出檔案
```

### 2.2 Redis 儲存結構

```
RiskAnalysisHash:
  ├─ ClonePaths           → { "project1": "/path/to/clone", ... }
  ├─ PrDiffs              → { "project1": [...diffs], "project2": [...diffs] }
  ├─ Intermediate:1-1     → Pass 1 第一個專案分析報告
  ├─ Intermediate:1-2     → Pass 1 第二個專案分析報告
  ├─ Intermediate:1-3-a   → Pass 1 第三個專案（大型 diff 第一批）
  ├─ Intermediate:1-3-b   → Pass 1 第三個專案（大型 diff 第二批）
  ├─ Intermediate:1-3     → Pass 1 第三個專案彙整報告
  ├─ ...
  ├─ Intermediate:2-1     → Pass 2（AI 決定的第一層深度分析）
  ├─ Intermediate:2-2     → Pass 2（AI 決定的第一層深度分析）
  ├─ ...
  ├─ Intermediate:3-1     → Pass 3（AI 決定的第二層深度分析，若需要）
  ├─ ...                  → （最多至 Pass 10，由 AI 動態決定層數）
  ├─ PassMetadata:{n}     → 每層的 metadata（分析策略、是否需要下一層）
  └─ Final                → 最終整合報告（Markdown）
```

### 2.3 Clean Architecture 整合

```
┌──────────────────────────────────────────────────────┐
│                ReleaseKit.Console                     │
│  新增 CLI 命令: analyze-risk (+ 各階段獨立命令)       │
├──────────────────────────────────────────────────────┤
│              ReleaseKit.Infrastructure                │
│  ┌──────────────────────────────────────────────┐    │
│  │  Git/GitService.cs (Clone, Diff 操作)        │    │
│  │  Copilot/CopilotRiskAnalyzer.cs (AI 分析)    │    │
│  └──────────────────────────────────────────────┘    │
├──────────────────────────────────────────────────────┤
│              ReleaseKit.Application                   │
│  新增 Tasks:                                          │
│  ├─ CloneRepositoriesTask                            │
│  ├─ ExtractPrDiffsTask                               │
│  ├─ AnalyzeProjectRiskTask                           │
│  ├─ AnalyzeCrossProjectRiskTask                      │
│  ├─ GenerateRiskReportTask                           │
│  └─ AnalyzeRiskTask (Orchestrator)                   │
├──────────────────────────────────────────────────────┤
│                ReleaseKit.Domain                      │
│  新增:                                                │
│  ├─ Entities: RiskAnalysisReport, RiskItem           │
│  ├─ ValueObjects: RiskLevel, RiskCategory,           │
│  │                AnalysisPassKey                     │
│  └─ Abstractions: IRiskAnalyzer, IGitService         │
└──────────────────────────────────────────────────────┘
```

---

## 3. Domain Layer 設計

> **注意**: `PrDiffContext` 雖為 DTO，但因被 Domain 層的 `IRiskAnalyzer` 介面引用，
> 故放置於 Domain 層（與現有 `MergeRequest` 實體同層級），避免違反依賴方向。

### 3.1 實體

#### RiskAnalysisReport（聚合根）

```csharp
/// <summary>風險分析報告</summary>
public sealed record RiskAnalysisReport
{
    /// <summary>報告的分析階段金鑰</summary>
    public required AnalysisPassKey PassKey { get; init; }

    /// <summary>來源專案名稱（Pass 1 時使用）</summary>
    public string? ProjectName { get; init; }

    /// <summary>風險類別（Pass 2 時使用）</summary>
    public RiskCategory? Category { get; init; }

    /// <summary>識別到的風險項目</summary>
    public required IReadOnlyList<RiskItem> RiskItems { get; init; }

    /// <summary>分析摘要（繁體中文）</summary>
    public required string Summary { get; init; }

    /// <summary>分析時間戳</summary>
    public required DateTimeOffset AnalyzedAt { get; init; }
}
```

#### RiskItem

```csharp
/// <summary>單一風險項目</summary>
public sealed record RiskItem
{
    /// <summary>風險類別</summary>
    public required RiskCategory Category { get; init; }

    /// <summary>風險等級</summary>
    public required RiskLevel Level { get; init; }

    /// <summary>變更摘要（繁體中文）</summary>
    public required string ChangeSummary { get; init; }

    /// <summary>影響的檔案路徑</summary>
    public required IReadOnlyList<string> AffectedFiles { get; init; }

    /// <summary>可能受影響的外部服務或元件</summary>
    public required IReadOnlyList<string> PotentiallyAffectedServices { get; init; }

    /// <summary>來源專案（跨專案分析時填入）</summary>
    public string? SourceProject { get; init; }

    /// <summary>受影響專案（跨專案分析時填入）</summary>
    public string? AffectedProject { get; init; }

    /// <summary>影響描述（繁體中文）</summary>
    public required string ImpactDescription { get; init; }

    /// <summary>建議的驗證步驟</summary>
    public required IReadOnlyList<string> SuggestedValidationSteps { get; init; }
}
```

### 3.2 值物件

#### RiskLevel

```csharp
/// <summary>風險等級</summary>
public enum RiskLevel
{
    /// <summary>高風險：需立即處理</summary>
    High,

    /// <summary>中風險：建議關注</summary>
    Medium,

    /// <summary>低風險：知悉即可</summary>
    Low
}
```

#### RiskCategory

```csharp
/// <summary>風險類別</summary>
public enum RiskCategory
{
    /// <summary>API 契約變更</summary>
    ApiContract,

    /// <summary>DB Schema 變更</summary>
    DatabaseSchema,

    /// <summary>DB 資料異動</summary>
    DatabaseData,

    /// <summary>事件/訊息格式變更</summary>
    EventFormat,

    /// <summary>設定檔變更</summary>
    Configuration
}
```

#### AnalysisPassKey

```csharp
/// <summary>分析階段金鑰（如 "1-3", "2-1", "1-3-a"）</summary>
public sealed record AnalysisPassKey
{
    /// <summary>階段編號</summary>
    public required int Pass { get; init; }

    /// <summary>序號</summary>
    public required int Sequence { get; init; }

    /// <summary>子序號（大型 diff 拆分時使用）</summary>
    public string? SubSequence { get; init; }

    /// <summary>產生 Redis field 名稱</summary>
    public string ToRedisField()
    {
        var key = $"Intermediate:{Pass}-{Sequence}";
        return SubSequence is not null ? $"{key}-{SubSequence}" : key;
    }
}
```

### 3.3 抽象介面

#### IRiskAnalyzer

```csharp
/// <summary>AI 風險分析服務介面</summary>
public interface IRiskAnalyzer
{
    /// <summary>分析單一專案的 PR 變更風險（Pass 1）</summary>
    Task<RiskAnalysisReport> AnalyzeProjectRiskAsync(
        string projectName,
        IReadOnlyList<PrDiffContext> diffs,
        CancellationToken cancellationToken = default);

    /// <summary>動態深度分析：接收前一層報告，產出下一層分析（Pass 2~10）</summary>
    /// <returns>分析結果，包含 ContinueAnalysis 旗標指示是否需要更多層分析</returns>
    Task<DynamicAnalysisResult> AnalyzeDeepAsync(
        int currentPass,
        IReadOnlyList<RiskAnalysisReport> previousPassReports,
        CancellationToken cancellationToken = default);

    /// <summary>產生最終整合報告 Markdown</summary>
    Task<string> GenerateFinalReportAsync(
        IReadOnlyList<RiskAnalysisReport> lastPassReports,
        CancellationToken cancellationToken = default);
}
```

#### DynamicAnalysisResult

```csharp
/// <summary>動態分析結果，包含是否需要繼續分析的決策</summary>
public sealed record DynamicAnalysisResult
{
    /// <summary>本層分析產生的報告</summary>
    public required IReadOnlyList<RiskAnalysisReport> Reports { get; init; }

    /// <summary>AI 判斷是否需要繼續更深層分析</summary>
    public required bool ContinueAnalysis { get; init; }

    /// <summary>繼續分析的理由（繁體中文，供 log 與追蹤）</summary>
    public string? ContinueReason { get; init; }

    /// <summary>本層使用的分析策略描述</summary>
    public required string AnalysisStrategy { get; init; }
}
```

#### IGitService

```csharp
/// <summary>Git 操作服務介面</summary>
public interface IGitService
{
    /// <summary>完整 Clone 指定 repository（含 fetch --all）</summary>
    Task<Result<string>> CloneRepositoryAsync(
        string repoUrl,
        string targetPath,
        CancellationToken cancellationToken = default);

    /// <summary>取得指定兩個 branch 之間的 diff</summary>
    Task<Result<string>> GetBranchDiffAsync(
        string repoPath,
        string sourceBranch,
        string targetBranch,
        CancellationToken cancellationToken = default);

    /// <summary>取得指定 commit 的 diff</summary>
    Task<Result<string>> GetCommitDiffAsync(
        string repoPath,
        string commitSha,
        CancellationToken cancellationToken = default);

    /// <summary>取得 repository 的遠端 URL</summary>
    Task<Result<string>> GetRemoteUrlAsync(
        string repoPath,
        CancellationToken cancellationToken = default);
}
```

#### PrDiffContext（Domain 層 DTO）

> 因被 `IRiskAnalyzer` 介面引用，放置於 Domain 層。

```csharp
/// <summary>PR Diff 上下文資訊</summary>
public sealed record PrDiffContext
{
    /// <summary>PR 標題</summary>
    public required string Title { get; init; }

    /// <summary>PR 描述</summary>
    public string? Description { get; init; }

    /// <summary>來源分支</summary>
    public required string SourceBranch { get; init; }

    /// <summary>目標分支</summary>
    public required string TargetBranch { get; init; }

    /// <summary>作者</summary>
    public required string AuthorName { get; init; }

    /// <summary>PR URL</summary>
    public required string PrUrl { get; init; }

    /// <summary>Git diff 內容</summary>
    public required string DiffContent { get; init; }

    /// <summary>異動的檔案清單</summary>
    public required IReadOnlyList<string> ChangedFiles { get; init; }

    /// <summary>所屬平台</summary>
    public required SourceControlPlatform Platform { get; init; }
}
```

---

## 4. Application Layer 設計

### 4.1 新增 DTO

> **PrDiffContext 已移至 Domain 層**（見 3.3 節），此處不重複定義。
> 以下為 Application 層專用的結果 DTO：

### 4.2 新增 Task

#### Task 清單

| Task | CLI 命令 | 職責 |
|------|---------|------|
| `CloneRepositoriesTask` | `clone-repos` | 從組態取得所有 project，完整 clone |
| `ExtractPrDiffsTask` | `extract-pr-diffs` | 讀取 Redis PR 資訊，在 cloned repo 中擷取 diff |

#### Clone URL 建構規則

- **GitLab**: `{GitLab.ApiUrl 的 base domain}/{ projectPath }.git`（例如 `https://gitlab.example.com/group/project.git`）
- **Bitbucket**: `https://bitbucket.org/{projectPath}.git`（使用 Basic Auth clone）
- 驗證邏輯：若組態中的 `ApiUrl` 包含 `/api/v4`（GitLab）或 `/2.0`（Bitbucket），自動去除 API 路徑取得 base domain

#### Diff 擷取策略

ExtractPrDiffsTask 從 Redis 讀取的 `MergeRequestOutput` 包含 `SourceBranch` 與 `TargetBranch`：
- **主要方式**：使用 `git diff {targetBranch}...{sourceBranch}` 取得 PR 的完整變更
- **備選方式**：若 branch 已被刪除（常見於 merge 後），改用 merge commit 的 `git show {mergeCommitSha}` 取得 diff
- **處理流程**：先嘗試 branch diff → 失敗時 fallback 到 commit diff
| `AnalyzeProjectRiskTask` | `analyze-project-risk` | Pass 1：平行 AI 分析每個專案 |
| `AnalyzeCrossProjectRiskTask` | `analyze-cross-project-risk` | Pass 2~10：動態深度分析（AI 決定層數，上限 10 層） |
| `GenerateRiskReportTask` | `generate-risk-report` | Final：產出最終 Markdown 報告 |
| `AnalyzeRiskTask` | `analyze-risk` | Orchestrator：一鍵串聯以上所有 Task |

#### AnalyzeRiskTask（Orchestrator）

```csharp
/// <summary>風險分析 Orchestrator，串聯所有子 Task</summary>
public sealed class AnalyzeRiskTask : ITask
{
    /// <summary>動態分析層數上限</summary>
    private const int MaxAnalysisPasses = 10;

    public async Task ExecuteAsync()
    {
        // Step 0-1: Clone repositories
        await _cloneRepositoriesTask.ExecuteAsync();

        // Step 0-2: Extract PR diffs
        await _extractPrDiffsTask.ExecuteAsync();

        // Step 1: Per-project AI analysis（固定）
        await _analyzeProjectRiskTask.ExecuteAsync();

        // Step 2~10: 動態深度分析（AI 決定是否繼續）
        await _analyzeCrossProjectRiskTask.ExecuteAsync();

        // Final: Generate report
        await _generateRiskReportTask.ExecuteAsync();
    }
}
```

#### AnalyzeProjectRiskTask（Pass 1 核心邏輯）

```csharp
/// <summary>Pass 1：平行分析每個專案的風險</summary>
public sealed class AnalyzeProjectRiskTask : ITask
{
    public async Task ExecuteAsync()
    {
        // 1. 從 Redis 讀取各專案的 PrDiffContext 列表
        // 2. 對每個專案：
        //    a. 若 diff 大小在 Token 限額內 → 直接呼叫 IRiskAnalyzer.AnalyzeProjectRiskAsync()
        //    b. 若 diff 超過限額 → 按檔案分群，每群各自呼叫 AI，再彙整
        //    c. 驗證分析覆蓋所有變更檔案
        // 3. 將每個專案的報告存入 Redis（Intermediate:1-{n}）
    }
}
```

#### 大型 Diff Sub-Agent 拆分邏輯

```csharp
/// <summary>拆分大型 diff 為多批次分析</summary>
private async Task<RiskAnalysisReport> AnalyzeLargeProjectAsync(
    string projectName,
    IReadOnlyList<PrDiffContext> diffs,
    int projectSequence)
{
    // 1. 將 diffs 依檔案分群（每群控制在 Token 限額內）
    // 2. 每群呼叫 IRiskAnalyzer.AnalyzeProjectRiskAsync()
    //    - 產生中間報告 1-{n}-a, 1-{n}-b, 1-{n}-c, ...
    //    - 每個中間報告存入 Redis
    // 3. 驗證：已分析檔案集合 == 原始 diff 檔案集合
    // 4. 彙整所有子報告為完整專案報告 1-{n}
}
```

#### AnalyzeCrossProjectRiskTask（Pass 2~10 動態深度分析）

```csharp
/// <summary>Pass 2~10：動態深度分析，AI 決定是否繼續</summary>
public sealed class AnalyzeCrossProjectRiskTask : ITask
{
    /// <summary>動態分析層數上限</summary>
    private const int MaxAnalysisPasses = 10;

    public async Task ExecuteAsync()
    {
        // 1. 從 Redis 讀取 Pass 1 所有中間報告
        var previousReports = await LoadPassReportsAsync(pass: 1);
        
        // 2. 迴圈：Pass 2 ~ MaxAnalysisPasses
        for (var currentPass = 2; currentPass <= MaxAnalysisPasses; currentPass++)
        {
            // 3. 呼叫 IRiskAnalyzer.AnalyzeDeepAsync()
            //    AI 決定：分析策略、產出報告、是否需要繼續
            var result = await _riskAnalyzer.AnalyzeDeepAsync(
                currentPass, previousReports);

            // 4. 將本層報告存入 Redis（Intermediate:{pass}-{n}）
            await StorePassReportsAsync(currentPass, result.Reports);

            // 5. 儲存本層 metadata（分析策略、繼續原因）
            await StorePassMetadataAsync(currentPass, result);

            // 6. AI 判斷不需要繼續 → 結束迴圈
            if (!result.ContinueAnalysis) break;

            // 7. 下一輪使用本層報告作為輸入
            previousReports = result.Reports;
        }
    }
}
```

---

## 5. Infrastructure Layer 設計

### 5.1 GitService

```csharp
/// <summary>Git 操作服務實作</summary>
public sealed class GitService : IGitService
{
    // 使用 System.Diagnostics.Process 執行 git 命令
    // Clone: git clone {url} {path} && cd {path} && git fetch --all
    // Diff: git diff {sourceBranch}...{targetBranch}
    // Commit diff: git show {commitSha} --format=""
}
```

### 5.2 CopilotRiskAnalyzer

```csharp
/// <summary>使用 GitHub Copilot SDK 實作風險分析</summary>
public sealed class CopilotRiskAnalyzer : IRiskAnalyzer
{
    // 依照 CopilotTitleEnhancer 的模式實作
    // 1. 初始化 Copilot Session
    // 2. 建構適當的 System Prompt（依據 Pass 階段不同）
    // 3. 送出分析請求
    // 4. 解析 JSON 回應 → RiskAnalysisReport
    // 5. Fallback: AI 失敗時回傳「無法分析」標記
}
```

### 5.3 AI Prompt 設計

#### Pass 1 System Prompt

```
你是一位資深軟體架構師，專精於微服務架構風險分析。
分析以下專案 "{projectName}" 的 PR 變更，識別所有可能影響其他服務的風險。

風險類別：
1. API 契約變更：
   - Controller endpoint 路徑修改
   - Request/Response 模型欄位新增/修改/刪除
   - 特別關注向後不相容的變更（必填欄位新增、欄位型別變更、欄位移除）

2. DB Schema 變更：
   - Migration 檔案
   - SQL 腳本
   - Entity 欄位變更

3. DB 資料異動（重點分析）：
   - Seed data / 初始資料變更（可能影響程式邏輯的判斷條件）
   - 資料修正/遷移腳本（UPDATE/DELETE/INSERT 既有資料）
   - Lookup table / 參照表資料異動（如狀態碼、分類、設定值）
   - 預設值變更（DEFAULT constraint）
   - Stored Procedure / Function / View 修改
   - Enum 對應值變更
   ⚠️ 特別分析：這些資料異動是否可能導致其他服務的程式邏輯異常
   （如 switch/case 未涵蓋新值、LINQ 查詢條件失效、商業規則判斷出錯）

4. 事件/訊息格式變更：
   - Event class 欄位修改
   - 訊息結構變更

5. 設定檔變更：
   - appsettings 鍵值修改
   - 環境變數新增/移除

對每個識別到的風險，回報 JSON 格式：
{
  "riskItems": [
    {
      "category": "ApiContract|DatabaseSchema|DatabaseData|EventFormat|Configuration",
      "level": "High|Medium|Low",
      "changeSummary": "變更摘要（繁體中文）",
      "affectedFiles": ["file1.cs", "file2.cs"],
      "potentiallyAffectedServices": ["ServiceA", "ServiceB"],
      "impactDescription": "影響描述（繁體中文）",
      "suggestedValidationSteps": ["步驟1", "步驟2"]
    }
  ],
  "summary": "整體分析摘要（繁體中文）"
}
```

#### Pass 2~10 Dynamic Analysis System Prompt

> Pass 2 起為動態深度分析。每次呼叫時，AI 同時決定分析策略與是否需要繼續。

```
你是一位資深軟體架構師，專精於微服務架構風險分析。

當前是第 {currentPass} 層分析（上限 10 層）。
以下是前一層的分析報告。請根據報告內容，決定最適合的分析策略並執行。

可能的分析策略（不限於此）：
- 按風險類別分組交叉比對
- 按專案間依賴關係深入追蹤
- 針對特定高風險項目進行更細緻分析
- 驗證前一層識別的跨專案風險是否完整

特別關注「改 A 壞 B」場景：
- 專案 A 修改了 API endpoint/模型 → 專案 B 呼叫了該 API
- 專案 A 修改了 DB Schema → 專案 B 也存取同一張 Table
- 專案 A 異動了 DB 資料（Lookup table、狀態碼、設定值）→ 專案 B 的程式邏輯依賴這些資料
- 專案 A 修改了 Event 格式 → 專案 B 消費該 Event
- 設定檔鍵值改名 → 其他專案依賴同一鍵值

回報 JSON 格式：
{
  "analysisStrategy": "本層使用的分析策略描述",
  "continueAnalysis": true|false,
  "continueReason": "繼續分析的理由（若 continueAnalysis 為 true）",
  "riskItems": [...],
  "summary": "本層分析摘要（繁體中文）"
}
```

#### Final Report System Prompt

```
將以下風險分析結果彙整成一份完整的 Release 風險評估報告（Markdown 格式，繁體中文）。

報告結構：
# Release 風險評估報告
## 報告資訊（分析日期、涵蓋專案數、PR 數量）
## 風險摘要（各等級風險數量統計）
## 🔴 高風險項目（需立即處理）
## 🟡 中風險項目（建議關注）
## 🟢 低風險項目（知悉即可）
## 跨專案影響矩陣（表格形式：來源專案 × 受影響專案）
## 建議的測試計畫（依風險等級排序）
## 附錄：各專案變更摘要
```

---

## 6. 組態設計

### 新增 appsettings 區段

```json
{
  "RiskAnalysis": {
    "CloneBasePath": "/tmp/release-kit-repos",
    "MaxConcurrentClones": 5,
    "MaxTokensPerAiCall": 100000,
    "MaxAnalysisPasses": 10,
    "ReportOutputPath": "./reports"
  }
}
```

### 新增 RiskAnalysisOptions

```csharp
/// <summary>風險分析組態</summary>
public sealed class RiskAnalysisOptions
{
    /// <summary>Clone 的基底路徑</summary>
    public required string CloneBasePath { get; init; }

    /// <summary>最大平行 Clone 數量</summary>
    public int MaxConcurrentClones { get; init; } = 5;

    /// <summary>每次 AI 呼叫的最大 Token 數</summary>
    public int MaxTokensPerAiCall { get; init; } = 100000;

    /// <summary>動態分析最大層數（硬上限 10）</summary>
    public int MaxAnalysisPasses { get; init; } = 10;

    /// <summary>報告輸出路徑</summary>
    public required string ReportOutputPath { get; init; }
}
```

---

## 7. CLI 命令設計

### 新增命令

| CLI 命令 | TaskType | 說明 |
|---------|----------|------|
| `clone-repos` | `CloneRepositories` | 完整 Clone 所有組態中的 repo |
| `extract-pr-diffs` | `ExtractPrDiffs` | 擷取 PR diff 並存入 Redis |
| `analyze-project-risk` | `AnalyzeProjectRisk` | Pass 1：Per-Project AI 分析 |
| `analyze-cross-project-risk` | `AnalyzeCrossProjectRisk` | Pass 2~10：動態深度分析（AI 決定層數） |
| `generate-risk-report` | `GenerateRiskReport` | Final：產出最終報告 |
| `analyze-risk` | `AnalyzeRisk` | Orchestrator：一鍵執行全部 |

### 使用方式

```bash
# 一鍵執行完整風險分析
dotnet run -- analyze-risk

# 或分步驟執行（方便除錯）
dotnet run -- clone-repos
dotnet run -- extract-pr-diffs
dotnet run -- analyze-project-risk
dotnet run -- analyze-cross-project-risk
dotnet run -- generate-risk-report
```

---

## 8. Redis Key 設計

### 新增 RedisKeys 常數

```csharp
/// <summary>風險分析 Redis Hash</summary>
public const string RiskAnalysisHash = "RiskAnalysis";

public static class Fields
{
    // 既有欄位...

    /// <summary>Clone 路徑資訊</summary>
    public const string ClonePaths = "ClonePaths";

    /// <summary>PR Diff 資料</summary>
    public const string PrDiffs = "PrDiffs";

    /// <summary>最終報告</summary>
    public const string FinalReport = "FinalReport";

    // 中間報告使用動態 key: "Intermediate:{pass}-{sequence}[-{sub}]"
}
```

---

## 9. 測試策略

### 9.1 Domain Tests

- `RiskAnalysisReport` 實體建構與驗證
- `RiskLevel`、`RiskCategory`、`AnalysisPassKey` 值物件行為
- `AnalysisPassKey.ToRedisField()` 格式化驗證

### 9.2 Application Tests

- `CloneRepositoriesTask` — Mock IGitService，驗證 clone 流程與 Redis 儲存
- `ExtractPrDiffsTask` — Mock IGitService，驗證 diff 擷取與 Redis 儲存
- `AnalyzeProjectRiskTask`:
  - Mock IRiskAnalyzer 驗證 Pass 1 流程
  - 中間報告 Redis 鍵命名正確性（1-1, 1-2, ...）
  - 大型 diff 拆分邏輯：分群正確、子報告彙整正確
  - **完整覆蓋驗證**：確認拆分後沒有遺漏任何檔案
- `AnalyzeCrossProjectRiskTask`:
  - Mock IRiskAnalyzer 驗證 Pass 2 流程
  - 風險類別分組邏輯正確性
- `GenerateRiskReportTask` — Mock IRiskAnalyzer，驗證最終報告產出
- DTO 序列化/反序列化驗證

### 9.3 Infrastructure Tests

- `GitService`:
  - 整合測試（需 git 環境）
  - Clone 成功/失敗處理
  - Diff 擷取正確性
- `CopilotRiskAnalyzer`:
  - Mock Copilot SDK
  - Prompt 建構驗證
  - JSON 回應解析（正常 + 異常格式）
  - Fallback 行為驗證
  - 各 Pass 階段 prompt 差異驗證

### 9.4 關鍵測試場景

| 場景 | 驗證重點 |
|------|---------|
| 大型 diff Sub-Agent 拆分 | diff 被正確分群、每群各自產生中間報告、彙整邏輯正確 |
| 完整覆蓋驗證 | 拆分後沒有遺漏任何變更檔案 |
| AI 回應格式異常 | Fallback 行為正確，不中斷整體流程 |
| 跨專案風險匹配 | Pass 2 能正確關聯 Pass 1 的風險項目 |
| 空資料處理 | 無 PR、無 diff、無風險等邊界情況 |
| Redis 中間報告讀寫 | 鍵命名規則一致、資料序列化/反序列化正確 |

---

## 10. 風險類別定義

### 5 大風險類別

| # | 類別 | Enum 值 | 重點偵測項目 |
|---|------|---------|-------------|
| 1 | API 契約變更 | `ApiContract` | Request/Response 欄位新增/修改/刪除的相容性問題 |
| 2 | DB Schema 變更 | `DatabaseSchema` | Migration、SQL 腳本、Entity 欄位變更 |
| 3 | DB 資料異動 | `DatabaseData` | Seed data、資料修正腳本、Lookup table、預設值、SP/Function/View、Enum 對應值 |
| 4 | 事件/訊息格式變更 | `EventFormat` | Event class 欄位修改、訊息結構變更 |
| 5 | 設定檔變更 | `Configuration` | appsettings 鍵值修改、環境變數新增/移除 |

### DB 資料異動特別關注點

DB 資料異動是最容易被忽略的跨專案風險。特別分析以下情況：

- **switch/case 未涵蓋新值**：新增的 Lookup 值可能導致其他服務的 switch/case 落入 default
- **LINQ 查詢條件失效**：資料範圍或格式變更可能導致既有 LINQ 查詢回傳不預期的結果
- **商業規則判斷出錯**：狀態碼或分類值變更可能導致商業邏輯走錯分支
- **硬編碼值不同步**：程式碼中寫死的 magic number 與 DB 資料不一致
- **Stored Procedure 行為變更**：修改 SP 邏輯可能影響所有呼叫方

---

## 11. 最終報告格式範例

```markdown
# Release 風險評估報告

## 報告資訊
- **分析日期**: 2026-04-06
- **涵蓋專案數**: 35
- **分析的 PR 數量**: 128
- **分析週期**: 2026-03-20 ~ 2026-04-05

## 風險摘要
| 風險等級 | 數量 |
|---------|------|
| 🔴 高風險 | 3 |
| 🟡 中風險 | 8 |
| 🟢 低風險 | 15 |

## 🔴 高風險項目

### 1. [API 契約變更] ServiceA → ServiceB
- **變更來源**: ServiceA - PR #234 修改了 /api/v1/orders 的 Response 模型
- **影響**: 移除了 `legacyOrderId` 欄位，ServiceB 仍在使用此欄位
- **建議**: 在 ServiceB 部署前先更新呼叫邏輯
- **驗證步驟**:
  1. 確認 ServiceB 中 legacyOrderId 的使用位置
  2. 更新 ServiceB 的 API 呼叫模型
  3. 執行整合測試

...（其他風險項目）

## 跨專案影響矩陣
| 來源 ↓ / 受影響 → | ServiceA | ServiceB | ServiceC |
|-------------------|----------|----------|----------|
| ServiceA          | -        | 🔴       | 🟡       |
| ServiceB          | -        | -        | -        |
| ServiceC          | 🟢       | -        | -        |

## 建議的測試計畫
1. **優先測試** ServiceA → ServiceB 的 API 整合（高風險）
2. **關注測試** ServiceA → ServiceC 的 DB 共用資料（中風險）
3. ...
```

---

## 12. 非功能性需求

- **效能**: 支援平行 Clone（最大 5 個同時）、平行 AI 分析
- **可觀測性**: 使用 Serilog 記錄每個階段的進度與結果
- **可恢復性**: 每個階段的結果存入 Redis，失敗後可從中斷點重新執行
- **可擴展性**: 風險類別可透過新增 `RiskCategory` enum 值擴充
