# 跨專案 Release 風險分析 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 建立自動化的跨專案 Release 風險分析 Pipeline，從 clone repo → 收集 diff → 靜態分析 → Copilot AI 分析 → 交叉比對 → 產生 Markdown 報告。

**Architecture:** 沿用現有 `ITask` + `TaskFactory` 架構新增 6 個 Stage Task，中間結果存 Redis（以 `RiskAnalysis:{runId}:StageN:{projectPath}` 為 key），最終產出 Markdown 風險報告。每個 Stage 為獨立 CLI 任務，透過 `dotnet run -- <task-name>` 執行。

**Tech Stack:** .NET 10, GitHub Copilot SDK 0.1.32, Redis (StackExchange.Redis), Serilog, xUnit + Moq

**Spec:** `docs/superpowers/specs/2026-04-26-cross-project-risk-analysis-design.md`

---

## File Structure

### Domain Layer (`src/ReleaseKit.Domain/`)

| File | Responsibility |
|------|---------------|
| `Entities/FileDiff.cs` | 單一檔案的差異資訊 |
| `Entities/ProjectDiffResult.cs` | 單一專案的所有差異結果 |
| `Entities/ApiEndpoint.cs` | API 端點資訊 |
| `Entities/ServiceDependency.cs` | 推斷的服務相依性 |
| `Entities/ProjectStructure.cs` | 專案結構分析結果 |
| `Entities/RiskFinding.cs` | 單一風險發現 |
| `Entities/ProjectRiskAnalysis.cs` | 單一專案的風險分析結果 |
| `Entities/CrossProjectCorrelation.cs` | 跨專案交叉比對結果 |
| `Entities/RiskReport.cs` | 風險報告資料模型 |
| `Abstractions/IGitOperationService.cs` | Git clone/pull/diff 操作介面 |
| `ValueObjects/RiskLevel.cs` | 風險等級列舉 |
| `ValueObjects/RiskScenario.cs` | 風險情境列舉 |
| `ValueObjects/ChangeType.cs` | 檔案變更類型列舉 |
| `ValueObjects/DependencyType.cs` | 相依類型列舉 |
| `Common/Error.cs` | 擴充 Git 操作相關錯誤 (修改) |

### Application Layer (`src/ReleaseKit.Application/`)

| File | Responsibility |
|------|---------------|
| `Tasks/CloneRepositoriesTask.cs` | Stage 1: Clone/Pull 所有 repo |
| `Tasks/AnalyzePRDiffsTask.cs` | Stage 2: 取得 PR + commit diff |
| `Tasks/StaticProjectAnalysisTask.cs` | Stage 3: 靜態掃描專案結構 |
| `Tasks/CopilotRiskAnalysisTask.cs` | Stage 4: Copilot SDK 風險分析 |
| `Tasks/CrossProjectCorrelationTask.cs` | Stage 5: 跨專案交叉比對 |
| `Tasks/GenerateRiskReportTask.cs` | Stage 6: 產生 Markdown 報告 |
| `Tasks/TaskType.cs` | 擴充新任務類型 (修改) |
| `Tasks/TaskFactory.cs` | 註冊新任務 (修改) |

### Infrastructure Layer (`src/ReleaseKit.Infrastructure/`)

| File | Responsibility |
|------|---------------|
| `Git/GitOperationService.cs` | IGitOperationService 實作（shell 呼叫 git） |
| `Git/CloneUrlBuilder.cs` | 建構 GitLab/Bitbucket clone URL |
| `Copilot/CopilotRiskAnalyzer.cs` | Copilot SDK 風險分析封裝 |
| `Copilot/RiskAnalysisPromptBuilder.cs` | System/User Prompt 建構器 |
| `SourceControl/GitLab/Models/GitLabMergeRequestResponse.cs` | 新增 MergeCommitSha 欄位 (修改) |
| `SourceControl/GitLab/GitLabMergeRequestMapper.cs` | 映射 MergeCommitSha (修改) |
| `SourceControl/Bitbucket/Models/BitbucketPullRequestResponse.cs` | 新增 MergeCommit 欄位 (修改) |
| `SourceControl/Bitbucket/BitbucketPullRequestMapper.cs` | 映射 MergeCommitSha (修改) |
| `Analysis/ProjectStructureScanner.cs` | 靜態專案結構掃描器 |
| `Analysis/DependencyInferrer.cs` | 相依性推斷引擎 |
| `Reporting/MarkdownReportGenerator.cs` | Markdown 報告產生器 |

### Common Layer (`src/ReleaseKit.Common/`)

| File | Responsibility |
|------|---------------|
| `Configuration/RiskAnalysisOptions.cs` | 風險分析設定模型 |
| `Constants/RedisKeys.cs` | 新增 RiskAnalysis 相關 key (修改) |
| `Constants/RiskAnalysisRedisKeys.cs` | 風險分析專用 Redis key 建構 |

### Console Layer (`src/ReleaseKit.Console/`)

| File | Responsibility |
|------|---------------|
| `Parsers/CommandLineParser.cs` | 新增風險分析任務映射 (修改) |
| `Extensions/ServiceCollectionExtensions.cs` | 新增 DI 註冊 (修改) |

### Tests

| File | Responsibility |
|------|---------------|
| `tests/ReleaseKit.Domain.Tests/Entities/FileDiffTests.cs` | FileDiff entity 測試 |
| `tests/ReleaseKit.Domain.Tests/ValueObjects/RiskLevelTests.cs` | 風險等級測試 |
| `tests/ReleaseKit.Domain.Tests/ValueObjects/RiskScenarioTests.cs` | 風險情境測試 |
| `tests/ReleaseKit.Infrastructure.Tests/Git/GitOperationServiceTests.cs` | Git 操作測試 |
| `tests/ReleaseKit.Infrastructure.Tests/Git/CloneUrlBuilderTests.cs` | Clone URL 建構測試 |
| `tests/ReleaseKit.Infrastructure.Tests/Analysis/ProjectStructureScannerTests.cs` | 靜態分析測試 |
| `tests/ReleaseKit.Infrastructure.Tests/Analysis/DependencyInferrerTests.cs` | 相依推斷測試 |
| `tests/ReleaseKit.Infrastructure.Tests/Reporting/MarkdownReportGeneratorTests.cs` | 報告產生測試 |
| `tests/ReleaseKit.Application.Tests/Tasks/CloneRepositoriesTaskTests.cs` | Stage 1 任務測試 |
| `tests/ReleaseKit.Application.Tests/Tasks/AnalyzePRDiffsTaskTests.cs` | Stage 2 任務測試 |
| `tests/ReleaseKit.Application.Tests/Tasks/StaticProjectAnalysisTaskTests.cs` | Stage 3 任務測試 |
| `tests/ReleaseKit.Application.Tests/Tasks/CopilotRiskAnalysisTaskTests.cs` | Stage 4 任務測試 |
| `tests/ReleaseKit.Application.Tests/Tasks/CrossProjectCorrelationTaskTests.cs` | Stage 5 任務測試 |
| `tests/ReleaseKit.Application.Tests/Tasks/GenerateRiskReportTaskTests.cs` | Stage 6 任務測試 |
| `tests/ReleaseKit.Console.Tests/Parsers/CommandLineParserTests.cs` | CLI 解析測試 (修改) |
| `tests/ReleaseKit.Application.Tests/Tasks/TaskFactoryTests.cs` | TaskFactory 測試 (修改) |

---

## Phase 1: 基礎建設（Domain Entities、Value Objects、Configuration）

### Task 1: 新增風險分析 Value Objects

**Files:**
- Create: `src/ReleaseKit.Domain/ValueObjects/RiskLevel.cs`
- Create: `src/ReleaseKit.Domain/ValueObjects/RiskScenario.cs`
- Create: `src/ReleaseKit.Domain/ValueObjects/ChangeType.cs`
- Create: `src/ReleaseKit.Domain/ValueObjects/DependencyType.cs`
- Test: `tests/ReleaseKit.Domain.Tests/ValueObjects/RiskLevelTests.cs`
- Test: `tests/ReleaseKit.Domain.Tests/ValueObjects/RiskScenarioTests.cs`

- [ ] **Step 1: 撰寫 RiskLevel 與 RiskScenario 的失敗測試**

```csharp
// tests/ReleaseKit.Domain.Tests/ValueObjects/RiskLevelTests.cs
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Tests.ValueObjects;

/// <summary>
/// RiskLevel 值物件單元測試
/// </summary>
public class RiskLevelTests
{
    [Fact]
    public void RiskLevel_應包含三個等級()
    {
        var values = Enum.GetValues<RiskLevel>();
        Assert.Equal(3, values.Length);
        Assert.Contains(RiskLevel.High, values);
        Assert.Contains(RiskLevel.Medium, values);
        Assert.Contains(RiskLevel.Low, values);
    }
}
```

