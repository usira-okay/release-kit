# Implementation Plan: Azure DevOps Work Item 資訊擷取

**Branch**: `003-fetch-azure-workitems` | **Date**: 2026-02-13 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/003-fetch-azure-workitems/spec.md`

## Summary

實作 `fetch-azure-workitems` Console 指令，從 Redis 讀取已過濾的 GitLab/Bitbucket PR 資料，以正規表達式解析 PR 標題中的 `VSTS{number}` Work Item ID，逐一呼叫 Azure DevOps REST API 取得 Work Item 詳細資訊，最終將結果寫入 Redis 並輸出統計摘要。

技術方案遵循既有 Clean Architecture 分層設計，重複使用 `IRedisService`、`Result<T>` Pattern、`JsonExtensions`、`IHttpClientFactory` Named Client 等既有元件，新增 `IAzureDevOpsRepository` 抽象、`AzureDevOpsRepository` 實作、以及相關 Domain Entity 與 Application DTO。

## Technical Context

**Language/Version**: C# / .NET 9
**Primary Dependencies**: Microsoft.Extensions.Http (IHttpClientFactory), StackExchange.Redis (via IRedisService), System.Text.Json (via JsonExtensions), Microsoft.Extensions.Logging, Microsoft.Extensions.Options
**Storage**: Redis（透過既有 `IRedisService` 介面）
**Testing**: xUnit + Moq（遵循既有測試模式）
**Target Platform**: Linux Docker Container (Console Application)
**Project Type**: Multi-project Clean Architecture（Domain / Application / Infrastructure / Common / Console）
**Performance Goals**: 逐一循序呼叫 Azure DevOps API，無併發需求
**Constraints**: 無 TTL、無 TeamMapping 轉換（本階段）、無 PR 與 Work Item 反向關聯
**Scale/Scope**: PR 數量通常在百筆以內，Work Item ID 去重複後數量更少

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | 原則 | 狀態 | 說明 |
|---|------|------|------|
| I | TDD | ✅ 通過 | 所有新增類別均有對應測試，遵循 Red-Green-Refactor |
| II | DDD & CQRS | ✅ 通過 | WorkItem 為 Domain Entity，IAzureDevOpsRepository 定義於 Domain 層 |
| III | SOLID | ✅ 通過 | 單一職責（Repository 只負責 API 呼叫、Mapper 只負責轉換、Task 只負責流程協調） |
| IV | KISS | ✅ 通過 | 循序呼叫，無併發、無 TeamMapping，保持簡單 |
| V | 錯誤處理 | ✅ 通過 | 使用 Result Pattern，不使用 try-catch |
| VI | 效能與快取 | ✅ 通過 | 從 Redis 讀取已快取的 PR 資料 |
| VII | 避免硬編碼 | ✅ 通過 | Redis Key、HttpClient Name 使用常數，API URL/PAT 透過設定檔 |
| VIII | 文件與註解 | ✅ 通過 | 所有公開成員加 XML Summary（繁體中文） |
| IX | JSON 序列化 | ✅ 通過 | 使用 JsonExtensions；API Response Model 使用 [JsonPropertyName]（外部 API 契約例外） |
| X | Program.cs 整潔 | ✅ 通過 | 不修改 Program.cs，DI 註冊透過 ServiceCollectionExtensions |
| XI | 檔案組織 | ✅ 通過 | 一個檔案一個類別 |

## Reusable Components

| 元件 | 位置 | 重用方式 |
|------|------|---------|
| `ITask` 介面 | `Domain/Abstractions/ITask.cs` | FetchAzureDevOpsWorkItemsTask 實作此介面 |
| `IRedisService` | `Domain/Abstractions/IRedisService.cs` | 注入使用 GetAsync/SetAsync |
| `Result<T>` | `Domain/Common/Result.cs` | IAzureDevOpsRepository 回傳型別 |
| `Error` | `Domain/Common/Error.cs` | 擴充新增 AzureDevOps 錯誤類別 |
| `JsonExtensions` | `Common/Extensions/JsonExtensions.cs` | ToJson() 與 ToTypedObject<T>() |
| `RedisKeys` | `Common/Constants/RedisKeys.cs` | 擴充新增 AzureDevOpsWorkItems |
| `HttpClientNames` | `Common/Constants/HttpClientNames.cs` | 擴充新增 AzureDevOps |
| `AzureDevOpsOptions` | `Infrastructure/Configuration/AzureDevOpsOptions.cs` | 注入 OrganizationUrl、PAT |
| `IHttpClientFactory` | ServiceCollectionExtensions | 同 GitLab/Bitbucket 的 Named Client 模式 |
| `FetchResult` / `MergeRequestOutput` | `Application/Common/` | 從 Redis 反序列化 PR 資料的型別 |

## Project Structure

### Documentation (this feature)

```text
specs/003-fetch-azure-workitems/
├── spec.md              # 功能規格書
├── plan.md              # 本文件（實作計畫）
├── research.md          # Phase 0 研究結果
├── data-model.md        # Phase 1 資料模型定義
├── quickstart.md        # Phase 1 快速開始指引
├── contracts/           # Phase 1 API 契約
│   ├── azure-devops-api.md        # Azure DevOps API 消費契約
│   └── redis-output-contract.md   # Redis 輸出資料契約
└── tasks.md             # Phase 2 任務清單（由 /speckit.tasks 產出）
```

### Source Code (repository root)

```text
src/
├── ReleaseKit.Domain/
│   ├── Abstractions/
│   │   ├── ITask.cs                        # （既有）
│   │   ├── IRedisService.cs                # （既有）
│   │   └── IAzureDevOpsRepository.cs       # 【新增】Azure DevOps Repository 介面
│   ├── Entities/
│   │   ├── MergeRequest.cs                 # （既有）
│   │   └── WorkItem.cs                     # 【新增】Work Item 領域實體
│   └── Common/
│       ├── Error.cs                        # 【修改】新增 AzureDevOps 錯誤類別
│       └── Result.cs                       # （既有）
│
├── ReleaseKit.Application/
│   ├── Tasks/
│   │   └── FetchAzureDevOpsWorkItemsTask.cs  # 【修改】實作完整邏輯
│   └── Common/
│       ├── FetchResult.cs                    # （既有）
│       ├── MergeRequestOutput.cs             # （既有）
│       ├── WorkItemOutput.cs                 # 【新增】Work Item 輸出 DTO
│       └── WorkItemFetchResult.cs            # 【新增】查詢結果彙整 DTO
│
├── ReleaseKit.Infrastructure/
│   └── AzureDevOps/
│       ├── AzureDevOpsRepository.cs                   # 【新增】API Client 實作
│       ├── Models/
│       │   └── AzureDevOpsWorkItemResponse.cs         # 【新增】API Response Model
│       └── Mappers/
│           └── AzureDevOpsWorkItemMapper.cs            # 【新增】Response → Entity Mapper
│
├── ReleaseKit.Common/
│   └── Constants/
│       ├── RedisKeys.cs                    # 【修改】新增 AzureDevOpsWorkItems
│       └── HttpClientNames.cs              # 【修改】新增 AzureDevOps
│
└── ReleaseKit.Console/
    └── Extensions/
        └── ServiceCollectionExtensions.cs  # 【修改】新增 HttpClient + Repository DI

tests/
├── ReleaseKit.Application.Tests/
│   └── Tasks/
│       └── FetchAzureDevOpsWorkItemsTaskTests.cs  # 【新增】Task 單元測試
│
└── ReleaseKit.Infrastructure.Tests/
    └── AzureDevOps/
        └── AzureDevOpsRepositoryTests.cs          # 【新增】Repository 單元測試
```

**Structure Decision**: 遵循既有 Clean Architecture 分層結構，新增檔案放置於對應層級的既有目錄中。AzureDevOps 目錄已在 Infrastructure 層存在（AGENTS.md 架構圖中標示），本次在其下新增 Repository、Models、Mappers 子目錄。

## Complexity Tracking

> 無憲法違規需要記錄。所有設計決策均符合 Constitution 原則。

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| （無）| — | — |
