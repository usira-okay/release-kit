# Data Model: AppSettings 配置擴充

**Feature**: AppSettings 配置擴充  
**Branch**: `001-appsettings-config`  
**Date**: 2026-01-28  

## 概述

本文件定義 appsettings.json 新增的配置結構與對應的強型別類別。所有配置類別將放置於 `ReleaseKit.Console/Options` 資料夾。

---

## 配置結構總覽

```json
{
  "FetchMode": "DateTimeRange",
  "SourceBranch": "release/20260128",
  "StartDateTime": "2025-01-01",
  "EndDateTime": "2025-01-31",
  "GoogleSheet": { ... },
  "AzureDevOps": { ... },
  "GitLab": {
    "Projects": [
      {
        "FetchMode": "BranchDiff",
        "SourceBranch": "release/20260128",
        ...
      }
    ]
  }
}
```

---

## 實體 1：FetchMode (Enum)

### 職責
定義拉取模式的列舉型別，用於指定從版控平台拉取資料的方式。

### 欄位

| 欄位名稱 | 型別 | 必要 | 說明 |
|---------|------|------|------|
| DateTimeRange | int | - | 根據時間區間拉取 PR/MR |
| BranchDiff | int | - | 根據分支差異拉取 PR/MR |

### 驗證規則
- 必須為已定義的列舉值之一

### 檔案位置
`ReleaseKit.Console/Options/FetchMode.cs`

### C# 定義
```csharp
namespace ReleaseKit.Console.Options;

/// <summary>
/// 拉取模式
/// </summary>
public enum FetchMode
{
    /// <summary>
    /// 根據時間區間拉取 PR/MR
    /// </summary>
    DateTimeRange,
    
    /// <summary>
    /// 根據分支差異拉取 PR/MR
    /// </summary>
    BranchDiff
}
```

---

## 實體 2：GoogleSheetOptions

### 職責
封裝 Google Sheet 相關配置，包含 Spreadsheet ID、工作表名稱、憑證路徑與欄位對應。

### 欄位

| 欄位名稱 | 型別 | 必要 | 預設值 | 說明 |
|---------|------|------|--------|------|
| SpreadsheetId | string | ✅ | - | Google Spreadsheet ID |
| SheetName | string | ✅ | - | 工作表名稱 (例如 "Sheet1") |
| ServiceAccountCredentialPath | string | ✅ | - | 服務帳號憑證檔案路徑 |
| ColumnMapping | ColumnMappingOptions | ✅ | - | 欄位對應設定 |

### 驗證規則
- SpreadsheetId 不得為空字串
- SheetName 不得為空字串
- ServiceAccountCredentialPath 不得為空字串
- ColumnMapping 不得為 null

### 關聯
- **包含** `ColumnMappingOptions` (1 對 1)

### 檔案位置
`ReleaseKit.Console/Options/GoogleSheetOptions.cs`

### C# 定義
```csharp
namespace ReleaseKit.Console.Options;

/// <summary>
/// Google Sheet 設定選項
/// </summary>
public class GoogleSheetOptions
{
    /// <summary>
    /// Google Spreadsheet ID
    /// </summary>
    public string SpreadsheetId { get; set; } = string.Empty;
    
    /// <summary>
    /// 工作表名稱 (例如 "Sheet1")
    /// </summary>
    public string SheetName { get; set; } = string.Empty;
    
    /// <summary>
    /// 服務帳號憑證檔案路徑
    /// </summary>
    public string ServiceAccountCredentialPath { get; set; } = string.Empty;
    
    /// <summary>
    /// 欄位對應設定
    /// </summary>
    public ColumnMappingOptions ColumnMapping { get; set; } = new();
    
    /// <summary>
    /// 驗證配置是否正確
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SpreadsheetId))
            throw new InvalidOperationException("GoogleSheet:SpreadsheetId 組態設定不得為空");
            
        if (string.IsNullOrWhiteSpace(SheetName))
            throw new InvalidOperationException("GoogleSheet:SheetName 組態設定不得為空");
            
        if (string.IsNullOrWhiteSpace(ServiceAccountCredentialPath))
            throw new InvalidOperationException("GoogleSheet:ServiceAccountCredentialPath 組態設定不得為空");
        
        ColumnMapping.Validate();
    }
}
```

---

## 實體 3：ColumnMappingOptions

### 職責
封裝 Google Sheet 欄位對應設定，定義每個資料欄位對應到 Google Sheet 的哪個欄位。

### 欄位

