# 情境專家型風險分析 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 實作情境專家型多 Agent 風險分析（CopilotScenarioAnalysis），新增一個與現有 CopilotRiskAnalysis 共存的替代策略。

**Architecture:** 三層 Copilot SDK Agent Pipeline — Coordinator 分配 commit 至 5 個情境專家 Agent（平行執行），Synthesis Agent 匯總去重。每個 Expert 擁有 `get_commit_overview`、`get_full_diff`、`get_file_content`、`search_pattern`、`list_directory` 工具，AI 自主決定探索深度。

**Tech Stack:** C# .NET 10, GitHub Copilot SDK, Redis, git grep, Moq + xUnit + FluentAssertions

---

## File Structure

```
src/ReleaseKit.Domain/
  Abstractions/
    ICopilotScenarioDispatcher.cs          ← 新增介面

src/ReleaseKit.Infrastructure/
  Copilot/
    ScenarioAnalysis/
      CopilotScenarioDispatcher.cs         ← 主協調器
      CoordinatorAgentRunner.cs            ← Coordinator Agent 執行器
      ExpertAgentRunner.cs                 ← Expert Agent 執行器
      SynthesisAgentRunner.cs              ← Synthesis Agent 執行器
      ScenarioPromptBuilder.cs             ← Prompt 模板
      ExpertToolFactory.cs                 ← Expert 工具集工廠
      Models/
        CoordinatorAssignment.cs           ← Coordinator 輸出 DTO
        ExpertFindings.cs                  ← Expert 輸出 DTO
        SynthesisInput.cs                  ← Synthesis 輸入 DTO

src/ReleaseKit.Infrastructure/
  Git/
    GitOperationService.cs                 ← 修改：新增 SearchPatternAsync

src/ReleaseKit.Domain/
  Abstractions/
    IGitOperationService.cs                ← 修改：新增 SearchPatternAsync

src/ReleaseKit.Application/
  Tasks/
    CopilotScenarioAnalysisTask.cs         ← 新增任務
    TaskType.cs                            ← 修改：新增 enum 值
    TaskFactory.cs                         ← 修改：新增 case

src/ReleaseKit.Common/
  Constants/
    RiskAnalysisRedisKeys.cs               ← 修改：新增 Stage4Scenario keys

src/ReleaseKit.Console/
  Extensions/
    ServiceCollectionExtensions.cs         ← 修改：DI 註冊

tests/ReleaseKit.Infrastructure.Tests/
  Copilot/
    ScenarioAnalysis/
      ScenarioPromptBuilderTests.cs        ← Prompt 測試
      ExpertToolFactoryTests.cs            ← 工具工廠測試
      CoordinatorAgentRunnerTests.cs       ← Coordinator 測試
      ExpertAgentRunnerTests.cs            ← Expert 測試
      SynthesisAgentRunnerTests.cs         ← Synthesis 測試
      CopilotScenarioDispatcherTests.cs    ← 整合測試

tests/ReleaseKit.Infrastructure.Tests/
  Git/
    GitOperationServiceSearchPatternTests.cs ← SearchPattern 測試

tests/ReleaseKit.Application.Tests/
  Tasks/
    CopilotScenarioAnalysisTaskTests.cs    ← Task 測試
```

---

### Task 1: Domain 介面 — ICopilotScenarioDispatcher + IGitOperationService 擴充

**Files:**
- Create: `src/ReleaseKit.Domain/Abstractions/ICopilotScenarioDispatcher.cs`
- Modify: `src/ReleaseKit.Domain/Abstractions/IGitOperationService.cs`

- [ ] **Step 1: 建立 ICopilotScenarioDispatcher 介面**

```csharp
// src/ReleaseKit.Domain/Abstractions/ICopilotScenarioDispatcher.cs
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Abstractions;

/// <summary>
/// 情境專家型 Copilot 風險分析調度器介面
/// </summary>
public interface ICopilotScenarioDispatcher
{
    /// <summary>
    /// 啟動三層 Agent Pipeline（Coordinator → Expert × 5 → Synthesis）對指定專案進行風險分析
    /// </summary>
    /// <param name="runId">本次執行 ID</param>
    /// <param name="projectPath">專案路徑</param>
    /// <param name="commitSummaries">各 Commit 的異動摘要</param>
    /// <param name="localPath">本地 clone 路徑</param>
    /// <param name="scenarios">要分析的風險情境清單</param>
    /// <param name="ct">取消標記</param>
    /// <returns>分析結果</returns>
    Task<ProjectRiskAnalysis> DispatchAsync(
        string runId,
        string projectPath,
        IReadOnlyList<CommitSummary> commitSummaries,
        string localPath,
        IReadOnlyList<RiskScenario> scenarios,
        CancellationToken ct = default);
}
```

- [ ] **Step 2: 擴充 IGitOperationService 新增 SearchPatternAsync**

在 `IGitOperationService.cs` 末尾新增：

```csharp
/// <summary>
/// 使用 git grep 搜尋程式碼庫中符合模式的內容
/// </summary>
/// <param name="repoPath">本地 repo 路徑</param>
/// <param name="pattern">搜尋模式（正規表示式）</param>
/// <param name="fileGlob">檔案 glob 篩選（如 "*.cs"），null 表示搜尋所有檔案</param>
/// <param name="cancellationToken">取消標記</param>
/// <returns>搜尋結果（每行格式：檔案:行號:內容）</returns>
Task<Result<string>> SearchPatternAsync(
    string repoPath,
    string pattern,
    string? fileGlob = null,
    CancellationToken cancellationToken = default);
```

- [ ] **Step 3: 確認建置成功**

Run: `dotnet build src/release-kit.sln --no-restore -q`
Expected: 建置成功（SearchPatternAsync 尚未實作，但介面定義不影響編譯）

- [ ] **Step 4: Commit**

```bash
git add src/ReleaseKit.Domain/Abstractions/ICopilotScenarioDispatcher.cs src/ReleaseKit.Domain/Abstractions/IGitOperationService.cs
git commit -m "feat(domain): 新增 ICopilotScenarioDispatcher 介面與 IGitOperationService.SearchPatternAsync"
```

---

### Task 2: GitOperationService — 實作 SearchPatternAsync

**Files:**
- Modify: `src/ReleaseKit.Infrastructure/Git/GitOperationService.cs`
- Create: `tests/ReleaseKit.Infrastructure.Tests/Git/GitOperationServiceSearchPatternTests.cs`

- [ ] **Step 1: 撰寫失敗的測試**

```csharp
// tests/ReleaseKit.Infrastructure.Tests/Git/GitOperationServiceSearchPatternTests.cs
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReleaseKit.Infrastructure.Git;

namespace ReleaseKit.Infrastructure.Tests.Git;

/// <summary>
/// GitOperationService.SearchPatternAsync 單元測試
/// </summary>
public class GitOperationServiceSearchPatternTests
{
    private readonly GitOperationService _sut;

    public GitOperationServiceSearchPatternTests()
    {
        var logger = new Mock<ILogger<GitOperationService>>();
        _sut = new GitOperationService(logger.Object);
    }

    [Fact]
    public async Task SearchPatternAsync_非Git倉庫_應回傳失敗()
    {
        // Arrange
        var nonGitPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(nonGitPath);

        try
        {
            // Act
            var result = await _sut.SearchPatternAsync(nonGitPath, "test");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error!.Code.Should().Be("Git.SearchFailed");
        }
        finally
        {
            Directory.Delete(nonGitPath, true);
        }
    }

    [Fact]
    public async Task SearchPatternAsync_有效倉庫但無符合結果_應回傳空字串()
    {
        // Arrange — 使用當前專案 repo
        var repoPath = GetCurrentRepoRoot();

        // Act — 搜尋一個不可能存在的模式
        var result = await _sut.SearchPatternAsync(repoPath, "ZZZZZ_IMPOSSIBLE_PATTERN_99999");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchPatternAsync_有效倉庫有符合結果_應回傳搜尋內容()
    {
        // Arrange — 使用當前專案 repo
        var repoPath = GetCurrentRepoRoot();

        // Act — 搜尋 "IGitOperationService"，這在 Domain 層一定存在
        var result = await _sut.SearchPatternAsync(repoPath, "IGitOperationService", "*.cs");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("IGitOperationService");
    }

    private static string GetCurrentRepoRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null && !Directory.Exists(Path.Combine(dir, ".git")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("找不到 Git 倉庫根目錄");
    }
}
```

- [ ] **Step 2: 執行測試確認失敗**

Run: `dotnet test tests/ReleaseKit.Infrastructure.Tests --filter "FullyQualifiedName~GitOperationServiceSearchPatternTests" --no-build -v q`
Expected: 編譯失敗（方法尚未實作）

- [ ] **Step 3: 實作 SearchPatternAsync**

在 `GitOperationService.cs` 末尾（`SanitizeUrl` 方法之前）新增：

```csharp
/// <inheritdoc />
public async Task<Result<string>> SearchPatternAsync(
    string repoPath,
    string pattern,
    string? fileGlob = null,
    CancellationToken cancellationToken = default)
{
    if (!Directory.Exists(Path.Combine(repoPath, ".git")))
    {
        return Result<string>.Failure(
            new Error("Git.SearchFailed", $"'{repoPath}' 不是有效的 Git 倉庫"));
    }

    var arguments = fileGlob != null
        ? $"grep -n -E {pattern} -- {fileGlob}"
        : $"grep -n -E {pattern}";

    var result = await RunGitCommandAsync(arguments, repoPath, cancellationToken);

    // git grep exit code 1 表示無符合結果，不算錯誤
    if (!result.IsSuccess && result.Error?.Message == string.Empty)
    {
        return Result<string>.Success(string.Empty);
    }

    if (!result.IsSuccess)
    {
        // git grep 返回 exit code 1 時 stderr 為空表示無結果
        return Result<string>.Success(string.Empty);
    }

    return Result<string>.Success(result.Value ?? string.Empty);
}
```

