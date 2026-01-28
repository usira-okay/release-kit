# Implementation Plan: 配置設定類別與 DI 整合

**Branch**: `002-appsettings-config` | **Date**: 2026-01-28 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/002-appsettings-config/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

本功能將為 Release-Kit 建立強型別配置類別系統，支援 appsettings.json 配置映射至 Options 類別，並透過 DI 容器注入使用。主要實作內容包括：

1. 建立各平台配置類別（GitLabOptions, BitbucketOptions, AzureDevOpsOptions, GoogleSheetOptions）
2. 實作配置驗證機制，確保必要配置項目存在
3. 支援環境變數覆寫配置
4. 遵循 TDD 開發流程，確保配置綁定與驗證邏輯的正確性

## Technical Context

**Language/Version**: C# / .NET 9  
**Primary Dependencies**: Microsoft.Extensions.Configuration, Microsoft.Extensions.Options, Microsoft.Extensions.DependencyInjection  
**Storage**: N/A (配置載入，無資料儲存需求)  
**Testing**: xUnit, FluentAssertions  
**Target Platform**: Console Application (跨平台)  
**Project Type**: single (Clean Architecture 分層結構)  
**Performance Goals**: 啟動時配置載入時間 < 100ms  
**Constraints**: 必要配置缺失時必須在啟動階段立即失敗（< 1 秒）  
**Scale/Scope**: 支援 4 個主要配置區段（GitLab, Bitbucket, AzureDevOps, GoogleSheet），約 20 個配置屬性

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### ✅ TDD (測試驅動開發)
- 所有 Options 類別必須先撰寫單元測試，驗證配置綁定與驗證邏輯
- 測試場景涵蓋：正常綁定、必要屬性缺失、型別不匹配、環境變數覆寫

### ✅ DDD/CQRS
- Options 類別屬於 Infrastructure 層的配置模型，不涉及領域邏輯
- 無 Command/Query 需求（單純配置讀取）

### ✅ SOLID 原則
- 每個 Options 類別單一職責（單一配置區段）
- 透過 IOptions<T> 介面依賴抽象

### ✅ KISS (保持簡單)
- 使用 .NET 內建 Options Pattern，不引入額外配置框架
- 配置類別為簡單 POCO，不包含複雜邏輯

### ✅ 錯誤處理策略
- 使用 Data Annotations 或啟動驗證，不使用 try-catch
- 必要配置缺失時拋出 InvalidOperationException 並包含明確訊息

### ✅ 避免硬編碼
- 所有配置值從 appsettings.json 讀取
- 環境變數支援覆寫

### ✅ 文件與註解規範
- Options 類別加入 XML 註解說明用途
- 複雜驗證邏輯加入繁體中文註解

### ✅ 程式進入點規範
- 配置註冊邏輯抽取至 AddConfigurationOptions 擴充方法
- Program.cs 保持簡潔

### ✅ 檔案組織規範
- 每個 Options 類別獨立檔案
- 檔案名稱與類別名稱一致

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── ReleaseKit.Console/
│   ├── appsettings.json                  # 主配置檔（新增配置區段）
│   ├── Program.cs                        # 已存在，無需修改（透過擴充方法註冊）
│   └── Extensions/
│       └── ServiceCollectionExtensions.cs # 已存在，擴充 AddConfigurationOptions 方法
│
├── ReleaseKit.Infrastructure/
│   ├── Configuration/                    # 【新增】配置類別目錄
│   │   ├── FetchModeOptions.cs          # 【新增】拉取模式配置（Root Level）
│   │   ├── GoogleSheetOptions.cs        # 【新增】Google Sheet 配置
│   │   ├── ColumnMappingOptions.cs      # 【新增】欄位映射配置（巢狀）
│   │   ├── AzureDevOpsOptions.cs        # 【新增】Azure DevOps 配置
│   │   ├── TeamMappingOptions.cs        # 【新增】團隊映射配置（巢狀）
│   │   ├── GitLabOptions.cs             # 【修改】擴充現有 GitLab 配置
│   │   ├── GitLabProjectOptions.cs      # 【新增】GitLab 專案配置（巢狀）
│   │   ├── BitbucketOptions.cs          # 【修改】擴充現有 Bitbucket 配置
│   │   └── BitbucketProjectOptions.cs   # 【新增】Bitbucket 專案配置（巢狀）
│   │
│   ├── GoogleSheets/                     # 已存在目錄
│   └── SourceControl/                    # 已存在目錄

tests/
├── ReleaseKit.Infrastructure.Tests/
│   └── Configuration/                    # 【新增】配置測試目錄
│       ├── FetchModeOptionsTests.cs     # 【新增】拉取模式配置測試
│       ├── GoogleSheetOptionsTests.cs   # 【新增】Google Sheet 配置測試
│       ├── AzureDevOpsOptionsTests.cs   # 【新增】Azure DevOps 配置測試
│       ├── GitLabOptionsTests.cs        # 【新增】GitLab 配置測試
│       └── BitbucketOptionsTests.cs     # 【新增】Bitbucket 配置測試
```

**Structure Decision**: 採用 Clean Architecture 的 Infrastructure 層存放配置類別，因為配置屬於外部關注點。配置類別放在 `Configuration/` 目錄，測試檔案對應同名目錄結構。

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

無違規項目。本實作完全符合 Constitution 規範。

## Workplan

- [x] **Phase 0: Research**
  - [x] 研究 .NET 9 Options Pattern 最佳實踐
  - [x] 研究 Data Annotations 驗證機制
  - [x] 研究環境變數綁定規則
  - [x] 產出 research.md

- [x] **Phase 1: Design & Contracts**
  - [x] 設計 Options 類別結構（含巢狀物件）
  - [x] 設計驗證規則（必要屬性、型別驗證）
  - [x] 產出 data-model.md
  - [x] 產出 quickstart.md
  - [x] 更新 agent context

- [ ] **Phase 2: Implementation (後續由 /speckit.tasks 執行)**
  - 由 /speckit.tasks 指令產出 tasks.md 並執行實作