| 欄位名稱 | 型別 | 必要 | 預設值 | 說明 |
|---------|------|------|--------|------|
| RepositoryNameColumn | string | ✅ | - | Repository 名稱欄位 (例如 "Z") |
| FeatureColumn | string | ✅ | - | 功能說明欄位 (例如 "B") |
| TeamColumn | string | ✅ | - | 團隊名稱欄位 (例如 "D") |
| AuthorsColumn | string | ✅ | - | 作者欄位 (例如 "W") |
| PullRequestUrlsColumn | string | ✅ | - | PR/MR URL 欄位 (例如 "X") |
| UniqueKeyColumn | string | ✅ | - | 唯一識別碼欄位 (例如 "Y") |
| AutoSyncColumn | string | ✅ | - | 自動同步標記欄位 (例如 "F") |

### 驗證規則
- 所有欄位不得為空字串
- 欄位名稱必須為有效的 Excel 欄位格式 (例如 "A", "Z", "AA")

### 檔案位置
`ReleaseKit.Console/Options/ColumnMappingOptions.cs`

### C# 定義
```csharp
namespace ReleaseKit.Console.Options;

/// <summary>
/// Google Sheet 欄位對應設定
/// </summary>
public class ColumnMappingOptions
{
    /// <summary>
    /// Repository 名稱欄位
    /// </summary>
    public string RepositoryNameColumn { get; set; } = string.Empty;
    
    /// <summary>
    /// 功能說明欄位
    /// </summary>
    public string FeatureColumn { get; set; } = string.Empty;
    
    /// <summary>
    /// 團隊名稱欄位
    /// </summary>
    public string TeamColumn { get; set; } = string.Empty;
    
    /// <summary>
    /// 作者欄位
    /// </summary>
    public string AuthorsColumn { get; set; } = string.Empty;
    
    /// <summary>
    /// PR/MR URL 欄位
    /// </summary>
    public string PullRequestUrlsColumn { get; set; } = string.Empty;
    
    /// <summary>
    /// 唯一識別碼欄位
    /// </summary>
    public string UniqueKeyColumn { get; set; } = string.Empty;
    
    /// <summary>
    /// 自動同步標記欄位
    /// </summary>
    public string AutoSyncColumn { get; set; } = string.Empty;
    
    /// <summary>
    /// 驗證配置是否正確
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(RepositoryNameColumn))
            throw new InvalidOperationException("GoogleSheet:ColumnMapping:RepositoryNameColumn 組態設定不得為空");
            
        if (string.IsNullOrWhiteSpace(FeatureColumn))
            throw new InvalidOperationException("GoogleSheet:ColumnMapping:FeatureColumn 組態設定不得為空");
            
        if (string.IsNullOrWhiteSpace(TeamColumn))
            throw new InvalidOperationException("GoogleSheet:ColumnMapping:TeamColumn 組態設定不得為空");
            
        if (string.IsNullOrWhiteSpace(AuthorsColumn))
            throw new InvalidOperationException("GoogleSheet:ColumnMapping:AuthorsColumn 組態設定不得為空");
            
        if (string.IsNullOrWhiteSpace(PullRequestUrlsColumn))
            throw new InvalidOperationException("GoogleSheet:ColumnMapping:PullRequestUrlsColumn 組態設定不得為空");
            
        if (string.IsNullOrWhiteSpace(UniqueKeyColumn))
            throw new InvalidOperationException("GoogleSheet:ColumnMapping:UniqueKeyColumn 組態設定不得為空");
            
        if (string.IsNullOrWhiteSpace(AutoSyncColumn))
            throw new InvalidOperationException("GoogleSheet:ColumnMapping:AutoSyncColumn 組態設定不得為空");
    }
}
```

---

## 實體 4：AzureDevOpsOptions

### 職責
封裝 Azure DevOps 相關配置，包含組織 URL 與團隊名稱對應。

### 欄位

| 欄位名稱 | 型別 | 必要 | 預設值 | 說明 |
|---------|------|------|--------|------|
| OrganizationUrl | string | ✅ | - | Azure DevOps 組織 URL |
| TeamMapping | List<TeamMappingOptions> | ❌ | 空清單 | 團隊名稱對應清單 |

### 驗證規則
- OrganizationUrl 不得為空字串
- OrganizationUrl 必須為有效的 URL 格式

### 關聯
- **包含** `List<TeamMappingOptions>` (1 對多)

### 檔案位置
`ReleaseKit.Console/Options/AzureDevOpsOptions.cs`