```csharp
// tests/ReleaseKit.Domain.Tests/ValueObjects/RiskScenarioTests.cs
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Tests.ValueObjects;

/// <summary>
/// RiskScenario 值物件單元測試
/// </summary>
public class RiskScenarioTests
{
    [Fact]
    public void RiskScenario_應包含五種情境()
    {
        var values = Enum.GetValues<RiskScenario>();
        Assert.Equal(5, values.Length);
        Assert.Contains(RiskScenario.ApiContractBreak, values);
        Assert.Contains(RiskScenario.DatabaseSchemaChange, values);
        Assert.Contains(RiskScenario.MessageQueueFormat, values);
        Assert.Contains(RiskScenario.ConfigEnvChange, values);
        Assert.Contains(RiskScenario.DataSemanticChange, values);
    }
}
```

- [ ] **Step 2: 執行測試確認失敗**

Run: `cd src && dotnet test ../tests/ReleaseKit.Domain.Tests --filter "RiskLevelTests|RiskScenarioTests" --no-restore -v q`
Expected: FAIL — 找不到 RiskLevel/RiskScenario 類型

- [ ] **Step 3: 實作 Value Objects**

```csharp
// src/ReleaseKit.Domain/ValueObjects/RiskLevel.cs
namespace ReleaseKit.Domain.ValueObjects;

/// <summary>
/// 風險等級列舉
/// </summary>
public enum RiskLevel
{
    /// <summary>
    /// 高風險：破壞性變更（刪除欄位、移除 API、改變 MQ schema）
    /// </summary>
    High,

    /// <summary>
    /// 中風險：可能影響（新增必填欄位、修改回傳格式）
    /// </summary>
    Medium,

    /// <summary>
    /// 低風險：輕微影響（新增選填欄位、新增 API）
    /// </summary>
    Low
}
```

```csharp
// src/ReleaseKit.Domain/ValueObjects/RiskScenario.cs
namespace ReleaseKit.Domain.ValueObjects;

/// <summary>
/// 風險分析情境列舉
/// </summary>
public enum RiskScenario
{
    /// <summary>
    /// API 契約破壞
    /// </summary>
    ApiContractBreak,

    /// <summary>
    /// 資料庫 Schema 變更
    /// </summary>
    DatabaseSchemaChange,

    /// <summary>
    /// 訊息佇列格式變更
    /// </summary>
    MessageQueueFormat,

    /// <summary>
    /// 設定檔/環境變數變更
    /// </summary>
    ConfigEnvChange,

    /// <summary>
    /// 資料庫資料語意變更
    /// </summary>
    DataSemanticChange
}
```

```csharp
// src/ReleaseKit.Domain/ValueObjects/ChangeType.cs
namespace ReleaseKit.Domain.ValueObjects;

/// <summary>
/// 檔案變更類型列舉
/// </summary>
public enum ChangeType
{
    /// <summary>
    /// 新增檔案
    /// </summary>
    Added,

    /// <summary>
    /// 修改檔案
    /// </summary>
    Modified,

    /// <summary>
    /// 刪除檔案
    /// </summary>
    Deleted
}
```

```csharp
// src/ReleaseKit.Domain/ValueObjects/DependencyType.cs
namespace ReleaseKit.Domain.ValueObjects;

/// <summary>
/// 推斷的服務相依類型
/// </summary>
public enum DependencyType
{
    /// <summary>
    /// 共用 NuGet 套件
    /// </summary>
    NuGet,

    /// <summary>
    /// HTTP API 呼叫
    /// </summary>
    HttpCall,

    /// <summary>
    /// 共用資料庫
    /// </summary>
    SharedDb,

    /// <summary>
    /// 共用訊息佇列
    /// </summary>
    SharedMQ
}
```

- [ ] **Step 4: 執行測試確認通過**

Run: `cd src && dotnet test ../tests/ReleaseKit.Domain.Tests --filter "RiskLevelTests|RiskScenarioTests" --no-restore -v q`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/ReleaseKit.Domain/ValueObjects/ tests/ReleaseKit.Domain.Tests/ValueObjects/
git commit -m "feat: 新增風險分析 Value Objects（RiskLevel、RiskScenario、ChangeType、DependencyType）"
```

---

### Task 2: 新增 Domain Entities（FileDiff、ProjectDiffResult）

**Files:**
- Create: `src/ReleaseKit.Domain/Entities/FileDiff.cs`
- Create: `src/ReleaseKit.Domain/Entities/ProjectDiffResult.cs`
- Test: `tests/ReleaseKit.Domain.Tests/Entities/FileDiffTests.cs`

- [ ] **Step 1: 撰寫失敗測試**

```csharp
// tests/ReleaseKit.Domain.Tests/Entities/FileDiffTests.cs
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Tests.Entities;

/// <summary>
/// FileDiff 與 ProjectDiffResult 實體單元測試
/// </summary>
public class FileDiffTests
{
    [Fact]
    public void FileDiff_應正確建立()
    {
        var diff = new FileDiff
        {
            FilePath = "Controllers/UserController.cs",
            ChangeType = ValueObjects.ChangeType.Modified,
            DiffContent = "- old\n+ new",
            CommitSha = "abc123"
        };

        Assert.Equal("Controllers/UserController.cs", diff.FilePath);
        Assert.Equal(ValueObjects.ChangeType.Modified, diff.ChangeType);
        Assert.Equal("- old\n+ new", diff.DiffContent);
        Assert.Equal("abc123", diff.CommitSha);
    }

    [Fact]
    public void ProjectDiffResult_應正確建立()
    {
        var result = new ProjectDiffResult
        {
            ProjectPath = "mygroup/backend-api",
            FileDiffs = new List<FileDiff>
            {
                new()
                {
                    FilePath = "test.cs",
                    ChangeType = ValueObjects.ChangeType.Added,
                    DiffContent = "+ new file",
                    CommitSha = "def456"
                }
            }
        };

        Assert.Equal("mygroup/backend-api", result.ProjectPath);
        Assert.Single(result.FileDiffs);
    }
}
```

- [ ] **Step 2: 執行測試確認失敗**

Run: `cd src && dotnet test ../tests/ReleaseKit.Domain.Tests --filter "FileDiffTests" --no-restore -v q`
Expected: FAIL

- [ ] **Step 3: 實作 Entities**

```csharp
// src/ReleaseKit.Domain/Entities/FileDiff.cs
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Entities;

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
    /// 變更類型
    /// </summary>
    public required ChangeType ChangeType { get; init; }

    /// <summary>
    /// Diff 內容（unified diff 格式）
    /// </summary>
    public required string DiffContent { get; init; }

    /// <summary>
    /// 對應的 Commit SHA
    /// </summary>
    public required string CommitSha { get; init; }
}
```

```csharp
// src/ReleaseKit.Domain/Entities/ProjectDiffResult.cs
namespace ReleaseKit.Domain.Entities;

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

- [ ] **Step 4: 執行測試確認通過**

Run: `cd src && dotnet test ../tests/ReleaseKit.Domain.Tests --filter "FileDiffTests" --no-restore -v q`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/ReleaseKit.Domain/Entities/FileDiff.cs src/ReleaseKit.Domain/Entities/ProjectDiffResult.cs tests/ReleaseKit.Domain.Tests/Entities/FileDiffTests.cs
git commit -m "feat: 新增 FileDiff 與 ProjectDiffResult 領域實體"
```

---

### Task 3: 新增 Domain Entities（ProjectStructure、ApiEndpoint、ServiceDependency）

**Files:**
- Create: `src/ReleaseKit.Domain/Entities/ApiEndpoint.cs`
- Create: `src/ReleaseKit.Domain/Entities/ServiceDependency.cs`
- Create: `src/ReleaseKit.Domain/Entities/ProjectStructure.cs`
- Test: `tests/ReleaseKit.Domain.Tests/Entities/ProjectStructureTests.cs`

- [ ] **Step 1: 撰寫失敗測試**

```csharp
// tests/ReleaseKit.Domain.Tests/Entities/ProjectStructureTests.cs
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Tests.Entities;

