# Release 風險分析功能 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在現有 Release-Kit .NET 10 Console 應用程式中新增風險分析功能，利用 AI (GitHub Copilot SDK) 分析跨微服務 PR 變更的潛在風險。

**Architecture:** Clean Architecture 分層設計 — Domain 層定義實體/VO/介面，Application 層實作 6 個 Task（Clone → Extract Diffs → Analyze Project → Analyze Cross-Project → Generate Report → Orchestrator），Infrastructure 層實作 GitService（git 命令）和 CopilotRiskAnalyzer（Copilot SDK），Console 層註冊 CLI 命令與 DI。Redis 作為各 Task 間的資料中

**Tech Stack:** .NET 10, xUnit + Moq, GitHub Copilot SDK, System.Diagnostics.Process (git), StackExchange.Redis, System.Text.Json

---

## File Structure

### New Files to Create

**Domain Layer:**
- `src/ReleaseKit.Domain/ValueObjects/RiskLevel.cs` — .dockerignore .editorconfig .git .gitattributes .github .gitignore .opencode .specify .vscode AGENTS.md CLAUDE.md DOCKER.md Dockerfile README.md appsettings.docker.json docker-compose.yml docs src tests  enum
- `src/ReleaseKit.Domain/ValueObjects/RiskCategory.cs` — 風險類別 enum
- `src/ReleaseKit.Domain/ValueObjects/AnalysisPassKey.cs` — 分析階段金鑰 VO
- `src/ReleaseKit.Domain/Entities/RiskItem.cs` — 單一風險項目 record
- `src/ReleaseKit.Domain/Entities/RiskAnalysisReport.cs` — 風險分析報告聚合根
- `src/ReleaseKit.Domain/Entities/PrDiffContext.cs` — PR Diff 上下文 DTO
- `src/ReleaseKit.Domain/Entities/DynamicAnalysisResult.cs` — 動態分析結果
- `src/ReleaseKit.Domain/Abstractions/IGitService.cs` — Git 操作介面
- `src/ReleaseKit.Domain/Abstractions/IRiskAnalyzer.cs` — AI 風險分析介面

**Common Layer:**
-  — 風險分析組

**Application Layer:**
- `src/ReleaseKit.Application/Tasks/CloneRepositoriesTask.cs` — Clone 任務
- `src/ReleaseKit.Application/Tasks/ExtractPrDiffsTask.cs` — 擷取 Diff 任務
- `src/ReleaseKit.Application/Tasks/AnalyzeProjectRiskTask.cs` — Per-Project 分析
- `src/ReleaseKit.Application/Tasks/AnalyzeCrossProjectRiskTask.cs` — 動態深度分析
- `src/ReleaseKit.Application/Tasks/GenerateRiskReportTask.cs` — 報告產出
- `src/ReleaseKit.Application/Tasks/AnalyzeRiskTask.cs` — Orchestrator

**Infrastructure Layer:**
- `src/ReleaseKit.Infrastructure/Git/GitService.cs` — Git 操作實作

**Test Files:**
- `tests/ReleaseKit.Domain.Tests/ValueObjects/RiskLevelTests.cs`
- `tests/ReleaseKit.Domain.Tests/ValueObjects/RiskCategoryTests.cs`
- `tests/ReleaseKit.Domain.Tests/ValueObjects/AnalysisPassKeyTests.cs`
- `tests/ReleaseKit.Domain.Tests/Entities/RiskItemTests.cs`
- `tests/ReleaseKit.Domain.Tests/Entities/RiskAnalysisReportTests.cs`
- `tests/ReleaseKit.Domain.Tests/Entities/PrDiffContextTests.cs`
- `tests/ReleaseKit.Domain.Tests/Entities/DynamicAnalysisResultTests.cs`
- `tests/ReleaseKit.Application.Tests/Tasks/CloneRepositoriesTaskTests.cs`
- `tests/ReleaseKit.Application.Tests/Tasks/ExtractPrDiffsTaskTests.cs`
- `tests/ReleaseKit.Application.Tests/Tasks/AnalyzeProjectRiskTaskTests.cs`
- `tests/ReleaseKit.Application.Tests/Tasks/AnalyzeCrossProjectRiskTaskTests.cs`
- `tests/ReleaseKit.Application.Tests/Tasks/GenerateRiskReportTaskTests.cs`
- `tests/ReleaseKit.Application.Tests/Tasks/AnalyzeRiskTaskTests.cs`
- `tests/ReleaseKit.Infrastructure.Tests/Git/GitServiceTests.cs`
- `tests/ReleaseKit.Console.Tests/Parsers/CommandLineParserRiskTests.cs`

### Files to Modify

- `src/ReleaseKit.Common/Constants/RedisKeys.cs` — 新增 RiskAnalysis hash 與 fields
- `src/ReleaseKit.Application/Tasks/TaskType.cs` — 新增 6 個 TaskType enum 值
- `src/ReleaseKit.Application/Tasks/TaskFactory.cs` — 註冊新 Task 建立邏輯
- `src/ReleaseKit.Console/Parsers/CommandLineParser.cs` — 新增 6 個 CLI 命令對應
- `src/ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs` — DI 註冊新服務
- `src/ReleaseKit.Domain/Common/Error.cs` — 新增 RiskAnalysis 錯誤定義

---

### Task 1: Domain Value Objects — RiskLevel, RiskCategory

**Files:**
- Create: `src/ReleaseKit.Domain/ValueObjects/RiskLevel.cs`
- Create: `src/ReleaseKit.Domain/ValueObjects/RiskCategory.cs`
- Create: `tests/ReleaseKit.Domain.Tests/ValueObjects/RiskLevelTests.cs`
- Create: `tests/ReleaseKit.Domain.Tests/ValueObjects/RiskCategoryTests.cs`

- [ ] **Step 1: Write RiskLevel tests**

```csharp
// tests/ReleaseKit.Domain.Tests/ValueObjects/RiskLevelTests.cs
namespace ReleaseKit.Domain.Tests.ValueObjects;

using ReleaseKit.Domain.ValueObjects;

/// <summary>
/// RiskLevel 值物件單元測試
/// </summary>
public class RiskLevelTests
{
    [Fact]
    public void RiskLevel_ShouldHaveThreeValues()
    {
        // Assert
        var values = Enum.GetValues<RiskLevel>();
        Assert.Equal(3, values.Length);
    }

    [Theory]
    [InlineData(RiskLevel.High, 0)]
    [InlineData(RiskLevel.Medium, 1)]
    [InlineData(RiskLevel.Low, 2)]
    public void RiskLevel_ShouldHaveCorrectOrdinalValues(RiskLevel level, int expected)
    {
        Assert.Equal(expected, (int)level);
    }
}
```

- [ ] **Step 2: Write RiskCategory tests**

```csharp
// tests/ReleaseKit.Domain.Tests/ValueObjects/RiskCategoryTests.cs
namespace ReleaseKit.Domain.Tests.ValueObjects;

using ReleaseKit.Domain.ValueObjects;

/// <summary>
/// RiskCategory 值物件單元測試
/// </summary>
public class RiskCategoryTests
{
    [Fact]
    public void RiskCategory_ShouldHaveFiveValues()
    {
        var values = Enum.GetValues<RiskCategory>();
        Assert.Equal(5, values.Length);
    }

    [Theory]
    [InlineData(RiskCategory.ApiContract, 0)]
    [InlineData(RiskCategory.DatabaseSchema, 1)]
    [InlineData(RiskCategory.DatabaseData, 2)]
    [InlineData(RiskCategory.EventFormat, 3)]
    [InlineData(RiskCategory.Configuration, 4)]
    public void RiskCategory_ShouldHaveCorrectOrdinalValues(RiskCategory category, int expected)
    {
        Assert.Equal(expected, (int)category);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `cd src && dotnet test ../tests/ReleaseKit.Domain.Tests --filter "FullyQualifiedName~RiskLevel|FullyQualifiedName~RiskCategory" --no-restore 2>&1 | tail -20`
Expected: FAIL — types `RiskLevel` and `RiskCategory` do not exist

- [ ] **Step 4: Implement RiskLevel**

```csharp
// src/ReleaseKit.Domain/ValueObjects/RiskLevel.cs
namespace ReleaseKit.Domain.ValueObjects;

/// <summary>
/// 風險等級
/// </summary>
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

- [ ] **Step 5: Implement RiskCategory**

```csharp
// src/ReleaseKit.Domain/ValueObjects/RiskCategory.cs
namespace ReleaseKit.Domain.ValueObjects;

/// <summary>
/// 風險類別
/// </summary>
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

- [ ] **Step 6: Run tests to verify they pass**

Run: `cd src && dotnet test ../tests/ReleaseKit.Domain.Tests --filter "FullyQualifiedName~RiskLevel|FullyQualifiedName~RiskCategory" --no-restore 2>&1 | tail -20`
Expected: PASS — all 8 tests pass

- [ ] **Step 7: Commit**

```bash
git add src/ReleaseKit.Domain/ValueObjects/RiskLevel.cs src/ReleaseKit.Domain/ValueObjects/RiskCategory.cs tests/ReleaseKit.Domain.Tests/ValueObjects/
git commit -m "feat(domain): 新增 RiskLevel 與 RiskCategory 值物件

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 2: Domain Value Object — AnalysisPassKey