### C# 定義
```csharp
namespace ReleaseKit.Console.Options;

/// <summary>
/// Azure DevOps 設定選項
/// </summary>
public class AzureDevOpsOptions
{
    /// <summary>
    /// Azure DevOps 組織 URL (例如 "https://dev.azure.com/myorganization")
    /// </summary>
    public string OrganizationUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// 團隊名稱對應清單
    /// </summary>
    public List<TeamMappingOptions> TeamMapping { get; set; } = new();
    
    /// <summary>
    /// 驗證配置是否正確
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OrganizationUrl))
            throw new InvalidOperationException("AzureDevOps:OrganizationUrl 組態設定不得為空");
        
        if (!Uri.TryCreate(OrganizationUrl, UriKind.Absolute, out _))
            throw new InvalidOperationException("AzureDevOps:OrganizationUrl 必須為有效的 URL 格式");
        
        foreach (var mapping in TeamMapping)
        {
            mapping.Validate();
        }
    }
}
```

---

## 實體 5：TeamMappingOptions

### 職責
封裝團隊名稱對應設定，將原始團隊名稱對應到顯示名稱。

### 欄位

| 欄位名稱 | 型別 | 必要 | 預設值 | 說明 |
|---------|------|------|--------|------|
| OriginalTeamName | string | ✅ | - | 原始團隊名稱 (例如 "MoneyLogistic") |
| DisplayName | string | ✅ | - | 顯示名稱 (例如 "金流團隊") |

### 驗證規則
- OriginalTeamName 不得為空字串
- DisplayName 不得為空字串

### 檔案位置
`ReleaseKit.Console/Options/TeamMappingOptions.cs`

### C# 定義
```csharp
namespace ReleaseKit.Console.Options;

/// <summary>
/// 團隊名稱對應設定
/// </summary>
public class TeamMappingOptions
{
    /// <summary>
    /// 原始團隊名稱 (例如 "MoneyLogistic")
    /// </summary>
    public string OriginalTeamName { get; set; } = string.Empty;
    
    /// <summary>
    /// 顯示名稱 (例如 "金流團隊")
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>
    /// 驗證配置是否正確
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OriginalTeamName))
            throw new InvalidOperationException("AzureDevOps:TeamMapping:OriginalTeamName 組態設定不得為空");
            
        if (string.IsNullOrWhiteSpace(DisplayName))
            throw new InvalidOperationException("AzureDevOps:TeamMapping:DisplayName 組態設定不得為空");
    }
}
```

---

## 實體 6：GitLabProjectOptions (修改既有類別)

### 職責
封裝單個 GitLab 專案的設定，擴充既有類別以支援 FetchMode、SourceBranch、StartDateTime、EndDateTime。

### 欄位

| 欄位名稱 | 型別 | 必要 | 預設值 | 說明 |
|---------|------|------|--------|------|
| ProjectPath | string | ✅ | - | 專案路徑 (既有欄位) |
| TargetBranch | string | ✅ | - | 目標分支 (既有欄位) |
| FetchMode | FetchMode? | ❌ | null | 拉取模式 (新增欄位) |
| SourceBranch | string? | ❌ | null | 來源分支，僅在 FetchMode 為 BranchDiff 時使用 (新增欄位) |
| StartDateTime | DateTimeOffset? | ❌ | null | 開始時間，僅在 FetchMode 為 DateTimeRange 時使用 (新增欄位) |
| EndDateTime | DateTimeOffset? | ❌ | null | 結束時間，僅在 FetchMode 為 DateTimeRange 時使用 (新增欄位) |

### 驗證規則
- 當 FetchMode 為 BranchDiff 時，SourceBranch 不得為空
- 當 FetchMode 為 DateTimeRange 時，StartDateTime 與 EndDateTime 不得為 null
- StartDateTime 必須早於 EndDateTime

### 狀態轉換
無 (配置類別不涉及狀態轉換)

### 檔案位置
`ReleaseKit.Console/Options/GitLabProjectOptions.cs` (既有檔案，需修改)

