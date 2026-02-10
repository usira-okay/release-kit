# Implementation Plan: 取得各 Repository 最新 Release Branch 名稱

**Branch**: `002-fetch-release-branch` | **Date**: 2026-02-10 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/002-fetch-release-branch/spec.md`

## Summary

新增兩個 CLI 指令（`fetch-gitlab-release-branch` 與 `fetch-bitbucket-release-branch`），依照現有 Task 設計模式，對各已設定專案查詢最新 release branch 名稱，將結果依分支名稱分組後，輸出 JSON 至 Console 並存入 Redis。複用現有 `ISourceControlRepository.GetBranchesAsync` 方法，以及現有的 GitLab / Bitbucket 設定檔結構。

## Technical Context

**Language/Version**: C# / .NET 9
**Primary Dependencies**: Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Options, Microsoft.Extensions.Logging, StackExchange.Redis, Serilog, System.Text.Json
**Storage**: Redis（透過 `IRedisService` 介面）
**Testing**: xUnit + Moq
**Target Platform**: Linux / Windows Console Application
**Project Type**: Clean Architecture（Domain / Application / Infrastructure / Console / Common）
**Performance Goals**: N/A（批次查詢，非即時服務）
**Constraints**: 遵循 Constitution 所有規範
**Scale/Scope**: 依設定檔中的專案數量，通常 10-50 個專案

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| 原則 | 狀態 | 說明 |
|------|------|------|
| I. TDD | ✅ 通過 | 所有新增類別皆遵循 Red-Green-Refactor 循環 |
| II. DDD/CQRS | ✅ 通過 | 複用現有 Domain 層 `ISourceControlRepository` 介面 |
| III. SOLID | ✅ 通過 | 基底類別 + 具體任務遵循 OCP/SRP；依賴注入遵循 DIP |
| IV. KISS | ✅ 通過 | 複用現有元件，僅新增必要的類別 |
| V. 錯誤處理 | ✅ 通過 | 使用 Result Pattern（`GetBranchesAsync` 回傳 `Result<T>`） |
| VI. 效能與快取 | ✅ 通過 | 複用現有 `GetBranchesAsync`；結果存入 Redis |
| VII. 避免硬編碼 | ✅ 通過 | Redis Key 使用 `RedisKeys` 常數；分支前綴使用常數 |
| VIII. 文件與註解 | ✅ 通過 | 所有公開類別與方法加入 XML summary（繁體中文） |
| IX. JSON 序列化 | ✅ 通過 | 使用 `JsonExtensions.ToJson()` |
| X. 進入點規範 | ✅ 通過 | Program.cs 不變，僅修改 `ServiceCollectionExtensions` |
| XI. 檔案組織 | ✅ 通過 | 一個檔案一個類別 |

**結論**: 無違規，可進入 Phase 0。

## Project Structure

### Documentation (this feature)

```text
specs/002-fetch-release-branch/
├── plan.md              # 本文件
├── spec.md              # 功能規格
├── research.md          # Phase 0 研究輸出
├── data-model.md        # Phase 1 資料模型
├── quickstart.md        # Phase 1 快速上手指南
└── checklists/
    └── requirements.md  # 規格品質檢查清單
```

### Source Code (repository root)

```text
src/
├── ReleaseKit.Domain/
│   └── Abstractions/
│       └── ISourceControlRepository.cs      # [既有] 已含 GetBranchesAsync
│       └── ITask.cs                         # [既有] 任務介面
│       └── IRedisService.cs                 # [既有] Redis 服務介面
│
├── ReleaseKit.Common/
│   ├── Constants/
│   │   └── RedisKeys.cs                     # [修改] 新增 GitLabReleaseBranches / BitbucketReleaseBranches
│   └── Configuration/
│       └── GitLabOptions.cs                 # [既有] 不修改
│       └── BitbucketOptions.cs              # [既有] 不修改
│       └── IProjectOptions.cs               # [既有] 不修改
│
├── ReleaseKit.Application/
│   ├── Common/
│   │   └── ReleaseBranchResult.cs           # [新增] 輸出 DTO
│   └── Tasks/
│       ├── TaskType.cs                      # [修改] 新增 FetchGitLabReleaseBranches / FetchBitbucketReleaseBranches
│       ├── TaskFactory.cs                   # [修改] 新增兩個 case
│       ├── BaseFetchReleaseBranchTask.cs    # [新增] 基底任務類別
│       ├── FetchGitLabReleaseBranchTask.cs  # [新增] GitLab 具體任務
│       └── FetchBitbucketReleaseBranchTask.cs # [新增] Bitbucket 具體任務
│
├── ReleaseKit.Console/
│   ├── Parsers/
│   │   └── CommandLineParser.cs             # [修改] 新增兩個指令對應
│   └── Extensions/
│       └── ServiceCollectionExtensions.cs   # [修改] 註冊新的 Task

tests/
├── ReleaseKit.Application.Tests/
│   └── Tasks/
│       ├── FetchGitLabReleaseBranchTaskTests.cs       # [新增] GitLab Release Branch 任務測試
│       ├── FetchBitbucketReleaseBranchTaskTests.cs    # [新增] Bitbucket Release Branch 任務測試
│       ├── ReleaseBranchRedisIntegrationTests.cs      # [新增] Redis Key 使用正確性測試
│       └── TaskFactoryTests.cs                        # [修改] 新增兩個 TaskType 測試
├── ReleaseKit.Console.Tests/
│   └── Parsers/
│       └── CommandLineParserTests.cs                  # [修改] 新增兩個指令解析測試
```

**Structure Decision**: 遵循現有 Clean Architecture 結構，新增檔案放置於對應的層級目錄中。

## Complexity Tracking

無違規，不需要複雜度追蹤。

## Reusable Components

以下為本功能可直接複用的現有元件：

| 元件 | 路徑 | 用途 |
|------|------|------|
| `ISourceControlRepository.GetBranchesAsync` | Domain/Abstractions | 查詢分支（支援 `release/` 前綴篩選） |
| `IRedisService` | Domain/Abstractions | Redis 存取 |
| `ITask` | Domain/Abstractions | 任務介面 |
| `RedisKeys` | Common/Constants | Redis Key 常數集中管理 |
| `JsonExtensions.ToJson()` | Common/Extensions | JSON 序列化 |
| `GitLabOptions` / `BitbucketOptions` | Common/Configuration | 平台設定（含 Projects 清單） |
| `IProjectOptions` | Common/Configuration | 專案設定介面（取 ProjectPath） |
| `TaskFactory` | Application/Tasks | 工廠模式建立任務 |
| `CommandLineParser` | Console/Parsers | CLI 指令解析 |
| `ServiceCollectionExtensions` | Console/Extensions | DI 服務註冊 |
| `GitLabRepository` / `BitbucketRepository` | Infrastructure/SourceControl | 已實作 `GetBranchesAsync` |
