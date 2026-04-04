# Release Risk Analysis — 設計規格

## 概述

### 問題描述

公司採用微服務架構，擁有 30+ 個 Repository（分布於 GitLab 和 Bitbucket），每個 Release 週期有 100+ 個 PR。目前 Release-Kit 已能抓取 PR 基本資訊，但缺乏對程式碼變更的風險分析能力。

### 解決方案

新增 **Release Risk Analysis** 功能，透過混合模式（API Diff + 全 Repo Clone）收集 PR 的程式碼變更，利用 Copilot SDK 進行 AI 驅動的風險分析，並產出結構化的 Markdown 風險報告。

### 範圍

- **包含**：PR diff 收集、AI 風險初篩、深度分析、跨服務關聯分析、Markdown 報告
- **不包含**：自動修復建議、CI/CD 整合、即時監控

---

## 架構設計

### 新增 Bounded Context：ReleaseRisk

遵循現有 Clean Architecture 分層，新增以下元件：

```
ReleaseKit.Domain/
├── Entities/
│   ├── RiskAnalysisReport.cs        # 聚合根 — 整份風險報告
│   ├── RepositoryRiskResult.cs      # 單一 Repo 的風險結果
│   └── PullRequestRisk.cs           # 單一 PR 的風險評估
├── ValueObjects/
│   ├── RiskLevel.cs                 # enum: Critical / High / Medium / Low / None
│   ├── RiskCategory.cs              # enum: 8 種風險類別
│   └── RiskFinding.cs               # 單一風險發現
├── Abstractions/
│   ├── IRiskAnalyzer.cs             # AI 風險分析介面
│   ├── IDiffProvider.cs             # 取得 PR diff 的抽象
│   └── IRepositoryCloner.cs         # Clone repo 的抽象

ReleaseKit.Application/
├── Tasks/
│   └── AnalyzeReleaseRiskTask.cs    # 主要 Task（協調 5 個 Phase）
├── Common/
│   ├── RiskAnalysisContext.cs        # 分析過程的上下文資料
│   └── RiskReportGenerator.cs       # Markdown 報告產生器

ReleaseKit.Infrastructure/
├── RiskAnalysis/
│   ├── CopilotRiskAnalyzer.cs       # Copilot SDK 風險分析實作
│   ├── DiffProviders/
│   │   ├── GitLabDiffProvider.cs    # GitLab API 取 diff
│   │   └── BitbucketDiffProvider.cs # Bitbucket API 取 diff
│   └── RepositoryCloner.cs          # Git clone 實作
```

### 專案參照關係

```
Domain (核心，無依賴) — 新增 Risk 相關 Entity/VO/Abstraction
   ↑
Application (依賴 Domain) — 新增 AnalyzeReleaseRiskTask
   ↑
Infrastructure (依賴 Domain、Application) — 新增 CopilotRiskAnalyzer、DiffProviders、RepositoryCloner
   ↑
Console (依賴全部) — 新增 CLI command: release-risk
```

---

## 風險類別定義（RiskCategory）

| 類別 | Enum 值 | 說明 | 偵測方式 |
|------|---------|------|---------|
| 跨服務 API 破壞性變更 | `CrossServiceApiBreaking` | endpoint 修改、request/response 格式變動 | Controller/API 檔案 diff 分析 |
| 共用函式庫/套件變動 | `SharedLibraryChange` | NuGet 版本升降級 | .csproj 檔案版本變更偵測 |
| 資料庫 Schema 變更 | `DatabaseSchemaChange` | Migration、欄位異動 | Migration 檔案偵測 |
| 資料庫資料異動 | `DatabaseDataChange` | INSERT/UPDATE/DELETE/SaveChanges 邏輯變更 | 資料存取程式碼 diff 分析 |
| 設定檔變更 | `ConfigurationChange` | appsettings、環境變數異動 | 設定檔 diff 偵測 |
| 安全性相關變動 | `SecurityChange` | 認證、授權邏輯修改 | Auth/JWT/Policy 相關程式碼分析 |
| 效能相關變動 | `PerformanceChange` | 快取策略、查詢修改 | Cache/Redis/Query 相關程式碼分析 |
| 核心商業邏輯修改 | `CoreBusinessLogicChange` | 金流、訂單、權限等核心功能 | 核心 Service 層程式碼分析 |

