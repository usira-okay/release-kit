# Quickstart: AppSettings 配置擴充

**Feature**: AppSettings 配置擴充  
**Branch**: `001-appsettings-config`  
**Date**: 2026-01-28  

## 概述

本指南說明如何使用新增的 appsettings.json 配置結構，包含配置檔設定、環境變數覆寫與常見範例。

---

## 最小可行配置

### appsettings.json

```json
{
  "FetchMode": "DateTimeRange",
  "StartDateTime": "2025-01-01",
  "EndDateTime": "2025-01-31",
  "GoogleSheet": {
    "SpreadsheetId": "1234567890abcdefg",
    "SheetName": "Sheet1",
    "ServiceAccountCredentialPath": "/path/to/service-account.json",
    "ColumnMapping": {
      "RepositoryNameColumn": "Z",
      "FeatureColumn": "B",
      "TeamColumn": "D",
      "AuthorsColumn": "W",
      "PullRequestUrlsColumn": "X",
      "UniqueKeyColumn": "Y",
      "AutoSyncColumn": "F"
    }
  },
  "AzureDevOps": {
    "OrganizationUrl": "https://dev.azure.com/myorganization",
    "TeamMapping": [
      {
        "OriginalTeamName": "MoneyLogistic",
        "DisplayName": "金流團隊"
      }
    ]
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
  }
}
```

---

## 使用範例

### 範例 1：使用時間區間拉取 PR/MR

**appsettings.json**:
```json
{
  "FetchMode": "DateTimeRange",
  "StartDateTime": "2025-01-01T00:00:00+08:00",
  "EndDateTime": "2025-01-31T23:59:59+08:00"
}
```

**說明**：
- 所有 GitLab 專案將使用全域的 `FetchMode` 設定
- 拉取 2025/01/01 至 2025/01/31 期間的 PR/MR

---

### 範例 2：使用分支差異拉取 PR/MR

**appsettings.json**:
```json
{
  "FetchMode": "BranchDiff",
  "SourceBranch": "release/20260128"
}
```

**說明**：
- 拉取 `release/20260128` 與目標分支之間的 PR/MR

---

### 範例 3：專案層級覆寫全域設定

**appsettings.json**:
```json
{
  "FetchMode": "DateTimeRange",
  "StartDateTime": "2025-01-01",
  "EndDateTime": "2025-01-31",
  "GitLab": {
    "Projects": [
      {
        "ProjectPath": "mygroup/backend-api",
        "TargetBranch": "main"
      },
      {
        "ProjectPath": "mygroup/frontend-app",
        "TargetBranch": "main",
        "FetchMode": "BranchDiff",
        "SourceBranch": "release/20260128"
      }
    ]
  }
}
```

**說明**：
- 第一個專案使用全域的 `DateTimeRange` 設定
- 第二個專案覆寫為 `BranchDiff` 模式

---

### 範例 4：Google Sheet 欄位對應

**appsettings.json**:
```json
{
  "GoogleSheet": {
    "SpreadsheetId": "1AbcDefGhiJkLmNoPqRsTuVwXyZ",
    "SheetName": "2025 Release Notes",
    "ServiceAccountCredentialPath": "./credentials/service-account.json",
    "ColumnMapping": {
      "RepositoryNameColumn": "Z",
      "FeatureColumn": "B",
      "TeamColumn": "D",
      "AuthorsColumn": "W",
      "PullRequestUrlsColumn": "X",
      "UniqueKeyColumn": "Y",
      "AutoSyncColumn": "F"
    }
  }
}
```

**說明**：
- 將 Repository 名稱寫入 Z 欄
- 將功能說明寫入 B 欄
- 其餘欄位依此類推

---

### 範例 5：Azure DevOps 團隊名稱對應

**appsettings.json**:
```json
{
  "AzureDevOps": {
    "OrganizationUrl": "https://dev.azure.com/myorganization",
    "TeamMapping": [
      {
        "OriginalTeamName": "MoneyLogistic",
        "DisplayName": "金流團隊"
      },
      {
        "OriginalTeamName": "DailyResource",
        "DisplayName": "日常資源團隊"
      },
      {
        "OriginalTeamName": "Commerce",
        "DisplayName": "商務團隊"
      }
    ]
  }
}
```

**說明**：
- 將 Azure DevOps 的原始團隊名稱 `MoneyLogistic` 轉換為顯示名稱 `金流團隊`
- 其餘團隊依此類推

---

## 環境變數覆寫

### 基本語法

使用 `__` (雙底線) 作為階層分隔符號：

```bash
export ReleaseKit__GoogleSheet__SpreadsheetId="my-spreadsheet-id"
export ReleaseKit__AzureDevOps__OrganizationUrl="https://dev.azure.com/myorg"
```

### 陣列元素覆寫

使用索引號指定陣列元素：

```bash
# 覆寫第一個專案的 FetchMode
export ReleaseKit__GitLab__Projects__0__FetchMode="BranchDiff"
export ReleaseKit__GitLab__Projects__0__SourceBranch="release/20260128"

# 覆寫第二個專案的時間區間
export ReleaseKit__GitLab__Projects__1__FetchMode="DateTimeRange"
export ReleaseKit__GitLab__Projects__1__StartDateTime="2025-02-01"
export ReleaseKit__GitLab__Projects__1__EndDateTime="2025-02-28"
```

### 敏感資訊注入

