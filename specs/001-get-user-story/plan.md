# Implementation Plan: Get User Story

**Branch**: `001-get-user-story` | **Date**: 2026-02-14 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-get-user-story/spec.md`

## Summary

為 release-kit 新增三項功能：(1) PR 資料結構新增 PullRequestId 欄位，(2) Work Item 抓取邏輯重構為保留 PR 來源關聯的一對一記錄，(3) 新增 `get-user-story` 指令透過 Azure DevOps API 遞迴向上查詢 parent 直到 User Story/Feature/Epic 層級。

技術方案採用分離式任務（Approach A），三個功能獨立實作，遵循既有 ITask 任務模式。新增 `GetUserStoryTask` 讀取 Redis 中已抓取的 Work Item 後，透過 `IAzureDevOpsRepository.GetWorkItemAsync` 遞迴查詢 parent，結果存至新的 Redis key `AzureDevOps:UserStories`。

## Technical Context

**Language/Version**: C# / .NET 9.0
**Primary Dependencies**: StackExchange.Redis, System.Text.Json, Serilog, Microsoft.Extensions.DependencyInjection
**Storage**: Redis（透過 IRedisService 抽象存取）
**Testing**: xUnit, Moq
**Target Platform**: Linux / Windows Console Application
**Project Type**: Console（多層式架構：Domain / Application / Infrastructure / Common / Console）
**Performance Goals**: 無特定效能目標，遞迴查詢 parent 時使用快取避免重複 API 呼叫
**Constraints**: Azure DevOps API 速率限制；遞迴深度上限 10 層以避免無限迴圈
**Scale/Scope**: 單一 Redis instance，Work Item 數量預估數十至數百筆

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| 原則 | 狀態 | 說明 |
|------|------|------|
| I. TDD（不可妥協） | ✅ 通過 | 所有功能遵循 Red-Green-Refactor，先寫測試再實作 |
| II. DDD & CQRS | ✅ 通過 | 新增 `ParentWorkItemId` 到 Domain Entity；新增 DTO 在 Application Layer；Mapper 在 Infrastructure Layer |
| III. SOLID | ✅ 通過 | 單一職責：每個 Task 獨立職責；依賴反轉：透過 `IAzureDevOpsRepository` 與 `IRedisService` 抽象 |
| IV. KISS | ✅ 通過 | 最小變更，重用既有 ITask 模式與 Result Pattern |
| V. 錯誤處理 | ✅ 通過 | 使用 Result Pattern，不使用 try-catch |
| VI. 效能與快取優先 | ✅ 通過 | 重複的 Work Item ID 使用 Dictionary 快取，僅查詢一次 API |
| VII. 避免硬編碼 | ✅ 通過 | Redis Key 使用 `RedisKeys` 常數；高層級類型使用 HashSet 常數 |
| VIII. 文件與註解 | ✅ 通過 | 所有公開成員加入 XML Summary 繁體中文註解 |
| IX. JSON 序列化 | ✅ 通過 | 使用 JsonExtensions（ToJson / ToTypedObject）；外部 API 模型使用 JsonPropertyName（符合例外條件） |
| X. 程式進入點 | ✅ 通過 | Program.cs 不做變更，服務註冊透過 ServiceCollectionExtensions |
| XI. 檔案組織 | ✅ 通過 | 每個新類別獨立檔案，檔名與類名一致 |

**Gate 結果**: 全部通過，無需 Complexity Tracking。

## Project Structure

### Documentation (this feature)

```text
specs/001-get-user-story/
├── spec.md              # 功能規格書
├── plan.md              # 本檔案（實作計畫）
├── research.md          # Phase 0 研究結論
├── data-model.md        # Phase 1 資料模型
├── quickstart.md        # Phase 1 快速上手指引
└── checklists/
    └── requirements.md  # 規格品質檢核清單