/// <summary>
/// ProjectStructure 實體單元測試
/// </summary>
public class ProjectStructureTests
{
    [Fact]
    public void ProjectStructure_應正確建立含完整掃描結果()
    {
        var structure = new ProjectStructure
        {
            ProjectPath = "mygroup/backend-api",
            ApiEndpoints = new List<ApiEndpoint>
            {
                new()
                {
                    HttpMethod = "GET",
                    Route = "/api/v1/users",
                    ControllerName = "UserController",
                    ActionName = "GetUsers"
                }
            },
            NuGetPackages = new List<string> { "Newtonsoft.Json" },
            DbContextFiles = new List<string> { "Data/AppDbContext.cs" },
            MigrationFiles = new List<string> { "Migrations/20260101_Init.cs" },
            MessageContracts = new List<string> { "Events/OrderCreatedEvent.cs" },
            ConfigKeys = new List<string> { "ConnectionStrings:DefaultConnection" },
            InferredDependencies = new List<ServiceDependency>
            {
                new()
                {
                    DependencyType = DependencyType.SharedDb,
                    Target = "OrderDB"
                }
            }
        };

        Assert.Equal("mygroup/backend-api", structure.ProjectPath);
        Assert.Single(structure.ApiEndpoints);
        Assert.Equal("GET", structure.ApiEndpoints[0].HttpMethod);
        Assert.Single(structure.InferredDependencies);
        Assert.Equal(DependencyType.SharedDb, structure.InferredDependencies[0].DependencyType);
    }
}
```

- [ ] **Step 2: 執行測試確認失敗**

Run: `cd src && dotnet test ../tests/ReleaseKit.Domain.Tests --filter "ProjectStructureTests" --no-restore -v q`
Expected: FAIL

- [ ] **Step 3: 實作 Entities**

```csharp
// src/ReleaseKit.Domain/Entities/ApiEndpoint.cs
namespace ReleaseKit.Domain.Entities;

/// <summary>
/// API 端點資訊
/// </summary>
public sealed record ApiEndpoint
{
    /// <summary>
    /// HTTP 方法（GET、POST、PUT、DELETE 等）
    /// </summary>
    public required string HttpMethod { get; init; }

    /// <summary>
    /// 路由路徑
    /// </summary>
    public required string Route { get; init; }

    /// <summary>
    /// Controller 名稱
    /// </summary>
    public required string ControllerName { get; init; }

    /// <summary>
    /// Action 方法名稱
    /// </summary>
    public required string ActionName { get; init; }
}
```

```csharp
// src/ReleaseKit.Domain/Entities/ServiceDependency.cs
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Entities;

/// <summary>
/// 推斷的服務相依性
/// </summary>
public sealed record ServiceDependency
{
    /// <summary>
    /// 相依類型
    /// </summary>
    public required DependencyType DependencyType { get; init; }

    /// <summary>
    /// 目標：套件名稱、API URL、DB 名稱、MQ Topic
    /// </summary>
    public required string Target { get; init; }
}
```

```csharp
// src/ReleaseKit.Domain/Entities/ProjectStructure.cs
namespace ReleaseKit.Domain.Entities;

/// <summary>
/// 專案結構分析結果
/// </summary>
public sealed record ProjectStructure
{
    /// <summary>
    /// 專案路徑
    /// </summary>
    public required string ProjectPath { get; init; }

    /// <summary>
    /// API 端點清單
    /// </summary>
    public required IReadOnlyList<ApiEndpoint> ApiEndpoints { get; init; }

    /// <summary>
    /// NuGet 套件引用清單
    /// </summary>
    public required IReadOnlyList<string> NuGetPackages { get; init; }

    /// <summary>
    /// DbContext 檔案清單
    /// </summary>
    public required IReadOnlyList<string> DbContextFiles { get; init; }

    /// <summary>
    /// Migration 檔案清單
    /// </summary>
    public required IReadOnlyList<string> MigrationFiles { get; init; }

    /// <summary>
    /// 訊息契約檔案清單
    /// </summary>
    public required IReadOnlyList<string> MessageContracts { get; init; }

    /// <summary>
    /// 設定檔 Key 清單
    /// </summary>
    public required IReadOnlyList<string> ConfigKeys { get; init; }

    /// <summary>
    /// 推斷的服務相依性清單
    /// </summary>
    public required IReadOnlyList<ServiceDependency> InferredDependencies { get; init; }
}
```

- [ ] **Step 4: 執行測試確認通過**

Run: `cd src && dotnet test ../tests/ReleaseKit.Domain.Tests --filter "ProjectStructureTests" --no-restore -v q`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/ReleaseKit.Domain/Entities/ApiEndpoint.cs src/ReleaseKit.Domain/Entities/ServiceDependency.cs src/ReleaseKit.Domain/Entities/ProjectStructure.cs tests/ReleaseKit.Domain.Tests/Entities/ProjectStructureTests.cs
git commit -m "feat: 新增 ProjectStructure、ApiEndpoint、ServiceDependency 領域實體"
```

---

### Task 4: 新增風險分析 Domain Entities（RiskFinding、ProjectRiskAnalysis）

**Files:**
- Create: `src/ReleaseKit.Domain/Entities/RiskFinding.cs`
- Create: `src/ReleaseKit.Domain/Entities/ProjectRiskAnalysis.cs`
- Create: `src/ReleaseKit.Domain/Entities/CrossProjectCorrelation.cs`
- Create: `src/ReleaseKit.Domain/Entities/RiskReport.cs`
- Test: `tests/ReleaseKit.Domain.Tests/Entities/RiskFindingTests.cs`

- [ ] **Step 1: 撰寫失敗測試**

```csharp
// tests/ReleaseKit.Domain.Tests/Entities/RiskFindingTests.cs
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Tests.Entities;

/// <summary>
/// RiskFinding 與 ProjectRiskAnalysis 實體單元測試
/// </summary>
public class RiskFindingTests
{
    [Fact]
    public void RiskFinding_應正確建立含完整風險資訊()
    {
        var finding = new RiskFinding
        {
            Scenario = RiskScenario.ApiContractBreak,
            RiskLevel = RiskLevel.High,
            Description = "GET /api/v1/users 新增必填參數",
            AffectedFile = "Controllers/UserController.cs",
            DiffSnippet = "- GetUser(int id)\n+ GetUser(int id, bool details)",
            PotentiallyAffectedProjects = new List<string> { "frontend-app", "mobile-api" },
            RecommendedAction = "通知前端團隊更新 API 呼叫",
            ChangedBy = "John"
        };

        Assert.Equal(RiskScenario.ApiContractBreak, finding.Scenario);
        Assert.Equal(RiskLevel.High, finding.RiskLevel);
        Assert.Equal(2, finding.PotentiallyAffectedProjects.Count);
    }

    [Fact]
    public void ProjectRiskAnalysis_應正確建立()
    {
        var analysis = new ProjectRiskAnalysis
        {
            ProjectPath = "mygroup/backend-api",
            Findings = new List<RiskFinding>(),
            SessionCount = 2
        };

        Assert.Equal("mygroup/backend-api", analysis.ProjectPath);
        Assert.Empty(analysis.Findings);
        Assert.Equal(2, analysis.SessionCount);
    }

    [Fact]
    public void CrossProjectCorrelation_應正確建立含相依圖()
    {
        var correlation = new CrossProjectCorrelation
        {
            DependencyEdges = new List<DependencyEdge>
            {
                new()
                {
                    SourceProject = "backend-api",
                    TargetProject = "frontend-app",
                    DependencyType = DependencyType.HttpCall,
                    Target = "/api/v1/users"
                }
            },
            CorrelatedFindings = new List<CorrelatedRiskFinding>(),
            NotificationTargets = new List<NotificationTarget>()
        };

        Assert.Single(correlation.DependencyEdges);
    }
}
```

- [ ] **Step 2: 執行測試確認失敗**

Run: `cd src && dotnet test ../tests/ReleaseKit.Domain.Tests --filter "RiskFindingTests" --no-restore -v q`
Expected: FAIL

- [ ] **Step 3: 實作 Entities**

```csharp
// src/ReleaseKit.Domain/Entities/RiskFinding.cs
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Entities;

/// <summary>
/// 單一風險發現
/// </summary>
public sealed record RiskFinding
{
    /// <summary>
    /// 風險情境類型
    /// </summary>
    public required RiskScenario Scenario { get; init; }

    /// <summary>
    /// 風險等級
    /// </summary>
    public required RiskLevel RiskLevel { get; init; }

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
```

```csharp
// src/ReleaseKit.Domain/Entities/ProjectRiskAnalysis.cs
namespace ReleaseKit.Domain.Entities;

/// <summary>
/// 單一專案的風險分析結果
/// </summary>
public sealed record ProjectRiskAnalysis
{
    /// <summary>
    /// 專案路徑
    /// </summary>
    public required string ProjectPath { get; init; }

    /// <summary>
    /// 風險發現清單
    /// </summary>
    public required IReadOnlyList<RiskFinding> Findings { get; init; }

    /// <summary>
    /// 使用的 Copilot session 數量
    /// </summary>
    public required int SessionCount { get; init; }
}
```