注意：`git grep` 在無匹配時 exit code = 1，需要特殊處理。修改 `RunGitCommandAsync` 或新增一個允許 exit code 1 的重載：

```csharp
/// <summary>
/// 執行 git 命令（允許指定可接受的 exit code）
/// </summary>
private async Task<Result<string>> RunGitCommandAsync(
    string arguments,
    string workingDirectory,
    IReadOnlyList<int> acceptableExitCodes,
    CancellationToken cancellationToken)
{
    var startInfo = new ProcessStartInfo
    {
        FileName = "git",
        Arguments = arguments,
        WorkingDirectory = workingDirectory,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };

    using var process = Process.Start(startInfo);
    if (process == null)
    {
        return Result<string>.Failure(new Error("Git.ProcessFailed", "無法啟動 git 程序"));
    }

    var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
    var error = await process.StandardError.ReadToEndAsync(cancellationToken);
    await process.WaitForExitAsync(cancellationToken);

    if (process.ExitCode != 0 && !acceptableExitCodes.Contains(process.ExitCode))
    {
        _logger.LogError("git {Arguments} 失敗 (exit code {ExitCode}): {Error}",
            SanitizeUrl(arguments), process.ExitCode, error);
        return Result<string>.Failure(new Error("Git.CommandFailed", error.Trim()));
    }

    return Result<string>.Success(output);
}
```

然後 `SearchPatternAsync` 改用：

```csharp
public async Task<Result<string>> SearchPatternAsync(
    string repoPath,
    string pattern,
    string? fileGlob = null,
    CancellationToken cancellationToken = default)
{
    if (!Directory.Exists(Path.Combine(repoPath, ".git")))
    {
        return Result<string>.Failure(
            new Error("Git.SearchFailed", $"'{repoPath}' 不是有效的 Git 倉庫"));
    }

    var arguments = fileGlob != null
        ? $"grep -n -E \"{pattern}\" -- \"{fileGlob}\""
        : $"grep -n -E \"{pattern}\"";

    // git grep exit code 1 = 無符合結果，屬正常情境
    var result = await RunGitCommandAsync(arguments, repoPath, [1], cancellationToken);

    if (!result.IsSuccess)
    {
        return Result<string>.Failure(
            new Error("Git.SearchFailed", result.Error!.Message));
    }

    return Result<string>.Success(result.Value ?? string.Empty);
}
```

- [ ] **Step 4: 執行測試確認通過**

Run: `dotnet test tests/ReleaseKit.Infrastructure.Tests --filter "FullyQualifiedName~GitOperationServiceSearchPatternTests" -v q`
Expected: 3 tests passed

- [ ] **Step 5: Commit**

```bash
git add src/ReleaseKit.Infrastructure/Git/GitOperationService.cs tests/ReleaseKit.Infrastructure.Tests/Git/GitOperationServiceSearchPatternTests.cs
git commit -m "feat(infra): 實作 GitOperationService.SearchPatternAsync (git grep)"
```

---

### Task 3: Redis Keys 與 Models

**Files:**
- Modify: `src/ReleaseKit.Common/Constants/RiskAnalysisRedisKeys.cs`
- Create: `src/ReleaseKit.Infrastructure/Copilot/ScenarioAnalysis/Models/CoordinatorAssignment.cs`
- Create: `src/ReleaseKit.Infrastructure/Copilot/ScenarioAnalysis/Models/ExpertFindings.cs`
- Create: `src/ReleaseKit.Infrastructure/Copilot/ScenarioAnalysis/Models/SynthesisInput.cs`

- [ ] **Step 1: 擴充 RiskAnalysisRedisKeys**

在 `RiskAnalysisRedisKeys.cs` 新增：

```csharp
/// <summary>
/// 取得 Stage 4 Scenario Coordinator 的 Redis Hash Key
/// </summary>
public static string Stage4ScenarioCoordinatorHash(string runId) => $"{Prefix}:{runId}:Stage4Scenario:Coordinator";

/// <summary>
/// 取得 Stage 4 Scenario Expert 的 Redis Hash Key
/// </summary>
public static string Stage4ScenarioExpertHash(string runId) => $"{Prefix}:{runId}:Stage4Scenario:Expert";

/// <summary>
/// 取得 Stage 4 Scenario Synthesis 的 Redis Hash Key
/// </summary>
public static string Stage4ScenarioSynthesisHash(string runId) => $"{Prefix}:{runId}:Stage4Scenario:Synthesis";
```

- [ ] **Step 2: 建立 CoordinatorAssignment DTO**

```csharp
// src/ReleaseKit.Infrastructure/Copilot/ScenarioAnalysis/Models/CoordinatorAssignment.cs
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis.Models;

/// <summary>
/// Coordinator Agent 的分配結果
/// </summary>
public sealed record CoordinatorAssignment
{
    /// <summary>
    /// 各情境對應的 CommitSha 清單
    /// </summary>
    public required Dictionary<RiskScenario, List<string>> Assignments { get; init; }

    /// <summary>
    /// Coordinator 的推理說明
    /// </summary>
    public string Reasoning { get; init; } = string.Empty;

    /// <summary>
    /// 取得指定情境的 CommitSha 清單
    /// </summary>
    public IReadOnlyList<string> GetShas(RiskScenario scenario)
        => Assignments.TryGetValue(scenario, out var shas) ? shas : [];
}
```

- [ ] **Step 3: 建立 ExpertFindings DTO**

```csharp
// src/ReleaseKit.Infrastructure/Copilot/ScenarioAnalysis/Models/ExpertFindings.cs
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis.Models;

/// <summary>
/// Expert Agent 的分析結果
/// </summary>
public sealed record ExpertFindings
{
    /// <summary>
    /// 分析情境
    /// </summary>
    public required RiskScenario Scenario { get; init; }

    /// <summary>
    /// 風險發現清單
    /// </summary>
    public required IReadOnlyList<RiskFinding> Findings { get; init; }

    /// <summary>
    /// 是否分析失敗（需人工檢視）
    /// </summary>
    public bool Failed { get; init; }

    /// <summary>
    /// 失敗原因
    /// </summary>
    public string? FailureReason { get; init; }
}
```

- [ ] **Step 4: 建立 SynthesisInput DTO**

```csharp
// src/ReleaseKit.Infrastructure/Copilot/ScenarioAnalysis/Models/SynthesisInput.cs
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis.Models;

/// <summary>
/// Synthesis Agent 的輸入資料
/// </summary>
public sealed record SynthesisInput
{
    /// <summary>
    /// 專案路徑
    /// </summary>
    public required string ProjectPath { get; init; }

    /// <summary>
    /// 所有 Expert 的分析結果（依情境分組）
    /// </summary>
    public required IReadOnlyDictionary<RiskScenario, ExpertFindings> ExpertResults { get; init; }

    /// <summary>
    /// 其他專案的摘要資訊（供跨專案推斷，軟性依賴 Stage 3）
    /// </summary>
    public IReadOnlyList<string>? OtherProjectsSummary { get; init; }
}
```

- [ ] **Step 5: 確認建置成功**

Run: `dotnet build src/release-kit.sln --no-restore -q`
Expected: 建置成功

- [ ] **Step 6: Commit**

```bash
git add src/ReleaseKit.Common/Constants/RiskAnalysisRedisKeys.cs src/ReleaseKit.Infrastructure/Copilot/ScenarioAnalysis/Models/
git commit -m "feat: 新增 ScenarioAnalysis Redis Keys 與 DTO 模型"
```

---

### Task 4: ScenarioPromptBuilder

**Files:**
- Create: `src/ReleaseKit.Infrastructure/Copilot/ScenarioAnalysis/ScenarioPromptBuilder.cs`
- Create: `tests/ReleaseKit.Infrastructure.Tests/Copilot/ScenarioAnalysis/ScenarioPromptBuilderTests.cs`

- [ ] **Step 1: 撰寫失敗的測試**