**Files:**
- Create: `src/ReleaseKit.Domain/ValueObjects/AnalysisPassKey.cs`
- Create: `tests/ReleaseKit.Domain.Tests/ValueObjects/AnalysisPassKeyTests.cs`

- [ ] **Step 1: Write AnalysisPassKey tests**

```csharp
// tests/ReleaseKit.Domain.Tests/ValueObjects/AnalysisPassKeyTests.cs
namespace ReleaseKit.Domain.Tests.ValueObjects;

using ReleaseKit.Domain.ValueObjects;

/// <summary>
/// AnalysisPassKey 值物件單元測試
/// </summary>
public class AnalysisPassKeyTests
{
    [Fact]
    public void ToRedisField_WithPassAndSequence_ShouldReturnCorrectFormat()
    {
        // Arrange
        var key = new AnalysisPassKey { Pass = 1, Sequence = 3 };

        // Act
        var result = key.ToRedisField();

        // Assert
        Assert.Equal("Intermediate:1-3", result);
    }

    [Fact]
    public void ToRedisField_WithSubSequence_ShouldIncludeSubSequence()
    {
        // Arrange
        var key = new AnalysisPassKey { Pass = 1, Sequence = 3, SubSequence = "a" };

        // Act
        var result = key.ToRedisField();

        // Assert
        Assert.Equal("Intermediate:1-3-a", result);
    }

    [Fact]
    public void ToRedisField_WithNullSubSequence_ShouldOmitSubSequence()
    {
        // Arrange
        var key = new AnalysisPassKey { Pass = 2, Sequence = 1, SubSequence = null };

        // Act
        var result = key.ToRedisField();

        // Assert
        Assert.Equal("Intermediate:2-1", result);
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var key1 = new AnalysisPassKey { Pass = 1, Sequence = 2, SubSequence = "b" };
        var key2 = new AnalysisPassKey { Pass = 1, Sequence = 2, SubSequence = "b" };

        Assert.Equal(key1, key2);
    }

    [Fact]
    public void Equality_DifferentValues_ShouldNotBeEqual()
    {
        var key1 = new AnalysisPassKey { Pass = 1, Sequence = 2 };
        var key2 = new AnalysisPassKey { Pass = 1, Sequence = 3 };

        Assert.NotEqual(key1, key2);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd src && dotnet test ../tests/ReleaseKit.Domain.Tests --filter "FullyQualifiedName~AnalysisPassKey" --no-restore 2>&1 | tail -20`
Expected: FAIL — type `AnalysisPassKey` does not exist

- [ ] **Step 3: Implement AnalysisPassKey**

```csharp
// src/ReleaseKit.Domain/ValueObjects/AnalysisPassKey.cs
namespace ReleaseKit.Domain.ValueObjects;

/// <summary>
/// 分析階段金鑰（如 "1-3", "2-1", "1-3-a"）
/// </summary>
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

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd src && dotnet test ../tests/ReleaseKit.Domain.Tests --filter "FullyQualifiedName~AnalysisPassKey" --no-restore 2>&1 | tail -20`
Expected: PASS — all 5 tests pass

- [ ] **Step 5: Commit**

```bash
git add src/ReleaseKit.Domain/ValueObjects/AnalysisPassKey.cs tests/ReleaseKit.Domain.Tests/ValueObjects/AnalysisPassKeyTests.cs
git commit -m "feat(domain): 新增 AnalysisPassKey 值物件

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 3: Domain Entities — RiskItem, RiskAnalysisReport, PrDiffContext, DynamicAnalysisResult

**Files:**
- Create: `src/ReleaseKit.Domain/Entities/RiskItem.cs`
- Create: `src/ReleaseKit.Domain/Entities/RiskAnalysisReport.cs`
- Create: `src/ReleaseKit.Domain/Entities/PrDiffContext.cs`
- Create: `src/ReleaseKit.Domain/Entities/DynamicAnalysisResult.cs`
- Create: `tests/ReleaseKit.Domain.Tests/Entities/RiskItemTests.cs`
- Create: `tests/ReleaseKit.Domain.Tests/Entities/RiskAnalysisReportTests.cs`
- Create: `tests/ReleaseKit.Domain.Tests/Entities/PrDiffContextTests.cs`
- Create: `tests/ReleaseKit.Domain.Tests/Entities/DynamicAnalysisResultTests.cs`

- [ ] **Step 1: Write entity tests**

```csharp
// tests/ReleaseKit.Domain.Tests/Entities/RiskItemTests.cs
namespace ReleaseKit.Domain.Tests.Entities;

using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

/// <summary>
/// RiskItem 實體單元測試
/// </summary>
public class RiskItemTests
{
    [Fact]
    public void RiskItem_ShouldBeCreatedWithRequiredProperties()
    {
        var item = new RiskItem
        {
            Category = RiskCategory.ApiContract,
            Level = RiskLevel.High,
            ChangeSummary = "修改了 /api/v1/orders 的 Response 模型",
            AffectedFiles = new List<string> { "Controllers/OrderController.cs" },
            PotentiallyAffectedServices = new List<string> { "ServiceB" },
            ImpactDescription = "移除了 legacyOrderId 欄位",
            SuggestedValidationSteps = new List<string> { "確認 ServiceB 的呼叫邏輯" }
        };

        Assert.Equal(RiskCategory.ApiContract, item.Category);
        Assert.Equal(RiskLevel.High, item.Level);
        Assert.Null(item.SourceProject);
        Assert.Null(item.AffectedProject);
    }

    [Fact]
    public void RiskItem_WithOptionalProperties_ShouldSetCorrectly()
    {
        var item = new RiskItem
        {
            Category = RiskCategory.DatabaseData,
            Level = RiskLevel.Medium,
            ChangeSummary = "變更 Lookup table 資料",
            AffectedFiles = new List<string> { "Migrations/AddStatus.sql" },
            PotentiallyAffectedServices = new List<string> { "ServiceA", "ServiceC" },
            ImpactDescription = "新增狀態碼可能導致 switch/case 未涵蓋",
            SuggestedValidationSteps = new List<string> { "檢查 switch/case" },
            SourceProject = "ProjectA",
            AffectedProject = "ProjectB"
        };

        Assert.Equal("ProjectA", item.SourceProject);
        Assert.Equal("ProjectB", item.AffectedProject);
    }

    [Fact]
    public void RiskItem_Equality_SameValues_ShouldBeEqual()
    {
        var item1 = CreateTestRiskItem();
        var item2 = CreateTestRiskItem();

        Assert.Equal(item1, item2);
    }

    private static RiskItem CreateTestRiskItem() => new()
    {
        Category = RiskCategory.Configuration,
        Level = RiskLevel.Low,
        ChangeSummary = "修改 appsettings",
        AffectedFiles = new List<string> { "appsettings.json" },
        PotentiallyAffectedServices = new List<string>(),
        ImpactDescription = "鍵值變更",
        SuggestedValidationSteps = new List<string> { "驗證設定" }
    };
}
```

```csharp
// tests/ReleaseKit.Domain.Tests/Entities/RiskAnalysisReportTests.cs
namespace ReleaseKit.Domain.Tests.Entities;

using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

/// <summary>
/// RiskAnalysisReport 聚合根單元測試
/// </summary>
public class RiskAnalysisReportTests
{
    [Fact]
    public void RiskAnalysisReport_ShouldBeCreatedWithRequiredProperties()
    {
        var report = new RiskAnalysisReport
        {
            PassKey = new AnalysisPassKey { Pass = 1, Sequence = 1 },
            RiskItems = new List<RiskItem>(),
            Summary = "無風險項目",
            AnalyzedAt = DateTimeOffset.UtcNow
        };

        Assert.NotNull(report.PassKey);
        Assert.Empty(report.RiskItems);
        Assert.Null(report.ProjectName);
        Assert.Null(report.Category);
    }

    [Fact]
    public void RiskAnalysisReport_Pass1_ShouldHaveProjectName()
    {
        var report = new RiskAnalysisReport
        {
            PassKey = new AnalysisPassKey { Pass = 1, Sequence = 1 },
            ProjectName = "ServiceA",
            RiskItems = new List<RiskItem>(),
            Summary = "ServiceA 分析完成",
            AnalyzedAt = DateTimeOffset.UtcNow
        };

        Assert.Equal("ServiceA", report.ProjectName);
    }