```csharp
// src/ReleaseKit.Domain/Entities/CrossProjectCorrelation.cs
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Entities;

/// <summary>
/// 跨專案交叉比對結果
/// </summary>
public sealed record CrossProjectCorrelation
{
    /// <summary>
    /// 相依性邊清單
    /// </summary>
    public required IReadOnlyList<DependencyEdge> DependencyEdges { get; init; }

    /// <summary>
    /// 經交叉比對後的風險發現清單
    /// </summary>
    public required IReadOnlyList<CorrelatedRiskFinding> CorrelatedFindings { get; init; }

    /// <summary>
    /// 通知對象清單
    /// </summary>
    public required IReadOnlyList<NotificationTarget> NotificationTargets { get; init; }
}

/// <summary>
/// 相依性邊（描述兩個專案間的關聯）
/// </summary>
public sealed record DependencyEdge
{
    /// <summary>
    /// 來源專案
    /// </summary>
    public required string SourceProject { get; init; }

    /// <summary>
    /// 目標專案
    /// </summary>
    public required string TargetProject { get; init; }

    /// <summary>
    /// 相依類型
    /// </summary>
    public required DependencyType DependencyType { get; init; }

    /// <summary>
    /// 具體目標（API URL、DB 名稱等）
    /// </summary>
    public required string Target { get; init; }
}

/// <summary>
/// 經交叉比對後的風險發現（包含受影響專案的確認）
/// </summary>
public sealed record CorrelatedRiskFinding
{
    /// <summary>
    /// 原始風險發現
    /// </summary>
    public required RiskFinding OriginalFinding { get; init; }

    /// <summary>
    /// 經確認的受影響專案清單
    /// </summary>
    public required IReadOnlyList<string> ConfirmedAffectedProjects { get; init; }

    /// <summary>
    /// 最終風險等級（可能因交叉比對而調整）
    /// </summary>
    public required RiskLevel FinalRiskLevel { get; init; }
}

/// <summary>
/// 通知對象
/// </summary>
public sealed record NotificationTarget
{
    /// <summary>
    /// 人員名稱
    /// </summary>
    public required string PersonName { get; init; }

    /// <summary>
    /// 需注意的風險項描述
    /// </summary>
    public required string RiskDescription { get; init; }

    /// <summary>
    /// 相關專案
    /// </summary>
    public required string RelatedProject { get; init; }
}
```

```csharp
// src/ReleaseKit.Domain/Entities/RiskReport.cs
namespace ReleaseKit.Domain.Entities;

/// <summary>
/// 風險報告完整資料模型
/// </summary>
public sealed record RiskReport
{
    /// <summary>
    /// 執行 ID（yyyyMMddHHmmss 格式）
    /// </summary>
    public required string RunId { get; init; }

    /// <summary>
    /// 執行時間
    /// </summary>
    public required DateTimeOffset ExecutedAt { get; init; }

    /// <summary>
    /// 跨專案交叉比對結果
    /// </summary>
    public required CrossProjectCorrelation Correlation { get; init; }

    /// <summary>
    /// 各專案的風險分析結果
    /// </summary>
    public required IReadOnlyList<ProjectRiskAnalysis> ProjectAnalyses { get; init; }

    /// <summary>
    /// Markdown 報告內容
    /// </summary>
    public required string MarkdownContent { get; init; }
}
```

- [ ] **Step 4: 執行測試確認通過**

Run: `cd src && dotnet test ../tests/ReleaseKit.Domain.Tests --filter "RiskFindingTests" --no-restore -v q`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/ReleaseKit.Domain/Entities/RiskFinding.cs src/ReleaseKit.Domain/Entities/ProjectRiskAnalysis.cs src/ReleaseKit.Domain/Entities/CrossProjectCorrelation.cs src/ReleaseKit.Domain/Entities/RiskReport.cs tests/ReleaseKit.Domain.Tests/Entities/RiskFindingTests.cs
git commit -m "feat: 新增風險分析結果領域實體（RiskFinding、CrossProjectCorrelation、RiskReport）"
```

---

### Task 5: 新增 IGitOperationService 介面與 Error 擴充

**Files:**
- Create: `src/ReleaseKit.Domain/Abstractions/IGitOperationService.cs`
- Modify: `src/ReleaseKit.Domain/Common/Error.cs`

- [ ] **Step 1: 建立 IGitOperationService 介面**

```csharp
// src/ReleaseKit.Domain/Abstractions/IGitOperationService.cs
using ReleaseKit.Domain.Common;
using ReleaseKit.Domain.Entities;

namespace ReleaseKit.Domain.Abstractions;