```csharp
// tests/ReleaseKit.Infrastructure.Tests/Copilot/ScenarioAnalysis/ScenarioPromptBuilderTests.cs
using FluentAssertions;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis;

namespace ReleaseKit.Infrastructure.Tests.Copilot.ScenarioAnalysis;

/// <summary>
/// ScenarioPromptBuilder 單元測試
/// </summary>
public class ScenarioPromptBuilderTests
{
    [Fact]
    public void BuildCoordinatorSystemPrompt_應回傳非空字串()
    {
        var result = ScenarioPromptBuilder.BuildCoordinatorSystemPrompt();
        result.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void BuildCoordinatorSystemPrompt_應包含分配規則()
    {
        var result = ScenarioPromptBuilder.BuildCoordinatorSystemPrompt();
        result.Should().Contain("分配");
    }

    [Fact]
    public void BuildCoordinatorUserPrompt_應包含專案路徑與CommitSha()
    {
        var summaries = new List<CommitSummary>
        {
            new()
            {
                CommitSha = "abc123",
                ChangedFiles = new List<FileDiff>
                {
                    new() { FilePath = "Controllers/UserController.cs", ChangeType = ChangeType.Modified, CommitSha = "abc123" }
                },
                TotalFilesChanged = 1,
                TotalLinesAdded = 10,
                TotalLinesRemoved = 3
            }
        };

        var result = ScenarioPromptBuilder.BuildCoordinatorUserPrompt("team-a/api", summaries);

        result.Should().Contain("team-a/api");
        result.Should().Contain("abc123");
        result.Should().Contain("UserController.cs");
    }

    [Theory]
    [InlineData(RiskScenario.ApiContractBreak)]
    [InlineData(RiskScenario.DatabaseSchemaChange)]
    [InlineData(RiskScenario.MessageQueueFormat)]
    [InlineData(RiskScenario.ConfigEnvChange)]
    [InlineData(RiskScenario.DataSemanticChange)]
    public void BuildExpertSystemPrompt_各情境應回傳非空字串(RiskScenario scenario)
    {
        var result = ScenarioPromptBuilder.BuildExpertSystemPrompt(scenario);
        result.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void BuildExpertSystemPrompt_API情境應包含Controller關鍵字()
    {
        var result = ScenarioPromptBuilder.BuildExpertSystemPrompt(RiskScenario.ApiContractBreak);
        result.Should().Contain("Controller");
    }

    [Fact]
    public void BuildExpertUserPrompt_應包含CommitSha清單()
    {
        var shas = new List<string> { "sha1", "sha2" };
        var result = ScenarioPromptBuilder.BuildExpertUserPrompt("project-a", shas, RiskScenario.ApiContractBreak);

        result.Should().Contain("sha1");
        result.Should().Contain("sha2");
    }

    [Fact]
    public void BuildSynthesisSystemPrompt_應回傳非空字串()
    {
        var result = ScenarioPromptBuilder.BuildSynthesisSystemPrompt();
        result.Should().NotBeNullOrWhiteSpace();
        result.Should().Contain("去重");
    }
}
```

- [ ] **Step 2: 執行測試確認失敗**

Run: `dotnet test tests/ReleaseKit.Infrastructure.Tests --filter "FullyQualifiedName~ScenarioPromptBuilderTests" --no-build -v q`
Expected: 編譯失敗（類別不存在）

- [ ] **Step 3: 實作 ScenarioPromptBuilder**

```csharp
// src/ReleaseKit.Infrastructure/Copilot/ScenarioAnalysis/ScenarioPromptBuilder.cs
using System.Text;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

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
```

注意：`BuildSynthesisUserPrompt` 使用了 `SynthesisInput`，需要加上 using：
```csharp
using ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis.Models;
```

- [ ] **Step 4: 執行測試確認通過**

Run: `dotnet test tests/ReleaseKit.Infrastructure.Tests --filter "FullyQualifiedName~ScenarioPromptBuilderTests" -v q`
Expected: All tests passed

- [ ] **Step 5: Commit**

```bash
git add src/ReleaseKit.Infrastructure/Copilot/ScenarioAnalysis/ScenarioPromptBuilder.cs tests/ReleaseKit.Infrastructure.Tests/Copilot/ScenarioAnalysis/ScenarioPromptBuilderTests.cs
git commit -m "feat(infra): 實作 ScenarioPromptBuilder 各角色 prompt 模板"
```

---

### Task 5: ExpertToolFactory

**Files:**
- Create: `src/ReleaseKit.Infrastructure/Copilot/ScenarioAnalysis/ExpertToolFactory.cs`
- Create: `tests/ReleaseKit.Infrastructure.Tests/Copilot/ScenarioAnalysis/ExpertToolFactoryTests.cs`

- [ ] **Step 1: 撰寫失敗的測試**

```csharp
// tests/ReleaseKit.Infrastructure.Tests/Copilot/ScenarioAnalysis/ExpertToolFactoryTests.cs
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Common;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis;

namespace ReleaseKit.Infrastructure.Tests.Copilot.ScenarioAnalysis;

/// <summary>
/// ExpertToolFactory 單元測試
/// </summary>
public class ExpertToolFactoryTests
{
    private readonly Mock<IGitOperationService> _gitServiceMock;
    private readonly ExpertToolFactory _sut;

    public ExpertToolFactoryTests()
    {
        _gitServiceMock = new Mock<IGitOperationService>();
        var logger = new Mock<ILogger<ExpertToolFactory>>();
        _sut = new ExpertToolFactory(_gitServiceMock.Object, logger.Object);
    }

    [Fact]
    public void CreateTools_應回傳5個工具()
    {
        var tools = _sut.CreateTools("/repo/path", CancellationToken.None);
        tools.Should().HaveCount(5);
    }

    [Fact]
    public void CreateTools_應包含所有必要工具名稱()
    {
        var tools = _sut.CreateTools("/repo/path", CancellationToken.None);
        var names = tools.Select(t => t.Name).ToList();

        names.Should().Contain("get_commit_overview");
        names.Should().Contain("get_full_diff");
        names.Should().Contain("get_file_content");
        names.Should().Contain("search_pattern");
        names.Should().Contain("list_directory");
    }
}
```

- [ ] **Step 2: 執行測試確認失敗**

Run: `dotnet test tests/ReleaseKit.Infrastructure.Tests --filter "FullyQualifiedName~ExpertToolFactoryTests" --no-build -v q`
Expected: 編譯失敗

- [ ] **Step 3: 實作 ExpertToolFactory**

```csharp
// src/ReleaseKit.Infrastructure/Copilot/ScenarioAnalysis/ExpertToolFactory.cs
using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis;

/// <summary>
/// Expert Agent 工具集工廠
/// </summary>
public class ExpertToolFactory
{
    private readonly IGitOperationService _gitService;
    private readonly ILogger<ExpertToolFactory> _logger;

    /// <summary>
    /// 初始化 <see cref="ExpertToolFactory"/>
    /// </summary>
    public ExpertToolFactory(IGitOperationService gitService, ILogger<ExpertToolFactory> logger)
    {
        _gitService = gitService;
        _logger = logger;
    }

    /// <summary>
    /// 建立 Expert Agent 的工具集
    /// </summary>
    /// <param name="localPath">本地 repo 路徑</param>
    /// <param name="ct">取消標記</param>
    /// <returns>AI 工具清單</returns>
    public IReadOnlyList<AIFunction> CreateTools(string localPath, CancellationToken ct)
    {
        var getCommitOverview = AIFunctionFactory.Create(
            async ([Description("要查看的 CommitSha")] string commitSha) =>
            {
                var result = await _gitService.GetCommitStatAsync(localPath, commitSha, ct);
                if (!result.IsSuccess)
                    return $"錯誤: {result.Error!.Message}";

                var summary = result.Value;
                var sb = new StringBuilder();
                sb.AppendLine($"CommitSha: {commitSha}");
                sb.AppendLine($"統計: {summary.TotalFilesChanged} 檔案, +{summary.TotalLinesAdded}/-{summary.TotalLinesRemoved}");
                sb.AppendLine("異動檔案:");
                foreach (var f in summary.ChangedFiles)
                    sb.AppendLine($"  [{f.ChangeType}] {f.FilePath}");
                return sb.ToString();
            },
            "get_commit_overview",
            "取得 Commit 的異動摘要（檔案清單 + 行數統計），用於快速評估是否需要看完整 diff");

        var getFullDiff = AIFunctionFactory.Create(
            async ([Description("要取得完整 diff 的 CommitSha")] string commitSha) =>
            {
                var result = await _gitService.GetCommitRawDiffAsync(localPath, commitSha, ct);
                return result.IsSuccess ? result.Value! : $"錯誤: {result.Error!.Message}";
            },
            "get_full_diff",
            "取得指定 CommitSha 的完整 unified diff 內容（較大，建議先用 get_commit_overview 評估）");

        var getFileContent = AIFunctionFactory.Create(
            async ([Description("檔案的相對路徑（相對於 repo 根目錄）")] string filePath) =>
            {
                var fullPath = Path.Combine(localPath, filePath);
                if (!File.Exists(fullPath))
                    return $"檔案不存在: {filePath}";

                var content = await File.ReadAllTextAsync(fullPath, ct);
                // 限制檔案大小避免 token 爆量
                if (content.Length > 50000)
                    return content[..50000] + "\n\n... (檔案過大，已截斷至前 50000 字元)";
                return content;
            },
            "get_file_content",
            "取得指定檔案的完整內容（用於理解變更的上下文）");

        var searchPattern = AIFunctionFactory.Create(
            async (
                [Description("搜尋模式（正規表示式）")] string pattern,
                [Description("檔案 glob 篩選（如 '*.cs'），可省略表示搜尋所有檔案")] string? fileGlob) =>
            {
                var result = await _gitService.SearchPatternAsync(localPath, pattern, fileGlob, ct);
                if (!result.IsSuccess)
                    return $"搜尋失敗: {result.Error!.Message}";

                var output = result.Value ?? string.Empty;
                // 限制輸出大小
                if (output.Length > 20000)
                    return output[..20000] + "\n\n... (結果過多，已截斷)";
                return string.IsNullOrEmpty(output) ? "無符合結果" : output;
            },
            "search_pattern",
            "使用 git grep 搜尋程式碼庫中符合模式的內容（用於尋找呼叫端、消費端等）");

        var listDirectory = AIFunctionFactory.Create(
            ([Description("目錄的相對路徑（相對於 repo 根目錄），空字串表示根目錄")] string path) =>
            {
                var fullPath = Path.Combine(localPath, path ?? string.Empty);
                if (!Directory.Exists(fullPath))
                    return $"目錄不存在: {path}";

                var entries = Directory.GetFileSystemEntries(fullPath)
                    .Select(e => Path.GetRelativePath(localPath, e))
                    .OrderBy(e => e)
                    .ToList();

                return string.Join("\n", entries);
            },
            "list_directory",
            "列出指定目錄的檔案與子目錄結構");

        return [getCommitOverview, getFullDiff, getFileContent, searchPattern, listDirectory];
    }
}
```

