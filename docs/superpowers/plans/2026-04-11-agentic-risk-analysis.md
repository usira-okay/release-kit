# Agentic 風險分析 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 將風險分析流程從「預處理數據 → 被動傳遞」重構為 Copilot SDK 自主驅動的 agentic 模式，Copilot 接收 repo 路徑與 commit SHA 後自行決定要執行什麼 shell 指令來分析變更風險。

**Architecture:** 3 個 CLI 指令（clone-repos → analyze-risk → generate-risk-report）。analyze-risk 從 Redis 讀取 PR 資料與 clone 路徑，為每個專案建立 Copilot session 並註冊 `run_command` 工具讓 AI 自主探索 repo。移除 extract-pr-diffs、analyze-project-risk、analyze-cross-project-risk 三個指令。

**Tech Stack:** .NET 10, GitHub Copilot SDK v0.1.32, xUnit 2.9.2, Moq 4.20.72, FluentAssertions, StackExchange.Redis

**Design Spec:** `docs/superpowers/specs/2026-04-11-agentic-risk-analysis-design.md`

---

## File Structure

### 新增檔案

| 檔案 | 層級 | 職責 |
|------|------|------|
| `src/ReleaseKit.Domain/ValueObjects/ProjectAnalysisContext.cs` | Domain | 專案分析輸入上下文（projectName, repoPath, commitShas） |
| `src/ReleaseKit.Domain/ValueObjects/ShellCommandResult.cs` | Domain | Shell 指令執行結果 Value Object |
| `src/ReleaseKit.Domain/Abstractions/IShellCommandExecutor.cs` | Domain | Shell 指令執行器抽象介面 |
| `src/ReleaseKit.Infrastructure/Shell/ShellCommandExecutor.cs` | Infrastructure | Shell 指令執行器實作（Process.Start） |
| `tests/ReleaseKit.Infrastructure.Tests/Shell/ShellCommandExecutorTests.cs` | Tests | ShellCommandExecutor 單元測試 |
| `tests/ReleaseKit.Domain.Tests/ValueObjects/ProjectAnalysisContextTests.cs` | Tests | ProjectAnalysisContext 測試 |
| `tests/ReleaseKit.Domain.Tests/ValueObjects/ShellCommandResultTests.cs` | Tests | ShellCommandResult 測試 |

### 重構檔案

| 檔案 | 變更說明 |
|------|---------|
| `src/ReleaseKit.Domain/Abstractions/IRiskAnalyzer.cs` | 移除 `AnalyzeDeepAsync`，`AnalyzeProjectRiskAsync` 改為接收 `ProjectAnalysisContext` |
| `src/ReleaseKit.Domain/Entities/RiskAnalysisReport.cs` | PassKey 改為 `int Sequence`，移除 PassKey 依賴 |
| `src/ReleaseKit.Domain/ValueObjects/AnalysisPassKey.cs` | 刪除（不再有 Pass 概念） |
| `src/ReleaseKit.Domain/Entities/DynamicAnalysisResult.cs` | 刪除（不再有動態 Pass 機制） |
| `src/ReleaseKit.Domain/Entities/PrDiffContext.cs` | 刪除（不再預抽 diff） |
| `src/ReleaseKit.Common/Configuration/RiskAnalysisOptions.cs` | 新增 MaxOutputCharacters、CommandTimeoutSeconds、MaxConcurrentAnalysis；移除 MaxTokensPerAiCall、MaxAnalysisPasses |
| `src/ReleaseKit.Common/Constants/RedisKeys.cs` | 移除 PrDiffs 欄位；新增 AnalysisContext 欄位 |
| `src/ReleaseKit.Application/Tasks/AnalyzeRiskTask.cs` | 完全重寫為 agentic 分析流程 |
| `src/ReleaseKit.Application/Tasks/GenerateRiskReportTask.cs` | 簡化為讀取所有 `Intermediate:` 前綴 |
| `src/ReleaseKit.Application/Tasks/TaskType.cs` | 移除 ExtractPrDiffs、AnalyzeProjectRisk、AnalyzeCrossProjectRisk |
| `src/ReleaseKit.Application/Tasks/TaskFactory.cs` | 移除已刪除的 Task 類型對應 |
| `src/ReleaseKit.Infrastructure/Copilot/CopilotRiskAnalyzer.cs` | 重寫為 agentic 模式（工具註冊、新 system prompt） |
| `src/ReleaseKit.Console/Parsers/CommandLineParser.cs` | 移除已刪除的 CLI 指令 |
| `src/ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs` | 更新 DI 註冊 |

### 刪除檔案

| 檔案 | 原因 |
|------|------|
| `src/ReleaseKit.Application/Tasks/ExtractPrDiffsTask.cs` | Copilot 自行取得變更資訊 |
| `src/ReleaseKit.Application/Tasks/AnalyzeProjectRiskTask.cs` | 合併入新的 AnalyzeRiskTask |
| `src/ReleaseKit.Application/Tasks/AnalyzeCrossProjectRiskTask.cs` | 不再有多 Pass 機制 |
| `tests/ReleaseKit.Application.Tests/Tasks/ExtractPrDiffsTaskTests.cs` | 對應 Task 已刪除 |
| `tests/ReleaseKit.Application.Tests/Tasks/AnalyzeProjectRiskTaskTests.cs` | 對應 Task 已刪除 |
| `tests/ReleaseKit.Application.Tests/Tasks/AnalyzeCrossProjectRiskTaskTests.cs` | 對應 Task 已刪除 |

### 重寫測試

| 檔案 | 變更說明 |
|------|---------|
| `tests/ReleaseKit.Application.Tests/Tasks/AnalyzeRiskTaskTests.cs` | 完全重寫（新的 agentic 流程測試） |
| `tests/ReleaseKit.Application.Tests/Tasks/GenerateRiskReportTaskTests.cs` | 更新（移除 Pass 概念，改用新 Redis key 模式） |
| `tests/ReleaseKit.Infrastructure.Tests/Copilot/CopilotRiskAnalyzerTests.cs` | 重寫（新 prompt、新解析、移除 dynamic 測試） |
| `tests/ReleaseKit.Domain.Tests/Entities/RiskAnalysisReportTests.cs` | 更新（移除 PassKey 相關測試） |
| `tests/ReleaseKit.Console.Tests/Parsers/CommandLineParserRiskTests.cs` | 更新（移除已刪除指令的測試） |

---

## Task 1: Domain — ProjectAnalysisContext Value Object

**Files:**
- Create: `src/ReleaseKit.Domain/ValueObjects/ProjectAnalysisContext.cs`
- Test: `tests/ReleaseKit.Domain.Tests/ValueObjects/ProjectAnalysisContextTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// tests/ReleaseKit.Domain.Tests/ValueObjects/ProjectAnalysisContextTests.cs
using FluentAssertions;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Tests.ValueObjects;

/// <summary>
/// ProjectAnalysisContext 值物件測試
/// </summary>
public class ProjectAnalysisContextTests
{
    [Fact]
    public void 建構_應正確設定所有屬性()
    {
        // Arrange & Act
        var context = new ProjectAnalysisContext
        {
            ProjectName = "my-service",
            RepoPath = "/repos/my-service",
            CommitShas = new List<string> { "abc123", "def456" }
        };

        // Assert
        context.ProjectName.Should().Be("my-service");
        context.RepoPath.Should().Be("/repos/my-service");
        context.CommitShas.Should().HaveCount(2);
        context.CommitShas.Should().ContainInOrder("abc123", "def456");
    }

    [Fact]
    public void 兩個相同值的Context_應視為相等()
    {
        // Arrange
        var shas = new List<string> { "abc123" };
        var context1 = new ProjectAnalysisContext
        {
            ProjectName = "svc",
            RepoPath = "/repos/svc",
            CommitShas = shas
        };
        var context2 = new ProjectAnalysisContext
        {
            ProjectName = "svc",
            RepoPath = "/repos/svc",
            CommitShas = shas
        };

        // Assert
        context1.Should().Be(context2);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd src && dotnet test --filter "FullyQualifiedName~ProjectAnalysisContextTests" --no-restore -v minimal`
Expected: FAIL — `ProjectAnalysisContext` type not found

- [ ] **Step 3: Write minimal implementation**

```csharp
// src/ReleaseKit.Domain/ValueObjects/ProjectAnalysisContext.cs
namespace ReleaseKit.Domain.ValueObjects;

/// <summary>
/// 專案分析輸入上下文
/// </summary>
/// <remarks>
/// 提供 Copilot agentic 分析所需的專案資訊，
/// 包含專案名稱、本地 clone 路徑與要分析的 commit SHA 列表。
/// </remarks>
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

- [ ] **Step 4: Run test to verify it passes**

Run: `cd src && dotnet test --filter "FullyQualifiedName~ProjectAnalysisContextTests" --no-restore -v minimal`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/ReleaseKit.Domain/ValueObjects/ProjectAnalysisContext.cs tests/ReleaseKit.Domain.Tests/ValueObjects/ProjectAnalysisContextTests.cs
git commit -m "feat: add ProjectAnalysisContext value object

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 2: Domain — ShellCommandResult Value Object

**Files:**
- Create: `src/ReleaseKit.Domain/ValueObjects/ShellCommandResult.cs`
- Test: `tests/ReleaseKit.Domain.Tests/ValueObjects/ShellCommandResultTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// tests/ReleaseKit.Domain.Tests/ValueObjects/ShellCommandResultTests.cs
using FluentAssertions;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Tests.ValueObjects;

/// <summary>
/// ShellCommandResult 值物件測試
/// </summary>
public class ShellCommandResultTests
{
    [Fact]
    public void 成功結果_應正確設定屬性()
    {
        // Arrange & Act
        var result = new ShellCommandResult
        {
            StandardOutput = "output text",
            StandardError = "",
            ExitCode = 0,
            TimedOut = false
        };

        // Assert
        result.StandardOutput.Should().Be("output text");
        result.StandardError.Should().BeEmpty();
        result.ExitCode.Should().Be(0);
        result.TimedOut.Should().BeFalse();
    }

