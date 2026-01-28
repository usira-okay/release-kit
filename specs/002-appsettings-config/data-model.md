# Data Model: 配置設定類別

**Date**: 2026-01-28

## Overview

本文件定義 Release-Kit 中所有配置類別的資料模型，包含屬性定義、驗證規則與關聯關係。

---

## 1. Root Level Options

### 1.1 FetchModeOptions

**用途**: 定義全域的資料拉取模式（時間區間或分支差異）

| 屬性名稱 | 型別 | 必要 | 預設值 | 驗證規則 | 說明 |
|---------|------|------|--------|---------|------|
| `FetchMode` | `string` | ✅ | - | 必須為 "DateTimeRange" 或 "BranchDiff" | 拉取模式 |
| `SourceBranch` | `string?` | ❌ | `null` | BranchDiff 模式時必填 | 來源分支名稱 |
| `StartDateTime` | `DateTimeOffset?` | ❌ | `null` | DateTimeRange 模式時必填 | 開始時間（UTC） |
| `EndDateTime` | `DateTimeOffset?` | ❌ | `null` | DateTimeRange 模式時必填 | 結束時間（UTC） |

**驗證邏輯**:
- 若 `FetchMode == "BranchDiff"`，則 `SourceBranch` 不可為空
- 若 `FetchMode == "DateTimeRange"`，則 `StartDateTime` 與 `EndDateTime` 不可為空

**對應 JSON**:
```json
{
  "FetchMode": "DateTimeRange",
  "SourceBranch": null,
  "StartDateTime": "2025-01-01T00:00:00Z",
  "EndDateTime": "2025-01-31T23:59:59Z"
}
```

---

## 2. Google Sheet Options

### 2.1 GoogleSheetOptions

**用途**: Google Sheets 整合配置

| 屬性名稱 | 型別 | 必要 | 預設值 | 驗證規則 | 說明 |
|---------|------|------|--------|---------|------|
| `SpreadsheetId` | `string` | ✅ | - | 不可為空 | Google 試算表 ID |
| `SheetName` | `string` | ✅ | - | 不可為空 | 工作表名稱 |
| `ServiceAccountCredentialPath` | `string` | ✅ | - | 必須是有效路徑 | 服務帳戶憑證檔案路徑 |
| `ColumnMapping` | `ColumnMappingOptions` | ✅ | - | 子物件驗證 | 欄位映射配置 |

**對應 JSON**:
```json
{
  "GoogleSheet": {
    "SpreadsheetId": "1a2b3c4d5e6f",
    "SheetName": "Sheet1",
    "ServiceAccountCredentialPath": "/path/to/credentials.json",
    "ColumnMapping": { ... }
  }
}
```

### 2.2 ColumnMappingOptions

**用途**: Google Sheets 欄位映射配置（巢狀物件）

| 屬性名稱 | 型別 | 必要 | 預設值 | 驗證規則 | 說明 |
|---------|------|------|--------|---------|------|
| `RepositoryNameColumn` | `string` | ✅ | - | 必須是大寫字母（如 "Z"） | Repository 名稱欄位 |
| `FeatureColumn` | `string` | ✅ | - | 必須是大寫字母（如 "B"） | Feature 欄位 |
| `TeamColumn` | `string` | ✅ | - | 必須是大寫字母（如 "D"） | 團隊欄位 |
| `AuthorsColumn` | `string` | ✅ | - | 必須是大寫字母（如 "W"） | 作者欄位 |
| `PullRequestUrlsColumn` | `string` | ✅ | - | 必須是大寫字母（如 "X"） | PR URL 欄位 |
| `UniqueKeyColumn` | `string` | ✅ | - | 必須是大寫字母（如 "Y"） | 唯一鍵欄位 |
| `AutoSyncColumn` | `string` | ✅ | - | 必須是大寫字母（如 "F"） | 自動同步欄位 |

**驗證規則**:
- 所有欄位名稱必須符合正則表達式 `^[A-Z]+$`（一個或多個大寫字母）

**對應 JSON**:
```json
{
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
```

---

## 3. Azure DevOps Options

### 3.1 AzureDevOpsOptions

**用途**: Azure DevOps 整合配置

| 屬性名稱 | 型別 | 必要 | 預設值 | 驗證規則 | 說明 |
|---------|------|------|--------|---------|------|
| `OrganizationUrl` | `string` | ✅ | - | 必須是有效 URL | Azure DevOps 組織 URL |
| `TeamMapping` | `List<TeamMappingOptions>` | ✅ | `[]` | 至少包含一個項目 | 團隊映射清單 |

**對應 JSON**:
```json
{
  "AzureDevOps": {
    "OrganizationUrl": "https://dev.azure.com/myorganization",
    "TeamMapping": [ ... ]
  }
}
```