### 風險等級定義（RiskLevel）

| 等級 | 說明 | 範例 |
|------|------|------|
| `Critical` | 可能導致線上服務中斷或資料遺失 | DB Migration 刪除欄位、核心金流計算邏輯修改 |
| `High` | 可能造成功能異常或安全漏洞 | API 回傳格式變更、認證邏輯修改 |
| `Medium` | 可能影響效能或需要其他服務同步更新 | 快取策略變更、共用套件升級 |
| `Low` | 小幅修改，風險可控 | 非核心 Service 邏輯調整 |
| `None` | 無風險 | 純文件、測試、格式化變更 |

---

## 分析流程（5 個 Phase）

### Phase 1：收集 PR Diff 資料

**輸入**：Redis 中已存在的 PR 列表（由現有 FetchMergeRequests 任務已抓取）

**處理邏輯**：

1. 從 Redis 讀取已取得的 PR/MR 資訊
2. 對每個 PR，呼叫對應平台 API 取得 diff：
   - GitLab: `GET /api/v4/projects/:id/merge_requests/:iid/changes`
   - Bitbucket: `GET /2.0/repositories/:workspace/:repo/pullrequests/:id/diffstat` + `/diff`
3. 整理成統一的 `PullRequestDiff` 格式
4. 儲存到 Redis（`ReleaseData:RiskAnalysis:Diffs`）

**輸出**：`IReadOnlyList<PullRequestDiff>`

```csharp
/// <summary>
/// 統一的 PR diff 資料結構
/// </summary>
public sealed record PullRequestDiff
{
    /// <summary>PR 基本資訊</summary>
    public required MergeRequestOutput PullRequest { get; init; }

    /// <summary>變更的檔案清單</summary>
    public required IReadOnlyList<FileDiff> Files { get; init; }
}

/// <summary>
/// 單一檔案的 diff 資訊
/// </summary>
public sealed record FileDiff
{
    /// <summary>檔案路徑</summary>
    public required string FilePath { get; init; }

    /// <summary>新增行數</summary>
    public required int AddedLines { get; init; }

    /// <summary>刪除行數</summary>
    public required int DeletedLines { get; init; }

    /// <summary>Diff patch 內容</summary>
    public required string DiffContent { get; init; }

    /// <summary>是否為新增檔案</summary>
    public required bool IsNewFile { get; init; }

    /// <summary>是否為刪除檔案</summary>
    public required bool IsDeletedFile { get; init; }
}
```

### Phase 2：AI 初篩（風險分類）

**輸入**：Phase 1 的 diff 資料

**處理邏輯**：

1. 按 Repo 分組 PR
2. 每個 Repo 的 PRs 按批次送 AI（每批最多 10 個 PR）
3. 單一 PR 的 diff 超過 50KB 時，單獨一批
4. 對每個 PR，AI 評估：
   - 風險等級（RiskLevel）
   - 風險類別（RiskCategory，可多選）
   - 風險描述
   - 是否需要 Phase 3 深度分析
   - 受影響的元件清單
5. 儲存到 Redis

**AI 判斷邏輯**：

```
對每個 PR 的每個變更檔案：
├─ 檔案是 Controller/API Endpoint？        → CrossServiceApiBreaking
├─ 檔案是 Migration？                      → DatabaseSchemaChange
├─ 程式碼包含 INSERT/UPDATE/DELETE/SaveChanges？ → DatabaseDataChange
├─ 檔案是 appsettings/env/config？         → ConfigurationChange
├─ 涉及 Auth/JWT/Policy/Authorize？        → SecurityChange
├─ 涉及 Cache/Redis/Query 效能模式？       → PerformanceChange
├─ 涉及 .csproj 版本變更？                 → SharedLibraryChange
└─ 涉及核心業務 Service（金流、訂單等）？  → CoreBusinessLogicChange
```