- [ ] **Step 4: 執行測試確認通過**

Run: `dotnet test tests/ReleaseKit.Infrastructure.Tests --filter "FullyQualifiedName~ExpertToolFactoryTests" -v q`
Expected: All tests passed

- [ ] **Step 5: Commit**

```bash
git add src/ReleaseKit.Infrastructure/Copilot/ScenarioAnalysis/ExpertToolFactory.cs tests/ReleaseKit.Infrastructure.Tests/Copilot/ScenarioAnalysis/ExpertToolFactoryTests.cs
git commit -m "feat(infra): 實作 ExpertToolFactory 建立 Expert Agent 工具集"
```

---

### Task 6: CoordinatorAgentRunner

**Files:**
- Create: `src/ReleaseKit.Infrastructure/Copilot/ScenarioAnalysis/CoordinatorAgentRunner.cs`
- Create: `tests/ReleaseKit.Infrastructure.Tests/Copilot/ScenarioAnalysis/CoordinatorAgentRunnerTests.cs`

- [ ] **Step 1: 撰寫失敗的測試**

```csharp
// tests/ReleaseKit.Infrastructure.Tests/Copilot/ScenarioAnalysis/CoordinatorAgentRunnerTests.cs
using FluentAssertions;
using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis;

namespace ReleaseKit.Infrastructure.Tests.Copilot.ScenarioAnalysis;

/// <summary>
/// CoordinatorAgentRunner.ParseAssignment 單元測試
/// </summary>
public class CoordinatorAgentRunnerTests
{
    [Fact]
    public void ParseAssignment_有效JSON_應正確解析()
    {
        var json = """
            ```json
            {
              "assignments": {
                "ApiContractBreak": ["sha1", "sha2"],
                "DatabaseSchemaChange": ["sha3"],
                "MessageQueueFormat": [],
                "ConfigEnvChange": ["sha1"],
                "DataSemanticChange": []
              },
              "reasoning": "sha1 改了 Controller"
            }
            ```
            """;

        var result = CoordinatorAgentRunner.ParseAssignment(json);

        result.Should().NotBeNull();
        result!.Assignments[RiskScenario.ApiContractBreak].Should().Contain("sha1", "sha2");
        result.Assignments[RiskScenario.DatabaseSchemaChange].Should().Contain("sha3");
        result.Assignments[RiskScenario.MessageQueueFormat].Should().BeEmpty();
        result.Reasoning.Should().Contain("Controller");
    }

    [Fact]
    public void ParseAssignment_無效JSON_應回傳null()
    {
        var invalid = "這不是 JSON";

        var result = CoordinatorAgentRunner.ParseAssignment(invalid);

        result.Should().BeNull();
    }

    [Fact]
    public void ParseAssignment_無代碼塊的純JSON_應正確解析()
    {
        var json = """
            {
              "assignments": {
                "ApiContractBreak": ["sha1"],
                "DatabaseSchemaChange": [],
                "MessageQueueFormat": [],
                "ConfigEnvChange": [],
                "DataSemanticChange": []
              },
              "reasoning": "理由"
            }
            """;

        var result = CoordinatorAgentRunner.ParseAssignment(json);

        result.Should().NotBeNull();
        result!.Assignments[RiskScenario.ApiContractBreak].Should().Contain("sha1");
    }

    [Fact]
    public void BuildFallbackAssignment_應將所有commit分配給每個情境()
    {
        var commitShas = new List<string> { "sha1", "sha2", "sha3" };
        var scenarios = new List<RiskScenario>
        {
            RiskScenario.ApiContractBreak,
            RiskScenario.DatabaseSchemaChange
        };

        var result = CoordinatorAgentRunner.BuildFallbackAssignment(commitShas, scenarios);

        result.Assignments[RiskScenario.ApiContractBreak].Should().BeEquivalentTo(commitShas);
        result.Assignments[RiskScenario.DatabaseSchemaChange].Should().BeEquivalentTo(commitShas);
    }
}
```

- [ ] **Step 2: 執行測試確認失敗**

Run: `dotnet test tests/ReleaseKit.Infrastructure.Tests --filter "FullyQualifiedName~CoordinatorAgentRunnerTests" --no-build -v q`
Expected: 編譯失敗

- [ ] **Step 3: 實作 CoordinatorAgentRunner**

```csharp
// src/ReleaseKit.Infrastructure/Copilot/ScenarioAnalysis/CoordinatorAgentRunner.cs
using System.Text.Json;
using System.Text.RegularExpressions;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis.Models;
using System.ComponentModel;

namespace ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis;

/// <summary>
/// Coordinator Agent 執行器：分配 commit 至各情境專家
/// </summary>
public class CoordinatorAgentRunner
{
    private readonly IGitOperationService _gitService;
    private readonly IOptions<CopilotOptions> _options;
    private readonly ILogger<CoordinatorAgentRunner> _logger;

    /// <summary>
    /// 初始化 <see cref="CoordinatorAgentRunner"/>
    /// </summary>
    public CoordinatorAgentRunner(
        IGitOperationService gitService,
        IOptions<CopilotOptions> options,
        ILogger<CoordinatorAgentRunner> logger)
    {
        _gitService = gitService;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// 執行 Coordinator Agent，回傳分配結果
    /// </summary>
    public async Task<CoordinatorAssignment> RunAsync(
        IReadOnlyList<CommitSummary> commitSummaries,
        string localPath,
        IReadOnlyList<RiskScenario> scenarios,
        CopilotClientOptions clientOptions,
        CancellationToken ct = default)
    {
        var listFiles = AIFunctionFactory.Create(
            ([Description("目錄的相對路徑")] string path) =>
            {
                var fullPath = Path.Combine(localPath, path ?? string.Empty);
                if (!Directory.Exists(fullPath))
                    return $"目錄不存在: {path}";
                var entries = Directory.GetFileSystemEntries(fullPath)
                    .Select(e => Path.GetRelativePath(localPath, e))
                    .OrderBy(e => e);
                return string.Join("\n", entries);
            },
            "list_files",
            "列出指定目錄的檔案與子目錄");

        await using var client = new CopilotClient(clientOptions);
        var config = new SessionConfig
        {
            Model = _options.Value.Model,
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Replace,
                Content = ScenarioPromptBuilder.BuildCoordinatorSystemPrompt()
            },
            Tools = [listFiles],
            InfiniteSessions = new InfiniteSessionConfig { Enabled = false },
            OnPermissionRequest = PermissionHandler.ApproveAll
        };

        await using var session = await client.CreateSessionAsync(config);
        var userPrompt = ScenarioPromptBuilder.BuildCoordinatorUserPrompt("project", commitSummaries);
        var timeout = TimeSpan.FromSeconds(_options.Value.TimeoutSeconds);

        var response = await session.SendAndWaitAsync(
            new MessageOptions { Prompt = userPrompt }, timeout: timeout);

        var content = response?.Data?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("Coordinator Agent 回傳空白，使用 fallback 分配所有 commit");
            return BuildFallbackAssignment(
                commitSummaries.Select(c => c.CommitSha).ToList(), scenarios);
        }

        var assignment = ParseAssignment(content);
        if (assignment == null)
        {
            _logger.LogWarning("Coordinator Agent 回傳格式錯誤，使用 fallback: {Content}", content);
            return BuildFallbackAssignment(
                commitSummaries.Select(c => c.CommitSha).ToList(), scenarios);
        }

        _logger.LogInformation("Coordinator 分配完成: {Reasoning}", assignment.Reasoning);
        return assignment;
    }

    /// <summary>
    /// 解析 Coordinator 回傳的 JSON 為 CoordinatorAssignment
    /// </summary>
    internal static CoordinatorAssignment? ParseAssignment(string response)
    {
        var json = ExtractJsonBlock(response);
        if (string.IsNullOrWhiteSpace(json))
        {
            // 嘗試直接當 JSON 解析
            json = response.Trim();
        }

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var dto = JsonSerializer.Deserialize<CoordinatorAssignmentDto>(json, options);
            if (dto?.Assignments == null) return null;

            var assignments = new Dictionary<RiskScenario, List<string>>();
            foreach (var (key, value) in dto.Assignments)
            {
                if (Enum.TryParse<RiskScenario>(key, ignoreCase: true, out var scenario))
                {
                    assignments[scenario] = value ?? [];
                }
            }

            return new CoordinatorAssignment
            {
                Assignments = assignments,
                Reasoning = dto.Reasoning ?? string.Empty
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Fallback：將所有 commit 分配給每個情境（不篩選）
    /// </summary>
    internal static CoordinatorAssignment BuildFallbackAssignment(
        IReadOnlyList<string> commitShas,
        IReadOnlyList<RiskScenario> scenarios)
    {
        var assignments = scenarios.ToDictionary(
            s => s,
            _ => commitShas.ToList());

        return new CoordinatorAssignment
        {
            Assignments = assignments,
            Reasoning = "Fallback: Coordinator 回應解析失敗，全部 commit 分配給所有情境"
        };
    }

    private static string ExtractJsonBlock(string response)
    {
        var match = Regex.Match(response, @"```(?:json)?\s*\n?(.*?)\n?```",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private sealed record CoordinatorAssignmentDto
    {
        public Dictionary<string, List<string>>? Assignments { get; init; }
        public string? Reasoning { get; init; }
    }
}
```

- [ ] **Step 4: 執行測試確認通過**