### C# 定義
```csharp
namespace ReleaseKit.Console.Options;

/// <summary>
/// GitLab 專案設定選項
/// </summary>
public class GitLabProjectOptions
{
    /// <summary>
    /// 專案路徑 (例如 "mygroup/backend-api")
    /// </summary>
    public string ProjectPath { get; set; } = string.Empty;
    
    /// <summary>
    /// 目標分支 (例如 "main")
    /// </summary>
    public string TargetBranch { get; set; } = string.Empty;
    
    /// <summary>
    /// 拉取模式 (可選，若未設定則使用全域設定)
    /// </summary>
    public FetchMode? FetchMode { get; set; }
    
    /// <summary>
    /// 來源分支 (可選，僅在 FetchMode 為 BranchDiff 時使用)
    /// </summary>
    public string? SourceBranch { get; set; }
    
    /// <summary>
    /// 開始時間 (可選,僅在 FetchMode 為 DateTimeRange 時使用)
    /// </summary>
    public DateTimeOffset? StartDateTime { get; set; }
    
    /// <summary>
    /// 結束時間 (可選,僅在 FetchMode 為 DateTimeRange 時使用)
    /// </summary>
    public DateTimeOffset? EndDateTime { get; set; }
    
    /// <summary>
    /// 驗證配置是否正確
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProjectPath))
            throw new InvalidOperationException("GitLab:Projects:ProjectPath 組態設定不得為空");
            
        if (string.IsNullOrWhiteSpace(TargetBranch))
            throw new InvalidOperationException("GitLab:Projects:TargetBranch 組態設定不得為空");
        
        // 若有設定 FetchMode，則驗證對應欄位
        if (FetchMode.HasValue)
        {
            if (FetchMode.Value == Options.FetchMode.BranchDiff && string.IsNullOrWhiteSpace(SourceBranch))
                throw new InvalidOperationException("當 FetchMode 為 BranchDiff 時，SourceBranch 不得為空");
            
            if (FetchMode.Value == Options.FetchMode.DateTimeRange)
            {
                if (!StartDateTime.HasValue)
                    throw new InvalidOperationException("當 FetchMode 為 DateTimeRange 時，StartDateTime 不得為空");
                    
                if (!EndDateTime.HasValue)
                    throw new InvalidOperationException("當 FetchMode 為 DateTimeRange 時，EndDateTime 不得為空");
                
                if (StartDateTime.Value >= EndDateTime.Value)
                    throw new InvalidOperationException("StartDateTime 必須早於 EndDateTime");
            }
        }
    }
}
```

---

## 實體關係圖

```text
[appsettings.json Root]
├── FetchMode (Enum)
├── SourceBranch (string?)
├── StartDateTime (DateTimeOffset?)
├── EndDateTime (DateTimeOffset?)
├── GoogleSheet (GoogleSheetOptions)
│   └── ColumnMapping (ColumnMappingOptions)
├── AzureDevOps (AzureDevOpsOptions)
│   └── TeamMapping (List<TeamMappingOptions>)
└── GitLab (GitLabOptions)
    └── Projects (List<GitLabProjectOptions>)
        ├── FetchMode? (Enum?)
        ├── SourceBranch? (string?)
        ├── StartDateTime? (DateTimeOffset?)
        └── EndDateTime? (DateTimeOffset?)
```

---

## DI 註冊範例

```csharp
public static IServiceCollection AddConfigurationOptions(this IServiceCollection services, IConfiguration configuration)
{
    // 註冊 GoogleSheet 設定
    services.AddOptions<GoogleSheetOptions>()
        .BindConfiguration("GoogleSheet")
        .Validate(opts =>
        {
            opts.Validate();
            return true;
        })
        .ValidateOnStart();
    
    // 註冊 AzureDevOps 設定
    services.AddOptions<AzureDevOpsOptions>()
        .BindConfiguration("AzureDevOps")
        .Validate(opts =>
        {
            opts.Validate();
            return true;
        })
        .ValidateOnStart();
    
    // GitLab 設定（既有，需擴充驗證邏輯）
    services.Configure<GitLabOptions>(configuration.GetSection("GitLab"));
    
    return services;
}
```

---

## 檔案清單

### 新增檔案
- `ReleaseKit.Console/Options/FetchMode.cs`
- `ReleaseKit.Console/Options/GoogleSheetOptions.cs`
- `ReleaseKit.Console/Options/ColumnMappingOptions.cs`
- `ReleaseKit.Console/Options/AzureDevOpsOptions.cs`
- `ReleaseKit.Console/Options/TeamMappingOptions.cs`

### 修改檔案
- `ReleaseKit.Console/Options/GitLabProjectOptions.cs` (新增 FetchMode、SourceBranch、StartDateTime、EndDateTime)
- `ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs` (新增配置註冊邏輯)

---

**Phase 1 Data Model Complete**: ✅  
**Next**: Phase 1 - Quickstart & Contracts