**輸出**：`IReadOnlyList<PullRequestRisk>`

```csharp
/// <summary>
/// 單一 PR 的風險評估結果
/// </summary>
public sealed record PullRequestRisk
{
    /// <summary>PR 識別資訊</summary>
    public required string PrId { get; init; }

    /// <summary>所屬 Repository 名稱</summary>
    public required string RepositoryName { get; init; }

    /// <summary>風險等級</summary>
    public required RiskLevel RiskLevel { get; init; }

    /// <summary>風險類別（可多個）</summary>
    public required IReadOnlyList<RiskCategory> RiskCategories { get; init; }

    /// <summary>風險描述</summary>
    public required string RiskDescription { get; init; }

    /// <summary>是否需要深度分析</summary>
    public required bool NeedsDeepAnalysis { get; init; }

    /// <summary>受影響的元件</summary>
    public required IReadOnlyList<string> AffectedComponents { get; init; }

    /// <summary>建議行動</summary>
    public required string SuggestedAction { get; init; }
}
```

### Phase 3：深度分析（全 Repo Clone）

**輸入**：Phase 2 中風險等級達到設定門檻的 PR

**處理邏輯**：

1. Clone **所有已設定的 Repos** 到工作目錄
   - GitLab: 使用 AccessToken 進行 `git clone https://oauth2:{token}@gitlab.example.com/group/project.git`
   - Bitbucket: 使用 Email + AccessToken 進行 `git clone https://{email}:{token}@bitbucket.org/workspace/repo.git`
   - Clone 目錄：可設定的 `CloneBasePath`
   - 不使用 shallow clone，保留完整 repo
2. 對每個需要深度分析的 PR：
   - 讀取變更檔案的**完整內容**（整個 class/module）
   - 讀取相關的 interface、base class
   - 掃描**所有其他 Repos** 中是否有引用被變更程式碼的地方
   - 蒐集完整上下文
3. 將完整上下文送 AI 進行深度分析
4. 分析完成後，根據設定決定是否保留 clone 的 repos

**輸出**：深度分析後更新的 `PullRequestRisk`（更精確的風險描述和影響評估）

```csharp
/// <summary>
/// 深度分析的上下文資料
/// </summary>
public sealed record DeepAnalysisContext
{
    /// <summary>初篩結果</summary>
    public required PullRequestRisk InitialRisk { get; init; }

    /// <summary>變更檔案的完整內容</summary>
    public required IReadOnlyList<FileContent> FullFileContents { get; init; }

    /// <summary>相關的 interface 和 base class 內容</summary>
    public required IReadOnlyList<FileContent> RelatedFiles { get; init; }

    /// <summary>其他 Repos 中引用此程式碼的地方</summary>
    public required IReadOnlyList<CrossRepoReference> CrossRepoReferences { get; init; }
}
```

### Phase 4：跨服務關聯分析

**輸入**：Phase 2 + Phase 3 的所有分析結果

**處理邏輯**：

1. 彙整所有服務的 API 變更清單
2. 利用 Phase 3 clone 的所有 repos，透過 AI 推斷服務相依性
3. AI 分析跨服務影響：
   - API endpoint 簽名變更 → 哪些服務呼叫了這個 API
   - 共用 NuGet 套件版本升級 → 所有使用方是否都升級了
   - DB Schema 變更 → 多服務共用 DB 時的影響
   - 設定檔變更 → 其他服務是否需要同步調整
4. 產出跨服務風險關聯

**輸出**：`IReadOnlyList<CrossServiceRisk>`

