# Agentic 風險分析設計規格

> **前置設計**: `2026-04-06-release-risk-analysis-design.md`
> **日期**: 2026-04-11
> **狀態**: Draft

## 1. 問題陳述

現行風險分析流程採用「預處理數據 → 被動傳遞給 AI」模式：系統先 clone repo、抽取 PR diff，再將 diff 文字整批送入 Copilot 進行分析。此模式有以下限制：

1. **固化的數據收集策略**：`extract-pr-diffs` 只執行 `git diff`，無法根據變更特性動態調整探索策略
2. **Token 浪費**：將完整 diff 一次送入，即使多數內容與風險無關
3. **缺乏深度探索**：AI 無法主動查看特定檔案內容、搜尋程式碼模式、或檢查 git history
4. **多 Pass 機制複雜**：Pass 1-10 的動態層級設計增加了系統複雜度

## 2. 設計目標

將風險分析的「數據收集」與「分析」合併為 Copilot 自主驅動的 agentic 流程：

- Copilot 接收 `{ projectName, repoPath, commitShas[] }` 後，自行決定要執行什麼指令
- 透過 Copilot SDK 的 `Tools` 機制註冊 `run_command` 工具
- Copilot 可執行任意 shell 指令（git diff、grep、cat、find 等）
- 中間分析結果仍存入 Redis，最終報告由獨立步驟生成

## 3. 架構設計

### 3.1 整體流程

```
clone-repos → analyze-risk → generate-risk-report
     │              │                  │
     │              │                  │
     ▼              ▼                  ▼
  Clone repos    Per-project        讀取中間結果
  存 ClonePaths  Copilot session    Copilot 生成
  至 Redis       (並行, agentic)    最終 Markdown
                 存中間結果至 Redis  存 Redis + 檔案
```

### 3.2 CLI 指令重構

| 現有指令                    | 新指令                | 變更說明                    |
|-----------------------------|----------------------|----------------------------|
| `clone-repos`               | `clone-repos`        | 不動                        |
| `extract-pr-diffs`          | _(移除)_             | Copilot 自行取得變更資訊     |
| `analyze-project-risk`      | _(移除)_             | 合併入 `analyze-risk`       |
| `analyze-cross-project-risk`| _(移除)_             | 合併入 `analyze-risk`       |
| `generate-risk-report`      | `generate-risk-report`| 重構為讀取中間結果 + 生成   |
| `analyze-risk`              | `analyze-risk`       | 重構為 agentic 分析流程     |

### 3.3 `analyze-risk` 新流程

1. 從 Redis 讀取 PR 資料（`GitLabHash[PullRequestsByUser]` / `BitbucketHash[PullRequestsByUser]`）
2. 從 Redis 讀取 clone 路徑（`RiskAnalysisHash[ClonePaths]`）
3. 為每個專案組裝 `ProjectAnalysisContext`：
   - `ProjectName`: 專案名稱
   - `RepoPath`: 本地 clone 路徑
   - `CommitShas`: 相關的 commit SHA 列表
   - 若專案無 commit SHA 則跳過
4. 並行建立 Copilot session（使用 `SemaphoreSlim` 控制並行度）：
   - 每個 session 註冊 `run_command` 工具
   - System prompt 指導分析目標與輸出格式
   - Copilot 自主探索 repo 並進行風險分析
   - 產出混合格式結果（JSON 風險項目 + 文字分析）存入 Redis

### 3.4 `generate-risk-report` 新流程

1. 從 Redis 讀取所有 `Intermediate:*` 中間報告
2. 建立 Copilot session（**不**註冊 `run_command` 工具）
3. 將所有中間報告作為 user prompt 傳入
4. Copilot 彙整為最終 Markdown 報告
5. 存入 Redis `FinalReport` + 寫入檔案

## 4. 元件設計

### 4.1 `run_command` 工具（ShellCommandTool）

```csharp
/// <summary>在指定的 repo 目錄中執行 shell 指令</summary>
[Description("在指定的 repo 目錄中執行 shell 指令，回傳 stdout 與 stderr")]
public static async Task<string> RunCommandAsync(
    [Description("要執行的 shell 指令")] string command,
    [Description("工作目錄路徑")] string workingDirectory)
```

**安全性**：
- 工作目錄必須在 `CloneBasePath` 路徑下（防止路徑逃逸）
- 每次指令執行超時上限（可設定，預設 30 秒）

**輸出管理**：
- System prompt 中告知 Copilot 每次指令輸出最多 N 字元
- 由 Copilot 自行在指令中加入 `| head -n`、`| tail -n`、`| grep` 等控制
- 若實際輸出超限，截斷並附加提示訊息

### 4.2 IRiskAnalyzer 介面重設計

```csharp
/// <summary>風險分析器介面（Agentic 模式）</summary>
public interface IRiskAnalyzer
{
    /// <summary>分析單一專案的變更風險（Agentic：Copilot 自主探索 repo）</summary>
    Task<RiskAnalysisReport> AnalyzeProjectRiskAsync(
        ProjectAnalysisContext context,
        CancellationToken cancellationToken = default);

    /// <summary>產生最終整合報告 Markdown</summary>
    Task<string> GenerateFinalReportAsync(
        IReadOnlyList<RiskAnalysisReport> reports,
        CancellationToken cancellationToken = default);
}
```

