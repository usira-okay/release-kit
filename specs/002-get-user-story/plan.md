# Implementation Plan: Azure Work Item User Story Resolution

**Branch**: `002-get-user-story` | **Date**: 2026-02-18 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/002-get-user-story/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

新增一個 console 命令 `get-user-story`，用於將 Redis 中低於 User Story 層級的 Azure Work Item（如 Bug、Task）遞迴轉換為其對應的 User Story，並存入新的 Redis Key（`AzureDevOps:WorkItems:UserStories`）。系統將透過 Azure DevOps API 查詢 Parent Work Item，直到找到 User Story/Feature/Epic 層級為止，並處理循環參照、最大遞迴深度限制等邊界情況。

## Phase 0: Research (✅ Completed)

**Status**: ✅ All research completed, all NEEDS CLARIFICATION resolved

**Output**: [research.md](./research.md)

**Key Decisions**:
1. 使用現有 API `$expand=all` 取得 Relations，從中解析 Parent ID
2. 建立 `WorkItemTypeConstants` 管理 User Story 層級類型判斷
3. 使用 `HashSet<int>` 偵測循環參照
4. 預設遞迴深度 10 層，可透過 appsettings.json 設定
5. Redis Key 命名: `AzureDevOps:WorkItems:UserStories`
6. 擴充 `WorkItemOutput`，新增 `resolutionStatus` 與 `originalWorkItem` 欄位
7. 使用現有 `JsonExtensions` 進行序列化

## Phase 1: Design & Contracts (✅ Completed)

**Status**: ✅ Design artifacts generated

**Outputs**:
- [data-model.md](./data-model.md) - 包含 UserStoryWorkItemOutput、UserStoryResolutionStatus、WorkItemTypeConstants 定義
- [contracts/azure-devops-workitem-api.md](./contracts/azure-devops-workitem-api.md) - Azure DevOps Relations API 契約
- [quickstart.md](./quickstart.md) - 使用指南與整合範例

**Agent Context**: ✅ Updated via `update-agent-context.sh claude`

## Phase 2: Task Planning

**Status**: ⏭️ Ready for `/speckit.tasks` command

**Next Steps**:
1. 執行 `/speckit.tasks` 產生 tasks.md
2. 依據 tasks.md 實作功能（遵循 TDD）
3. 執行建置與測試驗證

---

## Constitution Re-Check (Post-Design)

**Status**: ✅ PASS - No changes to constitution compliance after design phase

所有設計決策符合 Constitution 原則：
- ✅ TDD: 測試優先開發
- ✅ DDD/CQRS: Application Layer 任務，使用現有抽象
- ✅ SOLID: 單一職責，依賴反轉
- ✅ KISS: 簡單遞迴實作，避免過度設計
- ✅ Result Pattern: 完全遵循，禁止 try-catch
- ✅ 重用現有元件: IAzureDevOpsRepository, IRedisService, JsonExtensions
- ✅ 避免硬編碼: 所有常數集中管理
- ✅ 繁體中文註解: 所有公開成員加入 XML summary

## Implementation Readiness

**✅ Ready to implement** - All prerequisites met:
- [x] Feature specification complete
- [x] Research questions answered
- [x] Data model defined
- [x] API contracts documented
- [x] Usage guide created
- [x] Constitution compliance verified
- [x] Agent context updated
- [ ] Tasks breakdown (next: `/speckit.tasks`)

## Technical Context

**Language/Version**: .NET 9  
**Primary Dependencies**: 
- StackExchange.Redis (Redis 快取)
- System.Text.Json (JSON 序列化)
- Serilog (日誌記錄)
- Microsoft.Extensions.DependencyInjection (依賴注入)

**Storage**: Redis (使用現有 IRedisService 抽象)  
**Testing**: xUnit + FluentAssertions + NSubstitute  
**Target Platform**: Linux/Windows Console Application  
**Project Type**: Console Application (Clean Architecture)  
**Performance Goals**: 100 筆 Work Item 轉換在 30 秒內完成（假設每筆 1-2 次 API 呼叫）  
**Constraints**: 
- 遞迴深度限制為 10 層
- 必須偵測並避免循環參照
- 必須使用 Result Pattern，禁止 try-catch

