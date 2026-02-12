# Implementation Plan: Filter Pull Requests by User

**Branch**: `001-filter-pr-by-user` | **Date**: 2026-02-12 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-filter-pr-by-user/spec.md`

## Summary

建立兩個新的 CLI 指令（`filter-gitlab-pr-by-user` 和 `filter-bitbucket-pr-by-user`），從 Redis 讀取已擷取的 PR 資料，依據 `appsettings.json` 中的 `UserMapping.Mappings` 使用者清單過濾 PR，僅保留指定使用者的 PR，並將過濾結果寫入新的 Redis Key。

技術方案採用現有的 Task/Command 模式，建立抽象基底類別封裝共用過濾邏輯，兩個平台各自繼承實作。

## Technical Context

**Language/Version**: C# / .NET 9
**Primary Dependencies**: Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Options, Microsoft.Extensions.Logging, StackExchange.Redis, Serilog, System.Text.Json
**Storage**: Redis（透過 `IRedisService` 介面）
**Testing**: xUnit + Moq
**Target Platform**: Linux / Docker Container (Console Application)
**Project Type**: Single (Console Application with layered architecture)
**Performance Goals**: N/A（批次處理，非即時服務）
**Constraints**: 無額外約束，僅需在現有架構下擴充
**Scale/Scope**: 過濾現有 Redis 中的 PR 資料，資料量取決於 fetch 指令的結果

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| 原則 | 狀態 | 說明 |
|------|------|------|
| I. TDD | ✅ 通過 | 將遵循 Red-Green-Refactor 循環，先寫測試再實作 |
| II. DDD & CQRS | ✅ 通過 | 本功能為 Query 操作（讀取 → 過濾 → 寫入），不涉及 Command 邏輯 |
| III. SOLID | ✅ 通過 | 使用抽象基底類別（SRP, OCP），子類別可替換（LSP），ITask 介面（ISP, DIP） |
| IV. KISS | ✅ 通過 | 過濾邏輯簡單直觀：讀取 → LINQ Where → 寫入 |
| V. 錯誤處理 | ✅ 通過 | 無 try-catch，JSON 反序列化失敗讓例外自然向上傳遞 |
| VI. 效能優先 | ✅ 通過 | 從 Redis 快取讀取資料，符合快取優先原則 |
| VII. 避免硬編碼 | ✅ 通過 | Redis Key 使用 `RedisKeys` 常數類別管理 |
| VIII. 文件註解 | ✅ 通過 | 所有公開類別與方法加入繁體中文 XML Summary |
| IX. JSON 規範 | ✅ 通過 | 使用 `JsonExtensions.ToJson()` 和 `ToTypedObject<T>()` |
| X. Program.cs | ✅ 通過 | 僅在 `ServiceCollectionExtensions` 新增 DI 註冊 |
| XI. 檔案組織 | ✅ 通過 | 一個類別一個檔案 |

**結論**: 所有 Constitution 原則均通過，無需 Complexity Tracking。

## Project Structure

### Documentation (this feature)

```text
specs/001-filter-pr-by-user/
├── spec.md              # 功能規格
├── plan.md              # 本文件
├── research.md          # Phase 0 研究結果
├── data-model.md        # Phase 1 資料模型
├── quickstart.md        # Phase 1 快速開始指引
└── tasks.md             # Phase 2 任務清單（由 /speckit.tasks 產生）
```

### Source Code (repository root)

```text
src/
├── ReleaseKit.Common/
│   └── Constants/
│       └── RedisKeys.cs                          # 新增 2 個常數
├── ReleaseKit.Application/
│   └── Tasks/
│       ├── TaskType.cs                           # 新增 2 個 enum 值
│       ├── TaskFactory.cs                        # 新增 2 個 case
│       ├── BaseFilterPullRequestsByUserTask.cs   # 新建：過濾基底類別
│       ├── FilterGitLabPullRequestsByUserTask.cs # 新建：GitLab 過濾
│       └── FilterBitbucketPullRequestsByUserTask.cs # 新建：Bitbucket 過濾
├── ReleaseKit.Console/
│   ├── Parsers/
│   │   └── CommandLineParser.cs                  # 新增 2 個指令對應
│   └── Extensions/
│       └── ServiceCollectionExtensions.cs        # 新增 DI 註冊

tests/
└── ReleaseKit.Application.Tests/
    └── Tasks/
        └── FilterPullRequestsByUserTaskTests.cs  # 新建：過濾任務測試
```

**Structure Decision**: 沿用現有分層架構。新功能僅在 Application Layer 新增 Task 類別，不需要新增 Domain 或 Infrastructure 元件。`UserMappingOptions` 已在 Console 層存在並完成 DI 註冊。

## Complexity Tracking

> 無違反項目，不需記錄。

## Reusable Components

以下現有元件可直接重用，無需重複實作：

| 元件 | 位置 | 用途 |
|------|------|------|
| `IRedisService` | Domain/Abstractions | Redis 讀寫操作 |
| `JsonExtensions` | Common/Extensions | JSON 序列化/反序列化 |
| `FetchResult` | Application/Common | 輸出資料結構 |
| `ProjectResult` | Application/Common | 專案結果資料結構 |
| `MergeRequestOutput` | Application/Common | PR 輸出模型 |
| `RedisKeys` | Common/Constants | Redis Key 常數（擴充） |
| `UserMappingOptions` | Console/Options | 使用者對應設定（已完成 DI 註冊） |
| `ITask` | Domain/Abstractions | 任務介面 |
| `TaskFactory` | Application/Tasks | 任務工廠（擴充） |
| `CommandLineParser` | Console/Parsers | 指令解析器（擴充） |