Run: `dotnet test tests/ReleaseKit.Infrastructure.Tests --filter "FullyQualifiedName~CoordinatorAgentRunnerTests" -v q`
Expected: All tests passed

- [ ] **Step 5: Commit**

```bash
git add src/ReleaseKit.Infrastructure/Copilot/ScenarioAnalysis/CoordinatorAgentRunner.cs tests/ReleaseKit.Infrastructure.Tests/Copilot/ScenarioAnalysis/CoordinatorAgentRunnerTests.cs
git commit -m "feat(infra): 實作 CoordinatorAgentRunner 任務分配"
```

---

### Task 7: ExpertAgentRunner

**Files:**
- Create: `src/ReleaseKit.Infrastructure/Copilot/ScenarioAnalysis/ExpertAgentRunner.cs`
- Create: `tests/ReleaseKit.Infrastructure.Tests/Copilot/ScenarioAnalysis/ExpertAgentRunnerTests.cs`

- [ ] **Step 1: 撰寫失敗的測試**

```csharp
// tests/ReleaseKit.Infrastructure.Tests/Copilot/ScenarioAnalysis/ExpertAgentRunnerTests.cs
using FluentAssertions;
using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis;

namespace ReleaseKit.Infrastructure.Tests.Copilot.ScenarioAnalysis;

/// <summary>
/// ExpertAgentRunner.ParseFindings 單元測試
/// </summary>
public class ExpertAgentRunnerTests
{
    [Fact]
    public void ParseFindings_有效JSON_應正確解析()
    {
        var json = """
            ```json
            [
              {
                "Scenario": "ApiContractBreak",
                "RiskLevel": "High",
                "Description": "UserController 新增必填參數",
                "AffectedFile": "Controllers/UserController.cs",
                "DiffSnippet": "- GetUser(int id)\n+ GetUser(int id, string tenantId)",
                "PotentiallyAffectedProjects": ["team-b/portal"],
                "RecommendedAction": "通知 portal 團隊"
              }
            ]
            ```
            """;

        var result = ExpertAgentRunner.ParseFindings(json, RiskScenario.ApiContractBreak);

        result.Should().HaveCount(1);
        result[0].Scenario.Should().Be(RiskScenario.ApiContractBreak);
        result[0].RiskLevel.Should().Be(RiskLevel.High);
        result[0].AffectedFile.Should().Be("Controllers/UserController.cs");
    }

    [Fact]
    public void ParseFindings_空陣列_應回傳空清單()
    {
        var json = """
            ```json
            []
            ```
            """;

        var result = ExpertAgentRunner.ParseFindings(json, RiskScenario.DatabaseSchemaChange);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseFindings_無效JSON_應回傳空清單()
    {
        var invalid = "這不是有效的 JSON 回應";

        var result = ExpertAgentRunner.ParseFindings(invalid, RiskScenario.ApiContractBreak);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseFindings_無代碼塊的純JSON_應正確解析()
    {
        var json = """
            [
              {
                "Scenario": "ConfigEnvChange",
                "RiskLevel": "Medium",
                "Description": "新增必填 Redis key",
                "AffectedFile": "appsettings.json",
                "DiffSnippet": "+ \"NewKey\": \"\"",
                "PotentiallyAffectedProjects": [],
                "RecommendedAction": "確認所有環境已設定"
              }
            ]
            """;

        var result = ExpertAgentRunner.ParseFindings(json, RiskScenario.ConfigEnvChange);

        result.Should().HaveCount(1);
        result[0].Scenario.Should().Be(RiskScenario.ConfigEnvChange);
    }
}
```

- [ ] **Step 2: 執行測試確認失敗**

Run: `dotnet test tests/ReleaseKit.Infrastructure.Tests --filter "FullyQualifiedName~ExpertAgentRunnerTests" --no-build -v q`
Expected: 編譯失敗

- [ ] **Step 3: 實作 ExpertAgentRunner**

```csharp
// src/ReleaseKit.Infrastructure/Copilot/ScenarioAnalysis/ExpertAgentRunner.cs
using System.Text.Json;
using System.Text.RegularExpressions;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis.Models;

namespace ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis;

/// <summary>
/// Expert Agent 執行器：依情境切換 prompt，使用工具集自主分析
/// </summary>
public class ExpertAgentRunner
{
    private readonly ExpertToolFactory _toolFactory;
    private readonly IOptions<CopilotOptions> _options;
    private readonly ILogger<ExpertAgentRunner> _logger;

    /// <summary>
    /// 初始化 <see cref="ExpertAgentRunner"/>
    /// </summary>
    public ExpertAgentRunner(
        ExpertToolFactory toolFactory,
        IOptions<CopilotOptions> options,
        ILogger<ExpertAgentRunner> logger)
    {
        _toolFactory = toolFactory;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// 執行 Expert Agent，回傳該情境的分析結果
    /// </summary>
    public async Task<ExpertFindings> RunAsync(
        RiskScenario scenario,
        IReadOnlyList<string> commitShas,
        string localPath,
        string projectPath,
        CopilotClientOptions clientOptions,
        CancellationToken ct = default)
    {
        if (commitShas.Count == 0)
        {
            return new ExpertFindings
            {
                Scenario = scenario,
                Findings = []
            };
        }

        var tools = _toolFactory.CreateTools(localPath, ct);

        await using var client = new CopilotClient(clientOptions);
        var config = new SessionConfig
        {
            Model = _options.Value.Model,
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Replace,
                Content = ScenarioPromptBuilder.BuildExpertSystemPrompt(scenario)
            },
            Tools = tools.ToList(),
            InfiniteSessions = new InfiniteSessionConfig { Enabled = false },
            OnPermissionRequest = PermissionHandler.ApproveAll
        };

        await using var session = await client.CreateSessionAsync(config);
        var userPrompt = ScenarioPromptBuilder.BuildExpertUserPrompt(projectPath, commitShas, scenario);
        var timeout = TimeSpan.FromSeconds(_options.Value.TimeoutSeconds);

        var response = await session.SendAndWaitAsync(
            new MessageOptions { Prompt = userPrompt }, timeout: timeout);

        var content = response?.Data?.Content;

        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("{Scenario} Expert 回傳空白，重試一次", scenario);
            response = await session.SendAndWaitAsync(
                new MessageOptions { Prompt = "請重新分析並以 JSON 格式回傳結果。" },
                timeout: timeout);
            content = response?.Data?.Content;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("{Scenario} Expert 重試後仍為空白", scenario);
            return new ExpertFindings
            {
                Scenario = scenario,
                Findings = [],
                Failed = true,
                FailureReason = "Agent 回應為空"
            };
        }

        var findings = ParseFindings(content, scenario);

        _logger.LogInformation("{Scenario} Expert 完成: {FindingCount} 個風險, {CommitCount} 個 commit",
            scenario, findings.Count, commitShas.Count);

        return new ExpertFindings
        {
            Scenario = scenario,
            Findings = findings
        };
    }

    /// <summary>
    /// 解析 Expert 回傳的 JSON 為 RiskFinding 清單
    /// </summary>
    internal static List<RiskFinding> ParseFindings(string response, RiskScenario expectedScenario)
    {
        var json = ExtractJsonBlock(response);
        if (string.IsNullOrWhiteSpace(json))
        {
            // 嘗試直接當 JSON 解析
            json = response.Trim();
        }

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var items = JsonSerializer.Deserialize<List<RiskFindingDto>>(json, options);
            if (items == null) return [];

            return items.Select(dto => new RiskFinding
            {
                Scenario = Enum.TryParse<RiskScenario>(dto.Scenario, ignoreCase: true, out var s) ? s : expectedScenario,
                RiskLevel = Enum.TryParse<RiskLevel>(dto.RiskLevel, ignoreCase: true, out var r) ? r : RiskLevel.Medium,
                Description = dto.Description ?? string.Empty,
                AffectedFile = dto.AffectedFile ?? string.Empty,
                DiffSnippet = dto.DiffSnippet ?? string.Empty,
                PotentiallyAffectedProjects = dto.PotentiallyAffectedProjects ?? [],
                RecommendedAction = dto.RecommendedAction ?? string.Empty,
                ChangedBy = string.Empty
            }).ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string ExtractJsonBlock(string response)
    {
        var match = Regex.Match(response, @"```(?:json)?\s*\n?(.*?)\n?```",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private sealed record RiskFindingDto
    {
        public string? Scenario { get; init; }
        public string? RiskLevel { get; init; }
        public string? Description { get; init; }
        public string? AffectedFile { get; init; }
        public string? DiffSnippet { get; init; }
        public List<string>? PotentiallyAffectedProjects { get; init; }
        public string? RecommendedAction { get; init; }
    }
}
```

- [ ] **Step 4: 執行測試確認通過**

Run: `dotnet test tests/ReleaseKit.Infrastructure.Tests --filter "FullyQualifiedName~ExpertAgentRunnerTests" -v q`
Expected: All tests passed

- [ ] **Step 5: Commit**

```bash
git add src/ReleaseKit.Infrastructure/Copilot/ScenarioAnalysis/ExpertAgentRunner.cs tests/ReleaseKit.Infrastructure.Tests/Copilot/ScenarioAnalysis/ExpertAgentRunnerTests.cs
git commit -m "feat(infra): 實作 ExpertAgentRunner 情境專家分析"
```

---

### Task 8: SynthesisAgentRunner

**Files:**
- Create: `src/ReleaseKit.Infrastructure/Copilot/ScenarioAnalysis/SynthesisAgentRunner.cs`
- Create: `tests/ReleaseKit.Infrastructure.Tests/Copilot/ScenarioAnalysis/SynthesisAgentRunnerTests.cs`

