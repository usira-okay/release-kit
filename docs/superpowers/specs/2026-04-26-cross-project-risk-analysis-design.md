# 跨專案 Release 風險分析功能設計

## 問題描述

公司採用微服務架構（16-30 個專案，分佈於 GitLab + Bitbucket），經常發生「改 A 壞 B」的情況。目前缺乏系統化的方式在 release 前分析這些跨專案異動的風險。

## 目標

建立一個自動化的 release 風險分析工具，能夠：

1. 收集所有專案本次 release 的異動資訊
2. 靜態分析專案結構，自動推斷專案間的相依關係
3. 透過 Copilot SDK 智慧分析異動風險
4. 跨專案交叉比對，識別「改 A 壞 B」的風險
5. 產生完整的 Markdown 風險報告，包含通知對象與建議動作

## 分析的風險情境

| 編號 | 情境 | 說明 |
|------|------|------|
| 1 | API 契約破壞 | 修改 REST API 路徑、參數、回傳格式，呼叫端未同步 |
| 2 | 資料庫 Schema 變更 | 修改 DB 欄位、資料表結構，共用 DB 的專案受影響 |
| 3 | 訊息佇列格式變更 | 修改 Event/Message 結構，消費端無法正確反序列化 |
| 4 | 設定檔/環境變數變更 | 新增或修改共用設定鍵值，其他專案部署時缺少設定 |
| 5 | 資料庫資料語意變更 | Schema 未變但資料意義改變，其他專案邏輯未跟上 |

## 整體架構

### 多階段 Pipeline 設計

沿用現有 `ITask` + `TaskFactory` 架構，新增 6 個階段性任務：

```
Stage 1: CloneRepositoriesTask     → Clone/Pull 所有 repo
Stage 2: AnalyzePRDiffsTask        → 取得每個專案的 PR + commit diff 資訊
Stage 3: StaticProjectAnalysisTask → 靜態掃描專案結構
Stage 4: CopilotRiskAnalysisTask   → Copilot SDK 智慧風險分析
Stage 5: CrossProjectCorrelationTask → 跨專案交叉比對 + 自動推斷相依關係
Stage 6: GenerateRiskReportTask    → 整合中間結果產生 Markdown 風險報告
```

### Redis Key 結構

使用 `runId`（格式 `yyyyMMddHHmmss`）確保每次執行可追溯且不互相覆蓋：

```
RiskAnalysis:{runId}:Stage1:{projectPath}              → Clone/Pull 狀態
RiskAnalysis:{runId}:Stage2:{projectPath}              → PR + Diff 資料
RiskAnalysis:{runId}:Stage3:{projectPath}              → 靜態分析結果
RiskAnalysis:{runId}:Stage4:{projectPath}:{sessionIdx} → Copilot 分析結果
RiskAnalysis:{runId}:Stage5:Correlation                → 跨專案比對結果
RiskAnalysis:{runId}:Stage6:Report                     → 最終報告
```

### 設定檔擴展

```json
{
  "RiskAnalysis": {
    "CloneBasePath": "/tmp/release-kit-repos",
    "AnalysisScenarios": [
      "ApiContractBreak",
      "DatabaseSchemaChange",
      "MessageQueueFormat",
      "ConfigEnvChange",
      "DataSemanticChange"
    ]
  }
}
```

Copilot 設定沿用現有 `Copilot` section（Model、TimeoutSeconds、GitHubToken）。

---

## Stage 1: CloneRepositoriesTask

### 邏輯流程

1. 從 appsettings 讀取 GitLab/Bitbucket 專案清單
2. 對每個專案，組合 clone URL（使用現有 AccessToken）
   - GitLab: `https://oauth2:{token}@{host}/{projectPath}.git`
   - Bitbucket: `https://{username}:{token}@bitbucket.org/{projectPath}.git`
3. 若目標資料夾已存在 → `git pull`；不存在 → `git clone`
4. 並行處理（SemaphoreSlim 限制最大並行數）
5. 結果存 Redis

### 新增 Domain 抽象

```csharp
/// <summary>
/// Git 操作服務介面
/// </summary>
public interface IGitOperationService
{
    /// <summary>
    /// Clone 或 Pull 遠端倉庫至本地路徑
    /// </summary>
    Task<Result<string>> CloneOrPullAsync(string repoUrl, string localPath, CancellationToken ct);

    /// <summary>
    /// 取得指定 commit 的異動檔案與 diff 內容
    /// </summary>
    Task<Result<IReadOnlyList<FileDiff>>> GetCommitDiffAsync(string repoPath, string commitSha, CancellationToken ct);
}
```

### Clone URL 建構