**變更說明**：
- 移除 `AnalyzeDeepAsync`（不再有多 Pass 機制）
- `AnalyzeProjectRiskAsync` 改為接收 `ProjectAnalysisContext`（包含 repoPath + commitShas）
- Copilot 在單一 session 中完成所有深度分析（不再區分 Pass 1 和 Pass 2-10）

### 4.3 ProjectAnalysisContext（Value Object）

```csharp
/// <summary>專案分析輸入上下文</summary>
public sealed record ProjectAnalysisContext
{
    /// <summary>專案名稱</summary>
    public required string ProjectName { get; init; }

    /// <summary>本地 clone 路徑</summary>
    public required string RepoPath { get; init; }

    /// <summary>要分析的 commit SHA 列表</summary>
    public required IReadOnlyList<string> CommitShas { get; init; }
}
```

### 4.4 IShellCommandExecutor 抽象介面

```csharp
/// <summary>Shell 指令執行器介面（供測試 Mock 使用）</summary>
public interface IShellCommandExecutor
{
    /// <summary>在指定工作目錄執行 shell 指令</summary>
    Task<ShellCommandResult> ExecuteAsync(
        string command,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

/// <summary>Shell 指令執行結果</summary>
public sealed record ShellCommandResult
{
    /// <summary>標準輸出</summary>
    public required string StandardOutput { get; init; }

    /// <summary>標準錯誤</summary>
    public required string StandardError { get; init; }

    /// <summary>結束碼</summary>
    public required int ExitCode { get; init; }

    /// <summary>是否因超時而終止</summary>
    public required bool TimedOut { get; init; }
}
```

### 4.5 CopilotRiskAnalyzer 重構

核心變更：
1. 建立 session 時註冊 `run_command` 工具
2. System prompt 包含分析指導 + 輸出格式要求 + 工具使用說明
3. User prompt 提供 `ProjectAnalysisContext` 的 JSON
4. `SendAndWaitAsync` 等待 Copilot 完成所有工具呼叫與分析
5. 解析回應為 `RiskAnalysisReport`

```csharp
// Session 建立範例
await using var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = model,
    SystemMessage = new SystemMessageConfig
    {
        Mode = SystemMessageMode.Replace,
        Content = systemPrompt
    },
    Tools = [AIFunctionFactory.Create(
        (string command, string workingDirectory) =>
            shellExecutor.ExecuteAsync(command, workingDirectory, timeout, ct))],
    InfiniteSessions = new InfiniteSessionConfig { Enabled = false },
    OnPermissionRequest = PermissionHandler.ApproveAll
});
```

## 5. System Prompt 設計

### 5.1 Per-Project 分析 Prompt

```
你是一位資深軟體架構師，專精於微服務架構風險分析。

## 你的任務
分析專案 "{projectName}" 的 commit 變更，識別所有可能影響其他服務的風險。

## 可用工具
你可以使用 `run_command` 工具在 repo 目錄中執行任意 shell 指令。
- 建議先用 `git log`、`git diff`、`git show` 了解變更範圍
- 可用 `grep`、`cat`、`find` 等深入檢查特定檔案
- 每次指令輸出最多 {maxOutputChars} 字元，請自行用 | head、| tail、| grep 控制輸出量

## 風險類別
1. API 契約變更 (ApiContract)
2. DB Schema 變更 (DatabaseSchema)
3. DB 資料異動 (DatabaseData)
4. 事件/訊息格式變更 (EventFormat)
5. 設定檔變更 (Configuration)

## 【最重要】分析重點
- 「改 A 壞 B」情境：資料異動可能導致其他服務的 switch/case、LINQ 查詢、硬編碼值失效
- Lookup table 新增/修改值 → 消費端可能沒有對應處理
- Stored Procedure 參數變更 → 呼叫端可能傳錯參數

## 輸出格式
你的最終回應必須是純 JSON（禁止 markdown code block），格式如下：
{
  "riskItems": [
    {
      "category": "ApiContract|DatabaseSchema|DatabaseData|EventFormat|Configuration",
      "level": "High|Medium|Low",
      "changeSummary": "變更摘要（繁體中文）",
      "affectedFiles": ["file1.cs", "file2.cs"],
      "potentiallyAffectedServices": ["ServiceA", "ServiceB"],
      "impactDescription": "影響說明（繁體中文）",
      "suggestedValidationSteps": ["驗證步驟1", "驗證步驟2"]
    }
  ],
  "summary": "整體分析摘要（繁體中文）",
  "analysisLog": "你執行了哪些指令、為什麼執行這些指令的簡要說明（繁體中文）"
}
```

### 5.2 最終報告生成 Prompt

與現有 `GenerateFinalReportAsync` 的 system prompt 基本一致，接收所有中間報告並產生 Markdown。

## 6. Redis 儲存變更

### 移除的 Fields
- `PrDiffs` — 不再預抽 diff