- [ ] **Step 1: 撰寫失敗的測試**

```csharp
// tests/ReleaseKit.Infrastructure.Tests/Copilot/ScenarioAnalysis/SynthesisAgentRunnerTests.cs
using FluentAssertions;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis;

namespace ReleaseKit.Infrastructure.Tests.Copilot.ScenarioAnalysis;

/// <summary>
/// SynthesisAgentRunner.ParseSynthesisFindings 單元測試
/// </summary>
public class SynthesisAgentRunnerTests
{
    [Fact]
    public void ParseSynthesisFindings_有效JSON含CompositeRisk_應正確解析()
    {
        var json = """
            ```json
            [
              {
                "Scenario": "ApiContractBreak",
                "RiskLevel": "High",
                "Description": "綜合判斷：API + DB 同時變更",
                "AffectedFile": "Controllers/UserController.cs",
                "DiffSnippet": "diff snippet",
                "PotentiallyAffectedProjects": ["team-b/portal"],
                "RecommendedAction": "通知團隊",
                "CompositeRisk": "與 DatabaseSchemaChange 相關"
              }
            ]
            ```
            """;

        var result = SynthesisAgentRunner.ParseSynthesisFindings(json);

        result.Should().HaveCount(1);
        result[0].Scenario.Should().Be(RiskScenario.ApiContractBreak);
        result[0].RiskLevel.Should().Be(RiskLevel.High);
    }

    [Fact]
    public void ParseSynthesisFindings_空陣列_應回傳空清單()
    {
        var json = "[]";
        var result = SynthesisAgentRunner.ParseSynthesisFindings(json);
        result.Should().BeEmpty();
    }

    [Fact]
    public void MergeExpertFindings_當Synthesis失敗_應回傳所有Expert結果合併()
    {
        var expertResults = new Dictionary<RiskScenario, ExpertFindings>
        {
            [RiskScenario.ApiContractBreak] = new ExpertFindings
            {
                Scenario = RiskScenario.ApiContractBreak,
                Findings = new List<RiskFinding>
                {
                    new()
                    {
                        Scenario = RiskScenario.ApiContractBreak,
                        RiskLevel = RiskLevel.High,
                        Description = "API risk",
                        AffectedFile = "file.cs",
                        DiffSnippet = "",
                        PotentiallyAffectedProjects = [],
                        RecommendedAction = "",
                        ChangedBy = ""
                    }
                }
            },
            [RiskScenario.DatabaseSchemaChange] = new ExpertFindings
            {
                Scenario = RiskScenario.DatabaseSchemaChange,
                Findings = new List<RiskFinding>
                {
                    new()
                    {
                        Scenario = RiskScenario.DatabaseSchemaChange,
                        RiskLevel = RiskLevel.Medium,
                        Description = "DB risk",
                        AffectedFile = "entity.cs",
                        DiffSnippet = "",
                        PotentiallyAffectedProjects = [],
                        RecommendedAction = "",
                        ChangedBy = ""
                    }
                }
            }
        };

        var result = SynthesisAgentRunner.MergeExpertFindings(expertResults);

        result.Should().HaveCount(2);
    }
}
```

- [ ] **Step 2: 執行測試確認失敗**

Run: `dotnet test tests/ReleaseKit.Infrastructure.Tests --filter "FullyQualifiedName~SynthesisAgentRunnerTests" --no-build -v q`
Expected: 編譯失敗

- [ ] **Step 3: 實作 SynthesisAgentRunner**

```csharp
// src/ReleaseKit.Infrastructure/Copilot/ScenarioAnalysis/SynthesisAgentRunner.cs
using System.Text.Json;
using System.Text.RegularExpressions;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis.Models;

namespace ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis;

/// <summary>
/// Synthesis Agent 執行器：匯總各 Expert 結果，去重、識別複合風險
/// </summary>
public class SynthesisAgentRunner
{
    private readonly IOptions<CopilotOptions> _options;
    private readonly ILogger<SynthesisAgentRunner> _logger;

    /// <summary>
    /// 初始化 <see cref="SynthesisAgentRunner"/>
    /// </summary>
    public SynthesisAgentRunner(
        IOptions<CopilotOptions> options,
        ILogger<SynthesisAgentRunner> logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// 執行 Synthesis Agent，回傳最終合併結果
    /// </summary>
    public async Task<IReadOnlyList<RiskFinding>> RunAsync(
        SynthesisInput input,
        CopilotClientOptions clientOptions,
        CancellationToken ct = default)
    {
        // 若所有 Expert 都無風險發現，直接回傳空
        var totalFindings = input.ExpertResults.Values
            .Where(e => !e.Failed)
            .Sum(e => e.Findings.Count);

        if (totalFindings == 0)
        {
            _logger.LogInformation("所有 Expert 均無風險發現，跳過 Synthesis");
            return [];
        }

        await using var client = new CopilotClient(clientOptions);
        var config = new SessionConfig
        {
            Model = _options.Value.Model,
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Replace,
                Content = ScenarioPromptBuilder.BuildSynthesisSystemPrompt()
            },
            Tools = [],
            InfiniteSessions = new InfiniteSessionConfig { Enabled = false },
            OnPermissionRequest = PermissionHandler.ApproveAll
        };

        await using var session = await client.CreateSessionAsync(config);
        var userPrompt = ScenarioPromptBuilder.BuildSynthesisUserPrompt(input);
        var timeout = TimeSpan.FromSeconds(_options.Value.TimeoutSeconds);

        var response = await session.SendAndWaitAsync(
            new MessageOptions { Prompt = userPrompt }, timeout: timeout);

        var content = response?.Data?.Content;

        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("Synthesis Agent 回傳空白，直接合併 Expert 結果");
            return MergeExpertFindings(input.ExpertResults);
        }

        var findings = ParseSynthesisFindings(content);
        if (findings.Count == 0 && totalFindings > 0)
        {
            _logger.LogWarning("Synthesis 解析結果為空但 Expert 有發現，使用 fallback 合併");
            return MergeExpertFindings(input.ExpertResults);
        }

        _logger.LogInformation("Synthesis 完成: {FindingCount} 個最終風險", findings.Count);
        return findings;
    }

    /// <summary>
    /// 解析 Synthesis 回傳的 JSON（格式同 Expert，可能含 CompositeRisk）
    /// </summary>
    internal static List<RiskFinding> ParseSynthesisFindings(string response)
    {
        var json = ExtractJsonBlock(response);
        if (string.IsNullOrWhiteSpace(json))
            json = response.Trim();

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var items = JsonSerializer.Deserialize<List<SynthesisFindingDto>>(json, options);
            if (items == null) return [];

            return items.Select(dto => new RiskFinding
            {
                Scenario = Enum.TryParse<RiskScenario>(dto.Scenario, ignoreCase: true, out var s) ? s : RiskScenario.ApiContractBreak,
                RiskLevel = Enum.TryParse<RiskLevel>(dto.RiskLevel, ignoreCase: true, out var r) ? r : RiskLevel.Medium,
                Description = BuildDescription(dto.Description, dto.CompositeRisk),
                AffectedFile = dto.AffectedFile ?? string.Empty,
                DiffSnippet = dto.DiffSnippet ?? string.Empty,
                PotentiallyAffectedProjects = dto.PotentiallyAffectedProjects ?? [],
                RecommendedAction = dto.RecommendedAction ?? string.Empty,
                ChangedBy = string.Empty
            }).ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// Fallback：直接合併所有 Expert 的 findings
    /// </summary>
    internal static List<RiskFinding> MergeExpertFindings(
        IReadOnlyDictionary<RiskScenario, ExpertFindings> expertResults)
    {
        return expertResults.Values
            .Where(e => !e.Failed)
            .SelectMany(e => e.Findings)
            .ToList();
    }

    private static string BuildDescription(string? description, string? compositeRisk)
    {
        if (string.IsNullOrEmpty(compositeRisk))
            return description ?? string.Empty;
        return $"{description} [複合風險: {compositeRisk}]";
    }

    private static string ExtractJsonBlock(string response)
    {
        var match = Regex.Match(response, @"```(?:json)?\s*\n?(.*?)\n?```",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private sealed record SynthesisFindingDto
    {
        public string? Scenario { get; init; }
        public string? RiskLevel { get; init; }
        public string? Description { get; init; }
        public string? AffectedFile { get; init; }
        public string? DiffSnippet { get; init; }
        public List<string>? PotentiallyAffectedProjects { get; init; }
        public string? RecommendedAction { get; init; }
        public string? CompositeRisk { get; init; }
    }
}
```

- [ ] **Step 4: 執行測試確認通過**

Run: `dotnet test tests/ReleaseKit.Infrastructure.Tests --filter "FullyQualifiedName~SynthesisAgentRunnerTests" -v q`
Expected: All tests passed

- [ ] **Step 5: Commit**

```bash
git add src/ReleaseKit.Infrastructure/Copilot/ScenarioAnalysis/SynthesisAgentRunner.cs tests/ReleaseKit.Infrastructure.Tests/Copilot/ScenarioAnalysis/SynthesisAgentRunnerTests.cs
git commit -m "feat(infra): 實作 SynthesisAgentRunner 綜合判斷"
```

---

### Task 9: CopilotScenarioDispatcher（主協調器）

**Files:**
- Create: `src/ReleaseKit.Infrastructure/Copilot/ScenarioAnalysis/CopilotScenarioDispatcher.cs`
- Create: `tests/ReleaseKit.Infrastructure.Tests/Copilot/ScenarioAnalysis/CopilotScenarioDispatcherTests.cs`

