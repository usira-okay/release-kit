# Implementation Plan: 取得 User Story 層級資訊

**Branch**: `001-get-user-story` | **Date**: 2026-02-17 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-get-user-story/spec.md`

## Summary

新增 `get-user-story` console arg，從 Redis 讀取既有的 Work Item 資料，對每個 Work Item 判斷是否為 User Story 以上層級。若不是，透過 Azure DevOps API 遞迴查找 Parent 直到找到 User Story 或更高層級。處理結果（含四種解析狀態）寫入新的 Redis Key `AzureDevOps:WorkItems:UserStories`。

技術方式：利用現有 `$expand=all` API 回傳的 `relations` 陣列解析 Parent 關係，無需額外 API endpoint。擴充既有的回應模型與 Mapper，新增 `GetUserStoryTask` 遵循現有 Task Pattern。

## Technical Context

**Language/Version**: C# / .NET 9
**Primary Dependencies**: StackExchange.Redis、System.Text.Json、Microsoft.Extensions.DependencyInjection
**Storage**: Redis（StackExchange.Redis）
**Testing**: xUnit 2.9.2 + Moq 4.20.72
**Target Platform**: Linux / Windows Console Application
**Project Type**: Clean Architecture 多層專案（Domain、Application、Infrastructure、Console）
**Performance Goals**: 無特殊效能需求（批次處理，非即時請求）
**Constraints**: 遞迴深度上限 10 層、避免循環參照
**Scale/Scope**: 處理數十至數百個 Work Item

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| 原則 | 狀態 | 說明 |
|------|------|------|
| I. TDD | ✅ 通過 | 所有新增類別將遵循 Red-Green-Refactor 循環 |
| II. DDD/CQRS | ✅ 通過 | 領域實體擴充於 Domain Layer、Task 實作於 Application Layer |
| III. SOLID | ✅ 通過 | 新增類別各有單一職責，透過介面注入依賴 |
| IV. KISS | ✅ 通過 | 不建立不必要的抽象，Task 直接實作 ITask |
| V. 錯誤處理 | ✅ 通過 | 使用 Result Pattern，不使用 try-catch |
| VI. 效能與快取 | ✅ 通過 | 優先從 Redis 讀取既有資料，避免重複 API 呼叫 |
| VII. 避免硬編碼 | ✅ 通過 | 類型集合、Redis Key、深度限制使用常數管理 |
| VIII. 文件與註解 | ✅ 通過 | 所有公開成員加入繁體中文 XML Summary |
| IX. JSON 序列化 | ✅ 通過 | 使用 JsonExtensions（ToJson / ToTypedObject） |
| X. Program.cs | ✅ 通過 | 不修改 Program.cs，僅擴充 ServiceCollectionExtensions |
| XI. 單一類別檔案 | ✅ 通過 | 每個新類別獨立為一個檔案 |
| XII. RESTful API | ⬜ 不適用 | 本功能不涉及 API endpoint |
| XIII. 組態管理 | ✅ 通過 | 無新增組態，使用既有 AzureDevOps 設定 |
| XIV. 程式碼重用 | ✅ 通過 | 重用 IRedisService、IAzureDevOpsRepository、JsonExtensions 等既有元件 |

### Post-Design Re-check

| 原則 | 狀態 | 說明 |
|------|------|------|
| II. DDD/CQRS | ✅ 通過 | WorkItem 擴充 ParentWorkItemId 欄位仍為 Domain Entity 職責 |
| III. SOLID | ✅ 通過 | UserStoryResolutionStatus enum 獨立檔案、新 DTO 各自獨立 |
| VII. 避免硬編碼 | ✅ 通過 | WorkItemTypeConstants 集中管理類型清單 |
| XIV. 程式碼重用 | ✅ 通過 | 見 research.md R-006 可重用元件清單 |

## Project Structure

### Documentation (this feature)

```text
specs/001-get-user-story/
├── plan.md                                    # 本檔案
├── spec.md                                    # 需求規格
├── research.md                                # 研究結果
├── data-model.md                              # 資料模型設計
├── quickstart.md                              # 快速開始指南
├── contracts/
│   └── redis-user-story-resolution.json       # Redis 資料契約（JSON Schema）
├── checklists/
│   └── requirements.md                        # 需求品質檢查表
└── tasks.md                                   # 任務清單（由 /speckit.tasks 產生）
```

### Source Code (repository root)

```text
src/
├── ReleaseKit.Domain/
│   ├── Entities/
│   │   └── WorkItem.cs                        # [修改] 新增 ParentWorkItemId 欄位
│   └── Common/
│       └── (無變更)
│
├── ReleaseKit.Common/
│   └── Constants/
│       ├── RedisKeys.cs                       # [修改] 新增 AzureDevOpsUserStories 常數
│       └── WorkItemTypeConstants.cs           # [新增] User Story 以上類型常數集合
│
├── ReleaseKit.Application/
│   ├── Tasks/
│   │   ├── TaskType.cs                        # [修改] 新增 GetUserStory enum 值
│   │   ├── TaskFactory.cs                     # [修改] 新增 GetUserStory case
│   │   └── GetUserStoryTask.cs                # [新增] 核心任務邏輯
│   └── Common/
│       ├── UserStoryResolutionStatus.cs       # [新增] 解析結果狀態 Enum
│       ├── UserStoryResolutionOutput.cs       # [新增] 單一結果 DTO
│       ├── UserStoryResolutionResult.cs       # [新增] 彙總結果 DTO
│       └── UserStoryInfo.cs                   # [新增] User Story 資訊 DTO
│
├── ReleaseKit.Infrastructure/
│   └── AzureDevOps/
│       ├── Models/
│       │   ├── AzureDevOpsWorkItemResponse.cs # [修改] 新增 Relations 欄位
│       │   └── AzureDevOpsRelationResponse.cs # [新增] 關聯回應模型
│       └── Mappers/
│           └── AzureDevOpsWorkItemMapper.cs   # [修改] 解析 Parent ID
│
└── ReleaseKit.Console/
    ├── Parsers/
    │   └── CommandLineParser.cs               # [修改] 新增 get-user-story 指令對應
    └── Extensions/
        └── ServiceCollectionExtensions.cs     # [修改] 註冊 GetUserStoryTask

tests/
├── ReleaseKit.Application.Tests/
│   └── Tasks/
│       └── GetUserStoryTaskTests.cs           # [新增] 核心任務測試
├── ReleaseKit.Infrastructure.Tests/
│   └── AzureDevOps/
│       └── Mappers/
│           └── AzureDevOpsWorkItemMapperTests.cs  # [修改] 新增 Parent ID 解析測試
├── ReleaseKit.Console.Tests/
│   └── Parsers/
│       └── CommandLineParserTests.cs          # [修改] 新增 get-user-story 測試
└── ReleaseKit.Common.Tests/
    └── Constants/
        └── WorkItemTypeConstantsTests.cs      # [新增] 類型常數測試
```

**Structure Decision**: 遵循既有 Clean Architecture 四層結構。所有新增檔案放置於各層對應的目錄中，修改檔案僅做最小幅度擴充。無需新增專案或目錄層級。

## Complexity Tracking

> 無違規項目，所有設計遵循 Constitution 原則。
