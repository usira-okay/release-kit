# Release-Kit 專案結構說明

## 專案概述

Release-Kit 是一個 .NET 9 Console 應用程式，用於從多個開發平台（GitLab、Bitbucket、Azure DevOps）收集 PR/MR 與 Work Item 資訊，並同步至 Google Sheet 以產出 Release Notes。

## 架構設計

採用 **Clean Architecture** 分層設計，遵循 DDD/CQRS 與 SOLID 原則。

```
┌─────────────────────────────────────────────────────────────┐
│                    ReleaseKit.Console                       │
│                    (Presentation Layer)                     │
├─────────────────────────────────────────────────────────────┤
│                  ReleaseKit.Infrastructure                  │
│                  (Infrastructure Layer)                     │
│    ┌─────────────┬─────────────┬─────────────────────┐     │
│    │   GitLab    │  Bitbucket  │    Azure DevOps     │     │
│    └─────────────┴─────────────┴─────────────────────┘     │
│    ┌─────────────────────────────────────────────────┐     │
│    │              Google Sheets                       │     │
│    └─────────────────────────────────────────────────┘     │
├─────────────────────────────────────────────────────────────┤
│                  ReleaseKit.Application                     │
│                   (Application Layer)                       │
│         Commands / Queries / Handlers (CQRS)               │
├─────────────────────────────────────────────────────────────┤
│                    ReleaseKit.Domain                        │
│                     (Domain Layer)                          │
│        Entities / Abstractions / ValueObjects              │
└─────────────────────────────────────────────────────────────┘
```

## 專案結構

```
release-kit/
├── src/
│   ├── release-kit.sln                 # Visual Studio Solution
│   │
│   ├── ReleaseKit.Domain/              # 領域層（核心，無外部依賴）
│   │   ├── Abstractions/               # 介面定義（Repository、Service）
│   │   ├── Entities/                   # 領域實體（WorkItem、MergeRequest）
│   │   ├── ValueObjects/               # 值物件（不可變物件）
│   │   └── Common/                     # 共用類別（Result Pattern、錯誤定義）
│   │
│   ├── ReleaseKit.Application/         # 應用層（業務流程協調）
│   │   ├── Commands/                   # CQRS Command 與 Handler
│   │   ├── Queries/                    # CQRS Query 與 Handler
│   │   └── Common/                     # 共用類別（介面、行為）
│   │
│   ├── ReleaseKit.Infrastructure/      # 基礎設施層（外部服務整合）
│   │   ├── SourceControl/
│   │   │   ├── GitLab/                 # GitLab REST API 整合
│   │   │   └── Bitbucket/              # Bitbucket REST API 整合
│   │   ├── AzureDevOps/                # Azure DevOps REST API 整合
│   │   └── GoogleSheets/               # Google Sheets API 整合
│   │
│   └── ReleaseKit.Console/             # Console 應用程式
│       └── Program.cs                  # 進入點（DI 設定、CLI 解析）
│
└── tests/
    ├── ReleaseKit.Domain.Tests/        # 領域層單元測試
    ├── ReleaseKit.Application.Tests/   # 應用層單元測試
    ├── ReleaseKit.Infrastructure.Tests/# 基礎設施層整合測試
    └── ReleaseKit.Console.Tests/       # Console 整合測試
```

## 專案參照關係

```
Domain (核心，無依賴)
   ↑
Application (依賴 Domain)
   ↑
Infrastructure (依賴 Domain、Application)
   ↑
Console (依賴 Domain、Application、Infrastructure)
```

**依賴方向：** 外層依賴內層，內層不知道外層的存在。

## 各層職責說明

### ReleaseKit.Domain

領域層是系統核心，包含業務邏輯與規則。