### 3.2 TeamMappingOptions

**用途**: Azure DevOps 團隊名稱映射（巢狀物件）

| 屬性名稱 | 型別 | 必要 | 預設值 | 驗證規則 | 說明 |
|---------|------|------|--------|---------|------|
| `OriginalTeamName` | `string` | ✅ | - | 不可為空 | 原始團隊名稱（英文） |
| `DisplayName` | `string` | ✅ | - | 不可為空 | 顯示名稱（中文） |

**對應 JSON**:
```json
{
  "TeamMapping": [
    {
      "OriginalTeamName": "MoneyLogistic",
      "DisplayName": "金流團隊"
    },
    {
      "OriginalTeamName": "DailyResource",
      "DisplayName": "日常資源團隊"
    }
  ]
}
```

---

## 4. GitLab Options

### 4.1 GitLabOptions

**用途**: GitLab 整合配置

| 屬性名稱 | 型別 | 必要 | 預設值 | 驗證規則 | 說明 |
|---------|------|------|--------|---------|------|
| `ApiUrl` | `string` | ✅ | - | 必須是有效 URL | GitLab API 基礎 URL |
| `AccessToken` | `string` | ✅ | - | 不可為空 | GitLab Personal Access Token |
| `Projects` | `List<GitLabProjectOptions>` | ✅ | `[]` | 至少包含一個項目 | 專案清單 |

**對應 JSON**:
```json
{
  "GitLab": {
    "ApiUrl": "https://gitlab.com/api/v4",
    "AccessToken": "glpat-xxxxxxxxxxxxxxxxxxxx",
    "Projects": [ ... ]
  }
}
```

### 4.2 GitLabProjectOptions

**用途**: GitLab 專案配置（巢狀物件，繼承全域 FetchMode 屬性）

| 屬性名稱 | 型別 | 必要 | 預設值 | 驗證規則 | 說明 |
|---------|------|------|--------|---------|------|
| `ProjectPath` | `string` | ✅ | - | 不可為空 | 專案路徑（如 "group/project"） |
| `TargetBranch` | `string` | ✅ | - | 不可為空 | 目標分支名稱 |
| `FetchMode` | `string?` | ❌ | `null` | 若提供，必須為 "DateTimeRange" 或 "BranchDiff" | 覆寫全域拉取模式 |
| `SourceBranch` | `string?` | ❌ | `null` | BranchDiff 模式時必填 | 來源分支名稱 |
| `StartDateTime` | `DateTimeOffset?` | ❌ | `null` | DateTimeRange 模式時必填 | 開始時間（UTC） |
| `EndDateTime` | `DateTimeOffset?` | ❌ | `null` | DateTimeRange 模式時必填 | 結束時間（UTC） |

**驗證邏輯**:
- 若 `FetchMode` 未提供，使用全域 `FetchModeOptions` 的值
- 若 `FetchMode == "BranchDiff"`，則 `SourceBranch` 不可為空
- 若 `FetchMode == "DateTimeRange"`，則 `StartDateTime` 與 `EndDateTime` 不可為空

**對應 JSON**:
```json
{
  "Projects": [
    {
      "ProjectPath": "mygroup/backend-api",
      "TargetBranch": "main",
      "FetchMode": "DateTimeRange",
      "StartDateTime": "2025-01-01T00:00:00Z",
      "EndDateTime": "2025-01-31T23:59:59Z"
    },
    {
      "ProjectPath": "mygroup/frontend",
      "TargetBranch": "main",
      "FetchMode": "BranchDiff",
      "SourceBranch": "release/20250128"
    }
  ]
}
```

---

## 5. Bitbucket Options

### 5.1 BitbucketOptions

**用途**: Bitbucket 整合配置

| 屬性名稱 | 型別 | 必要 | 預設值 | 驗證規則 | 說明 |
|---------|------|------|--------|---------|------|
| `ApiUrl` | `string` | ✅ | - | 必須是有效 URL | Bitbucket API 基礎 URL |
| `Email` | `string` | ✅ | - | 必須是有效 Email | Bitbucket 帳戶 Email |
| `AccessToken` | `string` | ✅ | - | 不可為空 | Bitbucket App Password |
| `Projects` | `List<BitbucketProjectOptions>` | ✅ | `[]` | 至少包含一個項目 | 專案清單 |

**對應 JSON**:
```json
{
  "Bitbucket": {
    "ApiUrl": "https://api.bitbucket.org/2.0",
    "Email": "user@example.com",
    "AccessToken": "app-password-here",
    "Projects": [ ... ]
  }
}
```

### 5.2 BitbucketProjectOptions

