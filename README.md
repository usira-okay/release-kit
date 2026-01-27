# Release-Kit

Release-Kit 是一個 .NET 9 Console 應用程式，用於從多個開發平台（GitLab、Bitbucket、Azure DevOps）收集 PR/MR 與 Work Item 資訊，並同步至 Google Sheet 以產出 Release Notes。

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

- .NET 9 SDK
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
# 拉取 GitLab PR 資訊
dotnet run -- fetch-gitlab-pr

# 拉取 Bitbucket PR 資訊
dotnet run -- fetch-bitbucket-pr

# 拉取 Azure DevOps Work Item 資訊
dotnet run -- fetch-azure-workitems

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
  "Logging": {
    "LogLevel": {
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
  },
  "GitLab": {
    "ApiUrl": "https://gitlab.com/api/v4",
    "AccessToken": "",
    "Projects": [
      {
        "ProjectPath": "mygroup/backend-api",
        "TargetBranch": "main"
      }
    ]
  },
  "Bitbucket": {
    "ApiUrl": "https://api.bitbucket.org/2.0",
    "Email": "",
    "AccessToken": "",
    "Projects": [
      {
        "ProjectPath": "mygroup/backend-api",
        "TargetBranch": "main"
      }
    ]
  },
  "UserMapping": {
    "Mappings": [
      {
        "GitLabUserId": "john.doe",
        "BitbucketUserId": "jdoe",
        "DisplayName": "John Doe"
      }
    ]
  }
}
```

**重要提醒：** `AccessToken`、`Email` 等敏感資訊不應直接寫入 `appsettings.json`，請使用環境變數或 User Secrets 進行設定。

環境變數可覆寫組態設定，例如：

```bash
export Redis__ConnectionString="your-redis-host:6379"
export Seq__ServerUrl="https://your-seq-server.com"
export GitLab__AccessToken="your-gitlab-token"
export Bitbucket__Email="your-email@example.com"
export Bitbucket__AccessToken="your-bitbucket-token"
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