| 目錄 | 職責 | 範例 |
|------|------|------|
| `Abstractions/` | 定義抽象介面 | `ISourceControlRepository`、`IWorkItemRepository`、`INow` |
| `Entities/` | 領域實體與聚合根 | `WorkItem`（聚合根）、`MergeRequest` |
| `ValueObjects/` | 不可變的值物件 | `WorkItemId`、`SourceControlPlatform` |
| `Common/` | 共用基礎類別 | `Result<T>`、`Error` |

### ReleaseKit.Application

應用層負責協調業務流程，實作 CQRS 模式。

| 目錄 | 職責 | 範例 |
|------|------|------|
| `Commands/` | 寫入操作 | `SyncToGoogleSheetCommand`、`SyncToGoogleSheetHandler` |
| `Queries/` | 讀取操作 | `FetchMergeRequestsQuery`、`FetchWorkItemsQuery` |
| `Common/` | 共用介面與行為 | `ICommandHandler<T>`、`IQueryHandler<T, R>` |

### ReleaseKit.Infrastructure

基礎設施層負責與外部系統通訊。

| 目錄 | 職責 | 整合對象 |
|------|------|---------|
| `SourceControl/GitLab/` | GitLab API 呼叫 | GitLab REST API v4 |
| `SourceControl/Bitbucket/` | Bitbucket API 呼叫 | Bitbucket Cloud REST API 2.0 |
| `AzureDevOps/` | Azure DevOps API 呼叫 | Azure DevOps REST API |
| `GoogleSheets/` | Google Sheets 讀寫 | Google Sheets API v4 |
| `Time/` | 時間服務 | SystemNow (實作 INow) |

### ReleaseKit.Console

Console 應用程式進入點，負責：

- CLI 參數解析
- 組態檔載入
- DI 容器設定
- 應用程式啟動

## 核心功能

1. **拉取 PR/MR 資訊**
   - 支援時間區間篩選
   - 支援 Branch 差異比對

2. **解析 Work Item ID**
   - 從 PR/MR 標題或描述解析 `VSTS123456` 格式

3. **拉取 Work Item 詳細資訊**
   - 從 Azure DevOps 取得完整 Work Item 資料

4. **同步至 Google Sheet**
   - 增量更新模式（比對現有資料，只更新差異）
   - 以 Work Item 為主體，PR/MR 作為附屬欄位

## 開發規範

本專案遵循 `.specify/memory/constitution.md` 定義的開發憲法：

- **TDD**：所有功能實作必須遵循 Red-Green-Refactor 循環
- **DDD/CQRS**：領域驅動設計，讀寫職責分離
- **SOLID**：遵循單一職責、開放封閉等原則
- **Result Pattern**：使用結構化錯誤處理，禁止 try-catch
- **繁體中文**：所有註解與文件使用繁體中文 (zh-tw)

### 時間處理規範

- **禁止直接使用 DateTime.Now 或 DateTime.UtcNow**：所有需要取得當前時間的程式碼必須透過 `INow` 介面
- **使用 DateTimeOffset**：`INow.UtcNow` 回傳 `DateTimeOffset` 類型，確保時區資訊完整
- **統一使用 UTC 時間**：所有時間戳記必須使用 UTC，避免時區轉換問題
- **可測試性**：透過 DI 注入 `INow`，測試時可使用 Mock 或 Fake 實作

### 組態設定規範

- **必要組態不提供預設值**：所有必要的組態設定（如 Redis:ConnectionString）若未設定必須拋出 `InvalidOperationException`
- **明確錯誤訊息**：錯誤訊息必須清楚指出缺少哪個組態鍵值
- **組態驗證時機**：在應用程式啟動時進行組態驗證，避免執行時才發現問題

## 組態管理

- **組態檔**：JSON/YAML 格式儲存 API URL、專案 ID 等設定
- **敏感資訊**：透過環境變數管理（API Token、Credentials）

## 執行方式

手動執行的 CLI 工具，供開發者或 PM 在需要產出 Release Notes 時使用。

## 開發指引

**請遵循 .specify\memory\constitution.md** 的規範
