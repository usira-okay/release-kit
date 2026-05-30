# Release-Kit

Release-Kit 是一個 .NET 10 Console 應用程式，用於從多個開發平台（GitLab、Bitbucket、Azure DevOps）收集 PR/MR 與 Work Item 資訊，並同步至 Google Sheet 以產出 Release Notes。

## 功能特色

- 支援多平台整合：GitLab、Bitbucket、Azure DevOps
- 自動解析 Work Item ID
- 同步至 Google Sheets
- Redis 快取支援
- Seq 結構化日誌

## 快速開始

### 使用 Docker（推薦）

```bash
docker-compose up -d
```

詳細說明請參考 [Docker 使用指南](DOCKER.md)

### 本機開發

#### 必要條件

- .NET 10 SDK
- Redis（可選）
- Seq（可選）

#### 建置與執行

```bash
cd src/ReleaseKit.Console
dotnet build
dotnet run -- <task-name>
```

#### 可用指令

執行應用程式時需指定任務名稱：

```bash
dotnet run -- <task-name>
```

| #   | 指令                          | 說明                                                                                                                            |
| --- | ----------------------------- | ------------------------------------------------------------------------------------------------------------------------------- |
| 1   | `fetch-gitlab-pr`             | 從 GitLab API 拉取各專案的 Pull Request（Merge Request）資訊，支援時間區間與 Branch 差異模式，結果存入 Redis                    |
| 2   | `filter-gitlab-pr-by-user`    | 依 UserMapping 設定中的 GitLabUserId 過濾已拉取的 GitLab PR，僅保留團隊成員的 PR，結果存入 Redis                                |
| 3   | `fetch-bitbucket-pr`          | 從 Bitbucket API 拉取各專案的 Pull Request 資訊，支援時間區間與 Branch 差異模式，結果存入 Redis                                 |
| 4   | `filter-bitbucket-pr-by-user` | 依 UserMapping 設定中的 BitbucketUserId 過濾已拉取的 Bitbucket PR，僅保留團隊成員的 PR，結果存入 Redis                          |
| 5   | `fetch-azure-workitems`       | 從已過濾的 PR 來源分支名稱中解析 Work Item ID（VSTS 格式），並透過 Azure DevOps API 查詢各 Work Item 的詳細資訊，結果存入 Redis |
| 6   | `get-user-story`              | 將低於 User Story 層級的 Work Item（如 Bug、Task）遞迴查詢其 Parent，直到找到對應的 User Story 層級項目，結果存入 Redis         |
| 7   | `consolidate-release-data`    | 整合 PR 資料與 Work Item 資料，以 Work Item 為主體配對 PR、依專案分組並排序，產出結構化的 Release 資料，結果存入 Redis          |
| 8   | `enhance-titles`              | 使用 AI（GitHub Copilot）增強各 Release 項目的標題，從候選標題中產生更具可讀性的描述，結果存入 Redis                            |
| 9   | `update-googlesheet`          | 從 Redis 讀取增強後的整合資料，同步至 Google Sheet（增量更新模式，比對現有資料僅更新差異）                                      |

**輔助指令**

| 指令 | 說明 |
|------|------|
| `fetch-gitlab-release-branch`    | 取得 GitLab 各專案最新的 Release Branch 名稱，結果存入 Redis                                                                    |
| `fetch-bitbucket-release-branch` | 取得 Bitbucket 各專案最新的 Release Branch 名稱，結果存入 Redis                                                                 |
| `get-release-setting` | 從 Redis 讀取各平台的 Release Branch 資訊，依規則判斷 FetchMode（BranchDiff 或 DateTimeRange），產生各專案的拉取設定並寫入 Redis |

#### 指令執行範本

以下為產出完整 Release Notes 的標準指令執行順序：

```bash
cd src/ReleaseKit.Console

# Step 1: 取得各平台最新 Release Branch
dotnet run -- fetch-gitlab-release-branch
dotnet run -- fetch-bitbucket-release-branch

# Step 2: 拉取各平台 PR 資訊
dotnet run -- fetch-gitlab-pr
dotnet run -- fetch-bitbucket-pr

# Step 3: 依使用者過濾 PR
dotnet run -- filter-gitlab-pr-by-user
dotnet run -- filter-bitbucket-pr-by-user

# Step 4: 從 PR 解析並拉取 Azure DevOps Work Item
dotnet run -- fetch-azure-workitems

# Step 5: 將 Work Item 轉換至 User Story 層級
dotnet run -- get-user-story

# Step 6: 整合 Release 資料
dotnet run -- consolidate-release-data

# Step 7: 使用 AI 增強標題
dotnet run -- enhance-titles

# Step 8: 同步至 Google Sheet
dotnet run -- update-googlesheet
```

> **注意**: 指令之間有相依關係，必須依照上述順序執行。每個步驟的輸出會存入 Redis，作為下一個步驟的輸入。

## 專案結構

```
release-kit/
├── src/
│   ├── ReleaseKit.Domain/          # 領域層（核心，無外部依賴）
│   ├── ReleaseKit.Application/     # 應用層（業務流程協調）
│   ├── ReleaseKit.Infrastructure/  # 基礎設施層（外部服務整合）
│   └── ReleaseKit.Console/         # Console 應用程式
└── tests/
    ├── ReleaseKit.Domain.Tests/
    ├── ReleaseKit.Application.Tests/
    ├── ReleaseKit.Infrastructure.Tests/
    └── ReleaseKit.Console.Tests/
```

## 組態設定

應用程式使用 `appsettings.json` 進行組態管理：

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information"
    }
  },
  "Redis": {
    "ConnectionString": "localhost:6379",
    "InstanceName": "ReleaseKit:"
  },
  "Seq": {
    "ServerUrl": "http://localhost:5341",
    "ApiKey": ""
  }
}
```

環境變數可覆寫組態設定，例如：

```bash
export Redis__ConnectionString="your-redis-host:6379"
export Seq__ServerUrl="https://your-seq-server.com"
```

## 開發

### 建置專案

```bash
dotnet build src/release-kit.sln
```

### 執行測試

```bash
dotnet test src/release-kit.sln
```

### 架構原則

本專案遵循 [AGENTS.md](AGENTS.md) 定義的原則：

- TDD（測試驅動開發）
- DDD（領域驅動設計）與 CQRS
- SOLID 原則
- Result Pattern 錯誤處理

## 文件

- [Docker 使用指南](DOCKER.md) - Docker Compose 執行說明
- [AGENTS.md](AGENTS.md) - 專案開發規範

## 授權

本專案採用 MIT 授權。
