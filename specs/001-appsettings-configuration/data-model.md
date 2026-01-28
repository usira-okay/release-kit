# Options 類別設計模型

**Feature**: Configuration Settings Infrastructure  
**Date**: 2025-01-28  
**Status**: Approved

本文件定義專案中 Options 類別的標準結構與設計規範，確保所有設定管理遵循一致的模式。

---

## 基本結構範本

### 命名規範

| 元素 | 規範 | 範例 |
|------|------|------|
| **類別名稱** | `{功能}Options` | `GitLabOptions`, `RedisOptions` |
| **檔案名稱** | 與類別名稱一致 | `GitLabOptions.cs` |
| **命名空間** | `ReleaseKit.Console.Options` | - |
| **屬性名稱** | PascalCase | `ApiUrl`, `AccessToken` |

### 最小 Options 類別範本

```csharp
namespace ReleaseKit.Console.Options;

/// <summary>
/// {功能} 設定選項
/// </summary>
public class {功能}Options
{
    /// <summary>
    /// {屬性說明}
    /// </summary>
    public string PropertyName { get; set; } = string.Empty;
}
```

**必要元素**:
- ✅ `namespace` 宣告
- ✅ 類別 XML 註解 (`<summary>`)
- ✅ 每個屬性的 XML 註解
- ✅ 屬性的預設值初始化

---

## 屬性類型與預設值策略

### 字串屬性

```csharp
/// <summary>
/// API 端點 URL
/// </summary>
public string ApiUrl { get; set; } = string.Empty;
```

**預設值**: `string.Empty`  
**理由**: 專案啟用 Nullable Reference Types，`string.Empty` 避免 null 檢查

### 數值屬性

```csharp
/// <summary>
/// 連線逾時時間（秒）
/// </summary>
public int Timeout { get; set; } = 30;

/// <summary>
/// 選用的最大重試次數
/// </summary>
public int? MaxRetries { get; set; }
```

**預設值策略**:
- 必填數值 → 設定合理的預設值
- 選用數值 → 使用可空型別 (`int?`, `double?`)

### 布林屬性

```csharp
/// <summary>
/// 是否啟用快取
/// </summary>
public bool EnableCache { get; set; } = true;
```

**預設值**: 明確設定 `true` 或 `false`，避免依賴 C# 預設的 `false`

### 列舉屬性

```csharp
/// <summary>
/// 日誌等級
/// </summary>
public LogLevel Level { get; set; } = LogLevel.Information;
```

**預設值**: 設定明確的列舉值

### 集合屬性

```csharp
/// <summary>
/// 專案設定清單
/// </summary>
public List<ProjectOptions> Projects { get; set; } = new();
```

**預設值**: `new()` 初始化空集合，避免 null

**選擇準則**:
- `List<T>` - 需要修改集合（新增/移除）
- `IReadOnlyList<T>` - 設定載入後不可變更

### 字典屬性

```csharp
/// <summary>
/// 使用者名稱對應表
/// </summary>
public Dictionary<string, string> UserMappings { get; set; } = new();
```

**JSON 綁定範例**:
```json
{
  "UserMappings": {
    "gitlab_user": "internal_user",
    "another_user": "mapped_user"
  }
}
```

---

## 複雜結構模式

### 巢狀物件

#### 何時拆分為獨立類別？

✅ **應拆分** (Independent Options Class):
- 巢狀物件有 **3 個以上屬性**
- 巢狀物件代表明確的**業務概念**（如 Project, Server, Credentials）
- 巢狀物件可能在**多個 Options 中重複使用**

❌ **不應拆分** (Keep Inline):
- 只有 1-2 個簡單屬性
- 僅用於該 Options 的私有結構

#### 範例: GitLab 設定（2 層巢狀）

**主 Options 類別** (`GitLabOptions.cs`):
```csharp
namespace ReleaseKit.Console.Options;

/// <summary>
/// GitLab 設定選項
/// </summary>
public class GitLabOptions
{
    /// <summary>
    /// GitLab API URL
    /// </summary>
    public string ApiUrl { get; set; } = string.Empty;

    /// <summary>
    /// GitLab 存取權杖
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// GitLab 專案設定清單
    /// </summary>
    public List<GitLabProjectOptions> Projects { get; set; } = new();
}
```

**巢狀 Options 類別** (`GitLabProjectOptions.cs` - 獨立檔案):
```csharp
namespace ReleaseKit.Console.Options;

/// <summary>
/// GitLab 專案設定選項
/// </summary>
public class GitLabProjectOptions
{
    /// <summary>
    /// 專案路徑（如 "group/project"）
    /// </summary>
    public string ProjectPath { get; set; } = string.Empty;

    /// <summary>
    /// 目標分支名稱
    /// </summary>
    public string TargetBranch { get; set; } = string.Empty;
}
```

