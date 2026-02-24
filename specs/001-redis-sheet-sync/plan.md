# Implementation Plan: Redis → Google Sheet 批次同步

**Branch**: `001-redis-sheet-sync` | **Date**: 2026-02-24 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-redis-sheet-sync/spec.md`

## Summary

實作 `UpdateGoogleSheetsTask`，將 Redis 中 `ReleaseData:Consolidated` 的整合資料批次同步至 Google Sheet。包含：修正 `ConsolidateReleaseDataTask` 無資料時拋錯行為、建立 `IGoogleSheetService` 介面與實作、實作完整的新增/更新/排序邏輯。

## Technical Context

**Language/Version**: C# / .NET 9.0
**Primary Dependencies**: Google.Apis.Sheets.v4（新增）、StackExchange.Redis（既有）、Microsoft.Extensions.*（既有）
**Storage**: Redis（既有，讀取整合資料）、Google Sheets API v4（新增，寫入目標）
**Testing**: xUnit 2.9.2 + Moq 4.20.72（既有）
**Target Platform**: Console Application (Linux/Windows)
**Project Type**: Clean Architecture 多層專案（Domain / Application / Infrastructure / Console）
**Performance Goals**: 批次操作減少 API 呼叫次數，避免逐列操作
**Constraints**: Google Sheets API 配額限制（每分鐘 60 次讀取、60 次寫入）；欄位範圍限制 A–Z
**Scale/Scope**: 單次同步涉及數十至數百筆 Release 資料

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| 原則 | 狀態 | 說明 |
|------|------|------|
| I. TDD | ✅ | 所有新增邏輯遵循 Red-Green-Refactor |
| II. DDD/CQRS | ✅ | IGoogleSheetService 定義於 Domain，實作於 Infrastructure；Task 為 Application 層 |
| III. SOLID | ✅ | 單一職責（Task/Service 分離）、依賴反轉（介面注入）、介面隔離 |
| IV. KISS | ✅ | 重用現有 ITask 模式、IRedisService、JsonExtensions |
| V. 錯誤處理 | ✅ | 無 try-catch；無資料時回傳並結束，不拋例外 |
| VI. 效能與快取 | ✅ | 批次讀取/寫入 Sheet 減少 API 呼叫；重用 IRedisService |
| VII. 避免硬編碼 | ✅ | 欄位對應使用 ColumnMappingOptions 設定 |
| VIII. 文件與註解 | ✅ | 繁體中文 XML Summary 註解 |
| IX. JSON 序列化 | ✅ | 使用 JsonExtensions 反序列化 Redis 資料 |
| X. Program.cs 整潔 | ✅ | 服務註冊於 ServiceCollectionExtensions |
| XI. 單一類別檔案 | ✅ | 每個新類別/介面獨立檔案 |
| XII. RESTful API | N/A | 本功能無 API 端點 |
| XIII. 組態管理 | ✅ | GoogleSheetOptions 已設定於 appsettings.json |
| XIV. 程式碼重用 | ✅ | 見下方可重用元件清單 |

### 可重用元件清單

| 元件 | 位置 | 用途 |
|------|------|------|
| `IRedisService` | Domain/Abstractions | 讀取 Redis 整合資料 |
| `RedisService` | Infrastructure/Redis | IRedisService 實作 |
| `RedisKeys` | Common/Constants | Redis 鍵值常數 |
| `JsonExtensions` | Common/Extensions | JSON 反序列化 |
| `ConsolidatedReleaseResult` | Application/Common | 整合資料模型 |
| `ConsolidatedReleaseEntry` | Application/Common | 單筆資料模型 |
| `ITask` | Domain/Abstractions | Task 介面 |
| `TaskFactory` | Application/Tasks | Task 建立工廠 |
| `GoogleSheetOptions` | Infrastructure/Configuration | Google Sheet 設定 |
| `ColumnMappingOptions` | Infrastructure/Configuration | 欄位對應設定 |
| `Result<T>` / `Error` | Domain/Common | 結構化錯誤處理 |

## Project Structure

### Documentation (this feature)

```text
specs/001-redis-sheet-sync/
├── plan.md              # 本檔案
├── research.md          # Phase 0 研究結果
├── data-model.md        # Phase 1 資料模型
├── quickstart.md        # Phase 1 快速開始指南
├── contracts/           # Phase 1 介面契約
│   └── IGoogleSheetService.md
└── tasks.md             # Phase 2 任務清單（由 /speckit.tasks 產生）
```

### Source Code (repository root)

```text
src/
├── ReleaseKit.Domain/
│   └── Abstractions/
│       └── IGoogleSheetService.cs          # 新增：Google Sheet 服務介面
│
├── ReleaseKit.Application/
│   ├── Tasks/
│   │   ├── UpdateGoogleSheetsTask.cs       # 修改：實作同步邏輯
│   │   └── ConsolidateReleaseDataTask.cs   # 修改：無資料時不拋錯
│   └── Common/
│       └── (既有模型，不修改)
│
├── ReleaseKit.Infrastructure/
│   ├── GoogleSheets/
│   │   └── GoogleSheetService.cs           # 新增：Google Sheets API 實作
│   └── ReleaseKit.Infrastructure.csproj    # 修改：新增 Google.Apis.Sheets.v4
│
└── ReleaseKit.Console/
    └── Extensions/
        └── ServiceCollectionExtensions.cs  # 修改：註冊 IGoogleSheetService

tests/
├── ReleaseKit.Application.Tests/
│   └── Tasks/
│       ├── UpdateGoogleSheetsTaskTests.cs  # 新增：同步邏輯單元測試
│       └── ConsolidateReleaseDataTaskTests.cs  # 修改：新增無資料情境測試
└── ReleaseKit.Infrastructure.Tests/
    └── GoogleSheets/
        └── GoogleSheetServiceTests.cs      # 新增：Google Sheet 服務測試
```

**Structure Decision**: 沿用現有 Clean Architecture 分層結構，新增檔案放置於對應層級的既有目錄中。

## Complexity Tracking

> 無違反 Constitution 的項目，無需額外記錄。
