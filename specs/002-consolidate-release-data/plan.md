# Implementation Plan: 整合 Release 資料

**Branch**: `002-consolidate-release-data` | **Date**: 2026-02-20 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/002-consolidate-release-data/spec.md`

## Summary

新增 `consolidate-release-data` console 指令，從 Redis 讀取已過濾的 PR 資料（Bitbucket/GitLab ByUser）與 Work Item 資料（UserStories），透過 PR ID 配對後，依專案路徑分組、依團隊顯示名稱與 Work Item ID 排序，並將整合結果存入新的 Redis Key。

**技術方案**：實作新的 `ConsolidateReleaseDataTask`（實作 `ITask`），遵循現有 Task 模式。不需新增 Base 類別，因為此任務是獨立的整合邏輯。

## Technical Context

**Language/Version**: C# / .NET 9  
**Primary Dependencies**: StackExchange.Redis, Microsoft.Extensions.Options, Serilog  
**Storage**: Redis（中間資料存儲）  
**Testing**: xUnit + Moq  
**Target Platform**: Linux / Console App  
**Project Type**: Clean Architecture 四層（Domain, Application, Infrastructure, Console）  
**Performance Goals**: N/A（手動執行的 CLI 工具）  
**Constraints**: 無  
**Scale/Scope**: 單一 Task 新增，影響 Application/Common/Console 層

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| 原則 | 狀態 | 說明 |
|------|------|------|
| I. TDD | ✅ 遵循 | 先撰寫失敗測試，再實作功能 |
| II. DDD/CQRS | ✅ 遵循 | Task 模式屬於 Application Layer 的 Command 操作 |
| III. SOLID | ✅ 遵循 | 單一職責 Task、依賴注入 |
| IV. KISS | ✅ 遵循 | 不過度抽象，直接實作整合邏輯 |
| V. 錯誤處理 | ✅ 遵循 | 資料缺失時直接拋出 InvalidOperationException，不使用 try-catch |
| VI. 效能/快取 | ✅ 遵循 | 直接讀取 Redis 現有資料 |
| VII. 避免硬編碼 | ✅ 遵循 | Redis Key 使用 RedisKeys 常數，TeamMapping 由設定檔管理 |
| VIII. 文件/註解 | ✅ 遵循 | 所有公開成員加入 XML Summary 繁體中文註解 |
| IX. JSON 規範 | ✅ 遵循 | 使用 JsonExtensions |
| X. Program.cs 整潔 | ✅ 遵循 | 僅新增 DI 註冊 |
| XI. 單一類別檔案 | ✅ 遵循 | 每個新類別獨立檔案 |
| XII. RESTful API | N/A | 非 API 功能 |
| XIII. 組態管理 | ✅ 遵循 | TeamMapping 已在 appsettings.json 中 |
| XIV. 程式碼重用 | ✅ 遵循 | 重用現有 RedisKeys、JsonExtensions、IRedisService、TeamMappingOptions |

## Project Structure

### Documentation (this feature)

```text
specs/002-consolidate-release-data/
├── plan.md              # 本檔案
├── research.md          # Phase 0 研究
├── data-model.md        # Phase 1 資料模型
├── quickstart.md        # Phase 1 快速啟動指南
└── tasks.md             # Phase 2 任務（/speckit.tasks 產出）
```

### Source Code (新增/修改檔案)

```text
src/
├── ReleaseKit.Common/
│   └── Constants/
│       └── RedisKeys.cs                        # [修改] 新增 ConsolidatedReleaseData Key
│
├── ReleaseKit.Application/
│   ├── Common/
│   │   ├── ConsolidatedReleaseEntry.cs         # [新增] 整合記錄 DTO
│   │   ├── ConsolidatedAuthorInfo.cs           # [新增] 作者資訊 DTO
│   │   ├── ConsolidatedPrInfo.cs               # [新增] PR 資訊 DTO
│   │   ├── ConsolidatedOriginalData.cs         # [新增] 原始資料 DTO
│   │   ├── ConsolidatedProjectGroup.cs         # [新增] 專案分組 DTO
│   │   └── ConsolidatedReleaseResult.cs        # [新增] 最終結果 DTO
│   └── Tasks/
│       ├── TaskType.cs                         # [修改] 新增 ConsolidateReleaseData 列舉
│       ├── TaskFactory.cs                      # [修改] 新增 case
│       └── ConsolidateReleaseDataTask.cs       # [新增] 整合任務
│
├── ReleaseKit.Console/
│   ├── Parsers/
│   │   └── CommandLineParser.cs                # [修改] 新增 command mapping
│   └── Extensions/
│       └── ServiceCollectionExtensions.cs      # [修改] 註冊新 Task

tests/
└── ReleaseKit.Application.Tests/
    └── Tasks/
        └── ConsolidateReleaseDataTaskTests.cs  # [新增] 單元測試
```

**Structure Decision**: 遵循既有 Clean Architecture 分層，新增 Task 於 Application Layer，DTO 於 Application/Common。

### 可重用元件清單

| 元件 | 位置 | 重用方式 |
|------|------|---------|
| `ITask` | Domain/Abstractions | Task 實作介面 |
| `IRedisService` | Domain/Abstractions | Redis 讀寫 |
| `RedisKeys` | Common/Constants | Redis Key 常數 |
| `JsonExtensions` | Common/Extensions | JSON 序列化/反序列化 |
| `FetchResult` / `ProjectResult` / `MergeRequestOutput` | Application/Common | PR 資料模型 |
| `UserStoryFetchResult` / `UserStoryWorkItemOutput` | Application/Common | Work Item 資料模型 |
| `TeamMappingOptions` / `AzureDevOpsOptions` | Infrastructure/Configuration | 團隊映射設定 |
| `TaskType` / `TaskFactory` / `CommandLineParser` | Application/Console | 指令註冊與派發 |

## Complexity Tracking

> 無違反憲法原則，無需記錄
