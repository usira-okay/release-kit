# ReleaseKit Configuration Guide

## 概述

ReleaseKit Console 應用程式支援透過 `appsettings.json` 設定檔、`.env` 檔案與環境變數來管理組態設定。

## 設定檔架構

### 基礎設定檔

- **appsettings.json**: 基礎設定，適用於所有環境
- **appsettings.Development.json**: 開發環境專用設定
- **appsettings.Qa.json**: QA 測試環境專用設定
- **appsettings.Production.json**: 正式環境專用設定

### .env 檔案

- **.env**: 預設環境變數設定（應提交至版本控制）
- **.env.local**: 本機專屬環境變數設定（不應提交至版本控制）

### 載入優先順序

設定值的載入順序（後者會覆蓋前者）：

1. `.env`（預設環境變數）
2. `.env.local`（本機環境變數，會覆寫 .env）
3. `appsettings.json`（基礎設定）
4. `appsettings.{Environment}.json`（環境特定設定）
5. 系統環境變數
6. User Secrets（僅開發環境）

## 使用方式

### 1. 使用 .env 檔案

建立 `.env` 或 `.env.local` 檔案來設定環境變數：

```bash
# .env.local 範例
ASPNETCORE_ENVIRONMENT=Development
Logging__LogLevel__Default=Debug
```

**重要提醒：**
- `.env` 可以提交至版本控制，用於預設設定
- `.env.local` 不應提交至版本控制（已加入 .gitignore），用於本機專屬設定
- `.env.local` 的設定會覆寫 `.env` 的設定

### 2. 切換環境

透過設定 `ASPNETCORE_ENVIRONMENT` 環境變數來切換環境：

```bash
# 使用 Production 環境（預設）
./ReleaseKit.Console

# 使用 Development 環境
ASPNETCORE_ENVIRONMENT=Development ./ReleaseKit.Console

# 使用 Qa 環境
ASPNETCORE_ENVIRONMENT=Qa ./ReleaseKit.Console

# 或在 .env.local 中設定
echo "ASPNETCORE_ENVIRONMENT=Development" > .env.local
./ReleaseKit.Console
```

### 3. 使用環境變數覆寫設定

環境變數使用雙底線 `__` 作為階層分隔符號：

```bash
# 透過命令列覆寫 Logging:LogLevel:Default 設定
Logging__LogLevel__Default="Debug" ./ReleaseKit.Console

# 或在 .env.local 中設定
echo 'Logging__LogLevel__Default=Debug' >> .env.local
./ReleaseKit.Console
```

### 4. 使用 User Secrets（僅開發環境）

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