- [ ] **Step 1: 撰寫失敗的測試**

```csharp
// tests/ReleaseKit.Infrastructure.Tests/Copilot/ScenarioAnalysis/CopilotScenarioDispatcherTests.cs
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis;
using ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis.Models;

namespace ReleaseKit.Infrastructure.Tests.Copilot.ScenarioAnalysis;

/// <summary>
/// CopilotScenarioDispatcher 單元測試（驗證協調邏輯，mock Agent Runners）
/// </summary>
public class CopilotScenarioDispatcherTests
{
    private readonly Mock<CoordinatorAgentRunner> _coordinatorMock;
    private readonly Mock<ExpertAgentRunner> _expertMock;
    private readonly Mock<SynthesisAgentRunner> _synthesisMock;

    public CopilotScenarioDispatcherTests()
    {
        // 注意：這些 mock 需要 Runner 類別是 virtual 或透過介面
        // 由於 Runner 直接依賴 Copilot SDK，測試以整合層級驗證邏輯
        _coordinatorMock = new Mock<CoordinatorAgentRunner>(
            MockBehavior.Loose, null!, null!, null!);
        _expertMock = new Mock<ExpertAgentRunner>(
            MockBehavior.Loose, null!, null!, null!);
        _synthesisMock = new Mock<SynthesisAgentRunner>(
            MockBehavior.Loose, null!, null!);
    }

    [Fact]
    public void BuildClientOptions_有Token_應設定Token()
    {
        var options = Options.Create(new CopilotOptions
        {
            GitHubToken = "test-token",
            Model = "gpt-4.1",
            TimeoutSeconds = 300
        });

        var dispatcher = new CopilotScenarioDispatcher(
            Mock.Of<CoordinatorAgentRunner>(),
            Mock.Of<ExpertAgentRunner>(),
            Mock.Of<SynthesisAgentRunner>(),
            Mock.Of<ILogger<CopilotScenarioDispatcher>>(),
            options);

        // 驗證 BuildClientOptions 不拋出例外
        var clientOptions = dispatcher.BuildClientOptions();
        clientOptions.GitHubToken.Should().Be("test-token");
    }

    [Fact]
    public void BuildClientOptions_無Token_不應拋出()
    {
        var options = Options.Create(new CopilotOptions
        {
            GitHubToken = "",
            Model = "gpt-4.1"
        });

        var dispatcher = new CopilotScenarioDispatcher(
            Mock.Of<CoordinatorAgentRunner>(),
            Mock.Of<ExpertAgentRunner>(),
            Mock.Of<SynthesisAgentRunner>(),
            Mock.Of<ILogger<CopilotScenarioDispatcher>>(),
            options);

        var clientOptions = dispatcher.BuildClientOptions();
        clientOptions.Should().NotBeNull();
    }
}
```

- [ ] **Step 2: 執行測試確認失敗**

Run: `dotnet test tests/ReleaseKit.Infrastructure.Tests --filter "FullyQualifiedName~CopilotScenarioDispatcherTests" --no-build -v q`
Expected: 編譯失敗

- [ ] **Step 3: 實作 CopilotScenarioDispatcher**

```csharp
// src/ReleaseKit.Infrastructure/Copilot/ScenarioAnalysis/CopilotScenarioDispatcher.cs
using GitHub.Copilot.SDK;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis.Models;

namespace ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis;

/// <summary>
/// 情境專家型 Copilot 風險分析主協調器
/// </summary>
/// <remarks>
/// 協調三層 Agent Pipeline：Coordinator → Expert × N → Synthesis
/// </remarks>
public class CopilotScenarioDispatcher : ICopilotScenarioDispatcher
{
    private readonly CoordinatorAgentRunner _coordinatorRunner;
    private readonly ExpertAgentRunner _expertRunner;
    private readonly SynthesisAgentRunner _synthesisRunner;
    private readonly ILogger<CopilotScenarioDispatcher> _logger;
    private readonly IOptions<CopilotOptions> _options;

    /// <summary>
    /// 初始化 <see cref="CopilotScenarioDispatcher"/>
    /// </summary>
    public CopilotScenarioDispatcher(
        CoordinatorAgentRunner coordinatorRunner,
        ExpertAgentRunner expertRunner,
        SynthesisAgentRunner synthesisRunner,
        ILogger<CopilotScenarioDispatcher> logger,
        IOptions<CopilotOptions> options)
    {
        _coordinatorRunner = coordinatorRunner;
        _expertRunner = expertRunner;
        _synthesisRunner = synthesisRunner;
        _logger = logger;
        _options = options;
    }

    /// <inheritdoc />
    public async Task<ProjectRiskAnalysis> DispatchAsync(
        string runId,
        string projectPath,
        IReadOnlyList<CommitSummary> commitSummaries,
        string localPath,
        IReadOnlyList<RiskScenario> scenarios,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "開始情境專家分析: {ProjectPath}, {CommitCount} 個 commit, {ScenarioCount} 個情境",
            projectPath, commitSummaries.Count, scenarios.Count);

        var clientOptions = BuildClientOptions();

        // 1. Coordinator 分配
        var assignment = await _coordinatorRunner.RunAsync(
            commitSummaries, localPath, scenarios, clientOptions, ct);

        // 2. Expert 平行分析
        var expertTasks = scenarios.Select(scenario =>
            _expertRunner.RunAsync(scenario, assignment.GetShas(scenario), localPath, projectPath, clientOptions, ct));
        var expertResults = await Task.WhenAll(expertTasks);

        var expertDict = expertResults.ToDictionary(e => e.Scenario);

        // 3. Synthesis 匯總
        var synthesisInput = new SynthesisInput
        {
            ProjectPath = projectPath,
            ExpertResults = expertDict
        };

        var finalFindings = await _synthesisRunner.RunAsync(synthesisInput, clientOptions, ct);

        var sessionCount = 1 + scenarios.Count + 1; // Coordinator + Experts + Synthesis

        _logger.LogInformation(
            "情境專家分析完成: {ProjectPath}, {FindingCount} 個風險, {SessionCount} 個 session",
            projectPath, finalFindings.Count, sessionCount);

        return new ProjectRiskAnalysis
        {
            ProjectPath = projectPath,
            Findings = finalFindings.ToList(),
            SessionCount = sessionCount
        };
    }

    /// <summary>
    /// 建立 CopilotClient 選項
    /// </summary>
    internal CopilotClientOptions BuildClientOptions()
    {
        var clientOptions = new CopilotClientOptions { AutoStart = true };
        if (!string.IsNullOrWhiteSpace(_options.Value.GitHubToken))
            clientOptions.GitHubToken = _options.Value.GitHubToken;
        return clientOptions;
    }
}
```

- [ ] **Step 4: 執行測試確認通過**

Run: `dotnet test tests/ReleaseKit.Infrastructure.Tests --filter "FullyQualifiedName~CopilotScenarioDispatcherTests" -v q`
Expected: All tests passed

- [ ] **Step 5: Commit**

```bash
git add src/ReleaseKit.Infrastructure/Copilot/ScenarioAnalysis/CopilotScenarioDispatcher.cs tests/ReleaseKit.Infrastructure.Tests/Copilot/ScenarioAnalysis/CopilotScenarioDispatcherTests.cs
git commit -m "feat(infra): 實作 CopilotScenarioDispatcher 三層 Agent 協調"
```

---

### Task 10: CopilotScenarioAnalysisTask + TaskType + TaskFactory + DI

**Files:**
- Create: `src/ReleaseKit.Application/Tasks/CopilotScenarioAnalysisTask.cs`
- Modify: `src/ReleaseKit.Application/Tasks/TaskType.cs`
- Modify: `src/ReleaseKit.Application/Tasks/TaskFactory.cs`
- Modify: `src/ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs`
- Create: `tests/ReleaseKit.Application.Tests/Tasks/CopilotScenarioAnalysisTaskTests.cs`

- [ ] **Step 1: 撰寫失敗的測試**

