# ReleaseKit Console 使用指南

## 概述

ReleaseKit Console 應用程式支援命令列參數執行不同的任務，並透過 `appsettings.json` 設定檔與環境變數來管理組態設定。

## 命令列使用方式

### 基本語法

```bash
dotnet run -- <task-name>
```

或者在編譯後直接執行：

```bash
./ReleaseKit.Console <task-name>
```

### 可用的任務

| #   | 指令                             | 說明                                                                                         |
| --- | -------------------------------- | -------------------------------------------------------------------------------------------- |
| 1   | `fetch-gitlab-release-branch`    | 取得 GitLab 各專案最新的 Release Branch 名稱，結果存入 Redis                                 |
| 2   | `fetch-bitbucket-release-branch` | 取得 Bitbucket 各專案最新的 Release Branch 名稱，結果存入 Redis                              |
| 3   | `fetch-gitlab-pr`                | 從 GitLab API 拉取各專案的 Pull Request（Merge Request）資訊，支援時間區間與 Branch 差異模式 |
| 4   | `filter-gitlab-pr-by-user`       | 依 UserMapping 設定中的 GitLabUserId 過濾已拉取的 GitLab PR，僅保留團隊成員的 PR             |
| 5   | `fetch-bitbucket-pr`             | 從 Bitbucket API 拉取各專案的 Pull Request 資訊，支援時間區間與 Branch 差異模式              |
| 6   | `filter-bitbucket-pr-by-user`    | 依 UserMapping 設定中的 BitbucketUserId 過濾已拉取的 Bitbucket PR，僅保留團隊成員的 PR       |
| 7   | `fetch-azure-workitems`          | 從已過濾的 PR 來源分支中解析 Work Item ID，並透過 Azure DevOps API 查詢詳細資訊              |
| 8   | `get-user-story`                 | 將低於 User Story 層級的 Work Item（如 Bug、Task）遞迴查詢 Parent 至 User Story 層級         |
| 9   | `consolidate-release-data`       | 整合 PR 與 Work Item 資料，以 Work Item 為主體配對 PR、依專案分組排序                        |
| 10  | `enhance-titles`                 | 使用 AI（GitHub Copilot）增強各 Release 項目的標題，產生更具可讀性的描述                     |
| 11  | `update-googlesheet`             | 從 Redis 讀取增強後的整合資料，同步至 Google Sheet（增量更新模式）                           |

**輔助指令**

| 指令                  | 說明                                                                                                                   |
| --------------------- | ---------------------------------------------------------------------------------------------------------------------- |
| `get-release-setting` | 從 Redis 讀取各平台 Release Branch 資訊，依規則判斷 FetchMode（BranchDiff 或 DateTimeRange），產生拉取設定並寫入 Redis |

### 指令執行範本

以下為產出完整 Release Notes 的標準指令執行順序：

