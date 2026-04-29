# Release-Kit

Release-Kit 是一個 .NET 10 Console 應用程式，用於從多個開發平台（GitLab、Bitbucket、Azure DevOps）收集 PR/MR 與 Work Item 資訊，並同步至 Google Sheet 以產出 Release Notes。

## 功能特色

- 支援多平台整合：GitLab、Bitbucket、Azure DevOps
- 支援兩種拉取模式：時間區間（DateTimeRange）與分支差異（BranchDiff）
- 跨平台使用者對應（GitLab ↔ Bitbucket ↔ 顯示名稱）
- 自動解析並查詢 Azure DevOps Work Item
- 整合 Release 資料（ConsolidateReleaseData）
- 使用 GitHub Copilot AI 自動增強 Release 標題
- 同步至 Google Sheets（支援欄位對應與團隊排序）
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

#### 可用任務

執行應用程式時需指定任務名稱：

```bash
# 取得 GitLab 各專案最新 Release Branch
dotnet run -- fetch-gitlab-release-branch

# 取得 Bitbucket 各專案最新 Release Branch
dotnet run -- fetch-bitbucket-release-branch

# 拉取 GitLab PR 資訊
dotnet run -- fetch-gitlab-pr

# 拉取 Bitbucket PR 資訊
dotnet run -- fetch-bitbucket-pr

# 依使用者過濾 GitLab Pull Request
dotnet run -- filter-gitlab-pr-by-user

# 依使用者過濾 Bitbucket Pull Request
dotnet run -- filter-bitbucket-pr-by-user

# 拉取 Azure DevOps Work Item 資訊
dotnet run -- fetch-azure-workitems

# 取得 User Story 層級的 Work Item
dotnet run -- get-user-story

# 整合 Release 資料
dotnet run -- consolidate-release-data

# 使用 AI 增強 Release 標題
dotnet run -- enhance-titles

# 產生 Release Setting 設定
dotnet run -- get-release-setting

# 更新 Google Sheets 資訊
dotnet run -- update-googlesheet
```

詳細說明請參考 [Console 使用指南](src/ReleaseKit.Console/README.md)

## 專案結構

```
release-kit/
├── src/
│   ├── ReleaseKit.Domain/          # 領域層（核心，無外部依賴）
│   ├── ReleaseKit.Application/     # 應用層（業務流程協調）
│   ├── ReleaseKit.Common/          # 共用層（組態選項、常數、擴充方法）
│   ├── ReleaseKit.Infrastructure/  # 基礎設施層（外部服務整合）
│   └── ReleaseKit.Console/         # Console 應用程式
└── tests/
    ├── ReleaseKit.Domain.Tests/
    ├── ReleaseKit.Application.Tests/
    ├── ReleaseKit.Common.Tests/
    ├── ReleaseKit.Infrastructure.Tests/
    └── ReleaseKit.Console.Tests/
```

## 組態設定

應用程式使用 `appsettings.json` 進行組態管理，可參考 `appsettings.Sample.json` 作為範本。

主要設定項目如下（完整範例請見 [Console 使用指南](src/ReleaseKit.Console/README.md)）：

```json
{
  "FetchMode": "DateTimeRange",
  "StartDateTime": "2025-01-01T00:00:00Z",
  "EndDateTime": "2025-01-31T23:59:59Z",
  "Redis": {
    "ConnectionString": "localhost:6379",
    "InstanceName": "ReleaseKit:"
  },
  "Seq": {
    "ServerUrl": "http://localhost:5341",
    "ApiKey": ""
  },
  "GitLab": { "ApiUrl": "...", "AccessToken": "", "Projects": [] },
  "Bitbucket": { "ApiUrl": "...", "Email": "", "AccessToken": "", "Projects": [] },
  "AzureDevOps": { "OrganizationUrl": "...", "PersonalAccessToken": "" },
  "GoogleSheet": { "SpreadsheetId": "...", "SheetName": "...", "ServiceAccountCredentialPath": "..." },
  "Copilot": { "Model": "gpt-4.1", "TimeoutSeconds": 600, "GitHubToken": "" }
}
```

環境變數可覆寫組態設定，例如：

```bash
export Redis__ConnectionString="your-redis-host:6379"
export GitLab__AccessToken="your-gitlab-token"
export Copilot__GitHubToken="your-github-token"
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

本專案遵循 [開發憲法](.specify/memory/constitution.md) 定義的原則：

- TDD（測試驅動開發）
- DDD（領域驅動設計）與 CQRS
- SOLID 原則
- Result Pattern 錯誤處理

## 文件

- [Console 使用指南](src/ReleaseKit.Console/README.md) - 命令列參數與組態設定
- [Docker 使用指南](DOCKER.md) - Docker Compose 執行說明
- [開發憲法](.specify/memory/constitution.md) - 專案開發規範

## 授權

本專案採用 MIT 授權。