沿用專案中既有的 `BuildGitLabCloneUrl` 與 `BuildBitbucketCloneUrl` 邏輯。

---

## Stage 2: AnalyzePRDiffsTask

### 邏輯流程

1. 讀取 Redis 中現有的 PR 資料（沿用 FetchPullRequests 任務結果）
2. 若無 PR 資料 → 跳過該專案
3. 從 PR 資訊中提取 commit hash（需擴充 `MergeRequest` entity）
4. 對 cloned repo 執行 `git show {commitSha}` 取得：
   - 異動檔案清單（含路徑）
   - 每個檔案的 diff 內容
   - 新增/修改/刪除狀態
5. 結果存 Redis

### MergeRequest Entity 擴充

```csharp
public sealed record MergeRequest
{
    // ... 現有欄位 ...

    /// <summary>
    /// Merge Commit SHA（合併後的 commit hash）
    /// </summary>
    public string? MergeCommitSha { get; init; }
}
```

### 新增 Domain Entity

```csharp
/// <summary>
/// 表示單一檔案的差異資訊
/// </summary>
public sealed record FileDiff
{
    /// <summary>
    /// 檔案路徑（相對於 repo 根目錄）
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// 變更類型：Added、Modified、Deleted
    /// </summary>
    public required string ChangeType { get; init; }

    /// <summary>
    /// Diff 內容（unified diff 格式）
    /// </summary>
    public required string DiffContent { get; init; }

    /// <summary>
    /// 對應的 Commit SHA
    /// </summary>
    public required string CommitSha { get; init; }
}

/// <summary>
/// 表示單一專案的所有差異結果
/// </summary>
public sealed record ProjectDiffResult
{
    /// <summary>
    /// 專案路徑
    /// </summary>
    public required string ProjectPath { get; init; }

    /// <summary>
    /// 所有異動檔案的差異清單
    /// </summary>
    public required IReadOnlyList<FileDiff> FileDiffs { get; init; }
}
```

---

## Stage 3: StaticProjectAnalysisTask

### 掃描項目

| 風險情境 | 掃描目標 | 偵測方式 |
|---------|---------|---------|
| API 契約破壞 | Controller 檔案、API Route 定義 | 掃描 `*Controller.cs`、`[Route]`、`[Http*]` 屬性 |
| DB Schema 變更 | DbContext、Migration 檔案 | 掃描 `*DbContext.cs`、`Migrations/` 目錄 |
| 訊息佇列格式 | Event/Message 模型 | 掃描含 `Event`、`Message`、`Command` 的類別定義 |
| 設定檔變更 | appsettings*.json、環境變數 | 掃描 JSON 設定檔的 key 結構 |
| 資料語意變更 | 資料存取層、Repository | 掃描 Repository/Service 中的查詢邏輯 |

### 額外掃描（自動推斷相依性）

- `.csproj` → NuGet 套件引用清單
- 程式碼中的 HTTP Client 呼叫 → 推斷呼叫了哪些其他微服務的 API
- Connection String / 資料庫名稱 → 推斷共用資料庫
- MQ Topic / Queue 名稱 → 推斷共用訊息通道

### 輸出模型

```csharp
/// <summary>
/// 專案結構分析結果
/// </summary>
public sealed record ProjectStructure
{
    public required string ProjectPath { get; init; }
    public required IReadOnlyList<ApiEndpoint> ApiEndpoints { get; init; }
    public required IReadOnlyList<string> NuGetPackages { get; init; }
    public required IReadOnlyList<string> DbContextFiles { get; init; }
    public required IReadOnlyList<string> MigrationFiles { get; init; }
    public required IReadOnlyList<string> MessageContracts { get; init; }
    public required IReadOnlyList<string> ConfigKeys { get; init; }
    public required IReadOnlyList<ServiceDependency> InferredDependencies { get; init; }
}

/// <summary>
/// API 端點資訊
/// </summary>
public sealed record ApiEndpoint
{
    public required string HttpMethod { get; init; }
    public required string Route { get; init; }
    public required string ControllerName { get; init; }
    public required string ActionName { get; init; }
}

/// <summary>
/// 推斷的服務相依性
/// </summary>
public sealed record ServiceDependency
{
    /// <summary>
    /// 相依類型：NuGet、HttpCall、SharedDb、SharedMQ
    /// </summary>
    public required string DependencyType { get; init; }

    /// <summary>
    /// 目標：套件名稱、API URL、DB 名稱、MQ Topic
    /// </summary>
    public required string Target { get; init; }
}
```

### 設計原則

靜態分析以「快速掃描、合理推斷」為原則，不追求 100% 精確。精確分析交給 Stage 4 的 Copilot SDK。

