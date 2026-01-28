# Research: 配置設定類別與 DI 整合

**Date**: 2026-01-28

## 研究目標

1. .NET 9 Options Pattern 最佳實踐
2. Data Annotations 驗證機制
3. 環境變數綁定規則
4. 巢狀配置物件處理

---

## 1. .NET 9 Options Pattern 最佳實踐

### Decision

使用 .NET 內建的 **Options Pattern**，透過 `IOptions<T>`、`IOptionsSnapshot<T>` 或 `IOptionsMonitor<T>` 注入配置類別。

### Rationale

- **強型別存取**: 編譯時期即可發現屬性名稱錯誤
- **DI 整合**: 透過建構函式注入，便於測試與模擬
- **變更追蹤**: `IOptionsMonitor<T>` 支援配置熱重載（本專案不需要，但保留彈性）
- **內建支援**: 無需額外套件，減少相依性

### Alternatives considered

- **直接使用 IConfiguration**: 失去型別安全，容易拼錯鍵值
- **自訂配置管理器**: 過度設計，增加維護成本

### Implementation Details

```csharp
// 註冊配置（Program.cs 或擴充方法）
services.Configure<GitLabOptions>(configuration.GetSection("GitLab"));

// 注入使用（任何服務）
public class MyService
{
    private readonly GitLabOptions _options;
    
    public MyService(IOptions<GitLabOptions> options)
    {
        _options = options.Value;
    }
}
```

**選擇依據**:
- `IOptions<T>`: 單例模式，配置在應用程式生命週期內不變（本專案使用）
- `IOptionsSnapshot<T>`: 每次請求重新讀取（Web 應用使用）
- `IOptionsMonitor<T>`: 支援熱重載與變更通知（動態配置使用）

---

## 2. Data Annotations 驗證機制

### Decision

使用 **Data Annotations** 進行配置驗證，並在啟動時透過 `ValidateDataAnnotations()` 或 `ValidateOnStart()` 執行驗證。

### Rationale

- **宣告式驗證**: 將驗證規則直接標記在屬性上，易讀易維護
- **內建支援**: `[Required]`、`[Url]`、`[Range]` 等豐富的驗證屬性
- **啟動時驗證**: 確保配置錯誤在啟動階段即被發現，避免執行時失敗
- **清楚錯誤訊息**: 驗證失敗時提供屬性名稱與原因

### Alternatives considered

- **手動驗證邏輯**: 需在每個服務建構函式中檢查，容易遺漏
- **FluentValidation**: 功能強大但過於複雜，本專案配置驗證需求簡單

### Implementation Details

```csharp
using System.ComponentModel.DataAnnotations;

public class GitLabOptions
{
    [Required(ErrorMessage = "GitLab:ApiUrl 不可為空")]
    [Url(ErrorMessage = "GitLab:ApiUrl 必須是有效的 URL")]
    public string ApiUrl { get; init; } = string.Empty;

    [Required(ErrorMessage = "GitLab:AccessToken 不可為空")]
    public string AccessToken { get; init; } = string.Empty;
}

// 啟動驗證（.NET 8+ 支援）
services.AddOptions<GitLabOptions>()
    .Bind(configuration.GetSection("GitLab"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

**驗證時機**:
- `ValidateDataAnnotations()`: 啟用 Data Annotations 驗證
- `ValidateOnStart()`: 在應用程式啟動時立即驗證（.NET 6+ 支援）

**錯誤處理**:
- 驗證失敗時拋出 `OptionsValidationException`，包含所有驗證錯誤訊息
- 可在 Program.cs 捕獲並記錄錯誤後終止應用程式

---

## 3. 環境變數綁定規則

### Decision

使用 **雙底線 `__` 作為階層分隔符**，環境變數優先於 `appsettings.json`。

### Rationale

- **.NET 預設行為**: 符合 ASP.NET Core 慣例
- **跨平台相容**: Windows、Linux、macOS 都支援
- **優先順序明確**: 環境變數 > appsettings.{Environment}.json > appsettings.json

### Alternatives considered

- **使用 `:` 分隔**: 在某些 Shell 中有特殊意義（如 Bash）
- **使用 `.` 分隔**: 不符合 .NET 慣例

### Implementation Details

**JSON 配置**:
```json
{
  "GitLab": {
    "ApiUrl": "https://gitlab.com/api/v4",
    "AccessToken": "default-token"
  }
}
```

**環境變數覆寫**:
```bash
# Linux/macOS
export GitLab__ApiUrl="https://custom.gitlab.com/api/v4"
export GitLab__AccessToken="my-secret-token"

# Windows PowerShell
$env:GitLab__ApiUrl="https://custom.gitlab.com/api/v4"
$env:GitLab__AccessToken="my-secret-token"