### 移除的 Key 模式
- `Intermediate:{pass}-{sequence}-{subSequence}` — 不再有 Pass/SubSequence
- `PassMetadata:{pass}` — 不再有多 Pass 機制

### 保留的 Fields
- `ClonePaths` — 仍由 clone-repos 寫入
- `FinalReport` — 仍由 generate-risk-report 寫入

### 簡化的中間結果
- `Intermediate:{sequence}` — 每個專案一個結果（sequence = 專案排序序號）
- `AnalysisContext:{sequence}` — 每個專案的輸入上下文（方便除錯重現）

## 7. 受影響的檔案

### 移除
| 檔案 | 原因 |
|------|------|
| `ExtractPrDiffsTask.cs` | Copilot 自行取得變更資訊 |
| `AnalyzeProjectRiskTask.cs` | 合併入新的 AnalyzeRiskTask |
| `AnalyzeCrossProjectRiskTask.cs` | 合併入新的 AnalyzeRiskTask |

### 重構
| 檔案 | 變更說明 |
|------|---------|
| `AnalyzeRiskTask.cs` | 從 orchestrator 重構為 agentic 分析流程 |
| `GenerateRiskReportTask.cs` | 簡化為讀取中間結果 + Copilot 生成 |
| `CopilotRiskAnalyzer.cs` | 重構為 agentic 模式（工具註冊、session 管理） |
| `IRiskAnalyzer.cs` | 介面重設計（移除 AnalyzeDeepAsync） |
| `CommandLineParser.cs` | 移除已刪除的 CLI 指令 |
| `TaskFactory.cs` | 移除已刪除的 Task 類型 |
| `ServiceCollectionExtensions.cs` | 更新 DI 註冊 |
| `RedisKeys.cs` | 更新 Redis key 常數 |
| `TaskType.cs` | 移除已刪除的枚舉值 |
| `AnalysisPassKey.cs` | 簡化或移除（不再有 Pass 概念） |

### 新增
| 檔案 | 說明 |
|------|------|
| `ShellCommandTool.cs` | `run_command` 工具實作（Infrastructure 層） |
| `IShellCommandExecutor.cs` | Shell 執行抽象介面（Domain 層） |
| `ShellCommandExecutor.cs` | Shell 執行器實作（Infrastructure 層） |
| `ShellCommandResult.cs` | Shell 執行結果 Value Object（Domain 層） |
| `ProjectAnalysisContext.cs` | 專案分析輸入 Value Object（Domain 層） |

### 不受影響
| 檔案 | 原因 |
|------|------|
| `CloneRepositoriesTask.cs` | 完全不動 |
| `CopilotTitleEnhancer.cs` | 完全不動 |
| `RiskItem.cs` | 保留 |
| `RiskAnalysisReport.cs` | 可能微調（移除 PassKey 相關欄位） |
| `DynamicAnalysisResult.cs` | 可能移除（不再有動態 Pass） |

## 8. 測試策略

### 單元測試

1. **ShellCommandExecutor 測試**
   - 工作目錄限制（路徑逃逸防護）
   - 指令超時處理
   - 正常執行回傳 stdout/stderr/exitCode

2. **AnalyzeRiskTask 測試**
   - Mock `IRiskAnalyzer` + `IRedisService`
   - 驗證從 Redis 讀取 PR 資料與 clone 路徑
   - 驗證 `ProjectAnalysisContext` 組裝邏輯
   - 驗證無 commit SHA 的專案被跳過
   - 驗證並行控制（SemaphoreSlim）
   - 驗證中間結果存入 Redis

3. **GenerateRiskReportTask 測試**
   - Mock 依賴
   - 驗證從 Redis 讀取中間報告
   - 驗證最終報告存入 Redis + 檔案

4. **CopilotRiskAnalyzer 測試**
   - 驗證 session 建立參數（model、system prompt、tools）
   - 驗證回應解析（JSON → RiskAnalysisReport）
   - 驗證認證失敗處理
   - 驗證超時處理

### 整合測試
- 需要 Copilot SDK 連線的端對端測試（可選）

## 9. 設定檔變更

### RiskAnalysisOptions 新增

```csharp
/// <summary>每次 shell 指令輸出字元數上限</summary>
public int MaxOutputCharacters { get; init; } = 50000;

/// <summary>每次 shell 指令超時（秒）</summary>
public int CommandTimeoutSeconds { get; init; } = 30;
```

### 移除

```csharp
/// <summary>每次 AI 呼叫的 token 上限（不再需要，由 Copilot 自管）</summary>
// 移除 MaxTokensPerAiCall

/// <summary>最大分析 Pass 數（不再有多 Pass 機制）</summary>
// 移除 MaxAnalysisPasses
```

## 10. 遷移計畫

此次變更為風險分析子系統的重構，不影響其他功能模組（如 PR 拉取、Work Item 同步、Google Sheet 更新等）。

**向後相容性**：
- Redis 中的舊格式中間結果（`Intermediate:1-*` 等）不再被讀取
- 建議在部署前清除 `RiskAnalysisHash` 的舊資料