```csharp
// tests/ReleaseKit.Application.Tests/Tasks/CopilotScenarioAnalysisTaskTests.cs
using FluentAssertions;
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
/// CopilotScenarioAnalysisTask 單元測試
/// </summary>
public class CopilotScenarioAnalysisTaskTests
{
    private readonly Mock<ICopilotScenarioDispatcher> _dispatcherMock;
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly Mock<ILogger<CopilotScenarioAnalysisTask>> _loggerMock;

    private const string RunId = "20240315103045";
    private const string ProjectPath = "group/project-a";
    private const string LocalPath = "/repos/group/project-a";

    public CopilotScenarioAnalysisTaskTests()
    {
        _dispatcherMock = new Mock<ICopilotScenarioDispatcher>();
        _redisServiceMock = new Mock<IRedisService>();
        _loggerMock = new Mock<ILogger<CopilotScenarioAnalysisTask>>();

        _redisServiceMock
            .Setup(x => x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
    }

    private CopilotScenarioAnalysisTask CreateTask(RiskAnalysisOptions? options = null)
    {
        var effectiveOptions = options ?? new RiskAnalysisOptions();
        return new CopilotScenarioAnalysisTask(
            _dispatcherMock.Object,
            _redisServiceMock.Object,
            Options.Create(effectiveOptions),
            _loggerMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_無RunId_應跳過不執行()
    {
        _redisServiceMock.Setup(x => x.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey))
            .ReturnsAsync((string?)null);

        var task = CreateTask();
        await task.ExecuteAsync();

        _dispatcherMock.Verify(
            x => x.DispatchAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyList<CommitSummary>>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyList<RiskScenario>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_有Stage2資料_應呼叫Dispatcher()
    {
        _redisServiceMock.Setup(x => x.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey))
            .ReturnsAsync(RunId);

        var diffResult = new ProjectDiffResult
        {
            ProjectPath = ProjectPath,
            CommitSummaries = new List<CommitSummary>
            {
                new()
                {
                    CommitSha = "abc123",
                    ChangedFiles = new List<FileDiff>(),
                    TotalFilesChanged = 3,
                    TotalLinesAdded = 50,
                    TotalLinesRemoved = 10
                }
            }
        };

        _redisServiceMock.Setup(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage2Hash(RunId)))
            .ReturnsAsync(new Dictionary<string, string> { [ProjectPath] = diffResult.ToJson() });

        var cloneResult = new { LocalPath = LocalPath, Status = "Success" };
        _redisServiceMock.Setup(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage1Hash(RunId)))
            .ReturnsAsync(new Dictionary<string, string> { [ProjectPath] = cloneResult.ToJson() });

        _dispatcherMock
            .Setup(x => x.DispatchAsync(RunId, ProjectPath,
                It.IsAny<IReadOnlyList<CommitSummary>>(), LocalPath,
                It.IsAny<IReadOnlyList<RiskScenario>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProjectRiskAnalysis
            {
                ProjectPath = ProjectPath,
                Findings = [],
                SessionCount = 7
            });

        var task = CreateTask();
        await task.ExecuteAsync();

        _dispatcherMock.Verify(
            x => x.DispatchAsync(RunId, ProjectPath,
                It.IsAny<IReadOnlyList<CommitSummary>>(), LocalPath,
                It.IsAny<IReadOnlyList<RiskScenario>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Stage2無CommitSummary_應跳過()
    {
        _redisServiceMock.Setup(x => x.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey))
            .ReturnsAsync(RunId);

        var emptyDiffResult = new ProjectDiffResult
        {
            ProjectPath = ProjectPath,
            CommitSummaries = new List<CommitSummary>()
        };

        _redisServiceMock.Setup(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage2Hash(RunId)))
            .ReturnsAsync(new Dictionary<string, string> { [ProjectPath] = emptyDiffResult.ToJson() });

        _redisServiceMock.Setup(x => x.HashGetAllAsync(RiskAnalysisRedisKeys.Stage1Hash(RunId)))
            .ReturnsAsync(new Dictionary<string, string>());

        var task = CreateTask();
        await task.ExecuteAsync();

        _dispatcherMock.Verify(
            x => x.DispatchAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyList<CommitSummary>>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyList<RiskScenario>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
```

- [ ] **Step 2: 執行測試確認失敗**

Run: `dotnet test tests/ReleaseKit.Application.Tests --filter "FullyQualifiedName~CopilotScenarioAnalysisTaskTests" --no-build -v q`
Expected: 編譯失敗

- [ ] **Step 3: 實作 CopilotScenarioAnalysisTask**

```csharp
// src/ReleaseKit.Application/Tasks/CopilotScenarioAnalysisTask.cs
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// Stage 4 替代方案：情境專家型 Copilot 風險分析
/// </summary>
/// <remarks>
/// 使用三層 Agent Pipeline（Coordinator → Expert × 5 → Synthesis）對每個專案進行風險分析。
/// 與 <see cref="CopilotRiskAnalysisTask"/> 共存，使用者依場景選用。
/// </remarks>
public class CopilotScenarioAnalysisTask : ITask
{
    private readonly ICopilotScenarioDispatcher _dispatcher;
    private readonly IRedisService _redisService;
    private readonly IOptions<RiskAnalysisOptions> _riskOptions;
    private readonly ILogger<CopilotScenarioAnalysisTask> _logger;

    /// <summary>
    /// 初始化 <see cref="CopilotScenarioAnalysisTask"/>
    /// </summary>
    public CopilotScenarioAnalysisTask(
        ICopilotScenarioDispatcher dispatcher,
        IRedisService redisService,
        IOptions<RiskAnalysisOptions> riskOptions,
        ILogger<CopilotScenarioAnalysisTask> logger)
    {
        _dispatcher = dispatcher;
        _redisService = redisService;
        _riskOptions = riskOptions;
        _logger = logger;
    }

    /// <summary>
    /// 執行情境專家型風險分析
    /// </summary>
    public async Task ExecuteAsync()
    {
        var runId = await _redisService.GetAsync(RiskAnalysisRedisKeys.CurrentRunIdKey);
        if (string.IsNullOrEmpty(runId))
        {
            _logger.LogWarning("找不到 RunId，請先執行 Stage 1");
            return;
        }

        _logger.LogInformation("開始情境專家型風險分析, RunId={RunId}", runId);

        var stage1Data = await _redisService.HashGetAllAsync(RiskAnalysisRedisKeys.Stage1Hash(runId));
        var stage2Data = await _redisService.HashGetAllAsync(RiskAnalysisRedisKeys.Stage2Hash(runId));

        var scenarios = _riskOptions.Value.Scenarios
            .Select(s => Enum.Parse<RiskScenario>(s, ignoreCase: true))
            .ToList();

        foreach (var (projectPath, diffJson) in stage2Data)
        {
            var diffResult = diffJson.ToTypedObject<ProjectDiffResult>();
            if (diffResult == null || diffResult.CommitSummaries.Count == 0)
            {
                _logger.LogInformation("專案 {ProjectPath} 無 CommitSummary 資料，跳過", projectPath);
                continue;
            }

            if (!stage1Data.TryGetValue(projectPath, out var cloneJson))
            {
                _logger.LogWarning("專案 {ProjectPath} 無 Stage 1 clone 記錄，跳過", projectPath);
                continue;
            }

            var cloneResult = cloneJson.ToTypedObject<CloneStageResult>();
            if (cloneResult?.Status != "Success" || string.IsNullOrEmpty(cloneResult.LocalPath))
            {
                _logger.LogWarning("專案 {ProjectPath} clone 狀態非 Success，跳過", projectPath);
                continue;
            }

            _logger.LogInformation("開始分析專案 {ProjectPath}，共 {CommitCount} 個 commit",
                projectPath, diffResult.CommitSummaries.Count);

            var analysis = await _dispatcher.DispatchAsync(
                runId, projectPath, diffResult.CommitSummaries,
                cloneResult.LocalPath, scenarios);

            await _redisService.HashSetAsync(
                RiskAnalysisRedisKeys.Stage4ScenarioSynthesisHash(runId),
                projectPath,
                analysis.ToJson());
        }

        _logger.LogInformation("情境專家型風險分析完成, RunId={RunId}", runId);
    }

    private sealed record CloneStageResult
    {
        public string LocalPath { get; init; } = "";
        public string Status { get; init; } = "";
    }
}
```

- [ ] **Step 4: 新增 TaskType enum 值**

在 `TaskType.cs` 的 `GenerateRiskReport` 後面新增：

```csharp
/// <summary>
/// 情境專家型 Copilot 風險分析
/// </summary>
CopilotScenarioAnalysis
```

- [ ] **Step 5: 更新 TaskFactory**

在 `TaskFactory.cs` 的 switch 中 `GenerateRiskReport` case 後新增：

```csharp
TaskType.CopilotScenarioAnalysis => _serviceProvider.GetRequiredService<CopilotScenarioAnalysisTask>(),
```

- [ ] **Step 6: 更新 DI 註冊**

在 `ServiceCollectionExtensions.cs` 的「風險分析任務」區塊新增：

```csharp
services.AddTransient<ICopilotScenarioDispatcher, CopilotScenarioDispatcher>();
services.AddTransient<CoordinatorAgentRunner>();
services.AddTransient<ExpertAgentRunner>();
services.AddTransient<SynthesisAgentRunner>();
services.AddTransient<ExpertToolFactory>();
services.AddTransient<CopilotScenarioAnalysisTask>();
```

並新增 using：
```csharp
using ReleaseKit.Infrastructure.Copilot.ScenarioAnalysis;
```

- [ ] **Step 7: 執行測試確認通過**

Run: `dotnet test tests/ReleaseKit.Application.Tests --filter "FullyQualifiedName~CopilotScenarioAnalysisTaskTests" -v q`
Expected: All tests passed

- [ ] **Step 8: 執行全部測試確認無回歸**

Run: `dotnet test src/release-kit.sln -v q`
Expected: All tests passed

- [ ] **Step 9: Commit**

```bash
git add src/ReleaseKit.Application/Tasks/CopilotScenarioAnalysisTask.cs \
        src/ReleaseKit.Application/Tasks/TaskType.cs \
        src/ReleaseKit.Application/Tasks/TaskFactory.cs \
        src/ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs \
        tests/ReleaseKit.Application.Tests/Tasks/CopilotScenarioAnalysisTaskTests.cs
git commit -m "feat: 完成 CopilotScenarioAnalysisTask 整合（TaskType + DI + Tests）"
```

---

### Task 11: 最終建置與全量測試驗證

**Files:** 無新增，驗證性任務

- [ ] **Step 1: 完整建置**

Run: `dotnet build src/release-kit.sln -v q`
Expected: 建置成功，無 warning

- [ ] **Step 2: 全量單元測試**

Run: `dotnet test src/release-kit.sln -v q`
Expected: All tests passed

- [ ] **Step 3: 確認 CLI 命令可用**

Run: `dotnet run --project src/ReleaseKit.Console -- --task CopilotScenarioAnalysis --help 2>&1 || true`
Expected: 不會因為 TaskType 未知而拋出例外（可能因無 Redis 連線失敗，但 TaskType 解析應成功）