---

## Stage 4: CopilotRiskAnalysisTask

### 核心策略

Per-Project, Multi-Session, 自動分割。

### Session 分割邏輯

1. 計算該專案的總上下文大小（diff + 靜態分析結果）
2. 程式自動估算 token 數量，超過閾值時自動拆分
3. 拆分策略優先順序：
   - 先按「分析情境」拆分（API / DB+Data / MQ+Config）
   - 若單一情境仍超過上限 → 按「檔案群組」再拆分
4. 不需要在 appsettings 中手動設定 token 上限

### Copilot Session 設計

每個 session 收到：

```
System Prompt:
  - 角色定義（微服務風險分析專家）
  - 技術棧資訊（.NET 微服務架構）
  - 分析規則與風險判定標準
  - 輸出格式要求（結構化 JSON）

User Prompt:
  1. 專案基本資訊（名稱、技術棧）
  2. 本次異動的 diff 片段
  3. 靜態分析結果（API endpoint 清單、DB schema、MQ 契約等）
  4. 其他相關專案的靜態分析摘要（供跨專案推斷參考）
  5. 分析指令
```

### 輸出模型

```csharp
/// <summary>
/// 單一風險發現
/// </summary>
public sealed record RiskFinding
{
    /// <summary>
    /// 風險情境類型
    /// </summary>
    public required string Scenario { get; init; }

    /// <summary>
    /// 風險等級：High、Medium、Low
    /// </summary>
    public required string RiskLevel { get; init; }

    /// <summary>
    /// 風險描述
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// 造成風險的檔案
    /// </summary>
    public required string AffectedFile { get; init; }

    /// <summary>
    /// 相關 diff 片段
    /// </summary>
    public required string DiffSnippet { get; init; }

    /// <summary>
    /// 可能受影響的專案清單
    /// </summary>
    public required IReadOnlyList<string> PotentiallyAffectedProjects { get; init; }

    /// <summary>
    /// 建議動作
    /// </summary>
    public required string RecommendedAction { get; init; }

    /// <summary>
    /// 變更者（PR 作者）
    /// </summary>
    public required string ChangedBy { get; init; }
}

/// <summary>
/// 單一專案的風險分析結果
/// </summary>
public sealed record ProjectRiskAnalysis
{
    public required string ProjectPath { get; init; }
    public required IReadOnlyList<RiskFinding> Findings { get; init; }
    public required int SessionCount { get; init; }
}
```

### 容錯機制

- Copilot 回應解析失敗 → 記錄原始回應、標記為需人工檢視
- Session 逾時 → 重試一次，仍失敗則跳過並記錄
- 沿用現有 `CopilotTitleEnhancer` 的 fallback 模式

---

## Stage 5: CrossProjectCorrelationTask

### 分析邏輯

1. **建立相依圖**（從 Stage 3 的 `InferredDependencies` 彙整）
   ```
   ProjectA --[SharedDb: OrderDB]--> ProjectB
   ProjectA --[HttpCall: /api/v1/users]--> ProjectC
   ProjectA --[SharedMQ: order.created]--> ProjectD
   ```

2. **交叉比對風險**：
   - 若 ProjectA 修改了 API endpoint `/api/v1/users`
   - 且 ProjectC 的靜態分析顯示它呼叫了這個 endpoint
   - → 標記 ProjectC 為「受影響專案」

3. **風險彙整與評分**：
   - High：破壞性變更（刪除欄位、移除 API、改變 MQ schema）
   - Medium：可能影響（新增必填欄位、修改回傳格式）
   - Low：輕微影響（新增選填欄位、新增 API）

4. **整合 Stage 4 各專案的推斷結果與 Stage 5 的交叉比對結果**

5. **識別通知對象**：
   - 變更者：從 PR 的 `AuthorName` 取得
   - 受影響專案負責人：從受影響專案的近期 PR author 推斷

---

## Stage 6: GenerateRiskReportTask

### Markdown 報告結構