# Windows CMD
set GitLab__ApiUrl=https://custom.gitlab.com/api/v4
set GitLab__AccessToken=my-secret-token
```

**綁定順序（由低到高）**:
1. appsettings.json
2. appsettings.{Environment}.json
3. User Secrets (開發環境)
4. 環境變數
5. 命令列參數

**注意事項**:
- 環境變數名稱大小寫不敏感（Windows），但建議與 JSON 鍵名保持一致
- 巢狀物件使用多個 `__` 連接（如 `GoogleSheet__ColumnMapping__FeatureColumn`）

---

## 4. 巢狀配置物件處理

### Decision

使用 **獨立的 Options 類別定義巢狀結構**，並透過屬性包含子物件。

### Rationale

- **型別安全**: 每層結構都有明確的型別定義
- **易於測試**: 可獨立測試每個子物件的綁定邏輯
- **符合單一職責**: 每個 Options 類別負責一個配置層級

### Alternatives considered

- **使用字典或動態型別**: 失去型別安全，難以維護
- **扁平化配置**: 屬性名稱冗長，不符合 JSON 結構慣例

### Implementation Details

**JSON 配置**:
```json
{
  "GoogleSheet": {
    "SpreadsheetId": "abc123",
    "SheetName": "Sheet1",
    "ColumnMapping": {
      "FeatureColumn": "B",
      "TeamColumn": "D"
    }
  }
}
```

**Options 類別定義**:
```csharp
public class GoogleSheetOptions
{
    [Required]
    public string SpreadsheetId { get; init; } = string.Empty;

    [Required]
    public string SheetName { get; init; } = string.Empty;

    [Required]
    public ColumnMappingOptions ColumnMapping { get; init; } = new();
}

public class ColumnMappingOptions
{
    [Required]
    [RegularExpression("^[A-Z]+$", ErrorMessage = "欄位必須是大寫英文字母")]
    public string FeatureColumn { get; init; } = string.Empty;

    [Required]
    [RegularExpression("^[A-Z]+$", ErrorMessage = "欄位必須是大寫英文字母")]
    public string TeamColumn { get; init; } = string.Empty;
}
```

**註冊方式**:
```csharp
// 只需註冊根物件，子物件會自動綁定
services.AddOptions<GoogleSheetOptions>()
    .Bind(configuration.GetSection("GoogleSheet"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

**環境變數覆寫巢狀屬性**:
```bash
export GoogleSheet__ColumnMapping__FeatureColumn="C"
```

**陣列處理**:
```json
{
  "GitLab": {
    "Projects": [
      { "ProjectPath": "group/project1" },
      { "ProjectPath": "group/project2" }
    ]
  }
}
```

```csharp
public class GitLabOptions
{
    public List<GitLabProjectOptions> Projects { get; init; } = new();
}

public class GitLabProjectOptions
{
    [Required]
    public string ProjectPath { get; init; } = string.Empty;
}
```

環境變數覆寫陣列元素:
```bash
export GitLab__Projects__0__ProjectPath="group/new-project"
```

---

## 5. 選擇性屬性處理 (Nullable)

### Decision

對於選擇性屬性，使用 **可空型別 (`string?`, `DateTimeOffset?`)** 並移除 `[Required]` 標記。

### Rationale

- **明確語意**: 可空型別明確表達「此屬性可選」
- **避免預設值混淆**: 不使用 `string.Empty` 或 `DateTime.MinValue` 代表未設定
- **便於驗證**: 可透過 `is null` 判斷是否提供配置

### Implementation Details

```csharp
public class FetchModeOptions
{
    /// <summary>
    /// 拉取模式: DateTimeRange 或 BranchDiff
    /// </summary>
    [Required]
    public string FetchMode { get; init; } = string.Empty;

    /// <summary>
    /// 來源分支（BranchDiff 模式必填）
    /// </summary>
    public string? SourceBranch { get; init; }

    /// <summary>
    /// 開始時間（DateTimeRange 模式必填）
    /// </summary>
    public DateTimeOffset? StartDateTime { get; init; }

    /// <summary>
    /// 結束時間（DateTimeRange 模式必填）
    /// </summary>
    public DateTimeOffset? EndDateTime { get; init; }
}
```

**條件驗證**（如需跨屬性驗證）:
```csharp
using System.ComponentModel.DataAnnotations;

public class FetchModeOptions : IValidatableObject
{
    public string FetchMode { get; init; } = string.Empty;
    public string? SourceBranch { get; init; }
    public DateTimeOffset? StartDateTime { get; init; }
    public DateTimeOffset? EndDateTime { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FetchMode == "BranchDiff" && string.IsNullOrEmpty(SourceBranch))
        {
            yield return new ValidationResult(
                "FetchMode 為 BranchDiff 時，SourceBranch 不可為空",
                new[] { nameof(SourceBranch) });
        }

        if (FetchMode == "DateTimeRange")
        {
            if (!StartDateTime.HasValue)
                yield return new ValidationResult(
                    "FetchMode 為 DateTimeRange 時，StartDateTime 不可為空",
                    new[] { nameof(StartDateTime) });

            if (!EndDateTime.HasValue)
                yield return new ValidationResult(
                    "FetchMode 為 DateTimeRange 時，EndDateTime 不可為空",
                    new[] { nameof(EndDateTime) });
        }
    }
}
```

---

## 總結

本研究確立了以下技術決策：

1. **Options Pattern**: 使用 `IOptions<T>` 進行強型別配置注入
2. **驗證機制**: Data Annotations + `ValidateOnStart()` 確保啟動時驗證
3. **環境變數**: 使用 `__` 分隔符，優先於 JSON 配置
4. **巢狀結構**: 獨立 Options 類別處理巢狀物件與陣列
5. **選擇性屬性**: 使用可空型別，搭配 `IValidatableObject` 進行條件驗證

這些決策完全符合 .NET 9 最佳實踐，並滿足 Release-Kit Constitution 的所有規範。