**不建議**將 API Token 寫入 appsettings.json，應透過環境變數注入：

```bash
export ReleaseKit__GitLab__AccessToken="glpat-xxxxxxxxxxxxxxxxxxxx"
export ReleaseKit__Bitbucket__AccessToken="ATBB-xxxxxxxxxxxxxxxxxxxx"
```

---

## 常見錯誤處理

### 錯誤 1：必要配置未設定

**錯誤訊息**:
```
System.InvalidOperationException: GoogleSheet:SpreadsheetId 組態設定不得為空
```

**解決方式**:
在 appsettings.json 中設定 `GoogleSheet:SpreadsheetId`，或透過環境變數設定：
```bash
export ReleaseKit__GoogleSheet__SpreadsheetId="your-spreadsheet-id"
```

---

### 錯誤 2：FetchMode 與對應欄位不一致

**錯誤訊息**:
```
System.InvalidOperationException: 當 FetchMode 為 BranchDiff 時,SourceBranch 不得為空
```

**解決方式**:
當 `FetchMode` 為 `BranchDiff` 時，必須同時設定 `SourceBranch`：
```json
{
  "FetchMode": "BranchDiff",
  "SourceBranch": "release/20260128"
}
```

---

### 錯誤 3：時間區間設定錯誤

**錯誤訊息**:
```
System.InvalidOperationException: StartDateTime 必須早於 EndDateTime
```

**解決方式**:
確保 `StartDateTime` 早於 `EndDateTime`：
```json
{
  "FetchMode": "DateTimeRange",
  "StartDateTime": "2025-01-01",
  "EndDateTime": "2025-01-31"
}
```

---

### 錯誤 4：URL 格式錯誤

**錯誤訊息**:
```
System.InvalidOperationException: AzureDevOps:OrganizationUrl 必須為有效的 URL 格式
```

**解決方式**:
確保 URL 包含完整的 scheme (https://)：
```json
{
  "AzureDevOps": {
    "OrganizationUrl": "https://dev.azure.com/myorganization"
  }
}
```

---

## C# 使用範例

### 注入配置到服務

```csharp
using Microsoft.Extensions.Options;
using ReleaseKit.Console.Options;

public class MyService
{
    private readonly GoogleSheetOptions _googleSheetOptions;
    private readonly AzureDevOpsOptions _azureDevOpsOptions;
    
    public MyService(
        IOptions<GoogleSheetOptions> googleSheetOptions,
        IOptions<AzureDevOpsOptions> azureDevOpsOptions)
    {
        // 在建構子中取值，避免重複存取 .Value
        _googleSheetOptions = googleSheetOptions.Value;
        _azureDevOpsOptions = azureDevOpsOptions.Value;
    }
    
    public void UseOptions()
    {
        var spreadsheetId = _googleSheetOptions.SpreadsheetId;
        var orgUrl = _azureDevOpsOptions.OrganizationUrl;
        
        // 使用配置值...
    }
}
```

---

## 配置驗證時機

配置驗證在應用程式啟動時自動執行，透過 `ValidateOnStart()` 機制：

```csharp
services.AddOptions<GoogleSheetOptions>()
    .BindConfiguration("GoogleSheet")
    .Validate(opts =>
    {
        opts.Validate();  // 在啟動時執行驗證
        return true;
    })
    .ValidateOnStart();  // 啟動時驗證
```

若配置驗證失敗，應用程式將無法啟動，並在 Console 輸出明確的錯誤訊息。

---

## 完整配置範例

```json
{
  "FetchMode": "DateTimeRange",
  "StartDateTime": "2025-01-01T00:00:00+08:00",
  "EndDateTime": "2025-01-31T23:59:59+08:00",
  "GoogleSheet": {
    "SpreadsheetId": "1AbcDefGhiJkLmNoPqRsTuVwXyZ",
    "SheetName": "2025 Release Notes",
    "ServiceAccountCredentialPath": "./credentials/service-account.json",
    "ColumnMapping": {
      "RepositoryNameColumn": "Z",
      "FeatureColumn": "B",
      "TeamColumn": "D",
      "AuthorsColumn": "W",
      "PullRequestUrlsColumn": "X",
      "UniqueKeyColumn": "Y",
      "AutoSyncColumn": "F"
    }
  },
  "AzureDevOps": {
    "OrganizationUrl": "https://dev.azure.com/myorganization",
    "TeamMapping": [
      {
        "OriginalTeamName": "MoneyLogistic",
        "DisplayName": "金流團隊"
      },
      {
        "OriginalTeamName": "DailyResource",
        "DisplayName": "日常資源團隊"
      },
      {
        "OriginalTeamName": "Commerce",
        "DisplayName": "商務團隊"
      }
    ]
  },
  "GitLab": {
    "ApiUrl": "https://gitlab.com/api/v4",
    "AccessToken": "",
    "Projects": [
      {
        "ProjectPath": "mygroup/backend-api",
        "TargetBranch": "main",
        "FetchMode": "DateTimeRange",
        "StartDateTime": "2025-01-01",
        "EndDateTime": "2025-01-31"
      },
      {
        "ProjectPath": "mygroup/frontend-app",
        "TargetBranch": "main",
        "FetchMode": "BranchDiff",
        "SourceBranch": "release/20260128"
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
  }
}
```

---

**Phase 1 Quickstart Complete**: ✅  
**Next**: Phase 1 - Update Agent Context