**對應 JSON 結構** (`appsettings.json`):
```json
{
  "GitLab": {
    "ApiUrl": "https://gitlab.com/api/v4",
    "AccessToken": "",
    "Projects": [
      {
        "ProjectPath": "mygroup/backend-api",
        "TargetBranch": "main"
      },
      {
        "ProjectPath": "mygroup/frontend",
        "TargetBranch": "develop"
      }
    ]
  }
}
```

---

## 實體範例

### 範例 1: 簡單設定（無巢狀結構）

**場景**: Redis 連線設定

**Options 類別** (`RedisOptions.cs`):
```csharp
namespace ReleaseKit.Console.Options;

/// <summary>
/// Redis 設定選項
/// </summary>
public class RedisOptions
{
    /// <summary>
    /// Redis 連線字串
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Redis 實例名稱前綴
    /// </summary>
    public string InstanceName { get; set; } = string.Empty;

    /// <summary>
    /// 連線逾時時間（毫秒）
    /// </summary>
    public int ConnectTimeout { get; set; } = 5000;

    /// <summary>
    /// 是否允許連線失敗時繼續啟動
    /// </summary>
    public bool AbortOnConnectFail { get; set; } = false;
}
```

**對應 JSON** (`appsettings.json`):
```json
{
  "Redis": {
    "ConnectionString": "localhost:6379",
    "InstanceName": "ReleaseKit:",
    "ConnectTimeout": 5000,
    "AbortOnConnectFail": false
  }
}
```

### 範例 2: 包含集合的設定

**場景**: Bitbucket 設定（多專案）

**Options 類別** (`BitbucketOptions.cs`):
```csharp
namespace ReleaseKit.Console.Options;

/// <summary>
/// Bitbucket 設定選項
/// </summary>
public class BitbucketOptions
{
    /// <summary>
    /// Bitbucket API URL
    /// </summary>
    public string ApiUrl { get; set; } = string.Empty;

    /// <summary>
    /// Bitbucket 電子郵件
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Bitbucket 存取權杖
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Bitbucket 專案設定清單
    /// </summary>
    public List<BitbucketProjectOptions> Projects { get; set; } = new();
}
```

**巢狀類別** (`BitbucketProjectOptions.cs`):
```csharp
namespace ReleaseKit.Console.Options;

/// <summary>
/// Bitbucket 專案設定選項
/// </summary>
public class BitbucketProjectOptions
{
    /// <summary>
    /// 專案路徑
    /// </summary>
    public string ProjectPath { get; set; } = string.Empty;

    /// <summary>
    /// 目標分支
    /// </summary>
    public string TargetBranch { get; set; } = string.Empty;
}
```

### 範例 3: 包含字典對應的設定

**場景**: 使用者名稱對應

**Options 類別** (`UserMappingOptions.cs`):
```csharp
namespace ReleaseKit.Console.Options;

/// <summary>
/// 使用者對應設定選項
/// </summary>
public class UserMappingOptions
{
    /// <summary>
    /// 使用者對應清單
    /// </summary>
    public List<UserMapping> Mappings { get; set; } = new();
}
```

**巢狀類別** (`UserMapping.cs`):
```csharp
namespace ReleaseKit.Console.Options;

/// <summary>
/// 使用者對應
/// </summary>
public class UserMapping
{
    /// <summary>
    /// 來源使用者名稱
    /// </summary>
    public string From { get; set; } = string.Empty;

    /// <summary>
    /// 目標使用者名稱
    /// </summary>
    public string To { get; set; } = string.Empty;
}
```

**對應 JSON**:
```json
{
  "UserMapping": {
    "Mappings": [
      { "From": "gitlab_user1", "To": "internal_user1" },
      { "From": "gitlab_user2", "To": "internal_user2" }
    ]
  }
}
```

---

## JSON 設定檔規範

### 檔案層次結構

```text
/src/ReleaseKit.Console/
├── appsettings.json              ← 基礎設定（必須）
├── appsettings.Development.json  ← 開發環境覆寫（選用）
├── appsettings.Production.json   ← 生產環境覆寫（選用）
├── appsettings.Qa.json           ← QA 環境覆寫（選用）
└── appsettings.Docker.json       ← Docker 環境（選用）
```

### 命名對應規則

**C# 屬性** ↔ **JSON 屬性** (不區分大小寫綁定)

```csharp
// C# Options 類別
public class GitLabOptions
{
    public string ApiUrl { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
}
```