```

### Source Code (repository root)

```text
src/
├── ReleaseKit.Domain/
│   ├── Entities/
│   │   ├── MergeRequest.cs          # [修改] 新增 PullRequestId 屬性
│   │   └── WorkItem.cs              # [修改] 新增 ParentWorkItemId 屬性
│   └── Abstractions/
│       └── IAzureDevOpsRepository.cs  # [不變] 已有 GetWorkItemAsync，API 已使用 $expand=all
│
├── ReleaseKit.Application/
│   ├── Common/
│   │   ├── MergeRequestOutput.cs    # [修改] 新增 PullRequestId 欄位
│   │   ├── WorkItemOutput.cs        # [修改] 新增 SourcePullRequestId, SourceProjectName, SourcePRUrl
│   │   ├── UserStoryOutput.cs       # [新增] User Story 解析結果 DTO
│   │   └── UserStoryFetchResult.cs  # [新增] User Story 解析彙整 DTO
│   └── Tasks/
│       ├── TaskType.cs              # [修改] 新增 GetUserStory 列舉值
│       ├── TaskFactory.cs           # [修改] 新增 GetUserStory case
│       ├── FetchAzureDevOpsWorkItemsTask.cs  # [修改] 重構為保留 PR 來源關聯
│       ├── BaseFetchPullRequestsTask.cs      # [修改] 輸出映射加入 PullRequestId
│       └── GetUserStoryTask.cs      # [新增] User Story 解析任務
│
├── ReleaseKit.Infrastructure/
│   ├── AzureDevOps/
│   │   ├── Models/
│   │   │   ├── AzureDevOpsWorkItemResponse.cs    # [修改] 新增 Relations 欄位
│   │   │   └── AzureDevOpsRelationResponse.cs    # [新增] Relation 回應模型
│   │   └── Mappers/
│   │       └── AzureDevOpsWorkItemMapper.cs      # [修改] 新增 ExtractParentWorkItemId
│   └── SourceControl/
│       ├── GitLab/
│       │   └── GitLabMergeRequestMapper.cs       # [修改] 映射 PullRequestId = Iid
│       └── Bitbucket/
│           └── BitbucketPullRequestMapper.cs     # [修改] 映射 PullRequestId = Id
│
├── ReleaseKit.Common/
│   └── Constants/
│       └── RedisKeys.cs             # [修改] 新增 AzureDevOpsUserStories
│
└── ReleaseKit.Console/
    ├── Parsers/
    │   └── CommandLineParser.cs     # [修改] 新增 get-user-story 映射
    └── Extensions/
        └── ServiceCollectionExtensions.cs  # [修改] 註冊 GetUserStoryTask

tests/
├── ReleaseKit.Infrastructure.Tests/
│   ├── SourceControl/
│   │   ├── GitLab/
│   │   │   └── GitLabMergeRequestMapperTests.cs      # [修改] 驗證 PullRequestId
│   │   └── Bitbucket/
│   │       └── BitbucketPullRequestMapperTests.cs    # [修改] 驗證 PullRequestId
│   └── AzureDevOps/
│       └── Mappers/
│           └── AzureDevOpsWorkItemMapperTests.cs     # [新增] 驗證 ParentWorkItemId
├── ReleaseKit.Application.Tests/
│   └── Tasks/
│       ├── FetchAzureDevOpsWorkItemsTaskTests.cs     # [修改] 驗證 PR 來源欄位
│       ├── TaskFactoryTests.cs                        # [修改] 驗證 GetUserStory
│       └── GetUserStoryTaskTests.cs                   # [新增] 完整解析場景測試
└── ReleaseKit.Console.Tests/
    └── Parsers/
        └── CommandLineParserTests.cs                  # [修改] 驗證 get-user-story
```

**Structure Decision**: 遵循既有多層式架構，新增檔案置於對應層級目錄。無需新增專案或新架構層級。

## Complexity Tracking

> 無違規項目，不需要記錄。

## Reusable Components

| 元件 | 路徑 | 重用方式 |
|------|------|----------|
| `Result<T>` Pattern | `Domain/Common/Result.cs` | GetUserStoryTask 使用現有 Result 處理 API 回傳 |
| `IAzureDevOpsRepository` | `Domain/Abstractions/` | GetUserStoryTask 注入使用，呼叫 `GetWorkItemAsync` |
| `IRedisService` | `Domain/Abstractions/` | GetUserStoryTask 注入使用，讀寫 Redis |
| `JsonExtensions` | `Common/Extensions/` | `ToJson()` / `ToTypedObject<T>()` 序列化 |
| `ITask` interface | `Domain/Abstractions/` | GetUserStoryTask 實作此介面 |
| `TaskFactory` | `Application/Tasks/` | 新增 switch case |
| `WorkItemFetchResult` | `Application/Common/` | GetUserStoryTask 讀取此結構作為輸入 |
| `FetchAzureDevOpsWorkItemsTask` | `Application/Tasks/` | 重構參考，保留相同的 Redis 讀寫模式 |