    [Fact]
    public void 超時結果_TimedOut應為True()
    {
        // Arrange & Act
        var result = new ShellCommandResult
        {
            StandardOutput = "",
            StandardError = "command timed out",
            ExitCode = -1,
            TimedOut = true
        };

        // Assert
        result.TimedOut.Should().BeTrue();
        result.ExitCode.Should().Be(-1);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd src && dotnet test --filter "FullyQualifiedName~ShellCommandResultTests" --no-restore -v minimal`
Expected: FAIL — `ShellCommandResult` type not found

- [ ] **Step 3: Write minimal implementation**

```csharp
// src/ReleaseKit.Domain/ValueObjects/ShellCommandResult.cs
namespace ReleaseKit.Domain.ValueObjects;

/// <summary>
/// Shell 指令執行結果
/// </summary>
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

- [ ] **Step 4: Run test to verify it passes**

Run: `cd src && dotnet test --filter "FullyQualifiedName~ShellCommandResultTests" --no-restore -v minimal`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/ReleaseKit.Domain/ValueObjects/ShellCommandResult.cs tests/ReleaseKit.Domain.Tests/ValueObjects/ShellCommandResultTests.cs
git commit -m "feat: add ShellCommandResult value object

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 3: Domain — IShellCommandExecutor Interface

**Files:**
- Create: `src/ReleaseKit.Domain/Abstractions/IShellCommandExecutor.cs`

- [ ] **Step 1: Create the interface**

```csharp
// src/ReleaseKit.Domain/Abstractions/IShellCommandExecutor.cs
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Abstractions;

/// <summary>
/// Shell 指令執行器介面（供測試 Mock 使用）
/// </summary>
public interface IShellCommandExecutor
{
    /// <summary>在指定工作目錄執行 shell 指令</summary>
    /// <param name="command">要執行的 shell 指令</param>
    /// <param name="workingDirectory">工作目錄路徑</param>
    /// <param name="timeout">超時時間</param>
    /// <param name="cancellationToken">取消權杖</param>
    /// <returns>指令執行結果</returns>
    Task<ShellCommandResult> ExecuteAsync(
        string command,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Verify build**

Run: `cd src && dotnet build --no-restore -v minimal 2>&1 | tail -5`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add src/ReleaseKit.Domain/Abstractions/IShellCommandExecutor.cs
git commit -m "feat: add IShellCommandExecutor interface

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 4: Domain — Refactor RiskAnalysisReport (remove PassKey)

**Files:**
- Modify: `src/ReleaseKit.Domain/Entities/RiskAnalysisReport.cs`
- Delete: `src/ReleaseKit.Domain/ValueObjects/AnalysisPassKey.cs`
- Delete: `src/ReleaseKit.Domain/Entities/DynamicAnalysisResult.cs`
- Delete: `src/ReleaseKit.Domain/Entities/PrDiffContext.cs`
- Modify: `tests/ReleaseKit.Domain.Tests/Entities/RiskAnalysisReportTests.cs`

- [ ] **Step 1: Write the updated test**

First, read the existing `tests/ReleaseKit.Domain.Tests/Entities/RiskAnalysisReportTests.cs` to understand current tests. Then rewrite it:

```csharp
// tests/ReleaseKit.Domain.Tests/Entities/RiskAnalysisReportTests.cs
using FluentAssertions;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Tests.Entities;

/// <summary>
/// RiskAnalysisReport 實體測試
/// </summary>
public class RiskAnalysisReportTests
{
    [Fact]
    public void 建構_應正確設定所有屬性()
    {
        // Arrange & Act
        var report = new RiskAnalysisReport
        {
            Sequence = 1,
            ProjectName = "my-service",
            RiskItems = new List<RiskItem>
            {
                new()
                {
                    Category = RiskCategory.ApiContract,
                    Level = RiskLevel.High,
                    ChangeSummary = "API 變更",
                    AffectedFiles = new List<string> { "Controller.cs" },
                    PotentiallyAffectedServices = new List<string> { "Frontend" },
                    ImpactDescription = "影響前端",
                    SuggestedValidationSteps = new List<string> { "測試 API" }
                }
            },
            Summary = "測試摘要",
            AnalyzedAt = DateTimeOffset.UtcNow
        };

        // Assert
        report.Sequence.Should().Be(1);
        report.ProjectName.Should().Be("my-service");
        report.RiskItems.Should().HaveCount(1);
        report.Summary.Should().Be("測試摘要");
    }

    [Fact]
    public void AnalysisLog為可選屬性_預設為null()
    {
        // Arrange & Act
        var report = new RiskAnalysisReport
        {
            Sequence = 1,
            ProjectName = "svc",
            RiskItems = new List<RiskItem>(),
            Summary = "空報告",
            AnalyzedAt = DateTimeOffset.UtcNow
        };

        // Assert
        report.AnalysisLog.Should().BeNull();
    }

    [Fact]
    public void AnalysisLog可設定值()
    {
        // Arrange & Act
        var report = new RiskAnalysisReport
        {
            Sequence = 1,
            ProjectName = "svc",
            RiskItems = new List<RiskItem>(),
            Summary = "摘要",
            AnalyzedAt = DateTimeOffset.UtcNow,
            AnalysisLog = "執行了 git diff 指令"
        };

        // Assert
        report.AnalysisLog.Should().Be("執行了 git diff 指令");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd src && dotnet test --filter "FullyQualifiedName~RiskAnalysisReportTests" --no-restore -v minimal`
Expected: FAIL — `Sequence` property not found, `PassKey` still required

- [ ] **Step 3: Rewrite RiskAnalysisReport**

```csharp
// src/ReleaseKit.Domain/Entities/RiskAnalysisReport.cs
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Entities;

/// <summary>
/// 風險分析報告
/// </summary>
public sealed record RiskAnalysisReport
{
    /// <summary>專案排序序號</summary>
    public required int Sequence { get; init; }

    /// <summary>來源專案名稱</summary>
    public string? ProjectName { get; init; }

    /// <summary>識別到的風險項目</summary>
    public required IReadOnlyList<RiskItem> RiskItems { get; init; }

    /// <summary>分析摘要（繁體中文）</summary>
    public required string Summary { get; init; }

    /// <summary>分析時間戳</summary>
    public required DateTimeOffset AnalyzedAt { get; init; }

    /// <summary>分析過程記錄（Copilot 執行了哪些指令與原因）</summary>
    public string? AnalysisLog { get; init; }
}
```

- [ ] **Step 4: Delete obsolete domain files**

```bash
rm src/ReleaseKit.Domain/ValueObjects/AnalysisPassKey.cs
rm src/ReleaseKit.Domain/Entities/DynamicAnalysisResult.cs
rm src/ReleaseKit.Domain/Entities/PrDiffContext.cs
```

- [ ] **Step 5: Fix compilation errors**

After deleting AnalysisPassKey, DynamicAnalysisResult, PrDiffContext, and modifying RiskAnalysisReport, there will be compilation errors in files that reference them. At this point, **do not try to compile yet** — these will be fixed in subsequent tasks. The domain layer itself should compile:

Run: `cd src && dotnet build ReleaseKit.Domain/ReleaseKit.Domain.csproj --no-restore -v minimal 2>&1 | tail -5`
Expected: Build succeeded

- [ ] **Step 6: Run domain tests**

Run: `cd src && dotnet test ../tests/ReleaseKit.Domain.Tests/ReleaseKit.Domain.Tests.csproj --no-restore -v minimal`
Expected: PASS (any tests referencing deleted types will fail — that's expected and handled in later tasks)

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "refactor: simplify RiskAnalysisReport, delete AnalysisPassKey/DynamicAnalysisResult/PrDiffContext

Remove PassKey concept from RiskAnalysisReport (replaced with Sequence).
Delete DynamicAnalysisResult (no more multi-pass).
Delete PrDiffContext (Copilot self-explores repos).
Delete AnalysisPassKey (no more Pass/SubSequence).
Add AnalysisLog field for agentic analysis tracing.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 5: Domain — Refactor IRiskAnalyzer Interface

**Files:**
- Modify: `src/ReleaseKit.Domain/Abstractions/IRiskAnalyzer.cs`

- [ ] **Step 1: Rewrite the interface**

```csharp
// src/ReleaseKit.Domain/Abstractions/IRiskAnalyzer.cs
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Abstractions;

/// <summary>
/// AI 風險分析服務介面（Agentic 模式）
/// </summary>
public interface IRiskAnalyzer
{
    /// <summary>分析單一專案的變更風險（Agentic：Copilot 自主探索 repo）</summary>
    /// <param name="context">專案分析上下文（含 repo 路徑與 commit SHA）</param>
    /// <param name="cancellationToken">取消權杖</param>
    /// <returns>風險分析報告</returns>
    Task<RiskAnalysisReport> AnalyzeProjectRiskAsync(
        ProjectAnalysisContext context,
        CancellationToken cancellationToken = default);

    /// <summary>產生最終整合報告 Markdown</summary>
    /// <param name="reports">所有專案的中間分析報告</param>
    /// <param name="cancellationToken">取消權杖</param>
    /// <returns>Markdown 格式的最終報告</returns>
    Task<string> GenerateFinalReportAsync(
        IReadOnlyList<RiskAnalysisReport> reports,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Verify domain build**

Run: `cd src && dotnet build ReleaseKit.Domain/ReleaseKit.Domain.csproj --no-restore -v minimal 2>&1 | tail -5`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add src/ReleaseKit.Domain/Abstractions/IRiskAnalyzer.cs
git commit -m "refactor: redesign IRiskAnalyzer for agentic mode

Remove AnalyzeDeepAsync (no more multi-pass).
Change AnalyzeProjectRiskAsync to accept ProjectAnalysisContext.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 6: Common — Update RiskAnalysisOptions and RedisKeys

**Files:**
- Modify: `src/ReleaseKit.Common/Configuration/RiskAnalysisOptions.cs`
- Modify: `src/ReleaseKit.Common/Constants/RedisKeys.cs`

- [ ] **Step 1: Update RiskAnalysisOptions**

```csharp
// src/ReleaseKit.Common/Configuration/RiskAnalysisOptions.cs
namespace ReleaseKit.Common.Configuration;

/// <summary>
/// 風險分析組態
/// </summary>
public sealed class RiskAnalysisOptions
{
    /// <summary>Clone 的基底路徑</summary>
    public required string CloneBasePath { get; init; }

    /// <summary>最大平行 Clone 數量</summary>
    public int MaxConcurrentClones { get; init; } = 5;

    /// <summary>最大平行分析數量</summary>
    public int MaxConcurrentAnalysis { get; init; } = 3;

    /// <summary>每次 shell 指令輸出字元數上限</summary>
    public int MaxOutputCharacters { get; init; } = 50000;

    /// <summary>每次 shell 指令超時（秒）</summary>
    public int CommandTimeoutSeconds { get; init; } = 30;

    /// <summary>報告輸出路徑</summary>
    public required string ReportOutputPath { get; init; }
}
```

- [ ] **Step 2: Update RedisKeys**

In `src/ReleaseKit.Common/Constants/RedisKeys.cs`, remove `PrDiffs` field and add `AnalysisContext`:

```csharp
// Replace the Fields inner class content — remove PrDiffs, add AnalysisContext
```

Specifically, apply these edits to `RedisKeys.cs`:
1. Remove the `PrDiffs` field constant
2. Add `AnalysisContext` field prefix constant

The updated `Fields` class should be:

```csharp
    public static class Fields
    {
        /// <summary>Pull Request 資料的欄位名稱</summary>
        public const string PullRequests = "PullRequests";

        /// <summary>過濾後（依使用者）的 Pull Request 資料欄位名稱</summary>
        public const string PullRequestsByUser = "PullRequests:ByUser";

        /// <summary>Release Branch 資料的欄位名稱</summary>
        public const string ReleaseBranches = "ReleaseBranches";

        /// <summary>Work Items 資料的欄位名稱</summary>
        public const string WorkItems = "WorkItems";

        /// <summary>User Story 層級 Work Items 資料的欄位名稱</summary>
        public const string WorkItemsUserStories = "WorkItems:UserStories";

        /// <summary>整合後的 Release 資料欄位名稱</summary>
        public const string Consolidated = "Consolidated";

        /// <summary>增強標題後的 Release 資料欄位名稱</summary>
        public const string EnhancedTitles = "EnhancedTitles";

        /// <summary>Clone 路徑對照資料欄位名稱</summary>
        public const string ClonePaths = "ClonePaths";

        /// <summary>中間分析結果欄位前綴（格式：Intermediate:{sequence}）</summary>
        public const string IntermediatePrefix = "Intermediate:";

        /// <summary>分析上下文欄位前綴（格式：AnalysisContext:{sequence}）</summary>
        public const string AnalysisContextPrefix = "AnalysisContext:";

        /// <summary>最終風險分析報告欄位名稱</summary>
        public const string FinalReport = "FinalReport";
    }
```

- [ ] **Step 3: Verify build**

Run: `cd src && dotnet build ReleaseKit.Common/ReleaseKit.Common.csproj --no-restore -v minimal 2>&1 | tail -5`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add src/ReleaseKit.Common/Configuration/RiskAnalysisOptions.cs src/ReleaseKit.Common/Constants/RedisKeys.cs
git commit -m "refactor: update RiskAnalysisOptions and RedisKeys for agentic mode

RiskAnalysisOptions: add MaxConcurrentAnalysis, MaxOutputCharacters,
CommandTimeoutSeconds; remove MaxTokensPerAiCall, MaxAnalysisPasses.
RedisKeys: remove PrDiffs field; add IntermediatePrefix, AnalysisContextPrefix.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 7: Application — Delete Obsolete Tasks and Update TaskType/TaskFactory

**Files:**
- Delete: `src/ReleaseKit.Application/Tasks/ExtractPrDiffsTask.cs`
- Delete: `src/ReleaseKit.Application/Tasks/AnalyzeProjectRiskTask.cs`
- Delete: `src/ReleaseKit.Application/Tasks/AnalyzeCrossProjectRiskTask.cs`
- Delete: `tests/ReleaseKit.Application.Tests/Tasks/ExtractPrDiffsTaskTests.cs`
- Delete: `tests/ReleaseKit.Application.Tests/Tasks/AnalyzeProjectRiskTaskTests.cs`
- Delete: `tests/ReleaseKit.Application.Tests/Tasks/AnalyzeCrossProjectRiskTaskTests.cs`
- Modify: `src/ReleaseKit.Application/Tasks/TaskType.cs`
- Modify: `src/ReleaseKit.Application/Tasks/TaskFactory.cs`

- [ ] **Step 1: Delete obsolete task files and test files**

```bash
rm src/ReleaseKit.Application/Tasks/ExtractPrDiffsTask.cs
rm src/ReleaseKit.Application/Tasks/AnalyzeProjectRiskTask.cs
rm src/ReleaseKit.Application/Tasks/AnalyzeCrossProjectRiskTask.cs
rm tests/ReleaseKit.Application.Tests/Tasks/ExtractPrDiffsTaskTests.cs
rm tests/ReleaseKit.Application.Tests/Tasks/AnalyzeProjectRiskTaskTests.cs
rm tests/ReleaseKit.Application.Tests/Tasks/AnalyzeCrossProjectRiskTaskTests.cs
```

- [ ] **Step 2: Update TaskType enum**

Remove `ExtractPrDiffs`, `AnalyzeProjectRisk`, `AnalyzeCrossProjectRisk` from `src/ReleaseKit.Application/Tasks/TaskType.cs`:

```csharp
// src/ReleaseKit.Application/Tasks/TaskType.cs
namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 任務類型列舉
/// </summary>
public enum TaskType
{
    /// <summary>拉取 GitLab Pull Request 資訊</summary>
    FetchGitLabPullRequests,

    /// <summary>拉取 Bitbucket Pull Request 資訊</summary>
    FetchBitbucketPullRequests,

    /// <summary>拉取 Azure DevOps Work Item 資訊</summary>
    FetchAzureDevOpsWorkItems,

    /// <summary>更新 Google Sheets 資訊</summary>
    UpdateGoogleSheets,

    /// <summary>取得 GitLab 各專案最新 Release Branch</summary>
    FetchGitLabReleaseBranch,

    /// <summary>取得 Bitbucket 各專案最新 Release Branch</summary>
    FetchBitbucketReleaseBranch,

    /// <summary>過濾 GitLab Pull Request 依使用者</summary>
    FilterGitLabPullRequestsByUser,

    /// <summary>過濾 Bitbucket Pull Request 依使用者</summary>
    FilterBitbucketPullRequestsByUser,

    /// <summary>取得 User Story 層級的 Work Item</summary>
    GetUserStory,

    /// <summary>整合 Release 資料</summary>
    ConsolidateReleaseData,

    /// <summary>使用 AI 增強 Release 標題</summary>
    EnhanceTitles,

    /// <summary>Clone 所有相關 repository</summary>
    CloneRepositories,

    /// <summary>產生最終風險報告</summary>
    GenerateRiskReport,

    /// <summary>Agentic 風險分析（Clone + 分析 + 報告）</summary>
    AnalyzeRisk
}
```

- [ ] **Step 3: Update TaskFactory**

```csharp
// src/ReleaseKit.Application/Tasks/TaskFactory.cs
using Microsoft.Extensions.DependencyInjection;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 任務工廠，使用工廠模式建立任務實例
/// </summary>
public class TaskFactory
{
    private readonly IServiceProvider _serviceProvider;

    public TaskFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// 根據任務類型建立任務實例
    /// </summary>
    /// <param name="taskType">任務類型</param>
    /// <returns>任務實例</returns>
    /// <exception cref="ArgumentException">當任務類型無效時拋出</exception>
    public ITask CreateTask(TaskType taskType)
    {
        return taskType switch
        {
            TaskType.FetchGitLabPullRequests => _serviceProvider.GetRequiredService<FetchGitLabPullRequestsTask>(),
            TaskType.FetchBitbucketPullRequests => _serviceProvider.GetRequiredService<FetchBitbucketPullRequestsTask>(),
            TaskType.FetchAzureDevOpsWorkItems => _serviceProvider.GetRequiredService<FetchAzureDevOpsWorkItemsTask>(),
            TaskType.UpdateGoogleSheets => _serviceProvider.GetRequiredService<UpdateGoogleSheetsTask>(),
            TaskType.FetchGitLabReleaseBranch => _serviceProvider.GetRequiredService<FetchGitLabReleaseBranchTask>(),
            TaskType.FetchBitbucketReleaseBranch => _serviceProvider.GetRequiredService<FetchBitbucketReleaseBranchTask>(),
            TaskType.FilterGitLabPullRequestsByUser => _serviceProvider.GetRequiredService<FilterGitLabPullRequestsByUserTask>(),
            TaskType.FilterBitbucketPullRequestsByUser => _serviceProvider.GetRequiredService<FilterBitbucketPullRequestsByUserTask>(),
            TaskType.GetUserStory => _serviceProvider.GetRequiredService<GetUserStoryTask>(),
            TaskType.ConsolidateReleaseData => _serviceProvider.GetRequiredService<ConsolidateReleaseDataTask>(),
            TaskType.EnhanceTitles => _serviceProvider.GetRequiredService<EnhanceTitlesWithCopilotTask>(),
            TaskType.CloneRepositories => _serviceProvider.GetRequiredService<CloneRepositoriesTask>(),
            TaskType.GenerateRiskReport => _serviceProvider.GetRequiredService<GenerateRiskReportTask>(),
            TaskType.AnalyzeRisk => _serviceProvider.GetRequiredService<AnalyzeRiskTask>(),
            _ => throw new ArgumentException($"不支援的任務類型: {taskType}", nameof(taskType))
        };
    }
}
```

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "refactor: delete obsolete tasks, update TaskType/TaskFactory

Delete ExtractPrDiffsTask, AnalyzeProjectRiskTask,
AnalyzeCrossProjectRiskTask and their tests.
Remove corresponding TaskType enum values and TaskFactory mappings.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 8: Application — Rewrite AnalyzeRiskTask (Agentic Mode)

**Files:**
- Modify: `src/ReleaseKit.Application/Tasks/AnalyzeRiskTask.cs`
- Modify: `tests/ReleaseKit.Application.Tests/Tasks/AnalyzeRiskTaskTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/ReleaseKit.Application.Tests/Tasks/AnalyzeRiskTaskTests.cs
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ReleaseKit.Application.Common;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Application.Tests.Tasks;

/// <summary>
/// AnalyzeRiskTask（Agentic 模式）單元測試
/// </summary>
public class AnalyzeRiskTaskTests
{
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly Mock<IRiskAnalyzer> _riskAnalyzerMock;
    private readonly Mock<ILogger<AnalyzeRiskTask>> _loggerMock;
    private readonly RiskAnalysisOptions _options;

    public AnalyzeRiskTaskTests()
    {
        _redisServiceMock = new Mock<IRedisService>();
        _riskAnalyzerMock = new Mock<IRiskAnalyzer>();
        _loggerMock = new Mock<ILogger<AnalyzeRiskTask>>();
        _options = new RiskAnalysisOptions
        {
            CloneBasePath = "/clone",
            MaxConcurrentAnalysis = 2,
            ReportOutputPath = "/reports"
        };
    }

    private AnalyzeRiskTask CreateTask()
    {
        return new AnalyzeRiskTask(
            _redisServiceMock.Object,
            _riskAnalyzerMock.Object,
            Options.Create(_options),
            _loggerMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_無PR資料時_應跳過分析()
    {
        // Arrange — GitLab 和 Bitbucket 都無資料
        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.GitLabHash, RedisKeys.Fields.PullRequestsByUser))
            .ReturnsAsync((string?)null);
        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.BitbucketHash, RedisKeys.Fields.PullRequestsByUser))
            .ReturnsAsync((string?)null);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — IRiskAnalyzer 不應被呼叫
        _riskAnalyzerMock.Verify(x => x.AnalyzeProjectRiskAsync(
            It.IsAny<ProjectAnalysisContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_無ClonePaths時_應跳過分析()
    {
        // Arrange — 有 PR 資料但無 clone 路徑
        var fetchResult = new FetchResult
        {
            Results = new List<ProjectResult>
            {
                new()
                {
                    ProjectPath = "group/my-service",
                    PullRequests = new List<MergeRequestOutput>
                    {
                        new() { MergeCommitSha = "abc123", Title = "fix: bug", SourceBranch = "fix/bug", TargetBranch = "main", AuthorName = "dev", PRUrl = "http://url" }
                    }
                }
            }
        };
        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.GitLabHash, RedisKeys.Fields.PullRequestsByUser))
            .ReturnsAsync(fetchResult.ToJson());
        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.BitbucketHash, RedisKeys.Fields.PullRequestsByUser))
            .ReturnsAsync((string?)null);
        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.ClonePaths))
            .ReturnsAsync((string?)null);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 無 clone 路徑 → 跳過
        _riskAnalyzerMock.Verify(x => x.AnalyzeProjectRiskAsync(
            It.IsAny<ProjectAnalysisContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_專案無CommitSha時_應跳過該專案()
    {
        // Arrange — PR 沒有 MergeCommitSha
        var fetchResult = new FetchResult
        {
            Results = new List<ProjectResult>
            {
                new()
                {
                    ProjectPath = "group/no-sha-svc",
                    PullRequests = new List<MergeRequestOutput>
                    {
                        new() { MergeCommitSha = null, Title = "PR without SHA", SourceBranch = "feat/x", TargetBranch = "main", AuthorName = "dev", PRUrl = "http://url" }
                    }
                }
            }
        };
        var clonePaths = new Dictionary<string, string>
        {
            ["group/no-sha-svc"] = "/clone/no-sha-svc"
        };

        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.GitLabHash, RedisKeys.Fields.PullRequestsByUser))
            .ReturnsAsync(fetchResult.ToJson());
        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.BitbucketHash, RedisKeys.Fields.PullRequestsByUser))
            .ReturnsAsync((string?)null);
        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.ClonePaths))
            .ReturnsAsync(clonePaths.ToJson());

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 無 commit SHA 的專案應被跳過
        _riskAnalyzerMock.Verify(x => x.AnalyzeProjectRiskAsync(
            It.IsAny<ProjectAnalysisContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_正常專案_應組裝Context並呼叫Analyzer()
    {
        // Arrange
        var fetchResult = new FetchResult
        {
            Results = new List<ProjectResult>
            {
                new()
                {
                    ProjectPath = "group/my-service",
                    PullRequests = new List<MergeRequestOutput>
                    {
                        new() { MergeCommitSha = "sha1", Title = "feat: A", SourceBranch = "feat/a", TargetBranch = "main", AuthorName = "dev", PRUrl = "http://url" },
                        new() { MergeCommitSha = "sha2", Title = "fix: B", SourceBranch = "fix/b", TargetBranch = "main", AuthorName = "dev", PRUrl = "http://url" }
                    }
                }
            }
        };
        var clonePaths = new Dictionary<string, string>
        {
            ["group/my-service"] = "/clone/my-service"
        };

        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.GitLabHash, RedisKeys.Fields.PullRequestsByUser))
            .ReturnsAsync(fetchResult.ToJson());
        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.BitbucketHash, RedisKeys.Fields.PullRequestsByUser))
            .ReturnsAsync((string?)null);
        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.ClonePaths))
            .ReturnsAsync(clonePaths.ToJson());

        ProjectAnalysisContext? capturedContext = null;
        _riskAnalyzerMock.Setup(x => x.AnalyzeProjectRiskAsync(
                It.IsAny<ProjectAnalysisContext>(), It.IsAny<CancellationToken>()))
            .Returns((ProjectAnalysisContext ctx, CancellationToken _) =>
            {
                capturedContext = ctx;
                return Task.FromResult(new RiskAnalysisReport
                {
                    Sequence = 1,
                    ProjectName = ctx.ProjectName,
                    RiskItems = new List<RiskItem>(),
                    Summary = "無風險",
                    AnalyzedAt = DateTimeOffset.UtcNow
                });
            });

        _redisServiceMock.Setup(x => x.HashSetAsync(
                RedisKeys.RiskAnalysisHash, It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 驗證 Context 組裝
        Assert.NotNull(capturedContext);
        Assert.Equal("group/my-service", capturedContext!.ProjectName);
        Assert.Equal("/clone/my-service", capturedContext.RepoPath);
        Assert.Equal(2, capturedContext.CommitShas.Count);
        Assert.Contains("sha1", capturedContext.CommitShas);
        Assert.Contains("sha2", capturedContext.CommitShas);
    }

    [Fact]
    public async Task ExecuteAsync_分析結果_應存入Redis()
    {
        // Arrange
        var fetchResult = new FetchResult
        {
            Results = new List<ProjectResult>
            {
                new()
                {
                    ProjectPath = "group/svc-a",
                    PullRequests = new List<MergeRequestOutput>
                    {
                        new() { MergeCommitSha = "sha1", Title = "T", SourceBranch = "f/x", TargetBranch = "main", AuthorName = "dev", PRUrl = "http://url" }
                    }
                }
            }
        };
        var clonePaths = new Dictionary<string, string> { ["group/svc-a"] = "/clone/svc-a" };

        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.GitLabHash, RedisKeys.Fields.PullRequestsByUser))
            .ReturnsAsync(fetchResult.ToJson());
        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.BitbucketHash, RedisKeys.Fields.PullRequestsByUser))
            .ReturnsAsync((string?)null);
        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.ClonePaths))
            .ReturnsAsync(clonePaths.ToJson());

        _riskAnalyzerMock.Setup(x => x.AnalyzeProjectRiskAsync(
                It.IsAny<ProjectAnalysisContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RiskAnalysisReport
            {
                Sequence = 1,
                ProjectName = "group/svc-a",
                RiskItems = new List<RiskItem>(),
                Summary = "無風險",
                AnalyzedAt = DateTimeOffset.UtcNow
            });

        var storedFields = new Dictionary<string, string>();
        _redisServiceMock.Setup(x => x.HashSetAsync(
                RedisKeys.RiskAnalysisHash, It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string _, string field, string value) =>
            {
                storedFields[field] = value;
                return Task.FromResult(true);
            });

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 驗證中間結果與 context 皆存入 Redis
        Assert.Contains(storedFields.Keys, k => k.StartsWith("Intermediate:"));
        Assert.Contains(storedFields.Keys, k => k.StartsWith("AnalysisContext:"));
    }

    [Fact]
    public async Task ExecuteAsync_多專案_應並行處理並去重CommitSha()
    {
        // Arrange — 同一專案有重複 SHA
        var fetchResult = new FetchResult
        {
            Results = new List<ProjectResult>
            {
                new()
                {
                    ProjectPath = "group/svc-x",
                    PullRequests = new List<MergeRequestOutput>
                    {
                        new() { MergeCommitSha = "same-sha", Title = "T1", SourceBranch = "f/1", TargetBranch = "main", AuthorName = "dev", PRUrl = "http://url" },
                        new() { MergeCommitSha = "same-sha", Title = "T2", SourceBranch = "f/2", TargetBranch = "main", AuthorName = "dev", PRUrl = "http://url" },
                        new() { MergeCommitSha = "unique-sha", Title = "T3", SourceBranch = "f/3", TargetBranch = "main", AuthorName = "dev", PRUrl = "http://url" }
                    }
                }
            }
        };
        var clonePaths = new Dictionary<string, string> { ["group/svc-x"] = "/clone/svc-x" };

        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.GitLabHash, RedisKeys.Fields.PullRequestsByUser))
            .ReturnsAsync(fetchResult.ToJson());
        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.BitbucketHash, RedisKeys.Fields.PullRequestsByUser))
            .ReturnsAsync((string?)null);
        _redisServiceMock.Setup(x => x.HashGetAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.ClonePaths))
            .ReturnsAsync(clonePaths.ToJson());

        ProjectAnalysisContext? capturedContext = null;
        _riskAnalyzerMock.Setup(x => x.AnalyzeProjectRiskAsync(
                It.IsAny<ProjectAnalysisContext>(), It.IsAny<CancellationToken>()))
            .Returns((ProjectAnalysisContext ctx, CancellationToken _) =>
            {
                capturedContext = ctx;
                return Task.FromResult(new RiskAnalysisReport
                {
                    Sequence = 1,
                    ProjectName = ctx.ProjectName,
                    RiskItems = new List<RiskItem>(),
                    Summary = "ok",
                    AnalyzedAt = DateTimeOffset.UtcNow
                });
            });

        _redisServiceMock.Setup(x => x.HashSetAsync(
                RedisKeys.RiskAnalysisHash, It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — CommitShas 應去重
        Assert.NotNull(capturedContext);
        Assert.Equal(2, capturedContext!.CommitShas.Count);
        Assert.Contains("same-sha", capturedContext.CommitShas);
        Assert.Contains("unique-sha", capturedContext.CommitShas);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd src && dotnet test --filter "FullyQualifiedName~AnalyzeRiskTaskTests" --no-restore -v minimal`
Expected: FAIL — `AnalyzeRiskTask` constructor signature mismatch

- [ ] **Step 3: Write the implementation**

```csharp
// src/ReleaseKit.Application/Tasks/AnalyzeRiskTask.cs
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Application.Common;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// Agentic 風險分析任務
/// </summary>
/// <remarks>
/// 從 Redis 讀取 PR 資料與 clone 路徑，為每個專案組裝 <see cref="ProjectAnalysisContext"/>，
/// 並行建立 Copilot session 進行 agentic 風險分析。
/// Copilot 自主決定要執行的 shell 指令來探索 repo 並分析風險。
/// </remarks>
public sealed class AnalyzeRiskTask : ITask
{
    private readonly IRedisService _redisService;
    private readonly IRiskAnalyzer _riskAnalyzer;
    private readonly RiskAnalysisOptions _options;
    private readonly ILogger<AnalyzeRiskTask> _logger;

    /// <summary>
    /// 初始化 <see cref="AnalyzeRiskTask"/> 類別的新執行個體
    /// </summary>
    public AnalyzeRiskTask(
        IRedisService redisService,
        IRiskAnalyzer riskAnalyzer,
        IOptions<RiskAnalysisOptions> options,
        ILogger<AnalyzeRiskTask> logger)
    {
        _redisService = redisService;
        _riskAnalyzer = riskAnalyzer;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>執行 agentic 風險分析</summary>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始 Agentic 風險分析");

        var contexts = await BuildProjectContextsAsync();

        if (contexts.Count == 0)
        {
            _logger.LogInformation("無可分析的專案，跳過風險分析");
            return;
        }

        _logger.LogInformation("準備分析 {Count} 個專案", contexts.Count);

        var semaphore = new SemaphoreSlim(_options.MaxConcurrentAnalysis);
        var tasks = contexts.Select((ctx, index) =>
            AnalyzeProjectAsync(ctx, index + 1, semaphore));

        await Task.WhenAll(tasks);

        _logger.LogInformation("Agentic 風險分析完成，共處理 {Count} 個專案", contexts.Count);
    }

    /// <summary>
    /// 從 Redis 讀取 PR 資料與 clone 路徑，組裝各專案的分析上下文
    /// </summary>
    internal async Task<IReadOnlyList<ProjectAnalysisContext>> BuildProjectContextsAsync()
    {
        var gitLabJson = await _redisService.HashGetAsync(
            RedisKeys.GitLabHash, RedisKeys.Fields.PullRequestsByUser);
        var bitbucketJson = await _redisService.HashGetAsync(
            RedisKeys.BitbucketHash, RedisKeys.Fields.PullRequestsByUser);
        var clonePathsJson = await _redisService.HashGetAsync(
            RedisKeys.RiskAnalysisHash, RedisKeys.Fields.ClonePaths);

        var gitLabFetch = gitLabJson?.ToTypedObject<FetchResult>();
        var bitbucketFetch = bitbucketJson?.ToTypedObject<FetchResult>();
        var clonePaths = clonePathsJson?.ToTypedObject<Dictionary<string, string>>()
                         ?? new Dictionary<string, string>();

        if (clonePaths.Count == 0)
        {
            _logger.LogInformation("無 ClonePaths 資料，跳過分析");
            return [];
        }

        var allProjects = new List<ProjectResult>();
        if (gitLabFetch?.Results is not null)
            allProjects.AddRange(gitLabFetch.Results);
        if (bitbucketFetch?.Results is not null)
            allProjects.AddRange(bitbucketFetch.Results);

        var contexts = new List<ProjectAnalysisContext>();

        foreach (var project in allProjects.OrderBy(p => p.ProjectPath))
        {
            if (!clonePaths.TryGetValue(project.ProjectPath, out var clonePath))
            {
                _logger.LogWarning("找不到 {ProjectPath} 的 Clone 路徑，跳過", project.ProjectPath);
                continue;
            }

            var commitShas = project.PullRequests
                .Where(pr => !string.IsNullOrEmpty(pr.MergeCommitSha))
                .Select(pr => pr.MergeCommitSha!)
                .Distinct()
                .ToList();

            if (commitShas.Count == 0)
            {
                _logger.LogWarning("專案 {ProjectPath} 無有效 CommitSha，跳過", project.ProjectPath);
                continue;
            }

            contexts.Add(new ProjectAnalysisContext
            {
                ProjectName = project.ProjectPath,
                RepoPath = clonePath,
                CommitShas = commitShas
            });
        }

        return contexts;
    }

    /// <summary>
    /// 分析單一專案並將結果存入 Redis
    /// </summary>
    private async Task AnalyzeProjectAsync(
        ProjectAnalysisContext context,
        int sequence,
        SemaphoreSlim semaphore)
    {
        await semaphore.WaitAsync();
        try
        {
            _logger.LogInformation("分析專案 {ProjectName}（Sequence={Sequence}，CommitShas={Count}）",
                context.ProjectName, sequence, context.CommitShas.Count);

            var report = await _riskAnalyzer.AnalyzeProjectRiskAsync(context);
            report = report with { Sequence = sequence };

            await _redisService.HashSetAsync(
                RedisKeys.RiskAnalysisHash,
                $"{RedisKeys.Fields.IntermediatePrefix}{sequence}",
                report.ToJson());

            await _redisService.HashSetAsync(
                RedisKeys.RiskAnalysisHash,
                $"{RedisKeys.Fields.AnalysisContextPrefix}{sequence}",
                context.ToJson());

            _logger.LogInformation("專案 {ProjectName} 分析完成，識別 {Count} 個風險項目",
                context.ProjectName, report.RiskItems.Count);
        }
        finally
        {
            semaphore.Release();
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd src && dotnet test --filter "FullyQualifiedName~AnalyzeRiskTaskTests" --no-restore -v minimal`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/ReleaseKit.Application/Tasks/AnalyzeRiskTask.cs tests/ReleaseKit.Application.Tests/Tasks/AnalyzeRiskTaskTests.cs
git commit -m "feat: rewrite AnalyzeRiskTask for agentic mode

New flow: read PR data + clone paths from Redis, build
ProjectAnalysisContext per project, parallel Copilot analysis.
Store Intermediate:{seq} and AnalysisContext:{seq} in Redis.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 9: Application — Simplify GenerateRiskReportTask

**Files:**
- Modify: `src/ReleaseKit.Application/Tasks/GenerateRiskReportTask.cs`
- Modify: `tests/ReleaseKit.Application.Tests/Tasks/GenerateRiskReportTaskTests.cs`

- [ ] **Step 1: Rewrite the tests**

```csharp
// tests/ReleaseKit.Application.Tests/Tasks/GenerateRiskReportTaskTests.cs
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Application.Tests.Tasks;

/// <summary>
/// GenerateRiskReportTask 單元測試
/// </summary>
public class GenerateRiskReportTaskTests : IDisposable
{
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly Mock<IRiskAnalyzer> _riskAnalyzerMock;
    private readonly Mock<INow> _nowMock;
    private readonly Mock<ILogger<GenerateRiskReportTask>> _loggerMock;
    private readonly string _reportOutputPath;
    private readonly DateTimeOffset _fixedNow = new(2025, 7, 15, 10, 0, 0, TimeSpan.Zero);

    public GenerateRiskReportTaskTests()
    {
        _redisServiceMock = new Mock<IRedisService>();
        _riskAnalyzerMock = new Mock<IRiskAnalyzer>();
        _nowMock = new Mock<INow>();
        _loggerMock = new Mock<ILogger<GenerateRiskReportTask>>();

        _nowMock.Setup(x => x.UtcNow).Returns(_fixedNow);

        _reportOutputPath = Path.Combine(
            Path.GetTempPath(),
            $"release-kit-test-{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_reportOutputPath))
            Directory.Delete(_reportOutputPath, recursive: true);
    }

    private GenerateRiskReportTask CreateTask()
    {
        var options = Options.Create(new RiskAnalysisOptions
        {
            CloneBasePath = "/clone",
            ReportOutputPath = _reportOutputPath
        });

        return new GenerateRiskReportTask(
            _redisServiceMock.Object,
            _riskAnalyzerMock.Object,
            options,
            _nowMock.Object,
            _loggerMock.Object);
    }

    private static RiskAnalysisReport CreateReport(int sequence, string? projectName = null)
    {
        return new RiskAnalysisReport
        {
            Sequence = sequence,
            ProjectName = projectName ?? $"project-{sequence}",
            RiskItems = new List<RiskItem>
            {
                new()
                {
                    Category = RiskCategory.ApiContract,
                    Level = RiskLevel.High,
                    ChangeSummary = "測試變更",
                    AffectedFiles = new List<string> { "src/Foo.cs" },
                    PotentiallyAffectedServices = new List<string> { "ServiceA" },
                    ImpactDescription = "測試影響",
                    SuggestedValidationSteps = new List<string> { "步驟一" }
                }
            },
            Summary = "測試摘要",
            AnalyzedAt = DateTimeOffset.UtcNow
        };
    }

    [Fact]
    public async Task ExecuteAsync_讀取所有Intermediate報告並產生最終報告()
    {
        // Arrange
        var report1 = CreateReport(1, "svc-a");
        var report2 = CreateReport(2, "svc-b");
        var intermediateData = new Dictionary<string, string>
        {
            [$"{RedisKeys.Fields.IntermediatePrefix}1"] = report1.ToJson(),
            [$"{RedisKeys.Fields.IntermediatePrefix}2"] = report2.ToJson()
        };
        _redisServiceMock.Setup(x => x.HashGetByPrefixAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.IntermediatePrefix))
            .ReturnsAsync(intermediateData);

        IReadOnlyList<RiskAnalysisReport>? capturedReports = null;
        _riskAnalyzerMock.Setup(x => x.GenerateFinalReportAsync(
                It.IsAny<IReadOnlyList<RiskAnalysisReport>>(), It.IsAny<CancellationToken>()))
            .Returns((IReadOnlyList<RiskAnalysisReport> reports, CancellationToken _) =>
            {
                capturedReports = reports;
                return Task.FromResult("# 風險報告");
            });

        _redisServiceMock.Setup(x => x.HashSetAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.FinalReport, It.IsAny<string>()))
            .ReturnsAsync(true);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        Assert.NotNull(capturedReports);
        Assert.Equal(2, capturedReports!.Count);
    }

    [Fact]
    public async Task ExecuteAsync_報告存入Redis()
    {
        // Arrange
        _redisServiceMock.Setup(x => x.HashGetByPrefixAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.IntermediatePrefix))
            .ReturnsAsync(new Dictionary<string, string>
            {
                [$"{RedisKeys.Fields.IntermediatePrefix}1"] = CreateReport(1).ToJson()
            });

        const string expectedMarkdown = "# 最終報告";
        _riskAnalyzerMock.Setup(x => x.GenerateFinalReportAsync(
                It.IsAny<IReadOnlyList<RiskAnalysisReport>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedMarkdown);

        _redisServiceMock.Setup(x => x.HashSetAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.FinalReport, It.IsAny<string>()))
            .ReturnsAsync(true);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _redisServiceMock.Verify(x => x.HashSetAsync(
            RedisKeys.RiskAnalysisHash, RedisKeys.Fields.FinalReport, expectedMarkdown), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_報告寫入檔案()
    {
        // Arrange
        _redisServiceMock.Setup(x => x.HashGetByPrefixAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.IntermediatePrefix))
            .ReturnsAsync(new Dictionary<string, string>
            {
                [$"{RedisKeys.Fields.IntermediatePrefix}1"] = CreateReport(1).ToJson()
            });

        const string expectedMarkdown = "# 報告內容";
        _riskAnalyzerMock.Setup(x => x.GenerateFinalReportAsync(
                It.IsAny<IReadOnlyList<RiskAnalysisReport>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedMarkdown);

        _redisServiceMock.Setup(x => x.HashSetAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.FinalReport, It.IsAny<string>()))
            .ReturnsAsync(true);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        var expectedPath = Path.Combine(_reportOutputPath, "risk-report-2025-07-15.md");
        Assert.True(File.Exists(expectedPath));
        var content = await File.ReadAllTextAsync(expectedPath);
        Assert.Equal(expectedMarkdown, content);
    }

    [Fact]
    public async Task ExecuteAsync_空中間報告_正常完成()
    {
        // Arrange
        _redisServiceMock.Setup(x => x.HashGetByPrefixAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.IntermediatePrefix))
            .ReturnsAsync(new Dictionary<string, string>());

        _riskAnalyzerMock.Setup(x => x.GenerateFinalReportAsync(
                It.IsAny<IReadOnlyList<RiskAnalysisReport>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("# 空報告");

        _redisServiceMock.Setup(x => x.HashSetAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.FinalReport, It.IsAny<string>()))
            .ReturnsAsync(true);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert
        _riskAnalyzerMock.Verify(x => x.GenerateFinalReportAsync(
            It.Is<IReadOnlyList<RiskAnalysisReport>>(r => r.Count == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_報告依Sequence排序()
    {
        // Arrange — 故意反序存入
        var intermediateData = new Dictionary<string, string>
        {
            [$"{RedisKeys.Fields.IntermediatePrefix}3"] = CreateReport(3, "c").ToJson(),
            [$"{RedisKeys.Fields.IntermediatePrefix}1"] = CreateReport(1, "a").ToJson(),
            [$"{RedisKeys.Fields.IntermediatePrefix}2"] = CreateReport(2, "b").ToJson()
        };
        _redisServiceMock.Setup(x => x.HashGetByPrefixAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.IntermediatePrefix))
            .ReturnsAsync(intermediateData);

        IReadOnlyList<RiskAnalysisReport>? capturedReports = null;
        _riskAnalyzerMock.Setup(x => x.GenerateFinalReportAsync(
                It.IsAny<IReadOnlyList<RiskAnalysisReport>>(), It.IsAny<CancellationToken>()))
            .Returns((IReadOnlyList<RiskAnalysisReport> reports, CancellationToken _) =>
            {
                capturedReports = reports;
                return Task.FromResult("# 報告");
            });

        _redisServiceMock.Setup(x => x.HashSetAsync(
                RedisKeys.RiskAnalysisHash, RedisKeys.Fields.FinalReport, It.IsAny<string>()))
            .ReturnsAsync(true);

        var task = CreateTask();

        // Act
        await task.ExecuteAsync();

        // Assert — 應按 Sequence 排序
        Assert.NotNull(capturedReports);
        Assert.Equal(3, capturedReports!.Count);
        Assert.Equal(1, capturedReports[0].Sequence);
        Assert.Equal(2, capturedReports[1].Sequence);
        Assert.Equal(3, capturedReports[2].Sequence);
    }
}
```

- [ ] **Step 2: Rewrite GenerateRiskReportTask**

```csharp
// src/ReleaseKit.Application/Tasks/GenerateRiskReportTask.cs
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Entities;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 產生最終風險分析報告
/// </summary>
/// <remarks>
/// 從 Redis 載入所有 Intermediate 中間報告，透過 Copilot 產生 Markdown 最終報告。
/// </remarks>
public class GenerateRiskReportTask : ITask
{
    private readonly IRedisService _redisService;
    private readonly IRiskAnalyzer _riskAnalyzer;
    private readonly RiskAnalysisOptions _options;
    private readonly INow _now;
    private readonly ILogger<GenerateRiskReportTask> _logger;

    /// <summary>
    /// 初始化 <see cref="GenerateRiskReportTask"/> 類別的新執行個體
    /// </summary>
    public GenerateRiskReportTask(
        IRedisService redisService,
        IRiskAnalyzer riskAnalyzer,
        IOptions<RiskAnalysisOptions> options,
        INow now,
        ILogger<GenerateRiskReportTask> logger)
    {
        _redisService = redisService;
        _riskAnalyzer = riskAnalyzer;
        _options = options.Value;
        _now = now;
        _logger = logger;
    }

    /// <summary>執行最終報告產生任務</summary>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始產生最終風險分析報告");

        var reports = await LoadIntermediateReportsAsync();
        _logger.LogInformation("載入 {Count} 份中間報告", reports.Count);

        var markdown = await _riskAnalyzer.GenerateFinalReportAsync(reports);

        await _redisService.HashSetAsync(
            RedisKeys.RiskAnalysisHash, RedisKeys.Fields.FinalReport, markdown);
        _logger.LogInformation("最終報告已存入 Redis");

        await WriteReportToFileAsync(markdown);
        _logger.LogInformation("最終風險分析報告產生完成");
    }

    /// <summary>從 Redis 載入所有中間分析報告</summary>
    private async Task<IReadOnlyList<RiskAnalysisReport>> LoadIntermediateReportsAsync()
    {
        var entries = await _redisService.HashGetByPrefixAsync(
            RedisKeys.RiskAnalysisHash, RedisKeys.Fields.IntermediatePrefix);

        return entries.Values
            .Select(json => json.ToTypedObject<RiskAnalysisReport>()!)
            .OrderBy(r => r.Sequence)
            .ToList();
    }

    /// <summary>將報告寫入檔案系統</summary>
    private async Task WriteReportToFileAsync(string markdown)
    {
        Directory.CreateDirectory(_options.ReportOutputPath);

        var fileName = $"risk-report-{_now.UtcNow:yyyy-MM-dd}.md";
        var filePath = Path.Combine(_options.ReportOutputPath, fileName);

        await File.WriteAllTextAsync(filePath, markdown);
        _logger.LogInformation("報告已寫入 {FilePath}", filePath);
    }
}
```

- [ ] **Step 3: Run tests**

Run: `cd src && dotnet test --filter "FullyQualifiedName~GenerateRiskReportTaskTests" --no-restore -v minimal`
Expected: PASS

- [ ] **Step 4: Commit**

```bash
git add src/ReleaseKit.Application/Tasks/GenerateRiskReportTask.cs tests/ReleaseKit.Application.Tests/Tasks/GenerateRiskReportTaskTests.cs
git commit -m "refactor: simplify GenerateRiskReportTask

Remove DetermineLastPassAsync and PassMetadata logic.
Directly load all Intermediate:* entries, sort by Sequence.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 10: Infrastructure — ShellCommandExecutor

**Files:**
- Create: `src/ReleaseKit.Infrastructure/Shell/ShellCommandExecutor.cs`
- Create: `tests/ReleaseKit.Infrastructure.Tests/Shell/ShellCommandExecutorTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/ReleaseKit.Infrastructure.Tests/Shell/ShellCommandExecutorTests.cs
using FluentAssertions;
using ReleaseKit.Infrastructure.Shell;

namespace ReleaseKit.Infrastructure.Tests.Shell;

/// <summary>
/// ShellCommandExecutor 單元測試
/// </summary>
public class ShellCommandExecutorTests
{
    private readonly ShellCommandExecutor _executor = new();

    [Fact]
    public async Task ExecuteAsync_執行echo指令_應回傳stdout()
    {
        // Act
        var result = await _executor.ExecuteAsync(
            "echo hello", "/tmp", TimeSpan.FromSeconds(5));

        // Assert
        result.StandardOutput.Trim().Should().Be("hello");
        result.ExitCode.Should().Be(0);
        result.TimedOut.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_指令失敗_應回傳非零ExitCode()
    {
        // Act
        var result = await _executor.ExecuteAsync(
            "exit 42", "/tmp", TimeSpan.FromSeconds(5));

        // Assert
        result.ExitCode.Should().Be(42);
        result.TimedOut.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_超時_應終止程序並設定TimedOut()
    {
        // Act
        var result = await _executor.ExecuteAsync(
            "sleep 30", "/tmp", TimeSpan.FromMilliseconds(200));

        // Assert
        result.TimedOut.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_工作目錄不在允許路徑下_應拋出ArgumentException()
    {
        // Arrange
        var executor = new ShellCommandExecutor("/safe/path");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            executor.ExecuteAsync("echo test", "/etc/passwd", TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task ExecuteAsync_工作目錄在允許路徑下_應正常執行()
    {
        // Arrange
        var executor = new ShellCommandExecutor("/tmp");

        // Act
        var result = await executor.ExecuteAsync(
            "echo ok", "/tmp/subdir/../", TimeSpan.FromSeconds(5));

        // Assert
        result.StandardOutput.Trim().Should().Be("ok");
    }

    [Fact]
    public async Task ExecuteAsync_stderr有輸出_應回傳stderr()
    {
        // Act
        var result = await _executor.ExecuteAsync(
            "echo error >&2", "/tmp", TimeSpan.FromSeconds(5));

        // Assert
        result.StandardError.Trim().Should().Be("error");
        result.ExitCode.Should().Be(0);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd src && dotnet test --filter "FullyQualifiedName~ShellCommandExecutorTests" --no-restore -v minimal`
Expected: FAIL — `ShellCommandExecutor` not found

- [ ] **Step 3: Write the implementation**

```csharp
// src/ReleaseKit.Infrastructure/Shell/ShellCommandExecutor.cs
using System.Diagnostics;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Infrastructure.Shell;

/// <summary>
/// Shell 指令執行器實作
/// </summary>
/// <remarks>
/// 透過 Process.Start 執行 shell 指令，支援超時控制與路徑安全驗證。
/// </remarks>
public class ShellCommandExecutor : IShellCommandExecutor
{
    private readonly string? _allowedBasePath;

    /// <summary>
    /// 初始化 <see cref="ShellCommandExecutor"/> 類別的新執行個體
    /// </summary>
    /// <param name="allowedBasePath">
    /// 允許的工作目錄基底路徑（可選）。
    /// 若提供，所有指令的工作目錄必須在此路徑下，防止路徑逃逸。
    /// </param>
    public ShellCommandExecutor(string? allowedBasePath = null)
    {
        _allowedBasePath = allowedBasePath;
    }

    /// <summary>在指定工作目錄執行 shell 指令</summary>
    public async Task<ShellCommandResult> ExecuteAsync(
        string command,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ValidateWorkingDirectory(workingDirectory);

        var startInfo = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"-c \"{command.Replace("\"", "\\\"")}\"",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

        try
        {
            await process.WaitForExitAsync(cts.Token);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            return new ShellCommandResult
            {
                StandardOutput = stdout,
                StandardError = stderr,
                ExitCode = process.ExitCode,
                TimedOut = false
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // 超時而非外部取消
            SafeKillProcess(process);

            return new ShellCommandResult
            {
                StandardOutput = "",
                StandardError = "指令執行超時",
                ExitCode = -1,
                TimedOut = true
            };
        }
    }

    /// <summary>驗證工作目錄是否在允許的基底路徑下</summary>
    private void ValidateWorkingDirectory(string workingDirectory)
    {
        if (_allowedBasePath is null)
            return;

        var normalizedBase = Path.GetFullPath(_allowedBasePath);
        var normalizedDir = Path.GetFullPath(workingDirectory);

        if (!normalizedDir.StartsWith(normalizedBase, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"工作目錄 '{workingDirectory}' 不在允許的路徑 '{_allowedBasePath}' 下",
                nameof(workingDirectory));
        }
    }

    /// <summary>安全終止程序</summary>
    private static void SafeKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // 程序已結束，忽略
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd src && dotnet test --filter "FullyQualifiedName~ShellCommandExecutorTests" --no-restore -v minimal`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/ReleaseKit.Infrastructure/Shell/ShellCommandExecutor.cs tests/ReleaseKit.Infrastructure.Tests/Shell/ShellCommandExecutorTests.cs
git commit -m "feat: add ShellCommandExecutor with path safety and timeout

Executes shell commands via Process.Start with:
- Working directory path validation (escape prevention)
- Configurable timeout with process tree kill
- Stdout/stderr capture

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 11: Infrastructure — Rewrite CopilotRiskAnalyzer (Agentic Mode)

**Files:**
- Modify: `src/ReleaseKit.Infrastructure/Copilot/CopilotRiskAnalyzer.cs`
- Modify: `tests/ReleaseKit.Infrastructure.Tests/Copilot/CopilotRiskAnalyzerTests.cs`

- [ ] **Step 1: Rewrite the tests**

```csharp
// tests/ReleaseKit.Infrastructure.Tests/Copilot/CopilotRiskAnalyzerTests.cs
using FluentAssertions;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Infrastructure.Copilot;

namespace ReleaseKit.Infrastructure.Tests.Copilot;

/// <summary>
/// CopilotRiskAnalyzer（Agentic 模式）單元測試
/// </summary>
/// <remarks>
/// 因 CopilotClient 為 sealed 類別無法 mock，
/// 測試聚焦於 internal static 解析方法與提示詞內容驗證。
/// </remarks>
public class CopilotRiskAnalyzerTests
{
    private static readonly DateTimeOffset FixedTime = new(2025, 1, 15, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("API 契約")]
    [InlineData("DB Schema")]
    [InlineData("DB 資料異動")]
    [InlineData("事件/訊息格式")]
    [InlineData("設定檔")]
    public void AgenticSystemPrompt_應包含所有風險類別關鍵字(string keyword)
    {
        CopilotRiskAnalyzer.AgenticSystemPrompt.Should().Contain(keyword);
    }

    [Fact]
    public void AgenticSystemPrompt_應包含run_command工具說明()
    {
        CopilotRiskAnalyzer.AgenticSystemPrompt.Should().Contain("run_command");
    }

    [Fact]
    public void AgenticSystemPrompt_應包含git指令建議()
    {
        CopilotRiskAnalyzer.AgenticSystemPrompt.Should().Contain("git diff");
        CopilotRiskAnalyzer.AgenticSystemPrompt.Should().Contain("git log");
    }

    [Fact]
    public void ParseProjectRiskResponse_有效JSON_應正確解析()
    {
        // Arrange
        var json = """
            {
              "riskItems": [
                {
                  "category": "ApiContract",
                  "level": "High",
                  "changeSummary": "修改了 API 回傳格式",
                  "affectedFiles": ["src/Controller.cs"],
                  "potentiallyAffectedServices": ["Frontend"],
                  "impactDescription": "前端可能解析失敗",
                  "suggestedValidationSteps": ["確認前端 API 呼叫"]
                }
              ],
              "summary": "發現 1 項高風險",
              "analysisLog": "執行了 git diff abc123"
            }
            """;

        // Act
        var result = CopilotRiskAnalyzer.ParseProjectRiskResponse(json, "my-svc", FixedTime);

        // Assert
        result.Should().NotBeNull();
        result!.Sequence.Should().Be(0);
        result.ProjectName.Should().Be("my-svc");
        result.Summary.Should().Be("發現 1 項高風險");
        result.AnalysisLog.Should().Be("執行了 git diff abc123");
        result.RiskItems.Should().HaveCount(1);
        result.RiskItems[0].Category.Should().Be(RiskCategory.ApiContract);
    }

    [Fact]
    public void ParseProjectRiskResponse_無效JSON_應回傳null()
    {
        var result = CopilotRiskAnalyzer.ParseProjectRiskResponse("not json {{{", "svc", FixedTime);
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ParseProjectRiskResponse_空白或null_應回傳null(string? content)
    {
        var result = CopilotRiskAnalyzer.ParseProjectRiskResponse(content!, "svc", FixedTime);
        result.Should().BeNull();
    }

    [Fact]
    public void CleanMarkdownWrapper_包含json代碼區塊_應清除標記()
    {
        var wrapped = """
            ```json
            {"riskItems":[],"summary":"無風險"}
            ```
            """;

        var result = CopilotRiskAnalyzer.CleanMarkdownWrapper(wrapped);
        result.Should().Be("""{"riskItems":[],"summary":"無風險"}""");
    }

    [Fact]
    public void CleanMarkdownWrapper_無包裝_應原樣回傳()
    {
        var plain = """{"riskItems":[],"summary":"ok"}""";
        var result = CopilotRiskAnalyzer.CleanMarkdownWrapper(plain);
        result.Should().Be(plain);
    }

    [Fact]
    public void BuildUserPrompt_應包含專案名稱和CommitSha()
    {
        // Arrange
        var context = new ProjectAnalysisContext
        {
            ProjectName = "my-service",
            RepoPath = "/repos/my-service",
            CommitShas = new List<string> { "abc123", "def456" }
        };

        // Act
        var prompt = CopilotRiskAnalyzer.BuildUserPrompt(context);

        // Assert
        prompt.Should().Contain("my-service");
        prompt.Should().Contain("abc123");
        prompt.Should().Contain("def456");
        prompt.Should().Contain("/repos/my-service");
    }
}
```

- [ ] **Step 2: Rewrite CopilotRiskAnalyzer**

```csharp
// src/ReleaseKit.Infrastructure/Copilot/CopilotRiskAnalyzer.cs
using System.ComponentModel;
using System.Text.RegularExpressions;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Infrastructure.Copilot;

/// <summary>
/// 使用 GitHub Copilot SDK 實作的風險分析服務（Agentic 模式）
/// </summary>
/// <remarks>
/// 建立 Copilot session 並註冊 run_command 工具，
/// 讓 AI 自主決定要執行什麼 shell 指令來探索 repo 並分析風險。
/// </remarks>
public class CopilotRiskAnalyzer : IRiskAnalyzer
{
    private readonly IOptions<CopilotOptions> _copilotOptions;
    private readonly IOptions<RiskAnalysisOptions> _riskOptions;
    private readonly IShellCommandExecutor _shellExecutor;
    private readonly INow _now;
    private readonly ILogger<CopilotRiskAnalyzer> _logger;

    /// <summary>
    /// Agentic 分析系統提示詞
    /// </summary>
    internal const string AgenticSystemPrompt = """
        你是一位資深軟體架構師，專精於微服務架構風險分析。

        ## 你的任務
        分析指定專案的 commit 變更，識別所有可能影響其他服務的風險。

        ## 可用工具
        你可以使用 `run_command` 工具在 repo 目錄中執行任意 shell 指令。
        - 建議先用 `git log`、`git diff`、`git show` 了解變更範圍
        - 可用 `grep`、`cat`、`find` 等深入檢查特定檔案
        - 每次指令輸出最多 {maxOutputChars} 字元，請自行用 | head、| tail、| grep 控制輸出量

        ## 風險類別
        1. API 契約變更 (ApiContract) — Controller endpoint、Request/Response 模型
        2. DB Schema 變更 (DatabaseSchema) — Migration、SQL、Entity 欄位
        3. DB 資料異動 (DatabaseData)【重點分析】— Seed data、Lookup table、預設值、Stored Procedure
        4. 事件/訊息格式變更 (EventFormat) — Event class
        5. 設定檔變更 (Configuration) — appsettings、環境變數

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
        """;

    /// <summary>
    /// 最終報告系統提示詞
    /// </summary>
    internal const string FinalReportSystemPrompt = """
        將以下風險分析結果彙整成一份完整的 Release 風險評估報告（Markdown 格式，繁體中文）。

        報告結構：
        # Release 風險評估報告
        ## 風險摘要
        ## 🔴 高風險項目
        ## 🟡 中風險項目
        ## 🟢 低風險項目
        ## 跨專案影響矩陣
        ## 建議的測試計畫
        ## 附錄
        """;

    /// <summary>
    /// 初始化 <see cref="CopilotRiskAnalyzer"/> 類別的新執行個體
    /// </summary>
    public CopilotRiskAnalyzer(
        IOptions<CopilotOptions> copilotOptions,
        IOptions<RiskAnalysisOptions> riskOptions,
        IShellCommandExecutor shellExecutor,
        INow now,
        ILogger<CopilotRiskAnalyzer> logger)
    {
        _copilotOptions = copilotOptions;
        _riskOptions = riskOptions;
        _shellExecutor = shellExecutor;
        _now = now;
        _logger = logger;
    }

    /// <summary>
    /// 分析單一專案的變更風險（Agentic 模式）
    /// </summary>
    public async Task<RiskAnalysisReport> AnalyzeProjectRiskAsync(
        ProjectAnalysisContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("開始 agentic 分析專案 {ProjectName}，共 {Count} 個 commit",
            context.ProjectName, context.CommitShas.Count);

        var systemPrompt = AgenticSystemPrompt
            .Replace("{maxOutputChars}", _riskOptions.Value.MaxOutputCharacters.ToString());

        var userPrompt = BuildUserPrompt(context);
        var timeout = TimeSpan.FromSeconds(_riskOptions.Value.CommandTimeoutSeconds);

        // 例外處理：外部 AI 呼叫需要明確的錯誤恢復邏輯
        try
        {
            var responseContent = await SendAgenticRequestAsync(
                systemPrompt, userPrompt, context.RepoPath, timeout, cancellationToken);

            if (string.IsNullOrWhiteSpace(responseContent))
            {
                _logger.LogWarning("Copilot 回傳空白回應，專案: {ProjectName}", context.ProjectName);
                return CreateEmptyReport(context.ProjectName);
            }

            var report = ParseProjectRiskResponse(responseContent, context.ProjectName, _now.UtcNow);
            if (report is null)
            {
                _logger.LogWarning("無法解析 Copilot 回應，專案: {ProjectName}", context.ProjectName);
                return CreateEmptyReport(context.ProjectName);
            }

            _logger.LogInformation("專案 {ProjectName} agentic 分析完成，識別到 {Count} 項風險",
                context.ProjectName, report.RiskItems.Count);
            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Copilot agentic 分析失敗，專案: {ProjectName}", context.ProjectName);
            return CreateEmptyReport(context.ProjectName);
        }
    }

    /// <summary>
    /// 產生最終整合報告 Markdown
    /// </summary>
    public async Task<string> GenerateFinalReportAsync(
        IReadOnlyList<RiskAnalysisReport> reports,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("開始產生最終風險報告，共 {Count} 份中間報告", reports.Count);

        var userPrompt = BuildFinalReportUserPrompt(reports);

        // 例外處理：外部 AI 呼叫需要明確的錯誤恢復邏輯
        try
        {
            var responseContent = await SendSimpleRequestAsync(
                FinalReportSystemPrompt, userPrompt, cancellationToken);

            return responseContent ?? "# 風險報告產生失敗\n\n無法從 AI 取得分析結果。";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "最終報告產生失敗");
            return "# 風險報告產生失敗\n\n產生過程發生錯誤。";
        }
    }

    /// <summary>
    /// 解析 Copilot 回應為 RiskAnalysisReport
    /// </summary>
    internal static RiskAnalysisReport? ParseProjectRiskResponse(
        string content,
        string projectName,
        DateTimeOffset analyzedAt)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        var cleaned = CleanMarkdownWrapper(content);

        try
        {
            var dto = cleaned.ToTypedObject<ProjectRiskResponseDto>();
            if (dto is null)
                return null;

            return new RiskAnalysisReport
            {
                Sequence = 0,
                ProjectName = projectName,
                RiskItems = dto.RiskItems ?? [],
                Summary = dto.Summary ?? string.Empty,
                AnalysisLog = dto.AnalysisLog,
                AnalyzedAt = analyzedAt
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 建構 agentic 分析使用者提示詞
    /// </summary>
    internal static string BuildUserPrompt(ProjectAnalysisContext context)
    {
        var shas = string.Join("\n", context.CommitShas.Select(s => $"  - {s}"));
        return $"""
            請分析專案 "{context.ProjectName}" 的以下 commit 變更：

            Repo 路徑: {context.RepoPath}
            Commit SHAs:
            {shas}

            請使用 run_command 工具探索 repo，分析這些 commit 的變更風險。
            工作目錄請使用: {context.RepoPath}
            """;
    }

    /// <summary>
    /// 清除 Markdown 代碼區塊包裝
    /// </summary>
    internal static string CleanMarkdownWrapper(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.StartsWith("```"))
        {
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0)
                trimmed = trimmed[(firstNewline + 1)..];

            if (trimmed.EndsWith("```"))
                trimmed = trimmed[..^3];

            return trimmed.Trim();
        }

        return trimmed;
    }

    /// <summary>
    /// 發送 agentic Copilot 請求（含 run_command 工具）
    /// </summary>
    private async Task<string?> SendAgenticRequestAsync(
        string systemPrompt,
        string userPrompt,
        string repoPath,
        TimeSpan commandTimeout,
        CancellationToken cancellationToken)
    {
        var model = _copilotOptions.Value.Model;
        var token = _copilotOptions.Value.GitHubToken;

        var clientOptions = new CopilotClientOptions { AutoStart = true };
        if (!string.IsNullOrWhiteSpace(token))
            clientOptions.GitHubToken = token;

        await using var client = new CopilotClient(clientOptions);

        var authStatus = await client.GetAuthStatusAsync();
        if (authStatus is not { IsAuthenticated: true })
        {
            _logger.LogError("Copilot SDK 身份驗證失敗");
            return null;
        }

        var runCommandTool = AIFunctionFactory.Create(
            async ([Description("要執行的 shell 指令")] string command,
                   [Description("工作目錄路徑")] string workingDirectory) =>
            {
                _logger.LogDebug("run_command: {Command} in {Dir}", command, workingDirectory);
                var result = await _shellExecutor.ExecuteAsync(
                    command, workingDirectory, commandTimeout, cancellationToken);

                var output = result.StandardOutput;
                if (output.Length > _riskOptions.Value.MaxOutputCharacters)
                    output = output[.._riskOptions.Value.MaxOutputCharacters] +
                             $"\n\n[輸出已截斷，共 {output.Length} 字元，僅顯示前 {_riskOptions.Value.MaxOutputCharacters} 字元]";

                if (result.TimedOut)
                    return $"[指令超時] stderr: {result.StandardError}";

                if (result.ExitCode != 0)
                    return $"[exit code: {result.ExitCode}]\nstdout:\n{output}\nstderr:\n{result.StandardError}";

                return output;
            },
            name: "run_command",
            description: "在指定的 repo 目錄中執行 shell 指令，回傳 stdout 與 stderr");

        await using var session = await client.CreateSessionAsync(new SessionConfig
        {
            Model = model,
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Replace,
                Content = systemPrompt
            },
            Tools = [runCommandTool],
            InfiniteSessions = new InfiniteSessionConfig { Enabled = false },
            OnPermissionRequest = PermissionHandler.ApproveAll
        });

        _logger.LogDebug("Agentic session 已建立，發送分析請求");

        var response = await session.SendAndWaitAsync(new MessageOptions
        {
            Prompt = userPrompt
        }, timeout: TimeSpan.FromSeconds(_copilotOptions.Value.TimeoutSeconds));

        return response?.Data?.Content;
    }

    /// <summary>
    /// 發送簡單 Copilot 請求（不含工具）
    /// </summary>
    private async Task<string?> SendSimpleRequestAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken)
    {
        var model = _copilotOptions.Value.Model;
        var token = _copilotOptions.Value.GitHubToken;

        var clientOptions = new CopilotClientOptions { AutoStart = true };
        if (!string.IsNullOrWhiteSpace(token))
            clientOptions.GitHubToken = token;

        await using var client = new CopilotClient(clientOptions);

        var authStatus = await client.GetAuthStatusAsync();
        if (authStatus is not { IsAuthenticated: true })
        {
            _logger.LogError("Copilot SDK 身份驗證失敗");
            return null;
        }

        await using var session = await client.CreateSessionAsync(new SessionConfig
        {
            Model = model,
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Replace,
                Content = systemPrompt
            },
            InfiniteSessions = new InfiniteSessionConfig { Enabled = false },
            OnPermissionRequest = PermissionHandler.ApproveAll
        });

        var response = await session.SendAndWaitAsync(new MessageOptions
        {
            Prompt = userPrompt
        }, timeout: TimeSpan.FromSeconds(_copilotOptions.Value.TimeoutSeconds));

        return response?.Data?.Content;
    }

    /// <summary>建構最終報告使用者提示詞</summary>
    private static string BuildFinalReportUserPrompt(IReadOnlyList<RiskAnalysisReport> reports)
    {
        var reportsJson = reports.ToJson();
        return $"請根據以下風險分析結果產生最終報告：\n{reportsJson}";
    }

    /// <summary>建立空白報告（回退值）</summary>
    private RiskAnalysisReport CreateEmptyReport(string projectName)
    {
        return new RiskAnalysisReport
        {
            Sequence = 0,
            ProjectName = projectName,
            RiskItems = [],
            Summary = "AI 分析失敗，未產生風險報告",
            AnalyzedAt = _now.UtcNow
        };
    }

    /// <summary>回應 DTO</summary>
    private sealed record ProjectRiskResponseDto
    {
        public IReadOnlyList<RiskItem>? RiskItems { get; init; }
        public string? Summary { get; init; }
        public string? AnalysisLog { get; init; }
    }
}
```

- [ ] **Step 3: Run tests**

Run: `cd src && dotnet test --filter "FullyQualifiedName~CopilotRiskAnalyzerTests" --no-restore -v minimal`
Expected: PASS

- [ ] **Step 4: Commit**

```bash
git add src/ReleaseKit.Infrastructure/Copilot/CopilotRiskAnalyzer.cs tests/ReleaseKit.Infrastructure.Tests/Copilot/CopilotRiskAnalyzerTests.cs
git commit -m "feat: rewrite CopilotRiskAnalyzer for agentic mode

Register run_command tool for Copilot to self-explore repos.
New system prompt with tool usage guidance.
Remove multi-pass logic and PrDiffContext dependency.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 12: Console — Update CLI, DI, and Parser

**Files:**
- Modify: `src/ReleaseKit.Console/Parsers/CommandLineParser.cs`
- Modify: `src/ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs`
- Modify: `tests/ReleaseKit.Console.Tests/Parsers/CommandLineParserRiskTests.cs`

- [ ] **Step 1: Update CommandLineParser**

Remove `extract-pr-diffs`, `analyze-project-risk`, `analyze-cross-project-risk` from the `_taskMappings` dictionary in `src/ReleaseKit.Console/Parsers/CommandLineParser.cs`:

Find and remove these 3 lines:
```csharp
{ "extract-pr-diffs", TaskType.ExtractPrDiffs },
{ "analyze-project-risk", TaskType.AnalyzeProjectRisk },
{ "analyze-cross-project-risk", TaskType.AnalyzeCrossProjectRisk },
```

- [ ] **Step 2: Update ServiceCollectionExtensions**

In `src/ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs`, update the risk analysis service registration:

Remove:
```csharp
services.AddTransient<ExtractPrDiffsTask>();
services.AddTransient<AnalyzeProjectRiskTask>();
services.AddTransient<AnalyzeCrossProjectRiskTask>();
```

Add:
```csharp
services.AddTransient<IShellCommandExecutor>(sp =>
{
    var riskOptions = sp.GetRequiredService<IOptions<RiskAnalysisOptions>>().Value;
    return new ReleaseKit.Infrastructure.Shell.ShellCommandExecutor(riskOptions.CloneBasePath);
});
```

The `using` statements at the top of the file should include:
```csharp
using ReleaseKit.Infrastructure.Shell;
```

- [ ] **Step 3: Update CommandLineParserRiskTests**

Read `tests/ReleaseKit.Console.Tests/Parsers/CommandLineParserRiskTests.cs` and update: remove tests for deleted commands, keep tests for `clone-repos`, `analyze-risk`, `generate-risk-report`.

- [ ] **Step 4: Run console tests**

Run: `cd src && dotnet test --filter "FullyQualifiedName~CommandLineParserRiskTests" --no-restore -v minimal`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor: update CLI, DI, and parser for agentic mode

Remove deleted task registrations and CLI mappings.
Add IShellCommandExecutor DI registration with path safety.
Update parser tests.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 13: Full Build and Test Verification

- [ ] **Step 1: Full build**

Run: `cd src && dotnet build --no-restore 2>&1 | tail -20`
Expected: Build succeeded with 0 errors

- [ ] **Step 2: Fix any remaining compilation errors**

If there are errors referencing deleted types (AnalysisPassKey, DynamicAnalysisResult, PrDiffContext, etc.), fix them by updating the referencing code.

- [ ] **Step 3: Full test run**

Run: `cd src && dotnet test --no-restore -v minimal`
Expected: All tests pass

- [ ] **Step 4: Fix any failing tests**

Address any test failures due to the refactoring.

- [ ] **Step 5: Final commit**

```bash
git add -A
git commit -m "fix: resolve remaining compilation and test issues

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 14: Update appsettings Configuration

**Files:**
- Modify: `appsettings.docker.json` (if it contains RiskAnalysis section)

- [ ] **Step 1: Check and update config files**

Search for `MaxTokensPerAiCall` and `MaxAnalysisPasses` in config files and replace with new settings:

```json
"RiskAnalysis": {
  "CloneBasePath": "/app/repos",
  "MaxConcurrentClones": 5,
  "MaxConcurrentAnalysis": 3,
  "MaxOutputCharacters": 50000,
  "CommandTimeoutSeconds": 30,
  "ReportOutputPath": "/app/reports"
}
```

- [ ] **Step 2: Commit**

```bash
git add -A
git commit -m "chore: update RiskAnalysis config for agentic mode

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Dependencies

```
Task 1 (ProjectAnalysisContext) ──┐
Task 2 (ShellCommandResult) ─────┤
Task 3 (IShellCommandExecutor) ──┼── Task 4 (RiskAnalysisReport refactor)
                                 │       │
                                 │   Task 5 (IRiskAnalyzer)
                                 │       │
                                 │   Task 6 (Options + RedisKeys)
                                 │       │
                                 │   Task 7 (Delete tasks + TaskType)
                                 │       │
                                 ├── Task 8 (AnalyzeRiskTask) ────┐
                                 │                                │
                                 ├── Task 9 (GenerateRiskReportTask)│
                                 │                                │
                                 ├── Task 10 (ShellCommandExecutor)│
                                 │                                │
                                 ├── Task 11 (CopilotRiskAnalyzer)│
                                 │                                │
                                 └── Task 12 (Console/DI) ───────┤
                                                                  │
                                                              Task 13 (Full verification)
                                                                  │
                                                              Task 14 (Config)
```

Tasks 1-3 can run in parallel.
Tasks 4-6 are sequential (domain changes).
Tasks 7-12 depend on 4-6 but some can overlap.
Tasks 8-11 can run in parallel after 7.
Task 13 must run after all others.