/// <summary>
/// Git 操作服務介面
/// </summary>
public interface IGitOperationService
{
    /// <summary>
    /// Clone 或 Pull 遠端倉庫至本地路徑
    /// </summary>
    /// <param name="repoUrl">遠端倉庫 URL（含認證資訊）</param>
    /// <param name="localPath">本地路徑</param>
    /// <param name="cancellationToken">取消標記</param>
    /// <returns>成功時回傳本地路徑；失敗時回傳錯誤</returns>
    Task<Result<string>> CloneOrPullAsync(string repoUrl, string localPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得指定 commit 的異動檔案與 diff 內容
    /// </summary>
    /// <param name="repoPath">本地 repo 路徑</param>
    /// <param name="commitSha">Commit SHA</param>
    /// <param name="cancellationToken">取消標記</param>
    /// <returns>成功時回傳 FileDiff 清單；失敗時回傳錯誤</returns>
    Task<Result<IReadOnlyList<FileDiff>>> GetCommitDiffAsync(string repoPath, string commitSha, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: 擴充 Error 類別新增 Git 操作相關錯誤**

在 `src/ReleaseKit.Domain/Common/Error.cs` 中，在 `AzureDevOps` 類別後新增：

```csharp
    /// <summary>
    /// Git 操作相關錯誤
    /// </summary>
    public static class Git
    {
        /// <summary>
        /// Clone 失敗錯誤
        /// </summary>
        /// <param name="repoUrl">倉庫 URL</param>
        /// <param name="message">詳細錯誤訊息</param>
        public static Error CloneFailed(string repoUrl, string message) =>
            new("Git.CloneFailed", $"Clone '{repoUrl}' 失敗：{message}");

        /// <summary>
        /// Pull 失敗錯誤
        /// </summary>
        /// <param name="localPath">本地路徑</param>
        /// <param name="message">詳細錯誤訊息</param>
        public static Error PullFailed(string localPath, string message) =>
            new("Git.PullFailed", $"Pull '{localPath}' 失敗：{message}");

        /// <summary>
        /// Diff 取得失敗錯誤
        /// </summary>
        /// <param name="commitSha">Commit SHA</param>
        /// <param name="message">詳細錯誤訊息</param>
        public static Error DiffFailed(string commitSha, string message) =>
            new("Git.DiffFailed", $"取得 commit '{commitSha}' 的 diff 失敗：{message}");
    }
```

- [ ] **Step 3: 確認建置通過**

Run: `cd src && dotnet build --no-restore -v q`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add src/ReleaseKit.Domain/Abstractions/IGitOperationService.cs src/ReleaseKit.Domain/Common/Error.cs
git commit -m "feat: 新增 IGitOperationService 介面與 Git 操作相關 Error 定義"
```

---

### Task 6: 新增設定模型與 Redis Key 常數

**Files:**
- Create: `src/ReleaseKit.Common/Configuration/RiskAnalysisOptions.cs`
- Create: `src/ReleaseKit.Common/Constants/RiskAnalysisRedisKeys.cs`
- Modify: `src/ReleaseKit.Common/Constants/RedisKeys.cs`

- [ ] **Step 1: 建立 RiskAnalysisOptions**

```csharp
// src/ReleaseKit.Common/Configuration/RiskAnalysisOptions.cs
namespace ReleaseKit.Common.Configuration;

/// <summary>
/// 風險分析設定選項
/// </summary>
public class RiskAnalysisOptions
{
    /// <summary>
    /// Clone 到本地的基礎路徑
    /// </summary>
    public string CloneBasePath { get; init; } = "/tmp/release-kit-repos";

    /// <summary>
    /// 啟用的分析情境清單
    /// </summary>
    public List<string> AnalysisScenarios { get; init; } = new()
    {
        "ApiContractBreak",
        "DatabaseSchemaChange",
        "MessageQueueFormat",
        "ConfigEnvChange",
        "DataSemanticChange"
    };
}
```

- [ ] **Step 2: 建立 RiskAnalysisRedisKeys**

```csharp
// src/ReleaseKit.Common/Constants/RiskAnalysisRedisKeys.cs
namespace ReleaseKit.Common.Constants;

/// <summary>
/// 風險分析專用 Redis Key 建構器
/// </summary>
public static class RiskAnalysisRedisKeys
{
    /// <summary>
    /// 風險分析 Hash 前綴
    /// </summary>
    private const string Prefix = "RiskAnalysis";

    /// <summary>
    /// 取得 Stage 1（Clone）的 Redis Hash Key
    /// </summary>
    public static string Stage1Hash(string runId) => $"{Prefix}:{runId}:Stage1";

    /// <summary>
    /// 取得 Stage 2（PR Diff）的 Redis Hash Key
    /// </summary>
    public static string Stage2Hash(string runId) => $"{Prefix}:{runId}:Stage2";

    /// <summary>
    /// 取得 Stage 3（靜態分析）的 Redis Hash Key
    /// </summary>
    public static string Stage3Hash(string runId) => $"{Prefix}:{runId}:Stage3";

    /// <summary>
    /// 取得 Stage 4（Copilot 分析）的 Redis Hash Key
    /// </summary>
    public static string Stage4Hash(string runId) => $"{Prefix}:{runId}:Stage4";

    /// <summary>
    /// 取得 Stage 5（交叉比對）的 Redis Hash Key
    /// </summary>
    public static string Stage5Hash(string runId) => $"{Prefix}:{runId}:Stage5";

    /// <summary>
    /// 取得 Stage 6（報告）的 Redis Hash Key
    /// </summary>
    public static string Stage6Hash(string runId) => $"{Prefix}:{runId}:Stage6";

    /// <summary>
    /// 取得當前 Run ID 的 Redis Key
    /// </summary>
    public const string CurrentRunIdKey = "RiskAnalysis:CurrentRunId";

    /// <summary>
    /// Stage 5 交叉比對結果的欄位名稱
    /// </summary>
    public const string CorrelationField = "Correlation";

    /// <summary>
    /// Stage 6 報告結果的欄位名稱
    /// </summary>
    public const string ReportField = "Report";
}
```

- [ ] **Step 3: 確認建置通過**

Run: `cd src && dotnet build --no-restore -v q`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add src/ReleaseKit.Common/Configuration/RiskAnalysisOptions.cs src/ReleaseKit.Common/Constants/RiskAnalysisRedisKeys.cs
git commit -m "feat: 新增風險分析設定模型與 Redis Key 常數"
```

---

### Task 7: 擴充 TaskType、TaskFactory、CommandLineParser 與 DI 註冊

**Files:**
- Modify: `src/ReleaseKit.Application/Tasks/TaskType.cs`
- Modify: `src/ReleaseKit.Console/Parsers/CommandLineParser.cs`
- Modify: `tests/ReleaseKit.Console.Tests/Parsers/CommandLineParserTests.cs`

- [ ] **Step 1: 在現有 CommandLineParser 測試中新增風險分析任務測試**

在 `tests/ReleaseKit.Console.Tests/Parsers/CommandLineParserTests.cs` 中新增：

```csharp
[Theory]
[InlineData("clone-repositories", TaskType.CloneRepositories)]
[InlineData("analyze-pr-diffs", TaskType.AnalyzePRDiffs)]
[InlineData("static-project-analysis", TaskType.StaticProjectAnalysis)]
[InlineData("copilot-risk-analysis", TaskType.CopilotRiskAnalysis)]
[InlineData("cross-project-correlation", TaskType.CrossProjectCorrelation)]
[InlineData("generate-risk-report", TaskType.GenerateRiskReport)]
public void Parse_風險分析任務名稱_應回傳對應TaskType(string taskName, TaskType expectedType)
{
    var parser = new CommandLineParser();
    var result = parser.Parse(new[] { taskName });
    Assert.True(result.IsSuccess);
    Assert.Equal(expectedType, result.TaskType.Value);
}
```

- [ ] **Step 2: 執行測試確認失敗**

Run: `cd src && dotnet test ../tests/ReleaseKit.Console.Tests --filter "風險分析任務名稱" --no-restore -v q`
Expected: FAIL — TaskType 中找不到新的列舉值

- [ ] **Step 3: 擴充 TaskType enum**

在 `src/ReleaseKit.Application/Tasks/TaskType.cs` 的 `GetReleaseSetting` 後新增：

```csharp
    /// <summary>
    /// Clone/Pull 所有專案 Repo
    /// </summary>
    CloneRepositories,

    /// <summary>
    /// 分析 PR Diff 資訊
    /// </summary>
    AnalyzePRDiffs,

    /// <summary>
    /// 靜態專案結構分析
    /// </summary>
    StaticProjectAnalysis,

    /// <summary>
    /// Copilot SDK 風險分析
    /// </summary>
    CopilotRiskAnalysis,

    /// <summary>
    /// 跨專案交叉比對
    /// </summary>
    CrossProjectCorrelation,

    /// <summary>
    /// 產生風險報告
    /// </summary>
    GenerateRiskReport
```

- [ ] **Step 4: 擴充 CommandLineParser 的 _taskMappings**

在 `src/ReleaseKit.Console/Parsers/CommandLineParser.cs` 的 `_taskMappings` 字典中新增：

```csharp
        { "clone-repositories", TaskType.CloneRepositories },
        { "analyze-pr-diffs", TaskType.AnalyzePRDiffs },
        { "static-project-analysis", TaskType.StaticProjectAnalysis },
        { "copilot-risk-analysis", TaskType.CopilotRiskAnalysis },
        { "cross-project-correlation", TaskType.CrossProjectCorrelation },
        { "generate-risk-report", TaskType.GenerateRiskReport },
```

- [ ] **Step 5: 執行測試確認通過**

Run: `cd src && dotnet test ../tests/ReleaseKit.Console.Tests --filter "風險分析任務名稱" --no-restore -v q`
Expected: PASS

- [ ] **Step 6: 執行所有既有測試確認無回歸**

Run: `cd src && dotnet test --no-restore -v q`
Expected: 所有測試 PASS

- [ ] **Step 7: Commit**

```bash
git add src/ReleaseKit.Application/Tasks/TaskType.cs src/ReleaseKit.Console/Parsers/CommandLineParser.cs tests/ReleaseKit.Console.Tests/Parsers/CommandLineParserTests.cs
git commit -m "feat: 擴充 TaskType 與 CommandLineParser 支援風險分析任務"
```

---

### Task 8: 擴充 MergeRequest Entity 新增 MergeCommitSha

**Files:**
- Modify: `src/ReleaseKit.Domain/Entities/MergeRequest.cs`
- Modify: `src/ReleaseKit.Infrastructure/SourceControl/GitLab/Models/GitLabMergeRequestResponse.cs`
- Modify: `src/ReleaseKit.Infrastructure/SourceControl/GitLab/GitLabMergeRequestMapper.cs`
- Modify: `src/ReleaseKit.Infrastructure/SourceControl/Bitbucket/BitbucketPullRequestMapper.cs`
- Modify: `tests/ReleaseKit.Domain.Tests/Entities/MergeRequestTests.cs`
- Modify: `tests/ReleaseKit.Infrastructure.Tests/SourceControl/GitLab/GitLabMergeRequestMapperTests.cs`
- Modify: `tests/ReleaseKit.Infrastructure.Tests/SourceControl/Bitbucket/BitbucketPullRequestMapperTests.cs`

- [ ] **Step 1: 先檢查現有 MergeRequest 測試以了解測試模式**

Read: `tests/ReleaseKit.Domain.Tests/Entities/MergeRequestTests.cs` 與 mapper 測試

- [ ] **Step 2: 在 MergeRequest 測試中新增 MergeCommitSha 測試**

```csharp
[Fact]
public void MergeRequest_MergeCommitSha_預設為null()
{
    var mr = new MergeRequest
    {
        Title = "test",
        SourceBranch = "feature/test",
        TargetBranch = "main",
        CreatedAt = DateTimeOffset.UtcNow,
        MergedAt = DateTimeOffset.UtcNow,
        State = "merged",
        AuthorUserId = "1",
        AuthorName = "test",
        PrId = "1",
        PRUrl = "https://test.com",
        Platform = SourceControlPlatform.GitLab,
        ProjectPath = "test/project"
    };

    Assert.Null(mr.MergeCommitSha);
}

[Fact]
public void MergeRequest_MergeCommitSha_可設定值()
{
    var mr = new MergeRequest
    {
        Title = "test",
        SourceBranch = "feature/test",
        TargetBranch = "main",
        CreatedAt = DateTimeOffset.UtcNow,
        MergedAt = DateTimeOffset.UtcNow,
        State = "merged",
        AuthorUserId = "1",
        AuthorName = "test",
        PrId = "1",
        PRUrl = "https://test.com",
        Platform = SourceControlPlatform.GitLab,
        ProjectPath = "test/project",
        MergeCommitSha = "abc123def456"
    };

    Assert.Equal("abc123def456", mr.MergeCommitSha);
}
```

- [ ] **Step 3: 執行測試確認失敗**

Run: `cd src && dotnet test ../tests/ReleaseKit.Domain.Tests --filter "MergeCommitSha" --no-restore -v q`
Expected: FAIL

- [ ] **Step 4: 在 MergeRequest entity 新增 MergeCommitSha 屬性**

在 `src/ReleaseKit.Domain/Entities/MergeRequest.cs` 中 `WorkItemId` 屬性之前新增：

```csharp
    /// <summary>
    /// Merge Commit SHA（合併後的 commit hash）
    /// </summary>
    /// <remarks>
    /// 對應 GitLab 的 merge_commit_sha 欄位或 Bitbucket 的 merge_commit.hash 欄位。
    /// 用於在 clone 下來的 repo 中取得具體的異動 diff。
    /// 若 PR/MR 尚未合併，此值為 null。
    /// </remarks>
    public string? MergeCommitSha { get; init; }
```

- [ ] **Step 5: 在 GitLab API 回應模型新增 merge_commit_sha 欄位**

在 `src/ReleaseKit.Infrastructure/SourceControl/GitLab/Models/GitLabMergeRequestResponse.cs` 中新增：

```csharp
    /// <summary>
    /// 合併 Commit SHA
    /// </summary>
    [JsonPropertyName("merge_commit_sha")]
    public string? MergeCommitSha { get; init; }
```

- [ ] **Step 6: 更新 GitLab Mapper 映射 MergeCommitSha**

在 `src/ReleaseKit.Infrastructure/SourceControl/GitLab/GitLabMergeRequestMapper.cs` 的 `ToDomain` 方法中新增：

```csharp
            MergeCommitSha = response.MergeCommitSha
```

（在 `WorkItemId = VstsIdParser.Parse(...)` 之前加入）

- [ ] **Step 7: 更新 Bitbucket Mapper 映射 MergeCommitSha**

在 `src/ReleaseKit.Infrastructure/SourceControl/Bitbucket/BitbucketPullRequestMapper.cs` 的 `ToDomain` 方法中新增：

```csharp
            MergeCommitSha = response.MergeCommit?.Hash
```

注意：Bitbucket API 的 merge_commit 欄位結構需檢查，可能需要在 Bitbucket 的 Models 中新增對應類型。

- [ ] **Step 8: 執行所有測試確認通過**

Run: `cd src && dotnet test --no-restore -v q`
Expected: 所有測試 PASS（新測試 + 既有測試無回歸）

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "feat: MergeRequest entity 新增 MergeCommitSha 欄位並更新 GitLab/Bitbucket Mapper"
```

---

## Phase 2: Stage 1（Clone/Pull）與 Stage 2（PR Diff 分析）

### Task 9: 實作 CloneUrlBuilder

**Files:**
- Create: `src/ReleaseKit.Infrastructure/Git/CloneUrlBuilder.cs`
- Test: `tests/ReleaseKit.Infrastructure.Tests/Git/CloneUrlBuilderTests.cs`

- [ ] **Step 1: 撰寫失敗測試**

```csharp
// tests/ReleaseKit.Infrastructure.Tests/Git/CloneUrlBuilderTests.cs
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Infrastructure.Git;

namespace ReleaseKit.Infrastructure.Tests.Git;

/// <summary>
/// CloneUrlBuilder 單元測試
/// </summary>
public class CloneUrlBuilderTests
{
    [Fact]
    public void BuildGitLabCloneUrl_應移除ApiV4路徑並嵌入Token()
    {
        var gitLabOptions = new GitLabOptions
        {
            ApiUrl = "https://gitlab.example.com/api/v4",
            AccessToken = "test-token-123"
        };

        var result = CloneUrlBuilder.BuildGitLabCloneUrl(gitLabOptions, "mygroup/backend-api");

        Assert.Equal("https://oauth2:test-token-123@gitlab.example.com/mygroup/backend-api.git", result);
    }

    [Fact]
    public void BuildGitLabCloneUrl_無ApiV4路徑時應直接使用()
    {
        var gitLabOptions = new GitLabOptions
        {
            ApiUrl = "https://gitlab.example.com",
            AccessToken = "test-token"
        };

        var result = CloneUrlBuilder.BuildGitLabCloneUrl(gitLabOptions, "mygroup/backend-api");

        Assert.Equal("https://oauth2:test-token@gitlab.example.com/mygroup/backend-api.git", result);
    }

    [Fact]
    public void BuildBitbucketCloneUrl_應嵌入Email與Token()
    {
        var bitbucketOptions = new BitbucketOptions
        {
            Email = "user@example.com",
            AccessToken = "bb-token-456"
        };

        var result = CloneUrlBuilder.BuildBitbucketCloneUrl(bitbucketOptions, "workspace/repo");

        Assert.Equal("https://user%40example.com:bb-token-456@bitbucket.org/workspace/repo.git", result);
    }
}
```

- [ ] **Step 2: 執行測試確認失敗**

Run: `cd src && dotnet test ../tests/ReleaseKit.Infrastructure.Tests --filter "CloneUrlBuilderTests" --no-restore -v q`
Expected: FAIL

- [ ] **Step 3: 實作 CloneUrlBuilder**

```csharp
// src/ReleaseKit.Infrastructure/Git/CloneUrlBuilder.cs
using ReleaseKit.Common.Configuration;

namespace ReleaseKit.Infrastructure.Git;

/// <summary>
/// 建構 Git Clone URL 的工具類別
/// </summary>
public static class CloneUrlBuilder
{
    /// <summary>
    /// 建構 GitLab Clone URL（移除 /api/v4 後，使用 oauth2:{PAT} 內嵌認證）
    /// </summary>
    /// <param name="options">GitLab 配置選項</param>
    /// <param name="projectPath">專案路徑</param>
    /// <returns>包含 PAT 認證的 GitLab Clone URL</returns>
    public static string BuildGitLabCloneUrl(GitLabOptions options, string projectPath)
    {
        var uri = new Uri(options.ApiUrl);
        var encodedToken = Uri.EscapeDataString(options.AccessToken);
        var basePath = uri.AbsolutePath.TrimEnd('/');

        if (basePath.EndsWith("/api/v4", StringComparison.OrdinalIgnoreCase))
        {
            basePath = basePath[..^"/api/v4".Length];
        }

        basePath = basePath.TrimEnd('/');
        return $"{uri.Scheme}://oauth2:{encodedToken}@{uri.Authority}{basePath}/{projectPath}.git";
    }

    /// <summary>
    /// 建構 Bitbucket Clone URL（使用 email:AccessToken 內嵌認證）
    /// </summary>
    /// <param name="options">Bitbucket 配置選項</param>
    /// <param name="projectPath">專案路徑</param>
    /// <returns>Bitbucket Clone URL</returns>
    public static string BuildBitbucketCloneUrl(BitbucketOptions options, string projectPath)
    {
        var encodedEmail = Uri.EscapeDataString(options.Email);
        return $"https://{encodedEmail}:{options.AccessToken}@bitbucket.org/{projectPath}.git";
    }
}
```

- [ ] **Step 4: 執行測試確認通過**

Run: `cd src && dotnet test ../tests/ReleaseKit.Infrastructure.Tests --filter "CloneUrlBuilderTests" --no-restore -v q`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/ReleaseKit.Infrastructure/Git/CloneUrlBuilder.cs tests/ReleaseKit.Infrastructure.Tests/Git/CloneUrlBuilderTests.cs
git commit -m "feat: 實作 CloneUrlBuilder（GitLab/Bitbucket clone URL 建構）"
```

---

### Task 10: 實作 GitOperationService

**Files:**
- Create: `src/ReleaseKit.Infrastructure/Git/GitOperationService.cs`
- Test: `tests/ReleaseKit.Infrastructure.Tests/Git/GitOperationServiceTests.cs`

- [ ] **Step 1: 撰寫失敗測試（使用 temp 目錄模擬 git 操作）**

```csharp
// tests/ReleaseKit.Infrastructure.Tests/Git/GitOperationServiceTests.cs
using Microsoft.Extensions.Logging;
using Moq;
using ReleaseKit.Domain.ValueObjects;
using ReleaseKit.Infrastructure.Git;

namespace ReleaseKit.Infrastructure.Tests.Git;

/// <summary>
/// GitOperationService 單元測試
/// </summary>
public class GitOperationServiceTests
{
    private readonly Mock<ILogger<GitOperationService>> _loggerMock = new();
    private readonly GitOperationService _service;

    public GitOperationServiceTests()
    {
        _service = new GitOperationService(_loggerMock.Object);
    }

    [Fact]
    public async Task CloneOrPullAsync_目錄不存在時應執行Clone()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-clone-{Guid.NewGuid():N}");

        // 使用一個不存在的 URL 測試錯誤處理
        var result = await _service.CloneOrPullAsync(
            "https://invalid-url.example.com/nonexistent.git",
            tempDir);

        // 預期失敗（因為 URL 不存在）
        Assert.True(result.IsFailure);
        Assert.Contains("Clone", result.Error!.Code);

        // 清理
        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task CloneOrPullAsync_目錄已存在且為Git倉庫時應執行Pull()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-pull-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(Path.Combine(tempDir, ".git"));

        var result = await _service.CloneOrPullAsync(
            "https://invalid-url.example.com/nonexistent.git",
            tempDir);

        // 預期失敗（因為不是真的 git repo）
        Assert.True(result.IsFailure);

        // 清理
        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task GetCommitDiffAsync_無效路徑應回傳失敗()
    {
        var result = await _service.GetCommitDiffAsync("/nonexistent/path", "abc123");

        Assert.True(result.IsFailure);
        Assert.Contains("Diff", result.Error!.Code);
    }
}
```

- [ ] **Step 2: 執行測試確認失敗**

Run: `cd src && dotnet test ../tests/ReleaseKit.Infrastructure.Tests --filter "GitOperationServiceTests" --no-restore -v q`
Expected: FAIL

- [ ] **Step 3: 實作 GitOperationService**

```csharp
// src/ReleaseKit.Infrastructure/Git/GitOperationService.cs
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Common;
using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Infrastructure.Git;

/// <summary>
/// Git 操作服務，透過 shell 呼叫 git CLI 執行 clone/pull/diff
/// </summary>
public class GitOperationService : IGitOperationService
{
    private readonly ILogger<GitOperationService> _logger;

    /// <summary>
    /// 初始化 GitOperationService
    /// </summary>
    public GitOperationService(ILogger<GitOperationService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<string>> CloneOrPullAsync(string repoUrl, string localPath, CancellationToken cancellationToken = default)
    {
        if (Directory.Exists(Path.Combine(localPath, ".git")))
        {
            _logger.LogInformation("目錄已存在，執行 git pull: {LocalPath}", localPath);
            var pullResult = await RunGitCommandAsync("pull", localPath, cancellationToken);
            if (!pullResult.IsSuccess)
            {
                return Result<string>.Failure(Error.Git.PullFailed(localPath, pullResult.Error!.Message));
            }
            return Result<string>.Success(localPath);
        }

        _logger.LogInformation("目錄不存在，執行 git clone: {RepoUrl} -> {LocalPath}", SanitizeUrl(repoUrl), localPath);
        var parentDir = Path.GetDirectoryName(localPath) ?? localPath;
        Directory.CreateDirectory(parentDir);

        var cloneResult = await RunGitCommandAsync($"clone {repoUrl} {localPath}", parentDir, cancellationToken);
        if (!cloneResult.IsSuccess)
        {
            return Result<string>.Failure(Error.Git.CloneFailed(SanitizeUrl(repoUrl), cloneResult.Error!.Message));
        }

        return Result<string>.Success(localPath);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<FileDiff>>> GetCommitDiffAsync(string repoPath, string commitSha, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(Path.Combine(repoPath, ".git")))
        {
            return Result<IReadOnlyList<FileDiff>>.Failure(
                Error.Git.DiffFailed(commitSha, $"'{repoPath}' 不是有效的 Git 倉庫"));
        }

        // 取得異動檔案清單
        var nameStatusResult = await RunGitCommandAsync(
            $"diff-tree --no-commit-id -r --name-status {commitSha}",
            repoPath, cancellationToken);

        if (!nameStatusResult.IsSuccess)
        {
            return Result<IReadOnlyList<FileDiff>>.Failure(
                Error.Git.DiffFailed(commitSha, nameStatusResult.Error!.Message));
        }

        // 取得完整 diff
        var diffResult = await RunGitCommandAsync(
            $"show {commitSha} --format= --unified=3",
            repoPath, cancellationToken);

        if (!diffResult.IsSuccess)
        {
            return Result<IReadOnlyList<FileDiff>>.Failure(
                Error.Git.DiffFailed(commitSha, diffResult.Error!.Message));
        }

        var fileDiffs = ParseDiffOutput(nameStatusResult.Value!, diffResult.Value!, commitSha);
        return Result<IReadOnlyList<FileDiff>>.Success(fileDiffs);
    }

    /// <summary>
    /// 執行 git 命令
    /// </summary>
    private async Task<Result<string>> RunGitCommandAsync(string arguments, string workingDirectory, CancellationToken cancellationToken)
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

        if (process.ExitCode != 0)
        {
            _logger.LogError("git {Arguments} 失敗 (exit code {ExitCode}): {Error}",
                SanitizeUrl(arguments), process.ExitCode, error);
            return Result<string>.Failure(new Error("Git.CommandFailed", error.Trim()));
        }

        return Result<string>.Success(output);
    }

    /// <summary>
    /// 解析 diff 輸出為 FileDiff 清單
    /// </summary>
    internal static IReadOnlyList<FileDiff> ParseDiffOutput(string nameStatusOutput, string diffOutput, string commitSha)
    {
        var fileDiffs = new List<FileDiff>();
        var fileChanges = ParseNameStatus(nameStatusOutput);
        var fileDiffContents = SplitDiffByFile(diffOutput);

        foreach (var (changeType, filePath) in fileChanges)
        {
            var diffContent = fileDiffContents.GetValueOrDefault(filePath, string.Empty);
            fileDiffs.Add(new FileDiff
            {
                FilePath = filePath,
                ChangeType = changeType,
                DiffContent = diffContent,
                CommitSha = commitSha
            });
        }

        return fileDiffs;
    }

    /// <summary>
    /// 解析 git diff-tree --name-status 的輸出
    /// </summary>
    private static List<(ChangeType ChangeType, string FilePath)> ParseNameStatus(string output)
    {
        var results = new List<(ChangeType, string)>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t', 2);
            if (parts.Length < 2) continue;

            var changeType = parts[0].Trim() switch
            {
                "A" => ChangeType.Added,
                "D" => ChangeType.Deleted,
                _ => ChangeType.Modified
            };

            results.Add((changeType, parts[1].Trim()));
        }
        return results;
    }

    /// <summary>
    /// 將完整 diff 輸出依檔案拆分
    /// </summary>
    private static Dictionary<string, string> SplitDiffByFile(string diffOutput)
    {
        var result = new Dictionary<string, string>();
        var diffPattern = new Regex(@"^diff --git a/(.+?) b/", RegexOptions.Multiline);
        var matches = diffPattern.Matches(diffOutput);

        for (var i = 0; i < matches.Count; i++)
        {
            var filePath = matches[i].Groups[1].Value;
            var startIndex = matches[i].Index;
            var endIndex = i + 1 < matches.Count ? matches[i + 1].Index : diffOutput.Length;
            result[filePath] = diffOutput[startIndex..endIndex].Trim();
        }

        return result;
    }

    /// <summary>
    /// 移除 URL 中的認證資訊（用於日誌記錄）
    /// </summary>
    private static string SanitizeUrl(string urlOrArgs)
    {
        return Regex.Replace(urlOrArgs, @"://[^@]+@", "://***@");
    }
}
```

- [ ] **Step 4: 執行測試確認通過**

Run: `cd src && dotnet test ../tests/ReleaseKit.Infrastructure.Tests --filter "GitOperationServiceTests" --no-restore -v q`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/ReleaseKit.Infrastructure/Git/ tests/ReleaseKit.Infrastructure.Tests/Git/
git commit -m "feat: 實作 GitOperationService（clone/pull/diff shell 呼叫）"
```

---

## Phase 3-6: 後續任務概要

> 以下為 Phase 3-6 的任務大綱。每個 Task 在實際實作時，均需依循 Phase 1-2 的 TDD 模式（撰寫失敗測試 → 確認失敗 → 實作 → 確認通過 → Commit）展開為完整的步驟。

### Task 11: 實作 CloneRepositoriesTask（Stage 1）

**Files:**
- Create: `src/ReleaseKit.Application/Tasks/CloneRepositoriesTask.cs`
- Test: `tests/ReleaseKit.Application.Tests/Tasks/CloneRepositoriesTaskTests.cs`

**要點：**
- 從 appsettings 讀取 GitLab + Bitbucket 專案清單
- 使用 `CloneUrlBuilder` 建構 clone URL
- 使用 `IGitOperationService.CloneOrPullAsync` 並行處理
- SemaphoreSlim 限制最大並行數（預設 3）
- 將每個專案的 clone 狀態存入 Redis（`Stage1Hash(runId)`, field=projectPath）
- RunId 由 `INow.UtcNow` 產生 `yyyyMMddHHmmss` 格式
- RunId 同時存入 Redis `CurrentRunIdKey` 供後續 Stage 使用

---

### Task 12: 實作 AnalyzePRDiffsTask（Stage 2）

**Files:**
- Create: `src/ReleaseKit.Application/Tasks/AnalyzePRDiffsTask.cs`
- Test: `tests/ReleaseKit.Application.Tests/Tasks/AnalyzePRDiffsTaskTests.cs`

**要點：**
- 從 Redis 讀取 `CurrentRunIdKey` 取得 runId
- 從 Redis 讀取 GitLab/Bitbucket 的 PR 資料（使用現有 RedisKeys）
- 提取每個 MergeRequest 的 MergeCommitSha
- 使用 `IGitOperationService.GetCommitDiffAsync` 取得 diff
- 將 ProjectDiffResult 存入 Redis（`Stage2Hash(runId)`, field=projectPath）
- 無 PR 資料的專案直接跳過，記錄日誌

---

### Task 13: 實作 ProjectStructureScanner（靜態分析核心）

**Files:**
- Create: `src/ReleaseKit.Infrastructure/Analysis/ProjectStructureScanner.cs`
- Test: `tests/ReleaseKit.Infrastructure.Tests/Analysis/ProjectStructureScannerTests.cs`

**要點：**
- 掃描 `.csproj` 檔案取得 NuGet 套件引用（XML 解析 `<PackageReference>`）
- 掃描 `*Controller.cs` 檔案解析 `[Route]`、`[HttpGet]` 等屬性取得 API endpoint
- 掃描 `*DbContext.cs` 與 `Migrations/` 目錄
- 掃描含 `Event`、`Message`、`Command` 的類別定義
- 掃描 `appsettings*.json` 取得設定 key 結構
- 測試使用 temp 目錄建立模擬的專案結構檔案

---

### Task 14: 實作 DependencyInferrer（相依性推斷）

**Files:**
- Create: `src/ReleaseKit.Infrastructure/Analysis/DependencyInferrer.cs`
- Test: `tests/ReleaseKit.Infrastructure.Tests/Analysis/DependencyInferrerTests.cs`

**要點：**
- 從 NuGet 套件清單推斷共用套件（跨專案比對同名套件）
- 從程式碼中的 HttpClient/RestClient 呼叫推斷 HTTP API 相依（正則掃描 URL 模式）
- 從 appsettings 中的 ConnectionString 推斷共用資料庫（提取 database 名稱）
- 從程式碼中的 MQ topic/queue 名稱推斷共用訊息通道
- 輸出 `ServiceDependency` 清單

---

### Task 15: 實作 StaticProjectAnalysisTask（Stage 3）

**Files:**
- Create: `src/ReleaseKit.Application/Tasks/StaticProjectAnalysisTask.cs`
- Test: `tests/ReleaseKit.Application.Tests/Tasks/StaticProjectAnalysisTaskTests.cs`

**要點：**
- 從 Redis 讀取 runId 與 Stage 1 clone 結果（取得本地路徑）
- 對每個已 clone 的專案執行 `ProjectStructureScanner` 掃描
- 執行 `DependencyInferrer` 推斷相依性
- 將 `ProjectStructure` 存入 Redis（`Stage3Hash(runId)`, field=projectPath）

---

### Task 16: 實作 CopilotRiskAnalyzer 與 RiskAnalysisPromptBuilder

**Files:**
- Create: `src/ReleaseKit.Infrastructure/Copilot/CopilotRiskAnalyzer.cs`
- Create: `src/ReleaseKit.Infrastructure/Copilot/RiskAnalysisPromptBuilder.cs`
- Test: `tests/ReleaseKit.Infrastructure.Tests/Copilot/RiskAnalysisPromptBuilderTests.cs`

**要點：**
- `RiskAnalysisPromptBuilder` 負責建構 System Prompt 與 User Prompt
  - System Prompt：角色定義、技術棧、分析規則、JSON 輸出格式
  - User Prompt：專案資訊 + diff 片段 + 靜態分析結果 + 分析指令
- `CopilotRiskAnalyzer` 封裝 Copilot SDK session 管理
  - 自動估算 token 數（字元數 / 4 近似）
  - 超過閾值自動拆分 session（先按情境，再按檔案群組）
  - 解析 JSON 回應為 `RiskFinding` 清單
  - 容錯：解析失敗記錄原始回應、逾時重試一次
- 沿用現有 `CopilotTitleEnhancer` 的 CopilotClient 使用模式

---

### Task 17: 實作 CopilotRiskAnalysisTask（Stage 4）

**Files:**
- Create: `src/ReleaseKit.Application/Tasks/CopilotRiskAnalysisTask.cs`
- Test: `tests/ReleaseKit.Application.Tests/Tasks/CopilotRiskAnalysisTaskTests.cs`

**要點：**
- 從 Redis 讀取 Stage 2（diff）與 Stage 3（靜態分析）結果
- 對每個有異動的專案呼叫 `CopilotRiskAnalyzer`
- 將每個 session 的結果存入 Redis（`Stage4Hash(runId)`, field=`{projectPath}:{sessionIdx}`）
- 彙整所有 session 結果為 `ProjectRiskAnalysis`

---

### Task 18: 實作 CrossProjectCorrelationTask（Stage 5）

**Files:**
- Create: `src/ReleaseKit.Application/Tasks/CrossProjectCorrelationTask.cs`
- Test: `tests/ReleaseKit.Application.Tests/Tasks/CrossProjectCorrelationTaskTests.cs`

**要點：**
- 從 Redis 讀取 Stage 3（靜態分析）與 Stage 4（Copilot 分析）結果
- 建立相依圖（`DependencyEdge` 清單）
- 交叉比對：若 ProjectA 修改了某 API，且 ProjectB 呼叫該 API → 確認影響
- 風險評分：根據變更類型調整最終風險等級
- 識別通知對象：從 PR author 取得變更者、從受影響專案的近期 PR author 取得負責人
- 將 `CrossProjectCorrelation` 存入 Redis（`Stage5Hash(runId)`, field=`Correlation`）

---

### Task 19: 實作 MarkdownReportGenerator

**Files:**
- Create: `src/ReleaseKit.Infrastructure/Reporting/MarkdownReportGenerator.cs`
- Test: `tests/ReleaseKit.Infrastructure.Tests/Reporting/MarkdownReportGeneratorTests.cs`

**要點：**
- 接收 `RiskReport` 資料模型，輸出 Markdown 字串
- 報告結構：風險摘要表格 → 通知清單 → Mermaid 相依圖 → 高/中/低風險詳情 → 需人工檢視
- 每個風險項包含：變更者、異動檔案、diff 片段、受影響專案、需通知人員、建議動作
- 測試驗證：產出的 Markdown 包含預期的標題、表格、diff 片段

---

### Task 20: 實作 GenerateRiskReportTask（Stage 6）

**Files:**
- Create: `src/ReleaseKit.Application/Tasks/GenerateRiskReportTask.cs`
- Test: `tests/ReleaseKit.Application.Tests/Tasks/GenerateRiskReportTaskTests.cs`

**要點：**
- 從 Redis 讀取 Stage 5 結果
- 呼叫 `MarkdownReportGenerator` 產生報告
- 將 Markdown 報告輸出至 Console 並存入 Redis（`Stage6Hash(runId)`, field=`Report`）
- 同時寫入本地檔案（路徑由 appsettings 設定或使用預設路徑）

---

### Task 21: 更新 TaskFactory 與 ServiceCollectionExtensions

**Files:**
- Modify: `src/ReleaseKit.Application/Tasks/TaskFactory.cs`
- Modify: `src/ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs`
- Modify: `tests/ReleaseKit.Application.Tests/Tasks/TaskFactoryTests.cs`

**要點：**
- TaskFactory 新增 6 個 TaskType → Task 實例的映射
- ServiceCollectionExtensions 新增：
  - `services.Configure<RiskAnalysisOptions>(configuration.GetSection("RiskAnalysis"))`
  - `services.AddTransient<IGitOperationService, GitOperationService>()`
  - `services.AddTransient<CloneRepositoriesTask>()`
  - `services.AddTransient<AnalyzePRDiffsTask>()`
  - `services.AddTransient<StaticProjectAnalysisTask>()`
  - `services.AddTransient<CopilotRiskAnalysisTask>()`
  - `services.AddTransient<CrossProjectCorrelationTask>()`
  - `services.AddTransient<GenerateRiskReportTask>()`
- TaskFactory 測試新增對 6 個新 TaskType 的驗證

---

### Task 22: 更新 appsettings 與最終驗證

**Files:**
- Modify: `src/ReleaseKit.Console/appsettings.json`
- Modify: `src/ReleaseKit.Console/appsettings.Sample.json`

**要點：**
- 在 appsettings.json 與 appsettings.Sample.json 新增 `RiskAnalysis` section
- 執行完整建置：`dotnet build`
- 執行所有測試：`dotnet test`
- 確認所有測試通過，無回歸

---

## 驗證清單

| 驗證項目 | 指令 |
|---------|------|
| 建置成功 | `cd src && dotnet build --no-restore` |
| 所有測試通過 | `cd src && dotnet test --no-restore` |
| 新任務類型可解析 | `dotnet run --project src/ReleaseKit.Console -- clone-repositories` (dry run) |
| Redis Key 格式正確 | 檢查 Redis 中 `RiskAnalysis:*` key 結構 |