```csharp
/// <summary>
/// 跨服務風險關聯
/// </summary>
public sealed record CrossServiceRisk
{
    /// <summary>來源服務（發起變更的服務）</summary>
    public required string SourceService { get; init; }

    /// <summary>受影響的服務</summary>
    public required IReadOnlyList<string> AffectedServices { get; init; }

    /// <summary>風險等級</summary>
    public required RiskLevel RiskLevel { get; init; }

    /// <summary>影響描述</summary>
    public required string ImpactDescription { get; init; }

    /// <summary>建議行動</summary>
    public required string SuggestedAction { get; init; }

    /// <summary>關聯的 PR</summary>
    public required IReadOnlyList<string> RelatedPrIds { get; init; }
}
```

### Phase 5：報告產出

**輸入**：Phase 2 + Phase 3 + Phase 4 的所有結果

**處理邏輯**：

1. 彙整所有風險分析結果
2. 建構 `RiskAnalysisReport` 聚合根
3. 產出 Markdown 報告
4. 儲存到設定的 `ReportOutputPath`

**報告結構**：

```markdown
# Release Risk Analysis Report
## 分析時間：{timestamp}
## 分析範圍：{repoCount} repositories, {prCount} PRs

---

## 📊 風險摘要
| 風險等級 | 數量 |
|---------|------|
| 🔴 Critical | {count} |
| 🟠 High | {count} |
| 🟡 Medium | {count} |
| 🟢 Low | {count} |
| ⚪ None | {count} |

## 📈 風險類別分布
| 風險類別 | 數量 |
|---------|------|
| 跨服務 API 破壞性變更 | {count} |
| 共用函式庫/套件變動 | {count} |
| ... | ... |

---

## 🚨 Critical & High Risk Items

### [{repoName}] PR #{prId} - {prTitle}
- **風險等級**: 🔴 Critical
- **風險類別**: 核心商業邏輯修改, 跨服務 API 變更
- **變更摘要**: {summary}
- **風險描述**: {description}
- **受影響元件**: {components}
- **跨服務影響**: {affectedServices}
- **建議行動**: {suggestedAction}
- **PR 連結**: {prUrl}

---

## 🔗 跨服務影響分析

### {sourceService} → {affectedServices}
- **風險等級**: {riskLevel}
- **影響描述**: {description}
- **關聯 PR**: {prLinks}
- **建議行動**: {action}

---

## 📁 各 Repository 詳細分析

### {repoName} ({platform})
**分析 PR 數**: {count} | **最高風險等級**: {maxRiskLevel}

#### PR #{prId} - {prTitle}
- **風險等級**: {riskLevel}
- **風險類別**: {categories}
- **說明**: {description}
```

---

## AI 分析策略

### Copilot SDK 使用模式

沿用現有 `CopilotTitleEnhancer` 的成功模式：

```csharp
/// <summary>
/// AI 風險分析介面
/// </summary>
public interface IRiskAnalyzer
{
    /// <summary>Phase 2：批次初篩風險分類</summary>
    Task<IReadOnlyList<PullRequestRisk>> ScreenRisksAsync(
        IReadOnlyList<PullRequestDiff> diffs);

    /// <summary>Phase 3：深度分析高風險 PR</summary>
    Task<IReadOnlyList<PullRequestRisk>> DeepAnalyzeAsync(
        IReadOnlyList<DeepAnalysisContext> contexts);

    /// <summary>Phase 4：跨服務關聯分析</summary>
    Task<IReadOnlyList<CrossServiceRisk>> AnalyzeCrossServiceImpactAsync(
        IReadOnlyList<PullRequestRisk> allRisks,
        IReadOnlyList<ServiceDependencyInfo> dependencies);
}
```

### 批次處理策略

| Phase | 批次策略 | 原因 |
|-------|---------|------|
| Phase 2 | 按 Repo 分組，每批最多 10 個 PR | 控制 context window 大小 |
| Phase 3 | 逐 PR 分析 | 每個 PR 的完整上下文已經很大 |
| Phase 4 | 彙整所有風險摘要（不含完整 diff） | 全局視角分析 |

### Prompt 工程

**Phase 2 System Prompt（初篩）**：