**Scale/Scope**: 預計處理數十至數百筆 Work Item

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### ✅ I. 測試驅動開發 (TDD)
- **Compliance**: 將遵循 Red-Green-Refactor 循環
- **Plan**: 
  - 先撰寫 `GetUserStoryTaskTests` 測試類別
  - 測試遞迴查詢、循環偵測、深度限制等情境
  - 實作 `GetUserStoryTask` 使測試通過

### ✅ II. 領域驅動設計 (DDD) 與 CQRS
- **Compliance**: 本功能屬於 Application Layer 任務，不涉及 Domain 模型變更
- **Plan**:
  - 新增 `GetUserStoryTask` 位於 `ReleaseKit.Application/Tasks/`
  - 使用現有的 `IAzureDevOpsRepository` 與 `IRedisService` 抽象
  - 遵循 Result Pattern，使用 `Result<T>` 回傳結果

### ✅ III. SOLID 原則
- **Compliance**: 遵循單一職責原則
- **Plan**:
  - `GetUserStoryTask` 僅負責 Work Item 轉換邏輯
  - 依賴 `IAzureDevOpsRepository` (DIP)
  - 使用 `IRedisService` 抽象而非具體實作 (DIP)

### ✅ IV. 簡單原則 (KISS)
- **Compliance**: 採用直觀的遞迴實作
- **Plan**:
  - 使用遞迴方法查詢 Parent，避免複雜的狀態機
  - 使用 HashSet 偵測循環參照（簡單且有效）
  - 深度計數器防止無限遞迴

### ✅ V. 錯誤處理策略
- **Compliance**: 完全遵循 Result Pattern
- **Plan**:
  - 所有 API 呼叫使用 `Result<WorkItem>` 回傳
  - 禁止使用 try-catch
  - 錯誤訊息包含足夠診斷資訊

### ✅ VI. 效能與快取優先
- **Compliance**: 優先使用現有邏輯
- **Plan**:
  - 重複使用現有的 `IAzureDevOpsRepository.GetWorkItemAsync()`
  - 重複使用現有的 `IRedisService.GetAsync()` / `SetAsync()`
  - 避免重複查詢已處理的 Work Item

### ✅ VII. 避免硬編碼
- **Compliance**: 所有常數集中管理
- **Plan**:
  - 新增 `UserStoryResolutionStatus` enum 於 `ReleaseKit.Application/Common/`
  - User Story 類型判斷使用 `WorkItemTypeConstants`（需新增）
  - Redis Key 定義於 `RedisKeys.AzureDevOpsUserStoryWorkItems`
  - 最大遞迴深度定義為常數

### ✅ VIII. 文件與註解規範
- **Compliance**: 所有公開成員加入繁體中文 XML 註解
- **Plan**:
  - `GetUserStoryTask` 加入 summary 說明功能
  - 遞迴方法加入 remarks 說明遞迴終止條件
  - enum 每個值加入註解

### ✅ IX. JSON 序列化規範
- **Compliance**: 優先使用 JsonExtensions
- **Plan**:
  - 查詢是否存在 `JsonExtensions`，若存在則使用
  - 否則使用 `System.Text.Json`

### ✅ X. 程式進入點規範
- **Compliance**: Program.cs 僅負責啟動與 DI
- **Plan**:
  - 不修改 Program.cs，僅在 `ServiceCollectionExtensions` 註冊新 Task
  - 業務邏輯完全位於 `GetUserStoryTask`

### ✅ XI. 檔案組織規範
- **Compliance**: 一個檔案一個類別
- **Plan**:
  - `GetUserStoryTask.cs` - 主要任務類別
  - `UserStoryResolutionStatus.cs` - enum
  - `UserStoryWorkItemOutput.cs` - DTO（包含 originalWorkItem 欄位）

### ✅ XII. RESTful API 規範
- **Status**: N/A（本功能為 Console 應用程式，不涉及 API）