    [Fact]
    public void RiskAnalysisReport_Pass2_ShouldHaveCategory()
    {
        var report = new RiskAnalysisReport
        {
            PassKey = new AnalysisPassKey { Pass = 2, Sequence = 1 },
            Category = RiskCategory.ApiContract,
            RiskItems = new List<RiskItem>(),
            Summary = "API 契約風險分析",
            AnalyzedAt = DateTimeOffset.UtcNow
        };

        Assert.Equal(RiskCategory.ApiContract, report.Category);
    }
}
```

```csharp
// tests/ReleaseKit.Domain.Tests/Entities/PrDiffContextTests.cs
namespace ReleaseKit.Domain.Tests.Entities;

using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

/// <summary>
/// PrDiffContext 實體單元測試
/// </summary>
public class PrDiffContextTests
{
    [Fact]
    public void PrDiffContext_ShouldBeCreatedWithRequiredProperties()
    {
        var context = new PrDiffContext
        {
            Title = "修改 API endpoint",
            SourceBranch = "feature/VSTS12345-api-change",
            TargetBranch = "develop",
            AuthorName = "developer1",
            PrUrl = "https://gitlab.example.com/project/-/merge_requests/1",
            DiffContent = "diff --git a/file.cs b/file.cs\n...",
            ChangedFiles = new List<string> { "Controllers/OrderController.cs" },
            Platform = SourceControlPlatform.GitLab
        };

        Assert.Equal("修改 API endpoint", context.Title);
        Assert.Null(context.Description);
        Assert.Equal(SourceControlPlatform.GitLab, context.Platform);
    }

    [Fact]
    public void PrDiffContext_WithDescription_ShouldSetCorrectly()
    {
        var context = new PrDiffContext
        {
            Title = "修改 DB Schema",
            Description = "新增 Status 欄位至 Orders table",
            SourceBranch = "feature/db-change",
            TargetBranch = "main",
            AuthorName = "developer2",
            PrUrl = "https://bitbucket.org/team/repo/pull-requests/1",
            DiffContent = "ALTER TABLE Orders ADD Status INT",
            ChangedFiles = new List<string> { "Migrations/001_AddStatus.sql" },
            Platform = SourceControlPlatform.Bitbucket
        };

        Assert.Equal("新增 Status 欄位至 Orders table", context.Description);
    }
}
```

```csharp
// tests/ReleaseKit.Domain.Tests/Entities/DynamicAnalysisResultTests.cs
namespace ReleaseKit.Domain.Tests.Entities;

using ReleaseKit.Domain.Entities;
using ReleaseKit.Domain.ValueObjects;

/// <summary>
/// DynamicAnalysisResult 實體單元測試
/// </summary>
public class DynamicAnalysisResultTests
{
    [Fact]
    public void DynamicAnalysisResult_ContinueAnalysis_ShouldIndicateMorePasses()
    {
        var result = new DynamicAnalysisResult
        {
            Reports = new List<RiskAnalysisReport>(),
            ContinueAnalysis = true,
            ContinueReason = "發現需要進一步交叉比對的風險",
            AnalysisStrategy = "按風險類別分組交叉比對"
        };

        Assert.True(result.ContinueAnalysis);
        Assert.NotNull(result.ContinueReason);
    }