```bash
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

### 限制

- 每次只能執行**單一任務**
- 任務名稱**不區分大小寫**
- 若未提供任務名稱或提供無效的任務名稱，將顯示錯誤訊息

## 組態設定

### 設定項目

#### GitLab 設定

用於連接 GitLab API 並拉取 Pull Request 資訊。

```json
{
  "GitLab": {
    "ApiUrl": "https://gitlab.com",
    "AccessToken": "",
    "Projects": [
      {
        "ProjectPath": "mygroup/backend-api",
        "TargetBranch": "main"
      }
    ]
  }
}
```

- **ApiUrl**: GitLab API 端點 URL（必須是有效的 URL）
- **AccessToken**: GitLab 個人存取權杖（建議透過環境變數設定）
- **Projects**: 要追蹤的 GitLab 專案清單
  - **ProjectPath**: 專案路徑（格式：群組名稱/專案名稱）
  - **TargetBranch**: 目標分支名稱

**環境變數設定範例：**
```bash
GitLab__AccessToken="your-gitlab-token-here" ./ReleaseKit.Console fetch-gitlab-pr
```

#### Bitbucket 設定

用於連接 Bitbucket API 並拉取 Pull Request 資訊。

```json
{
  "Bitbucket": {
    "ApiUrl": "https://api.bitbucket.org",
    "Email": "",
    "AccessToken": "",
    "Projects": [
      {
        "ProjectPath": "mygroup/backend-api",
        "TargetBranch": "main"
      }
    ]
  }
}
```

- **ApiUrl**: Bitbucket API 端點 URL（必須是有效的 URL）
- **Email**: Bitbucket 帳號電子郵件（必須是有效的電子郵件地址，建議透過環境變數設定）
- **AccessToken**: Bitbucket App 密碼或存取權杖（建議透過環境變數設定）
- **Projects**: 要追蹤的 Bitbucket 專案清單
  - **ProjectPath**: 專案路徑（格式：群組名稱/專案名稱）
  - **TargetBranch**: 目標分支名稱

**環境變數設定範例：**
```bash
Bitbucket__Email="your-email@example.com" \
Bitbucket__AccessToken="your-bitbucket-token" \
./ReleaseKit.Console fetch-bitbucket-pr
```

#### UserMapping 設定

用於對應不同平台的使用者 ID 至統一的顯示名稱。

```json
{
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

- **Mappings**: 使用者對應清單
  - **GitLabUserId**: GitLab 使用者 ID
  - **BitbucketUserId**: Bitbucket 使用者 ID
  - **DisplayName**: 統一的顯示名稱

**注意事項：**
- 預設為空陣列，使用前需根據團隊成員進行設定
- 建議將使用者對應資訊放在環境特定設定檔中（如 `appsettings.Production.json`）
- 這不是敏感資訊，可以提交至版本控制

## 設定檔架構

### 基礎設定檔

- **appsettings.json**: 基礎設定，適用於所有環境
- **appsettings.Development.json**: 開發環境專用設定
- **appsettings.Qa.json**: QA 測試環境專用設定
- **appsettings.Production.json**: 正式環境專用設定

### 載入優先順序

設定值的載入順序（後者會覆蓋前者）：

1. `appsettings.json`（基礎設定）
2. `appsettings.{Environment}.json`（環境特定設定）
3. 環境變數
4. User Secrets（僅開發環境）

## 使用方式

### 1. 切換環境

透過設定 `ASPNETCORE_ENVIRONMENT` 環境變數來切換環境：

```bash
# 使用 Production 環境（預設）
./ReleaseKit.Console

# 使用 Development 環境
ASPNETCORE_ENVIRONMENT=Development ./ReleaseKit.Console

# 使用 Qa 環境
ASPNETCORE_ENVIRONMENT=Qa ./ReleaseKit.Console
```

### 2. 使用環境變數覆寫設定

環境變數使用雙底線 `__` 作為階層分隔符號：

```bash
# 覆寫 Application:Name 設定
Application__Name="Custom Name" ./ReleaseKit.Console

# 覆寫 Logging:LogLevel:Default 設定
Logging__LogLevel__Default="Debug" ./ReleaseKit.Console
```

### 3. 使用 User Secrets（僅開發環境）

User Secrets 適合儲存敏感資訊（如 API Token、密碼等），這些資料不會被提交至版本控制系統。

#### 初始化 User Secrets

```bash
cd src/ReleaseKit.Console
dotnet user-secrets init
```

#### 設定 Secret

```bash
# 設定單一值
dotnet user-secrets set "ApiToken" "your-secret-token-here"

# 設定階層結構
dotnet user-secrets set "ConnectionStrings:Database" "your-connection-string"
```

#### 列出所有 Secrets

```bash
dotnet user-secrets list
```

#### 移除 Secret

```bash
dotnet user-secrets remove "ApiToken"
```

#### 清除所有 Secrets

```bash
dotnet user-secrets clear
```

## 設定檔範例

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "System": "Warning"
    }
  },
  "Application": {
    "Name": "ReleaseKit",
    "Version": "1.0.0"
  }
}
```

### appsettings.Development.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Information",
      "System": "Information"
    }
  },
  "Application": {
    "Environment": "Development"
  }
}
```

## 在程式碼中讀取設定

```csharp
using Microsoft.Extensions.Configuration;

// 建立組態
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>(optional: true)
    .Build();

// 讀取單一設定值
var appName = configuration["Application:Name"];

// 讀取階層設定
var logLevel = configuration["Logging:LogLevel:Default"];
```

## 最佳實踐

1. **不要將敏感資訊提交至版本控制**
   - 使用 User Secrets（開發環境）
   - 使用環境變數（生產環境）

2. **使用環境特定設定檔**
   - 只在環境特定設定檔中覆寫需要變更的值
   - 保持 `appsettings.json` 為通用設定

3. **設定檔命名規範**
   - 環境名稱使用 PascalCase（如 `Development`、`Qa`、`Production`）

4. **文件化所有設定項目**
   - 為每個設定項目加入註解說明用途
   - 提供範例值

## 安全性考量

- ❌ **禁止**在 `appsettings.json` 中儲存密碼、API Token 等敏感資訊
- ✅ **建議**使用 User Secrets 進行本機開發
- ✅ **建議**在生產環境使用環境變數或安全的組態管理服務（如 Azure Key Vault）
- ✅ **建議**將 `appsettings.*.json` 加入版本控制，但敏感資訊必須透過環境變數覆寫

## 相關資源

- [ASP.NET Core Configuration](https://learn.microsoft.com/aspnet/core/fundamentals/configuration/)
- [Safe storage of app secrets in development](https://learn.microsoft.com/aspnet/core/security/app-secrets)
- [Environment Variables in .NET](https://learn.microsoft.com/dotnet/core/tools/dotnet-environment-variables)