### ✅ XIII. 組態管理規範
- **Compliance**: 所有設定位於 appsettings.json
- **Plan**:
  - 最大遞迴深度可設定於 `appsettings.json` 的 `GetUserStory:MaxDepth`（預設 10）
  - Redis 連線字串使用現有設定 `Redis:ConnectionString`

### ✅ XIV. 程式碼重用原則
- **Compliance**: 優先重用現有元件
- **Reusable Components Identified**:
  - ✅ `IAzureDevOpsRepository.GetWorkItemAsync()` - 查詢 Work Item
  - ✅ `IRedisService.GetAsync()` / `SetAsync()` - Redis 讀寫
  - ✅ `WorkItemOutput` - 可擴充為 `UserStoryWorkItemOutput`
  - ✅ `TaskType` enum - 新增 `GetUserStory` 值
  - ✅ `CommandLineParser` - 新增 `get-user-story` 對應
  - ✅ `TaskFactory` - 新增 `GetUserStoryTask` 建立邏輯

**Constitution Compliance: PASS ✅**  
所有原則皆符合，無需 Complexity Tracking 記錄。

## Project Structure

### Documentation (this feature)

```text
specs/002-get-user-story/
├── spec.md              # Feature specification (completed)
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (Azure DevOps API research)
├── data-model.md        # Phase 1 output (UserStoryWorkItemOutput structure)
├── quickstart.md        # Phase 1 output (usage guide)
├── contracts/           # Phase 1 output (Azure DevOps API contracts)
│   └── azure-devops-workitem-api.md
├── checklists/          # Quality checklists
│   └── requirements.md
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── ReleaseKit.Domain/           # 領域層
│   ├── Abstractions/
│   │   ├── IAzureDevOpsRepository.cs  # (existing, reuse)
│   │   └── IRedisService.cs           # (existing, reuse)
│   └── Entities/
│       └── WorkItem.cs                # (existing, reuse)
│
├── ReleaseKit.Application/      # 應用層
│   ├── Common/
│   │   ├── WorkItemOutput.cs          # (existing, reuse as base)
│   │   ├── UserStoryWorkItemOutput.cs # (NEW) 包含 originalWorkItem 欄位
│   │   └── UserStoryResolutionStatus.cs # (NEW) enum
│   └── Tasks/
│       ├── GetUserStoryTask.cs        # (NEW) 主要任務
│       ├── TaskType.cs                # (modify) 新增 GetUserStory
│       └── TaskFactory.cs             # (modify) 新增 GetUserStoryTask 建立
│
├── ReleaseKit.Infrastructure/   # 基礎設施層
│   ├── AzureDevOps/
│   │   ├── AzureDevOpsRepository.cs   # (existing, reuse)
│   │   └── Models/
│   │       └── AzureDevOpsWorkItemResponse.cs # (modify) 新增 Parent 欄位支援
│   └── Redis/
│       └── RedisService.cs            # (existing, reuse)
│
├── ReleaseKit.Common/           # 共用層
│   └── Constants/
│       ├── RedisKeys.cs               # (modify) 新增 AzureDevOpsUserStoryWorkItems
│       └── WorkItemTypeConstants.cs   # (NEW) User Story 類型判斷常數
│
└── ReleaseKit.Console/          # Console 應用程式
    ├── Program.cs                     # (no change)
    ├── Parsers/
    │   └── CommandLineParser.cs       # (modify) 新增 get-user-story 命令
    └── Extensions/
        └── ServiceCollectionExtensions.cs # (modify) 註冊 GetUserStoryTask

tests/
├── ReleaseKit.Application.Tests/
│   └── Tasks/
│       └── GetUserStoryTaskTests.cs   # (NEW) TDD 測試
└── ReleaseKit.Infrastructure.Tests/
    └── AzureDevOps/
        └── AzureDevOpsRepositoryTests.cs # (existing, may need update)
```

**Structure Decision**: 遵循現有的 Clean Architecture 四層結構（Domain, Application, Infrastructure, Console），不新增專案，僅在現有專案中新增必要的類別與修改相關設定。

## Complexity Tracking

**Status**: N/A - No constitution violations requiring justification.