**用途**: Bitbucket 專案配置（巢狀物件，結構同 GitLabProjectOptions）

| 屬性名稱 | 型別 | 必要 | 預設值 | 驗證規則 | 說明 |
|---------|------|------|--------|---------|------|
| `ProjectPath` | `string` | ✅ | - | 不可為空 | 專案路徑（如 "workspace/repo"） |
| `TargetBranch` | `string` | ✅ | - | 不可為空 | 目標分支名稱 |
| `FetchMode` | `string?` | ❌ | `null` | 若提供，必須為 "DateTimeRange" 或 "BranchDiff" | 覆寫全域拉取模式 |
| `SourceBranch` | `string?` | ❌ | `null` | BranchDiff 模式時必填 | 來源分支名稱 |
| `StartDateTime` | `DateTimeOffset?` | ❌ | `null` | DateTimeRange 模式時必填 | 開始時間（UTC） |
| `EndDateTime` | `DateTimeOffset?` | ❌ | `null` | DateTimeRange 模式時必填 | 結束時間（UTC） |

**驗證邏輯**: 同 GitLabProjectOptions

**對應 JSON**: 同 GitLabProjectOptions，僅將 `GitLab` 替換為 `Bitbucket`

---

## 6. 類別關聯圖

```
┌─────────────────────────────────────────────────────────────┐
│                     appsettings.json                        │
├─────────────────────────────────────────────────────────────┤
│  - FetchMode (Root)                                         │
│  - SourceBranch (Root)                                      │
│  - StartDateTime (Root)                                     │
│  - EndDateTime (Root)                                       │
├─────────────────────────────────────────────────────────────┤
│  GoogleSheet                                                │
│    ├── SpreadsheetId                                        │
│    ├── SheetName                                            │
│    ├── ServiceAccountCredentialPath                         │
│    └── ColumnMapping                                        │
│         ├── RepositoryNameColumn                            │
│         ├── FeatureColumn                                   │
│         ├── TeamColumn                                      │
│         ├── AuthorsColumn                                   │
│         ├── PullRequestUrlsColumn                           │
│         ├── UniqueKeyColumn                                 │
│         └── AutoSyncColumn                                  │
├─────────────────────────────────────────────────────────────┤
│  AzureDevOps                                                │
│    ├── OrganizationUrl                                      │
│    └── TeamMapping[]                                        │
│         ├── OriginalTeamName                                │
│         └── DisplayName                                     │
├─────────────────────────────────────────────────────────────┤
│  GitLab                                                     │
│    ├── ApiUrl                                               │
│    ├── AccessToken                                          │
│    └── Projects[]                                           │
│         ├── ProjectPath                                     │
│         ├── TargetBranch                                    │
│         ├── FetchMode (optional)                            │
│         ├── SourceBranch (optional)                         │
│         ├── StartDateTime (optional)                        │
│         └── EndDateTime (optional)                          │
├─────────────────────────────────────────────────────────────┤
│  Bitbucket                                                  │
│    ├── ApiUrl                                               │
│    ├── Email                                                │
│    ├── AccessToken                                          │
│    └── Projects[]                                           │
│         └── (同 GitLab Projects 結構)                      │
└─────────────────────────────────────────────────────────────┘
```

---

## 7. 驗證規則總結

### 7.1 必要屬性驗證

所有標記為「必要」的屬性必須使用 `[Required]` 標記，應用程式啟動時會自動驗證。

### 7.2 條件驗證

- `FetchModeOptions` 與 `GitLabProjectOptions`/`BitbucketProjectOptions` 需實作 `IValidatableObject` 介面
- 根據 `FetchMode` 的值，動態驗證對應屬性是否提供

### 7.3 格式驗證

- URL 屬性使用 `[Url]` 標記
- Email 屬性使用 `[EmailAddress]` 標記
- 欄位名稱使用 `[RegularExpression("^[A-Z]+$")]` 標記

### 7.4 啟動驗證

所有 Options 類別在註冊時必須使用 `ValidateDataAnnotations()` 與 `ValidateOnStart()`，確保配置錯誤在啟動階段被發現。

---

## 8. 檔案組織

所有 Options 類別檔案位於 `src/ReleaseKit.Infrastructure/Configuration/` 目錄：

```
Configuration/
├── FetchModeOptions.cs
├── GoogleSheetOptions.cs
├── ColumnMappingOptions.cs
├── AzureDevOpsOptions.cs
├── TeamMappingOptions.cs
├── GitLabOptions.cs
├── GitLabProjectOptions.cs
├── BitbucketOptions.cs
└── BitbucketProjectOptions.cs
```

每個檔案只包含一個類別（符合 Constitution 規範）。