```
你是一位資深軟體架構師和程式碼安全審查專家。
你的任務是分析 Pull Request 的程式碼變更，評估可能的風險。

你必須對每個 PR 產出 JSON 格式的分析結果，包含：
1. riskLevel: "Critical" | "High" | "Medium" | "Low" | "None"
2. riskCategories: [...] (可多選)
3. riskDescription: 風險描述（繁體中文）
4. needsDeepAnalysis: boolean
5. affectedComponents: 受影響的元件列表
6. suggestedAction: 建議行動（繁體中文）

【風險判斷準則】
- Critical: 可能導致線上服務中斷或資料遺失
- High: 可能造成功能異常或安全漏洞
- Medium: 可能影響效能或需要其他服務同步更新
- Low: 小幅修改，風險可控
- None: 純文件、測試、格式化等無風險變更

【最重要】回應必須只有純 JSON 陣列，禁止包含任何文字說明、markdown 格式或 code block 標記。
```

**Phase 3 System Prompt（深度分析）**：

```
你是一位微服務架構的資深審查專家。
你現在看到的是一個高風險 PR 的完整上下文，包括：
- 變更前後的完整檔案
- 相關的 interface 和 base class
- 其他服務中呼叫此程式碼的地方

請深入分析此變更的風險，特別關注：
1. 是否有破壞性 API 變更（endpoint 簽名、request/response model）？
2. 資料庫異動是否可能造成資料不一致或資料遺失？
3. 資料存取邏輯變更是否影響資料完整性？
4. 是否有向後相容性問題？
5. 哪些下游服務可能受影響？

【最重要】回應必須只有純 JSON，禁止包含任何文字說明、markdown 格式或 code block 標記。
```

**Phase 4 System Prompt（跨服務關聯）**：

```
你是一位微服務架構的資深架構師。
你現在看到的是所有服務的風險分析摘要和服務相依資訊。

請分析跨服務的影響關聯：
1. 哪些服務的變更可能影響其他服務？
2. 是否有服務修改了 API 但呼叫方未同步更新？
3. 共用套件版本是否一致？
4. DB Schema 變更是否影響共用資料庫的其他服務？

對每個跨服務風險，產出：來源服務、受影響服務、風險等級、影響描述、建議行動。

【最重要】回應必須只有純 JSON 陣列，禁止包含任何文字說明、markdown 格式或 code block 標記。
```

---

## 設定管理

### 新增設定區段

```json
{
  "RiskAnalysis": {
    "CloneBasePath": "/tmp/release-risk-repos",
    "CleanupAfterAnalysis": false,
    "MaxDiffSizeBytes": 51200,
    "BatchSize": 10,
    "RiskThresholdForDeepAnalysis": "Medium",
    "ReportOutputPath": "./reports"
  }
}
```

| 設定項 | 型別 | 預設值 | 說明 |
|-------|------|--------|------|
| `CloneBasePath` | string | `/tmp/release-risk-repos` | Clone repos 的工作目錄 |
| `CleanupAfterAnalysis` | bool | `false` | 分析完是否清理 clone 的 repos |
| `MaxDiffSizeBytes` | int | `51200` (50KB) | 超過此大小的 diff 單獨一批送 AI |
| `BatchSize` | int | `10` | Phase 2 每批最多 PR 數量 |
| `RiskThresholdForDeepAnalysis` | string | `"Medium"` | 達到此風險等級才進入 Phase 3 深度分析 |
| `ReportOutputPath` | string | `"./reports"` | Markdown 報告輸出路徑 |

### Clone 認證

重用現有的 GitLab/Bitbucket 設定中的 AccessToken，不需額外設定認證資訊。

---

## 錯誤處理

遵循專案憲法 — **禁止 try-catch**，使用 Result Pattern：

| 元件 | 回傳類型 | 錯誤處理 |
|------|---------|---------|
| `IDiffProvider` | `Result<PullRequestDiff>` | API 呼叫失敗回傳 Error |
| `IRepositoryCloner` | `Result<ClonedRepository>` | Clone 失敗回傳 Error |
| `IRiskAnalyzer` | fallback 模式 | AI 回應異常時使用 fallback |