```markdown
# Release 風險分析報告
> 執行時間: 2026-04-26 09:45 | Run ID: 20260426094500

## 📊 風險摘要
| 風險等級 | 數量 | 涉及專案 |
|---------|------|---------|
| 🔴 High | 3 | ProjectA, ProjectB |
| 🟡 Medium | 7 | ProjectC, ProjectD, ... |
| 🟢 Low | 12 | ... |

## 📋 通知清單
| 人員 | 需注意的風險項 | 相關專案 |
|------|-------------|---------|
| Alice | API 契約變更 | ProjectC |
| Bob | DB Schema 變更 | ProjectB |

## 🔗 專案相依圖
（Mermaid 語法的相依圖）

## 🔴 高風險項目

### [ProjectA] API 契約破壞
- **變更者**: John (PR #123)
- **異動檔案**: `Controllers/UserController.cs`
- **Diff 片段**:
  ```diff
  - public IActionResult GetUser(int id)
  + public IActionResult GetUser(int id, bool includeDetails = false)
  ```
- **受影響專案**: ProjectC
- **需通知**: Alice, Bob (ProjectC 近期 committer)
- **建議動作**: 通知 ProjectC 團隊確認 API 呼叫是否相容

## 🟡 中風險項目
...

## 🟢 低風險項目
...

## 📝 分析詳情（按專案分組）

### ProjectA (5 個風險項)
...

## ⚠️ 需人工檢視
（Copilot 分析失敗或無法確定的項目）
```

---

## 執行方式

### 任務觸發

沿用現有 CLI 架構，每個 Stage 為獨立的 TaskType，透過命令列參數指定執行：

```bash
dotnet run -- --task CloneRepositories
dotnet run -- --task AnalyzePRDiffs
dotnet run -- --task StaticProjectAnalysis
dotnet run -- --task CopilotRiskAnalysis
dotnet run -- --task CrossProjectCorrelation
dotnet run -- --task GenerateRiskReport
```

### 前置資料相依

Stage 2 依賴 Redis 中已有的 PR 資料（由現有 `FetchGitLabPullRequests` / `FetchBitbucketPullRequests` 任務產出）。若尚未執行過 PR 拉取任務，Stage 2 會跳過無資料的專案。

完整的執行順序建議：
```
1. FetchGitLabPullRequests / FetchBitbucketPullRequests （現有任務）
2. CloneRepositories          （Stage 1）
3. AnalyzePRDiffs             （Stage 2，依賴 Stage 1 + 現有 PR 資料）
4. StaticProjectAnalysis      （Stage 3，依賴 Stage 1）
5. CopilotRiskAnalysis        （Stage 4，依賴 Stage 2 + Stage 3）
6. CrossProjectCorrelation    （Stage 5，依賴 Stage 3 + Stage 4）
7. GenerateRiskReport         （Stage 6，依賴 Stage 5）
```

---

## 開發流程規劃

### Phase 1：基礎建設
- 新增 `RiskAnalysis` 相關 Domain Entity 與 Value Object
- 新增 `RiskAnalysisOptions` 設定模型
- 擴充 `TaskType` enum 與 `TaskFactory`
- 擴充 `MergeRequest` entity（新增 `MergeCommitSha`）

### Phase 2：Stage 1 + Stage 2（資料收集）
- 實作 `IGitOperationService` 與 Infrastructure 實作
- 實作 `CloneRepositoriesTask`
- 實作 `AnalyzePRDiffsTask`
- 修改 GitLab/Bitbucket API 呼叫以取得 commit SHA

### Phase 3：Stage 3（靜態分析）
- 實作專案結構掃描器（Controller、DbContext、MQ、Config）
- 實作相依性推斷邏輯
- 實作 `StaticProjectAnalysisTask`

### Phase 4：Stage 4（Copilot 分析）
- 設計 System Prompt 與 User Prompt 模板
- 實作自動 session 分割邏輯
- 實作 `CopilotRiskAnalysisTask`
- 實作 JSON 回應解析與容錯

### Phase 5：Stage 5 + Stage 6（整合與報告）
- 實作跨專案交叉比對邏輯
- 實作相依圖建立
- 實作通知對象識別
- 實作 Markdown 報告產生器
- 實作 `CrossProjectCorrelationTask` 與 `GenerateRiskReportTask`

### Phase 6：整合測試與優化
- 端對端測試流程
- 效能優化（並行處理、Redis 快取）
- 錯誤處理與 fallback 機制完善

## 技術決策

| 決策 | 選擇 | 理由 |
|------|------|------|
| Clone 策略 | 已存在用 pull，否則 clone | 避免重複下載，節省時間 |
| PAT 來源 | 沿用 appsettings 中的 AccessToken | 不需額外設定 |
| 中間結果存儲 | Redis Hash + runId | 可追溯、可重跑單一階段 |
| Copilot session 拆分 | 自動估算 token，自動拆分 | 不需人工設定上限 |
| 最終報告格式 | Markdown | 易讀、可存入 Git |
| 相依性推斷 | 靜態分析 + Copilot 驗證 | 快速且合理精確 |
| Repo 保留 | 保留供後續使用 | 避免每次重新 clone |
| 通知對象 | 從 PR author + git commit author 推斷 | 自動化，不需手動維護 |