**建議使用 PascalCase** (與 C# 一致):
```json
{
  "GitLab": {
    "ApiUrl": "https://gitlab.com/api/v4",
    "AccessToken": ""
  }
}
```

**也支援 camelCase** (相容性):
```json
{
  "GitLab": {
    "apiUrl": "https://gitlab.com/api/v4",
    "accessToken": ""
  }
}
```

### 環境特定覆寫模式

#### 模式 1: 部分屬性覆寫（推薦）

**appsettings.json** (基礎):
```json
{
  "GitLab": {
    "ApiUrl": "https://gitlab.com/api/v4",
    "AccessToken": "",
    "Projects": [...]
  }
}
```

**appsettings.Development.json** (只覆寫 ApiUrl):
```json
{
  "GitLab": {
    "ApiUrl": "https://gitlab-dev.internal.com/api/v4"
  }
}
```

**結果**: `AccessToken` 與 `Projects` 繼承自基礎設定

#### 模式 2: 完整區段替換

**appsettings.Production.json** (完整定義):
```json
{
  "GitLab": {
    "ApiUrl": "https://gitlab-prod.company.com/api/v4",
    "AccessToken": "${GITLAB_TOKEN}",
    "Projects": [
      {
        "ProjectPath": "company/production-api",
        "TargetBranch": "release"
      }
    ]
  }
}
```

**結果**: 完全取代基礎設定的 GitLab 區段

---

## 設計原則檢查表

新增 Options 類別時，請確認以下項目：

### 結構原則
- [ ] 類別名稱遵循 `{功能}Options` 命名規範
- [ ] 檔案名稱與類別名稱一致（One Class Per File）
- [ ] 命名空間為 `ReleaseKit.Console.Options`
- [ ] 類別為 `public` 且為 POCO（無業務邏輯方法）

### 文件原則
- [ ] 類別包含 XML `<summary>` 註解（繁體中文）
- [ ] 每個公開屬性包含 XML `<summary>` 註解
- [ ] 註解說明屬性的用途與有效值範圍

### 預設值原則
- [ ] 字串屬性預設為 `string.Empty`
- [ ] 集合屬性預設為 `new()`
- [ ] 數值與布林屬性有明確的預設值
- [ ] 選用屬性使用可空型別 (`T?`)

### 複雜度原則
- [ ] 巢狀物件（3+ 屬性）拆分為獨立類別
- [ ] 巢狀類別位於獨立檔案
- [ ] 避免超過 3 層的巢狀結構

### 一致性原則
- [ ] 與現有 Options 類別（GitLabOptions, BitbucketOptions）風格一致
- [ ] JSON 屬性使用 PascalCase
- [ ] 遵循專案憲章的命名與組織規範

---

## 反模式（Anti-Patterns）

### ❌ 反模式 1: 包含業務邏輯

```csharp
// ❌ 錯誤：Options 類別不應包含業務邏輯
public class GitLabOptions
{
    public string ApiUrl { get; set; } = string.Empty;
    
    // ❌ 驗證邏輯應在 IValidateOptions<T> 或服務層
    public bool IsValid() => !string.IsNullOrEmpty(ApiUrl);
    
    // ❌ 工廠方法應在獨立的 Factory 類別
    public GitLabClient CreateClient() => new(ApiUrl);
}
```

### ❌ 反模式 2: 使用 null 預設值

```csharp
// ❌ 錯誤：在啟用 Nullable Reference Types 的專案中
public class RedisOptions
{
    public string ConnectionString { get; set; } = null!;  // ❌
    public List<string> Servers { get; set; } = null!;     // ❌
}

// ✅ 正確
public class RedisOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public List<string> Servers { get; set; } = new();
}
```

### ❌ 反模式 3: 過度巢狀

```csharp
// ❌ 錯誤：超過 3 層巢狀，難以理解
public class SystemOptions
{
    public DatabaseOptions Database { get; set; } = new();
}

public class DatabaseOptions
{
    public ConnectionOptions Connection { get; set; } = new();
}

public class ConnectionOptions
{
    public PoolOptions Pool { get; set; } = new();
}

public class PoolOptions
{
    public int MaxSize { get; set; }  // 第 4 層！
}

// ✅ 正確：扁平化結構
public class DatabaseOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public int PoolMaxSize { get; set; } = 100;
}
```

### ❌ 反模式 4: 魔術字串作為設定值

```csharp
// ❌ 錯誤：使用魔術字串
public class CacheOptions
{
    public string Strategy { get; set; } = "memory";  // ❌ "memory", "redis", "distributed"?
}

// ✅ 正確：使用列舉
public enum CacheStrategy
{
    Memory,
    Redis,
    Distributed
}

public class CacheOptions
{
    public CacheStrategy Strategy { get; set; } = CacheStrategy.Memory;
}
```

---

## 版本歷史

| 版本 | 日期 | 變更內容 |
|------|------|---------|
| 1.0 | 2025-01-28 | 初始版本，基於現有專案模式制定規範 |