### Fallback 策略

- AI 回應格式錯誤 → 該批次 PR 標記為 `RiskLevel.Medium`，描述為「AI 分析失敗，建議人工審查」
- 單一 PR diff 取得失敗 → 跳過該 PR，記錄 warning log，繼續分析其他 PR
- Clone 失敗 → 該 Repo 跳過深度分析，使用 Phase 2 初篩結果
- 整體分析仍能產出報告（graceful degradation）

---

## 測試策略

遵循 TDD（Red-Green-Refactor），先測試再實作。

### Domain Layer Tests

| 測試案例 | 測試重點 |
|---------|---------|
| RiskLevel 比較 | Critical > High > Medium > Low > None |
| RiskCategory 列舉 | 8 種類別完整覆蓋 |
| RiskFinding 建構 | 必要欄位驗證 |
| RiskAnalysisReport 聚合 | 報告統計正確性 |
| PullRequestRisk 建構 | 風險評估結構正確性 |

### Application Layer Tests

| 測試案例 | Mock 策略 |
|---------|----------|
| 空 PR 列表 → 無風險報告 | Mock IDiffProvider 回空列表 |
| 單一高風險 PR → 觸發深度分析 | Mock IRiskAnalyzer 回高風險結果 |
| 批次分組邏輯 | Mock data，驗證分組正確 |
| Markdown 報告格式 | 驗證報告結構正確 |
| Phase 流程協調 | Mock 所有介面，驗證 Phase 順序 |
| AI 回應格式錯誤 → fallback | Mock IRiskAnalyzer 回異常結果 |

### Infrastructure Layer Tests

| 測試案例 | Mock 策略 |
|---------|----------|
| GitLabDiffProvider API 呼叫 | Mock HttpClient |
| BitbucketDiffProvider API 呼叫 | Mock HttpClient |
| CopilotRiskAnalyzer prompt 建構 | Mock CopilotClient |
| CopilotRiskAnalyzer response 解析 | 測試各種 AI 回應格式 |
| RepositoryCloner git 操作 | Integration test 或 Mock Process |

---

## CLI 整合

### 新增 CLI Command

```
dotnet ReleaseKit.Console release-risk
```

### TaskType 擴充

```csharp
AnalyzeReleaseRisk = "分析 Release 程式變更風險"
```

### CommandLineParser 映射

```csharp
{ "release-risk", TaskType.AnalyzeReleaseRisk }
```

### TaskFactory 註冊

```csharp
TaskType.AnalyzeReleaseRisk => _serviceProvider.GetRequiredService<AnalyzeReleaseRiskTask>()
```

---

## Redis 資料流

```
FetchMergeRequests (既有)
    ↓ Redis[ReleaseData:MergeRequests]
    
Phase 1: FetchDiffs
    ↓ Redis[ReleaseData:RiskAnalysis:Diffs]
    
Phase 2: ScreenRisks
    ↓ Redis[ReleaseData:RiskAnalysis:ScreenResults]
    
Phase 3: DeepAnalysis
    ↓ Redis[ReleaseData:RiskAnalysis:DeepResults]
    
Phase 4: CrossServiceAnalysis
    ↓ Redis[ReleaseData:RiskAnalysis:CrossServiceResults]
    
Phase 5: GenerateReport
    → Markdown file (file system)
```

---

## 實作優先順序

1. **Domain Layer** — Entity、ValueObject、Abstraction 定義
2. **Infrastructure: DiffProviders** — GitLab/Bitbucket diff API 整合
3. **Infrastructure: RepositoryCloner** — Git clone 功能
4. **Infrastructure: CopilotRiskAnalyzer** — AI 風險分析（3 個 Phase）
5. **Application: AnalyzeReleaseRiskTask** — 5 Phase 協調邏輯
6. **Application: RiskReportGenerator** — Markdown 報告產生
7. **Console: CLI Command** — release-risk 命令整合
8. **Configuration** — RiskAnalysisOptions 與 DI 註冊