    [Fact]
    public void DynamicAnalysisResult_StopAnalysis_ShouldIndicateComplete()
    {
        var result = new DynamicAnalysisResult
        {
            Reports = new List<RiskAnalysisReport>(),
            ContinueAnalysis = false,
            ContinueReason = null,
            AnalysisStrategy = "最終驗證"
        };

        Assert.False(result.ContinueAnalysis);
        Assert.Null(result.ContinueReason);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd src && dotnet test ../tests/ReleaseKit.Domain.Tests --filter "FullyQualifiedName~RiskItem|FullyQualifiedName~RiskAnalysisReport|FullyQualifiedName~PrDiffContext|FullyQualifiedName~DynamicAnalysisResult" --no-restore 2>&1 | tail -20`
Expected: FAIL — entity types do not exist

- [ ] **Step 3: Implement RiskItem**

```csharp
// src/ReleaseKit.Domain/Entities/RiskItem.cs
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Entities;

/// <summary>
/// 單一風險項目
/// </summary>
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

- [ ] **Step 4: Implement RiskAnalysisReport**

```csharp
// src/ReleaseKit.Domain/Entities/RiskAnalysisReport.cs
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Entities;

/// <summary>
/// 風險分析報告
/// </summary>
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

- [ ] **Step 5: Implement PrDiffContext**

```csharp
// src/ReleaseKit.Domain/Entities/PrDiffContext.cs
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Domain.Entities;

/// <summary>
/// PR Diff 上下文資訊
/// </summary>
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

- [ ] **Step 6: Implement DynamicAnalysisResult**

```csharp
// src/ReleaseKit.Domain/Entities/DynamicAnalysisResult.cs
namespace ReleaseKit.Domain.Entities;

/// <summary>
/// 動態分析結果（包含是否繼續分析的 AI 判斷）
/// </summary>
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

- [ ] **Step 7: Run tests to verify they pass**

Run: `cd src && dotnet test ../tests/ReleaseKit.Domain.Tests --filter "FullyQualifiedName~RiskItem|FullyQualifiedName~RiskAnalysisReport|FullyQualifiedName~PrDiffContext|FullyQualifiedName~DynamicAnalysisResult" --no-restore 2>&1 | tail -20`
Expected: PASS — all entity tests pass

- [ ] **Step 8: Commit**

```bash
git add src/ReleaseKit.Domain/Entities/RiskItem.cs src/ReleaseKit.Domain/Entities/RiskAnalysisReport.cs src/ReleaseKit.Domain/Entities/PrDiffContext.cs src/ReleaseKit.Domain/Entities/DynamicAnalysisResult.cs tests/ReleaseKit.Domain.Tests/Entities/
git commit -m "feat(domain): 新增風險分析相關實體

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 4: Domain Abstractions — IGitService, IRiskAnalyzer + Error definitions

**Files:**
- Create: `src/ReleaseKit.Domain/Abstractions/IGitService.cs`
- Create: `src/ReleaseKit.Domain/Abstractions/IRiskAnalyzer.cs`
- Modify: `src/ReleaseKit.Domain/Abstractions/IRedisService.cs` — 新增 HashGetAllAsync 與 HashFieldsAsync
- Modify: `src/ReleaseKit.Domain/Common/Error.cs`

- [ ] **Step 1: Extend IRedisService with field enumeration methods**

In `src/ReleaseKit.Domain/Abstractions/IRedisService.cs`, add the following methods:

```csharp
    /// <summary>
    /// 取得 Hash 所有欄位名稱
    /// </summary>
    /// <param name="hashKey">Hash 鍵值</param>
    /// <returns>所有欄位名稱清單</returns>
    Task<IReadOnlyList<string>> HashFieldsAsync(string hashKey);

    /// <summary>
    /// 取得 Hash 中符合前綴的所有欄位值
    /// </summary>
    /// <param name="hashKey">Hash 鍵值</param>
    /// <param name="fieldPrefix">欄位名稱前綴</param>
    /// <returns>符合前綴的欄位名稱與值字典</returns>
    Task<IReadOnlyDictionary<string, string>> HashGetByPrefixAsync(string hashKey, string fieldPrefix);
```

Also implement these in `src/ReleaseKit.Infrastructure/Redis/RedisService.cs` (using `HashGetAllAsync` from StackExchange.Redis and filtering by prefix).

- [ ] **Step 2: Implement IGitService**

```csharp
// src/ReleaseKit.Domain/Abstractions/IGitService.cs
using ReleaseKit.Domain.Common;

namespace ReleaseKit.Domain.Abstractions;

/// <summary>
/// Git 操作服務介面
/// </summary>
public interface IGitService
{
    /// <summary>完整 Clone 指定 repository（含 fetch --all）</summary>
    Task<Result<string>> CloneRepositoryAsync(
        string repoUrl,
        string targetPath,
        CancellationToken cancellationToken = default);

    /// <summary>取得指定兩個 branch 之間的 diff（git diff baseBranch...headBranch）</summary>
    Task<Result<string>> GetBranchDiffAsync(
        string repoPath,
        string baseBranch,
        string headBranch,
        CancellationToken cancellationToken = default);

    /// <summary>透過 merge commit 訊息搜尋 merge commit SHA（分支刪除時的 fallback）</summary>
    Task<Result<string>> FindMergeCommitAsync(
        string repoPath,
        string branchName,
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

- [ ] **Step 3: Implement IRiskAnalyzer**

```csharp
// src/ReleaseKit.Domain/Abstractions/IRiskAnalyzer.cs
using ReleaseKit.Domain.Entities;

namespace ReleaseKit.Domain.Abstractions;

/// <summary>
/// AI 風險分析服務介面
/// </summary>
public interface IRiskAnalyzer
{
    /// <summary>分析單一專案的 PR 變更風險（Pass 1）</summary>
    Task<RiskAnalysisReport> AnalyzeProjectRiskAsync(
        string projectName,
        IReadOnlyList<PrDiffContext> diffs,
        CancellationToken cancellationToken = default);

    /// <summary>動態深度分析：接收前一層報告，產出下一層分析（Pass 2~10）</summary>
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

- [ ] **Step 3: Add RiskAnalysis error definitions to Error.cs**

In `src/ReleaseKit.Domain/Common/Error.cs`, add the following nested class **after** the existing `AzureDevOps` class:

```csharp
    public static class RiskAnalysis
    {
        public static Error CloneFailed(string repoUrl) =>
            new("RiskAnalysis.CloneFailed", $"Clone 失敗：{repoUrl}");

        public static Error DiffExtractionFailed(string project) =>
            new("RiskAnalysis.DiffExtractionFailed", $"Diff 擷取失敗：{project}");

        public static Error AiAnalysisFailed(string reason) =>
            new("RiskAnalysis.AiAnalysisFailed", $"AI 分析失敗：{reason}");

        public static Error GitCommandFailed(string command, string error) =>
            new("RiskAnalysis.GitCommandFailed", $"Git 命令失敗：{command}，錯誤：{error}");
    }
```

- [ ] **Step 4: Build to verify compilation**

Run: `cd src && dotnet build --no-restore 2>&1 | tail -10`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add src/ReleaseKit.Domain/Abstractions/IGitService.cs src/ReleaseKit.Domain/Abstractions/IRiskAnalyzer.cs src/ReleaseKit.Domain/Common/Error.cs
git commit -m "feat(domain): 新增 IGitService、IRiskAnalyzer 介面與 RiskAnalysis 錯誤定義

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 5: Common Layer — RedisKeys + RiskAnalysisOptions

**Files:**
- Modify: `src/ReleaseKit.Common/Constants/RedisKeys.cs`
- Create: `src/ReleaseKit.Common/Configuration/RiskAnalysisOptions.cs`

- [ ] **Step 1: Add RiskAnalysis Redis keys**

In `src/ReleaseKit.Common/Constants/RedisKeys.cs`, add the following constant **after** `ReleaseDataHash`:

```csharp
    /// <summary>
    /// 風險分析資料的 Redis Hash 鍵值
    /// </summary>
    public const string RiskAnalysisHash = "RiskAnalysis";
```

In the `Fields` class, add the following constants **after** `EnhancedTitles`:

```csharp
        /// <summary>
        /// Clone 路徑資訊欄位名稱
        /// </summary>
        public const string ClonePaths = "ClonePaths";

        /// <summary>
        /// PR Diff 資料欄位名稱
        /// </summary>
        public const string PrDiffs = "PrDiffs";

        /// <summary>
        /// 最終風險分析報告欄位名稱
        /// </summary>
        public const string FinalReport = "FinalReport";
```

- [ ] **Step 2: Create RiskAnalysisOptions**

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

    /// <summary>每次 AI 呼叫的最大 Token 數</summary>
    public int MaxTokensPerAiCall { get; init; } = 100000;

    /// <summary>動態分析最大層數（硬上限 10）</summary>
    public int MaxAnalysisPasses { get; init; } = 10;

    /// <summary>報告輸出路徑</summary>
    public required string ReportOutputPath { get; init; }
}
```

- [ ] **Step 3: Build to verify compilation**

Run: `cd src && dotnet build --no-restore 2>&1 | tail -10`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add src/ReleaseKit.Common/Constants/RedisKeys.cs src/ReleaseKit.Common/Configuration/RiskAnalysisOptions.cs
git commit -m "feat(common): 新增 RiskAnalysis Redis keys 與 RiskAnalysisOptions 組態

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 6: Application Layer — TaskType enum + CommandLineParser + TaskFactory modifications

**Files:**
- Modify: `src/ReleaseKit.Application/Tasks/TaskType.cs`
- Modify: `src/ReleaseKit.Console/Parsers/CommandLineParser.cs`
- Create: `tests/ReleaseKit.Console.Tests/Parsers/CommandLineParserRiskTests.cs`

- [ ] **Step 1: Write CommandLineParser risk tests**

```csharp
// tests/ReleaseKit.Console.Tests/Parsers/CommandLineParserRiskTests.cs
using ReleaseKit.Application.Tasks;
using ReleaseKit.Console.Parsers;

namespace ReleaseKit.Console.Tests.Parsers;

/// <summary>
/// CommandLineParser 風險分析命令單元測試
/// </summary>
public class CommandLineParserRiskTests
{
    private readonly CommandLineParser _parser = new();

    [Theory]
    [InlineData("clone-repos", TaskType.CloneRepositories)]
    [InlineData("extract-pr-diffs", TaskType.ExtractPrDiffs)]
    [InlineData("analyze-project-risk", TaskType.AnalyzeProjectRisk)]
    [InlineData("analyze-cross-project-risk", TaskType.AnalyzeCrossProjectRisk)]
    [InlineData("generate-risk-report", TaskType.GenerateRiskReport)]
    [InlineData("analyze-risk", TaskType.AnalyzeRisk)]
    public void Parse_WithRiskAnalysisCommand_ShouldReturnCorrectTaskType(string command, TaskType expected)
    {
        var result = _parser.Parse(new[] { command });

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.TaskType);
    }

    [Theory]
    [InlineData("CLONE-REPOS", TaskType.CloneRepositories)]
    [InlineData("ANALYZE-RISK", TaskType.AnalyzeRisk)]
    public void Parse_WithRiskAnalysisCommand_ShouldBeCaseInsensitive(string command, TaskType expected)
    {
        var result = _parser.Parse(new[] { command });

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.TaskType);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd src && dotnet test ../tests/ReleaseKit.Console.Tests --filter "FullyQualifiedName~CommandLineParserRiskTests" --no-restore 2>&1 | tail -20`
Expected: FAIL — enum values and command mappings do not exist

- [ ] **Step 3: Add new TaskType enum values**

In `src/ReleaseKit.Application/Tasks/TaskType.cs`, add the following values **after** `EnhanceTitles`:

```csharp
    /// <summary>
    /// Clone 所有相關 repository
    /// </summary>
    CloneRepositories,

    /// <summary>
    /// 擷取 PR Diff 資訊
    /// </summary>
    ExtractPrDiffs,

    /// <summary>
    /// Per-Project AI 風險分析
    /// </summary>
    AnalyzeProjectRisk,

    /// <summary>
    /// 動態深度跨專案風險分析
    /// </summary>
    AnalyzeCrossProjectRisk,

    /// <summary>
    /// 產生最終風險報
    /// </summary>
    GenerateRiskReport,

    /// <summary>
    /// 風險分析 Orchestrator（一鍵執行全部）
    /// </summary>
    AnalyzeRisk
```

- [ ] **Step 4: Add new CLI command mappings**

In `src/ReleaseKit.Console/Parsers/CommandLineParser.cs`, add the following entries to `_taskMappings` dictionary **after** the `enhance-titles` entry:

```csharp
        { "clone-repos", TaskType.CloneRepositories },
        { "extract-pr-diffs", TaskType.ExtractPrDiffs },
        { "analyze-project-risk", TaskType.AnalyzeProjectRisk },
        { "analyze-cross-project-risk", TaskType.AnalyzeCrossProjectRisk },
        { "generate-risk-report", TaskType.GenerateRiskReport },
        { "analyze-risk", TaskType.AnalyzeRisk },
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd src && dotnet test ../tests/ReleaseKit.Console.Tests --filter "FullyQualifiedName~CommandLineParserRiskTests" --no-restore 2>&1 | tail -20`
Expected: PASS — all 8 tests pass

- [ ] **Step 6: Commit**

```bash
git add src/ReleaseKit.Application/Tasks/TaskType.cs src/ReleaseKit.Console/Parsers/CommandLineParser.cs tests/ReleaseKit.Console.Tests/Parsers/CommandLineParserRiskTests.cs
git commit -m "feat: 新增風險分析 TaskType enum 與 CLI 命令對應

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 7: Infrastructure — GitService

**Files:**
- Create: `src/ReleaseKit.Infrastructure/Git/GitService.cs`
- Create: `tests/ReleaseKit.Infrastructure.Tests/Git/GitServiceTests.cs`

- [ ] **Step 1: Write GitService unit tests**

```csharp
// tests/ReleaseKit.Infrastructure.Tests/Git/GitServiceTests.cs
using Microsoft.Extensions.Logging;
using Moq;
using ReleaseKit.Infrastructure.Git;

namespace ReleaseKit.Infrastructure.Tests.Git;

/// <summary>
/// GitService 單元測試
/// </summary>
public class GitServiceTests
{
    private readonly Mock<ILogger<GitService>> _loggerMock = new();
    private readonly GitService _sut;

    public GitServiceTests()
    {
        _sut = new GitService(_loggerMock.Object);
    }

    [Fact]
    public async Task CloneRepositoryAsync_WithEmptyUrl_ShouldReturnFailure()
    {
        var result = await _sut.CloneRepositoryAsync("", "/tmp/test", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Clone 失敗", result.Error!.Message);
    }

    [Fact]
    public async Task CloneRepositoryAsync_WithEmptyTargetPath_ShouldReturnFailure()
    {
        var result = await _sut.CloneRepositoryAsync("https://example.com/repo.git", "", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Clone 失敗", result.Error!.Message);
    }

    [Fact]
    public async Task GetBranchDiffAsync_WithEmptyRepoPath_ShouldReturnFailure()
    {
        var result = await _sut.GetBranchDiffAsync("", "main", "develop", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Git 命令失敗", result.Error!.Message);
    }

    [Fact]
    public async Task GetCommitDiffAsync_WithEmptyRepoPath_ShouldReturnFailure()
    {
        var result = await _sut.GetCommitDiffAsync("", "abc123", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Git 命令", result.Error!.Message);
    }

    [Fact]
    public async Task GetRemoteUrlAsync_WithEmptyRepoPath_ShouldReturnFailure()
    {
        var result = await _sut.GetRemoteUrlAsync("", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Git 命令失敗", result.Error!.Message);
    }

    [Fact]
    public async Task CloneRepositoryAsync_WithNonExistentRepo_ShouldReturnFailure()
    {
        var result = await _sut.CloneRepositoryAsync(
            "https://example.com/nonexistent.git",
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd src && dotnet test ../tests/ReleaseKit.Infrastructure.Tests --filter "FullyQualifiedName~GitServiceTests" --no-restore 2>&1 | tail -20`
Expected: FAIL — `GitService` class does not exist

- [ ] **Step 3: Implement GitService**

```csharp
// src/ReleaseKit.Infrastructure/Git/GitService.cs
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Common;

namespace ReleaseKit.Infrastructure.Git;

/// <summary>
/// Git 操作服務實作
/// </summary>
/// <remarks>
/// 透過 System.Diagnostics.Process 執行 git 命令，
/// 提供 Clone、Diff、Remote URL 等操作。
/// </remarks>
public sealed class GitService : IGitService
{
    private readonly ILogger<GitService> _logger;

    /// <summary>
    /// 初始化 <see cref="GitService"/> 類別的新執行個體
    /// </summary>
    public GitService(ILogger<GitService> logger)
    {
        _logger = logger;
    }

    /// <summary>完整 Clone 指定 repository（含 fetch --all）</summary>
    public async Task<Result<string>> CloneRepositoryAsync(
        string repoUrl, string targetPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repoUrl) || string.IsNullOrWhiteSpace(targetPath))
        {
            return Result<string>.Failure(Error.RiskAnalysis.CloneFailed("URL 或目標路徑不得為空"));
        }

        _logger.LogInformation("開始 Clone：{RepoUrl} → {TargetPath}", repoUrl, targetPath);

        var cloneResult = await RunGitCommandAsync(
            $"clone {repoUrl} {targetPath}",
            workingDirectory: null,
            cancellationToken);

        if (cloneResult.IsFailure) return Result<string>.Failure(Error.RiskAnalysis.CloneFailed(repoUrl));

        var fetchResult = await RunGitCommandAsync(
            "fetch --all",
            workingDirectory: targetPath,
            cancellationToken);

        if (fetchResult.IsFailure)
        {
            _logger.LogWarning("fetch --all 失敗，但 clone 已完成：{TargetPath}", targetPath);
        }

        _logger.LogInformation("Clone 完成：{TargetPath}", targetPath);
        return Result<string>.Success(targetPath);
    }

    /// <summary>取得指定兩個 branch 之間的 diff</summary>
    public async Task<Result<string>> GetBranchDiffAsync(
        string repoPath, string sourceBranch, string targetBranch, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repoPath))
        {
            return Result<string>.Failure(Error.RiskAnalysis.GitCommandFailed("git diff", "repoPath 不得為空"));
        }

        return await RunGitCommandAsync(
            $"diff {targetBranch}...{sourceBranch}",
            workingDirectory: repoPath,
            cancellationToken);
    }

    /// <summary>取得指定 commit 的 diff</summary>
    public async Task<Result<string>> GetCommitDiffAsync(
        string repoPath, string commitSha, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repoPath))
        {
            return Result<string>.Failure(Error.RiskAnalysis.GitCommandFailed("git show", "repoPath 不得為空"));
        }

        return await RunGitCommandAsync(
            $"show {commitSha} --format=\"\"",
            workingDirectory: repoPath,
            cancellationToken);
    }

    /// <summary>取得 repository 的遠端 URL</summary>
    public async Task<Result<string>> GetRemoteUrlAsync(
        string repoPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repoPath))
        {
            return Result<string>.Failure(Error.RiskAnalysis.GitCommandFailed("git remote", "repoPath 不得為空"));
        }

        var result = await RunGitCommandAsync(
            "remote get-url origin",
            workingDirectory: repoPath,
            cancellationToken);

        if (result.IsSuccess && result.Value is not null)
        {
            return Result<string>.Success(result.Value.Trim());
        }

        return result;
    }

    /// <summary>執行 git 命令並回傳輸出</summary>
    internal async Task<Result<string>> RunGitCommandAsync(
        string arguments, string? workingDirectory, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        _logger.LogDebug("執行 git 命令：git {Arguments}（工作目錄：{WorkingDirectory}）",
            arguments, workingDirectory ?? "(null)");

        var process = new Process { StartInfo = startInfo };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            _logger.LogWarning("git 命令失敗（exit code {ExitCode}）：git {Arguments}，錯誤：{Error}",
                process.ExitCode, arguments, error);
            return Result<string>.Failure(Error.RiskAnalysis.GitCommandFailed($"git {arguments}", error));
        }

        return Result<string>.Success(output);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd src && dotnet test ../tests/ReleaseKit.Infrastructure.Tests --filter "FullyQualifiedName~GitServiceTests" --no-restore 2>&1 | tail -20`
Expected: PASS — all 6 tests pass

- [ ] **Step 5: Commit**

```bash
git add src/ReleaseKit.Infrastructure/Git/GitService.cs tests/ReleaseKit.Infrastructure.Tests/Git/GitServiceTests.cs
git commit -m "feat(infrastructure): 新增 GitService 實作

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 8: Application — CloneRepositoriesTask

**Files:**
- Create: `src/ReleaseKit.Application/Tasks/CloneRepositoriesTask.cs`
- Create: `tests/ReleaseKit.Application.Tests/Tasks/CloneRepositoriesTaskTests.cs`

- [ ] **Step 1: Write CloneRepositoriesTask tests**

```csharp
// tests/ReleaseKit.Application.Tests/Tasks/CloneRepositoriesTaskTests.cs
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ReleaseKit.Application.Common;
using ReleaseKit.Application.Tasks;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;
using ReleaseKit.Domain.Common;
using ReleaseKit.Domain.ValueObjects;

namespace ReleaseKit.Application.Tests.Tasks;

/// <summary>
/// CloneRepositoriesTask 單元測試
/// </summary>
public class CloneRepositoriesTaskTests
{
    private readonly Mock<IRedisService> _redisServiceMock = new();
    private readonly Mock<IGitService> _gitServiceMock = new();
    private readonly Mock<ILogger<CloneRepositoriesTask>> _loggerMock = new();
    private readonly IOptions<GitLabOptions> _gitLabOptions;
    private readonly IOptions<BitbucketOptions> _bitbucketOptions;
    private readonly IOptions<RiskAnalysisOptions> _riskAnalysisOptions;

    public CloneRepositoriesTaskTests()
    {
        _gitLabOptions = Options.Create(new GitLabOptions
        {
            ApiUrl = "https://gitlab.example.com/api/v4",
            AccessToken = "test-token",
            Projects = new List<GitLabProjectOptions>
            {
                new() { ProjectPath = "group/project-a", TargetBranch = "main" }
            }
        });

        _bitbucketOptions = Options.Create(new BitbucketOptions
        {
            ApiUrl = "https://api.bitbucket.org/2.0",
            AccessToken = "test-token",
            Email = "test@example.com",
            Projects = new List<BitbucketProjectOptions>
            {
                new() { ProjectPath = "team/repo-b", TargetBranch = "main" }
            }
        });

        _riskAnalysisOptions = Options.Create(new RiskAnalysisOptions
        {
            CloneBasePath = "/tmp/test-clones",
            ReportOutputPath = "./reports",
            MaxConcurrentClones = 2
        });
    }

    private CloneRepositoriesTask CreateSut() => new(
        _redisServiceMock.Object,
        _gitServiceMock.Object,
        _gitLabOptions,
        _bitbucketOptions,
        _riskAnalysisOptions,
        _loggerMock.Object);

    [Fact]
    public async Task ExecuteAsync_ShouldCloneAllProjects()
    {
        // Arrange
        _gitServiceMock
            .Setup(x => x.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string url, string path, CancellationToken _) => Result<string>.Success(path));
        _redisServiceMock
            .Setup(x => x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — 2 projects = 2 clone calls
        _gitServiceMock.Verify(
            x => x.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldBuildCorrectGitLabCloneUrl()
    {
        // Arrange
        string? capturedUrl = null;
        _gitServiceMock
            .Setup(x => x.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string url, string path, CancellationToken _) =>
            {
                capturedUrl ??= url;
                return Task.FromResult(Result<string>.Success(path));
            });
        _redisServiceMock
            .Setup(x => x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — GitLab URL: strip /api/v4 from ApiUrl
        Assert.Equal("https://gitlab.example.com/group/project-a.git", capturedUrl);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStoreClonePathsInRedis()
    {
        // Arrange
        _gitServiceMock
            .Setup(x => x.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string url, string path, CancellationToken _) => Result<string>.Success(path));

        string? storedJson = null;
        _redisServiceMock
            .Setup(x => x.HashSetAsync(RedisKeys.RiskAnalysisHash, RedisKeys.Fields.ClonePaths, It.IsAny<string>()))
            .Returns((string hash, string field, string value) =>
            {
                storedJson = value;
                return Task.FromResult(true);
            });

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        Assert.NotNull(storedJson);
        var paths = storedJson.ToTypedObject<Dictionary<string, string>>();
        Assert.NotNull(paths);
        Assert.Equal(2, paths.Count);
        Assert.True(paths.ContainsKey("group/project-a"));
        Assert.True(paths.ContainsKey("team/repo-b"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenCloneFails_ShouldContinueWithOtherProjects()
    {
        // Arrange — first clone fails, second succeeds
        var callCount = 0;
        _gitServiceMock
            .Setup(x => x.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string url, string path, CancellationToken _) =>
            {
                callCount++;
                return Task.FromResult(callCount == 1
                    ? Result<string>.Failure(Error.RiskAnalysis.CloneFailed(url))
                    : Result<string>.Success(path));
            });
        _redisServiceMock
            .Setup(x => x.HashSetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert — both clones were attempted
        _gitServiceMock.Verify(
            x => x.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ExecuteAsync_WithNoProjects_ShouldComplete()
    {
        // Arrange — empty project lists
        var emptyGitLab = Options.Create(new GitLabOptions { ApiUrl = "https://gitlab.example.com/api/v4", AccessToken = "t" });
        var emptyBitbucket = Options.Create(new BitbucketOptions { ApiUrl = "https://api.bitbucket.org/2.0", AccessToken = "t", Email = "e" });

        var sut = new CloneRepositoriesTask(
            _redisServiceMock.Object, _gitServiceMock.Object,
            emptyGitLab, emptyBitbucket, _riskAnalysisOptions, _loggerMock.Object);

        // Act
        await sut.ExecuteAsync();

        // Assert — no clone calls
        _gitServiceMock.Verify(
            x => x.CloneRepositoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd src && dotnet test ../tests/ReleaseKit.Application.Tests --filter "FullyQualifiedName~CloneRepositoriesTaskTests" --no-restore 2>&1 | tail -20`
Expected: FAIL — `CloneRepositoriesTask` does not exist

- [ ] **Step 3: Implement CloneRepositoriesTask**

```csharp
// src/ReleaseKit.Application/Tasks/CloneRepositoriesTask.cs
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReleaseKit.Common.Configuration;
using ReleaseKit.Common.Constants;
using ReleaseKit.Common.Extensions;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// Clone 所有組態中的 repository 任務
/// </summary>
public sealed class CloneRepositoriesTask : ITask
{
    private readonly IRedisService _redisService;
    private readonly IGitService _gitService;
    private readonly GitLabOptions _gitLabOptions;
    private readonly BitbucketOptions _bitbucketOptions;
    private readonly RiskAnalysisOptions _riskAnalysisOptions;
    private readonly ILogger<CloneRepositoriesTask> _logger;

    /// <summary>
    /// 初始化 <see cref="CloneRepositoriesTask"/> 類別的新執行個體
    /// </summary>
    public CloneRepositoriesTask(
        IRedisService redisService,
        IGitService gitService,
        IOptions<GitLabOptions> gitLabOptions,
        IOptions<BitbucketOptions> bitbucketOptions,
        IOptions<RiskAnalysisOptions> riskAnalysisOptions,
        ILogger<CloneRepositoriesTask> logger)
    {
        _redisService = redisService;
        _gitService = gitService;
        _gitLabOptions = gitLabOptions.Value;
        _bitbucketOptions = bitbucketOptions.Value;
        _riskAnalysisOptions = riskAnalysisOptions.Value;
        _logger = logger;
    }

    /// <summary>執行 Clone 任務</summary>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始 Clone repositories");

        var clonePaths = new Dictionary<string, string>();
        var semaphore = new SemaphoreSlim(_riskAnalysisOptions.MaxConcurrentClones);

        var cloneTasks = new List<Task>();

        // GitLab projects
        foreach (var project in _gitLabOptions.Projects)
        {
            var cloneUrl = BuildGitLabCloneUrl(project.ProjectPath);
            var targetPath = Path.Combine(_riskAnalysisOptions.CloneBasePath, project.ProjectPath.Replace('/', '_'));
            cloneTasks.Add(CloneWithSemaphoreAsync(semaphore, cloneUrl, targetPath, project.ProjectPath, clonePaths));
        }

        // Bitbucket projects
        foreach (var project in _bitbucketOptions.Projects)
        {
            var cloneUrl = BuildBitbucketCloneUrl(project.ProjectPath);
            var targetPath = Path.Combine(_riskAnalysisOptions.CloneBasePath, project.ProjectPath.Replace('/', '_'));
            cloneTasks.Add(CloneWithSemaphoreAsync(semaphore, cloneUrl, targetPath, project.ProjectPath, clonePaths));
        }

        await Task.WhenAll(cloneTasks);

        // 儲存 clone paths 至 Redis
        if (clonePaths.Count > 0)
        {
            var json = clonePaths.ToJson();
            await _redisService.HashSetAsync(RedisKeys.RiskAnalysisHash, RedisKeys.Fields.ClonePaths, json);
        }

        _logger.LogInformation("Clone 完成，共 {Count} 個 repository", clonePaths.Count);
    }

    /// <summary>以信號量控制平行 clone 數量</summary>
    private async Task CloneWithSemaphoreAsync(
        SemaphoreSlim semaphore, string cloneUrl, string targetPath, string projectPath,
        Dictionary<string, string> clonePaths)
    {
        await semaphore.WaitAsync();

        var result = await _gitService.CloneRepositoryAsync(cloneUrl, targetPath);

        if (result.IsSuccess)
        {
            lock (clonePaths)
            {
                clonePaths[projectPath] = targetPath;
            }
        }
        else
        {
            _logger.LogWarning("Clone 失敗：{ProjectPath}，錯誤：{Error}", projectPath, result.Error?.Message);
        }

        semaphore.Release();
    }

    /// <summary>建構 GitLab clone URL（從 ApiUrl 去除 /api/v4 路徑）</summary>
    internal string BuildGitLabCloneUrl(string projectPath)
    {
        var baseUrl = _gitLabOptions.ApiUrl;
        var apiSuffix = "/api/v4";
        if (baseUrl.EndsWith(apiSuffix, StringComparison.OrdinalIgnoreCase))
        {
            baseUrl = baseUrl[..^apiSuffix.Length];
        }

        return $"{baseUrl}/{projectPath}.git";
    }

    /// <summary>建構 Bitbucket clone URL（嵌入 Basic Auth 認證資訊）</summary>
    internal string BuildBitbucketCloneUrl(string projectPath)
    {
        var uri = new Uri(_bitbucketOptions.ApiUrl);
        var email = Uri.EscapeDataString(_bitbucketOptions.Email);
        var token = Uri.EscapeDataString(_bitbucketOptions.AccessToken);
        return $"{uri.Scheme}://{email}:{token}@bitbucket.org/{projectPath}.git";
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd src && dotnet test ../tests/ReleaseKit.Application.Tests --filter "FullyQualifiedName~CloneRepositoriesTaskTests" --no-restore 2>&1 | tail -20`
Expected: PASS — all 5 tests pass

- [ ] **Step 5: Commit**

```bash
git add src/ReleaseKit.Application/Tasks/CloneRepositoriesTask.cs tests/ReleaseKit.Application.Tests/Tasks/CloneRepositoriesTaskTests.cs
git commit -m "feat(application): 新增 CloneRepositoriesTask

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 9: Application — ExtractPrDiffsTask

**Files:**
- Create: `src/ReleaseKit.Application/Tasks/ExtractPrDiffsTask.cs`
- Create: `tests/ReleaseKit.Application.Tests/Tasks/ExtractPrDiffsTaskTests.cs`

- [ ] **Step 1: Write ExtractPrDiffsTask tests**

Tests should cover:
- Reading PR info from Redis (GitLab and Bitbucket hashes, PullRequestsByUser field)
- Reading clone paths from Redis (RiskAnalysisHash, ClonePaths field)
- Branch diff extraction (primary strategy: `git diff targetBranch...sourceBranch`)
- Merge commit fallback: when branch diff fails (branch deleted after merge), use `IGitService.FindMergeCommitAsync(sourceBranch)` to locate the merge commit SHA, then `GetCommitDiffAsync(sha)`
- Building PrDiffContext from MergeRequestOutput + diff content
- Storing PrDiffs in Redis (RiskAnalysisHash, PrDiffs field)
- Empty data handling
- Skipping PR when both branch diff and merge commit fallback fail (log warning, continue)

Key test: verify diff extraction uses branch diff first, falls back to FindMergeCommitAsync + GetCommitDiffAsync.

- [ ] **Step 2: Run tests to verify they fail**

- [ ] **Step 3: Implement ExtractPrDiffsTask**

The task reads from Redis:
1. `GitLabHash:PullRequestsByUser` → `FetchResult` with `ProjectResult[]` each containing `MergeRequestOutput[]`
2. `BitbucketHash:PullRequestsByUser` → same structure
3. `RiskAnalysisHash:ClonePaths` → `Dictionary<string, string>` (projectPath → clonePath)

For each project's PRs:
1. Try `git diff targetBranch...sourceBranch` via `IGitService.GetBranchDiffAsync(repoPath, targetBranch, sourceBranch)`
2. If branch diff fails (branch deleted after merge):
   a. Call `IGitService.FindMergeCommitAsync(repoPath, sourceBranch)` — runs `git log --merges --format="%H" --grep="Merge branch '{sourceBranch}'" -1`
   b. If merge commit found, call `IGitService.GetCommitDiffAsync(repoPath, mergeCommitSha)`
   c. If both fail, log warning and skip this PR
3. Parse changed files from diff output (lines starting with `diff --git a/ b/`)
4. Build `PrDiffContext` for each PR
5. Group by project, store as `Dictionary<string, List<PrDiffContext>>` in Redis

- [ ] **Step 4: Run tests to verify they pass**

- [ ] **Step 5: Commit**

```bash
git add src/ReleaseKit.Application/Tasks/ExtractPrDiffsTask.cs tests/ReleaseKit.Application.Tests/Tasks/ExtractPrDiffsTaskTests.cs
git commit -m "feat(application): 新增 ExtractPrDiffsTask

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 10: Application — AnalyzeProjectRiskTask (Pass 1)

**Files:**
- Create: `src/ReleaseKit.Application/Tasks/AnalyzeProjectRiskTask.cs`
- Create: `tests/ReleaseKit.Application.Tests/Tasks/AnalyzeProjectRiskTaskTests.cs`

- [ ] **Step 1: Write AnalyzeProjectRiskTask tests**

Tests should cover:
- Reading PrDiffs from Redis
- Parallel AI analysis per project (via IRiskAnalyzer.AnalyzeProjectRiskAsync)
- Intermediate report storage in Redis (Intermediate:1-1, 1-2, ...)
- Large diff splitting: when total diff size > MaxTokensPerAiCall, split by file groups
- Sub-agent intermediate reports (Intermediate:1-3-a, 1-3-b, then merged into 1-3)
- Coverage verification: all changed files accounted for after splitting
- Empty PrDiffs handling

- [ ] **Step 2: Run tests to verify they fail**

- [ ] **Step 3: Implement AnalyzeProjectRiskTask**

Key logic:
1. Read `RiskAnalysisHash:PrDiffs` → `Dictionary<string, List<PrDiffContext>>`
2. For each project (parallel with SemaphoreSlim):
   a. Check total diff size against `MaxTokensPerAiCall`
   b. If within limit → call `IRiskAnalyzer.AnalyzeProjectRiskAsync(projectName, diffs)`
   c. If exceeds limit → split diffs into chunks, call AI per chunk, merge reports
   d. Store intermediate report in Redis
3. Track file coverage for split diffs

- [ ] **Step 4: Run tests to verify they pass**

- [ ] **Step 5: Commit**

```bash
git add src/ReleaseKit.Application/Tasks/AnalyzeProjectRiskTask.cs tests/ReleaseKit.Application.Tests/Tasks/AnalyzeProjectRiskTaskTests.cs
git commit -m "feat(application): 新增 AnalyzeProjectRiskTask (Pass 1)

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 11: Application — AnalyzeCrossProjectRiskTask (Pass 2~10 Dynamic)

**Files:**
- Create: `src/ReleaseKit.Application/Tasks/AnalyzeCrossProjectRiskTask.cs`
- Create: `tests/ReleaseKit.Application.Tests/Tasks/AnalyzeCrossProjectRiskTaskTests.cs`

- [ ] **Step 1: Write AnalyzeCrossProjectRiskTask tests**

Tests should cover:
- Loading Pass 1 reports from Redis
- Calling IRiskAnalyzer.AnalyzeDeepAsync with correct pass number
- Storing intermediate reports per pass (Intermediate:2-1, 2-2, ...)
- Storing pass metadata (PassMetadata:2, PassMetadata:3, ...)
- Loop termination when AI returns ContinueAnalysis = false
- Hard limit enforcement: stops at MaxAnalysisPasses (10)
- Empty reports handling
- ContinueReason logging

- [ ] **Step 2: Run tests to verify they fail**

- [ ] **Step 3: Implement AnalyzeCrossProjectRiskTask**

Key logic:
1. Load Pass 1 intermediate reports from Redis
2. Loop from Pass 2 to MaxAnalysisPasses:
   a. Call `IRiskAnalyzer.AnalyzeDeepAsync(currentPass, previousReports)`
   b. Store reports in Redis
   c. Store metadata (strategy, continueReason) in Redis
   d. If `!result.ContinueAnalysis` → break
   e. Set `previousReports = result.Reports` for next iteration

- [ ] **Step 4: Run tests to verify they pass**

- [ ] **Step 5: Commit**

```bash
git add src/ReleaseKit.Application/Tasks/AnalyzeCrossProjectRiskTask.cs tests/ReleaseKit.Application.Tests/Tasks/AnalyzeCrossProjectRiskTaskTests.cs
git commit -m "feat(application): 新增 AnalyzeCrossProjectRiskTask (Pass 2~10)

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 12: Application — GenerateRiskReportTask

**Files:**
- Create: `src/ReleaseKit.Application/Tasks/GenerateRiskReportTask.cs`
- Create: `tests/ReleaseKit.Application.Tests/Tasks/GenerateRiskReportTaskTests.cs`

- [ ] **Step 1: Write GenerateRiskReportTask tests**

Tests should cover:
- Loading the last pass's reports from Redis
- Calling IRiskAnalyzer.GenerateFinalReportAsync
- Storing final Markdown in Redis (RiskAnalysisHash:FinalReport)
- Writing report to file (ReportOutputPath)
- Empty reports handling

- [ ] **Step 2: Run tests to verify they fail**

- [ ] **Step 3: Implement GenerateRiskReportTask**

Key logic:
1. Determine last pass number by scanning Redis for PassMetadata keys
2. Load last pass's intermediate reports
3. Call `IRiskAnalyzer.GenerateFinalReportAsync(lastPassReports)`
4. Store in Redis: `RiskAnalysisHash:FinalReport`
5. Write to file: `{ReportOutputPath}/risk-report-{date}.md`

- [ ] **Step 4: Run tests to verify they pass**

- [ ] **Step 5: Commit**

```bash
git add src/ReleaseKit.Application/Tasks/GenerateRiskReportTask.cs tests/ReleaseKit.Application.Tests/Tasks/GenerateRiskReportTaskTests.cs
git commit -m "feat(application): 新增 GenerateRiskReportTask

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 13: Application — AnalyzeRiskTask (Orchestrator)

**Files:**
- Create: `src/ReleaseKit.Application/Tasks/AnalyzeRiskTask.cs`
- Create: `tests/ReleaseKit.Application.Tests/Tasks/AnalyzeRiskTaskTests.cs`

- [ ] **Step 1: Write AnalyzeRiskTask tests**

Tests should cover:
- All 5 sub-tasks are called in correct order
- If any sub-task is a separate ITask, verify the call sequence

- [ ] **Step 2: Run tests to verify they fail**

- [ ] **Step 3: Implement AnalyzeRiskTask**

```csharp
// src/ReleaseKit.Application/Tasks/AnalyzeRiskTask.cs
using Microsoft.Extensions.Logging;
using ReleaseKit.Domain.Abstractions;

namespace ReleaseKit.Application.Tasks;

/// <summary>
/// 風險分析 Orchestrator，串聯所有子 Task
/// </summary>
public sealed class AnalyzeRiskTask : ITask
{
    private readonly CloneRepositoriesTask _cloneRepositoriesTask;
    private readonly ExtractPrDiffsTask _extractPrDiffsTask;
    private readonly AnalyzeProjectRiskTask _analyzeProjectRiskTask;
    private readonly AnalyzeCrossProjectRiskTask _analyzeCrossProjectRiskTask;
    private readonly GenerateRiskReportTask _generateRiskReportTask;
    private readonly ILogger<AnalyzeRiskTask> _logger;

    /// <summary>
    /// 初始 <see cref="AnalyzeRiskTask"/> 類別的新執行個體
    /// </summary>
    public AnalyzeRiskTask(
        CloneRepositoriesTask cloneRepositoriesTask,
        ExtractPrDiffsTask extractPrDiffsTask,
        AnalyzeProjectRiskTask analyzeProjectRiskTask,
        AnalyzeCrossProjectRiskTask analyzeCrossProjectRiskTask,
        GenerateRiskReportTask generateRiskReportTask,
        ILogger<AnalyzeRiskTask> logger)
    {
        _cloneRepositoriesTask = cloneRepositoriesTask;
        _extractPrDiffsTask = extractPrDiffsTask;
        _analyzeProjectRiskTask = analyzeProjectRiskTask;
        _analyzeCrossProjectRiskTask = analyzeCrossProjectRiskTask;
        _generateRiskReportTask = generateRiskReportTask;
        _logger = logger;
    }

    /// <summary>執行完整風險分析流程</summary>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("開始執行完整風險分析流程");

        _logger.LogInformation("Step 1/5: Clone repositories");
        await _cloneRepositoriesTask.ExecuteAsync();

        _logger.LogInformation("Step 2/5: Extract PR diffs");
        await _extractPrDiffsTask.ExecuteAsync();

        _logger.LogInformation("Step 3/5: Per-project AI analysis (Pass 1)");
        await _analyzeProjectRiskTask.ExecuteAsync();

        _logger.LogInformation("Step 4/5: Cross-project dynamic analysis (Pass 2~10)");
        await _analyzeCrossProjectRiskTask.ExecuteAsync();

        _logger.LogInformation("Step 5/5: Generate final risk report");
        await _generateRiskReportTask.ExecuteAsync();

        _logger.LogInformation("Release 風險分析完成");
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

- [ ] **Step 5: Commit**

```bash
git add src/ReleaseKit.Application/Tasks/AnalyzeRiskTask.cs tests/ReleaseKit.Application.Tests/Tasks/AnalyzeRiskTaskTests.cs
git commit -m "feat(application): 新增 AnalyzeRiskTask Orchestrator

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 14: Infrastructure — CopilotRiskAnalyzer + Unit Tests

**Files:**
- Create: `src/ReleaseKit.Infrastructure/Copilot/CopilotRiskAnalyzer.cs`
- Create: `tests/ReleaseKit.Infrastructure.Tests/Copilot/CopilotRiskAnalyzerTests.cs`

- [ ] **Step 1: Write CopilotRiskAnalyzer unit tests**

Create test file `tests/ReleaseKit.Infrastructure.Tests/Copilot/CopilotRiskAnalyzerTests.cs`.

Tests should cover (mocking CopilotClient):
1. **Prompt construction tests** — verify internal const prompts contain expected keywords/instructions
2. **Valid JSON response parsing** — AnalyzeProjectRiskAsync returns correct ProjectRiskReport from well-formed JSON
3. **Invalid JSON response parsing** — returns empty/fallback report, logs warning
4. **Empty response handling** — returns empty/fallback report
5. **Markdown-wrapped JSON** — strips ```json ... ``` wrapper correctly (same pattern as CopilotTitleEnhancer)
6. **AnalyzeDeepAsync** — verify DynamicAnalysisResult parsing, including ContinueAnalysis flag
7. **GenerateFinalReportAsync** — verify final report generation with multiple project risk inputs
8. **AI failure fallback** — when Copilot SDK throws, return empty report instead of propagating exception (this is the one exception to the no-try-catch rule: external AI call with explicit recovery)

Follow the CopilotTitleEnhancer test patterns (see `tests/ReleaseKit.Infrastructure.Tests/Copilot/` for reference).

Key: Make prompts `internal const string` and use `[InternalsVisibleTo]` to allow test assertions on prompt content.

- [ ] **Step 2: Run tests to verify they fail**

- [ ] **Step 3: Implement CopilotRiskAnalyzer**

The class implements `IRiskAnalyzer` with:
- Constructor: `IOptions<CopilotOptions>`, `IOptions<RiskAnalysisOptions>`, `INow`, `ILogger<CopilotRiskAnalyzer>`
- `AnalyzeProjectRiskAsync`: Uses Pass 1 System Prompt from spec (section 5.3)
- `AnalyzeDeepAsync`: Uses Pass 2~10 Dynamic Analysis System Prompt from spec
- `GenerateFinalReportAsync`: Uses Final Report System Prompt from spec
- JSON response parsing with markdown cleanup (same as CopilotTitleEnhancer.ParseResponse)
- Fallback: return empty report on AI failure (this is the documented exception to the no-try-catch rule — external AI service with explicit recovery)

Key prompts should be `internal const string` for testability.

- [ ] **Step 4: Run tests to verify they pass**

- [ ] **Step 5: Build to verify compilation**

Run: `cd src && dotnet build --no-restore 2>&1 | tail -10`
Expected: Build succeeded

- [ ] **Step 6: Commit**

```bash
git add src/ReleaseKit.Infrastructure/Copilot/CopilotRiskAnalyzer.cs tests/ReleaseKit.Infrastructure.Tests/Copilot/CopilotRiskAnalyzerTests.cs
git commit -m "feat(infrastructure): 新增 CopilotRiskAnalyzer 實作與單元測試

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 15: Console — DI Registration + TaskFactory

**Files:**
- Modify: `src/ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs`
- Modify: `src/ReleaseKit.Application/Tasks/TaskFactory.cs`
- Modify: `tests/ReleaseKit.Application.Tests/Tasks/TaskFactoryTests.cs`

- [ ] **Step 1: Update TaskFactoryTests to include new task types**

Add test cases for each new TaskType → Task class mapping.

- [ ] **Step 2: Run tests to verify they fail**

- [ ] **Step 3: Update TaskFactory switch expression**

Add cases for:
```csharp
TaskType.CloneRepositories => _serviceProvider.GetRequiredService<CloneRepositoriesTask>(),
TaskType.ExtractPrDiffs => _serviceProvider.GetRequiredService<ExtractPrDiffsTask>(),
TaskType.AnalyzeProjectRisk => _serviceProvider.GetRequiredService<AnalyzeProjectRiskTask>(),
TaskType.AnalyzeCrossProjectRisk => _serviceProvider.GetRequiredService<AnalyzeCrossProjectRiskTask>(),
TaskType.GenerateRiskReport => _serviceProvider.GetRequiredService<GenerateRiskReportTask>(),
TaskType.AnalyzeRisk => _serviceProvider.GetRequiredService<AnalyzeRiskTask>(),
```

- [ ] **Step 4: Update ServiceCollectionExtensions.AddConfigurationOptions**

Add:
```csharp
// 註冊 RiskAnalysis 配置
services.Configure<RiskAnalysisOptions>(configuration.GetSection("RiskAnalysis"));
```

- [ ] **Step 5: Update ServiceCollectionExtensions.AddApplicationServices**

Add:
```csharp
// 註冊風險分析服務
services.AddTransient<IGitService, ReleaseKit.Infrastructure.Git.GitService>();
services.AddTransient<IRiskAnalyzer, ReleaseKit.Infrastructure.Copilot.CopilotRiskAnalyzer>();

// 註冊風險分析任務
services.AddTransient<CloneRepositoriesTask>();
services.AddTransient<ExtractPrDiffsTask>();
services.AddTransient<AnalyzeProjectRiskTask>();
services.AddTransient<AnalyzeCrossProjectRiskTask>();
services.AddTransient<GenerateRiskReportTask>();
services.AddTransient<AnalyzeRiskTask>();
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `cd src && dotnet test ../tests/ReleaseKit.Application.Tests --filter "FullyQualifiedName~TaskFactory" --no-restore 2>&1 | tail -20`
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add src/ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs src/ReleaseKit.Application/Tasks/TaskFactory.cs tests/ReleaseKit.Application.Tests/Tasks/TaskFactoryTests.cs
git commit -m "feat(console): 註冊風險分析服務與任務至 DI 容器

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 16: Final Verification

- [ ] **Step 1: Full build**

Run: `cd src && dotnet build 2>&1 | tail -10`
Expected: Build succeeded with 0 errors

- [ ] **Step 2: Run all tests**

Run: `cd src && dotnet test ../tests/ 2>&1 | tail -20`
Expected: All tests pass

- [ ] **Step 3: Verify no regressions in existing tests**

Run: `cd src && dotnet test ../tests/ReleaseKit.Console.Tests --filter "FullyQualifiedName~CommandLineParserTests" --no-restore 2>&1 | tail -10`
Expected: All existing parser tests still pass (including the new risk commands)

- [ ] **Step 4: Final commit if any fixups needed**

```bash
git add -A
git commit -m "fix: 修正風險分析功能整合問題

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```
