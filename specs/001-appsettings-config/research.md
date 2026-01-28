# Research: AppSettings 配置擴充

**Feature**: AppSettings 配置擴充  
**Branch**: `001-appsettings-config`  
**Date**: 2026-01-28  

## Phase 0 研究目標

本階段需解決以下技術未知項目：

1. **Options Pattern 最佳實踐**：如何正確使用 `IOptions<T>` vs `IOptionsSnapshot<T>` vs `IOptionsMonitor<T>`？
2. **配置驗證機制**：如何在應用程式啟動時驗證必要配置欄位？
3. **環境變數覆寫機制**：如何透過環境變數覆寫巢狀配置？
4. **可選配置欄位處理**：如何設計可為 null 的 `DateTimeOffset?` 與 `string?` 欄位？

---

## 研究主題 1：Options Pattern 選擇策略

### 決策：使用 `IOptions<T>` 搭配啟動驗證

**理由**：
- Release-Kit 為 Console 應用程式，配置在啟動後不會變更（immutable after startup）
- `IOptions<T>` 為 Singleton，效能最佳且符合不可變需求
- `IOptionsSnapshot<T>` 和 `IOptionsMonitor<T>` 適用於需要熱重載的場景（如 ASP.NET Core），但此專案不需要

**考慮的替代方案**：
- ❌ **IOptionsSnapshot<T>**：每次請求建立新實例，Console 應用程式無 Scope 概念，不適用
- ❌ **IOptionsMonitor<T>**：支援配置熱重載，但增加複雜度且不符合需求
- ✅ **IOptions<T>**：最簡單且符合不可變配置需求

**實作方式**：
```csharp
// 註冊配置
services.Configure<GoogleSheetOptions>(configuration.GetSection("GoogleSheet"));

// 注入使用
public class MyService
{
    private readonly GoogleSheetOptions _options;
    
    public MyService(IOptions<GoogleSheetOptions> options)
    {
        _options = options.Value; // 在建構子中取值，避免重複存取 .Value
    }
}
```

**參考資料**：
- [Microsoft Docs: Options pattern in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options)

---

## 研究主題 2：配置驗證機制

### 決策：使用 ValidateOnStart 與自訂驗證邏輯

**理由**：
- 需在應用程式啟動時立即失敗（Fail-Fast），而非執行時才發現配置錯誤
- 必要欄位未設定應拋出 `InvalidOperationException` 並提供明確錯誤訊息
- .NET 提供兩種驗證機制：Data Annotations 與自訂驗證，本專案採用自訂驗證以提供更精確的錯誤訊息

**考慮的替代方案**：
- ❌ **Data Annotations (Required, Range, etc.)**：錯誤訊息較通用，無法提供精確的配置鍵名稱
- ✅ **ValidateOnStart + 自訂驗證**：可在啟動時執行驗證，並在 Options 類別中加入 `Validate()` 方法

**實作方式**：

**方案 A：使用 Data Annotations** (較簡潔，但錯誤訊息較通用)
```csharp
using System.ComponentModel.DataAnnotations;

public class GoogleSheetOptions
{
    [Required(ErrorMessage = "GoogleSheet:SpreadsheetId 設定不得為空")]
    public string SpreadsheetId { get; set; } = string.Empty;
}

// 註冊時啟用驗證
services.AddOptions<GoogleSheetOptions>()
    .BindConfiguration("GoogleSheet")
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

**方案 B：使用自訂驗證** (錯誤訊息更精確，建議採用)
```csharp
public class GoogleSheetOptions
{
    public string SpreadsheetId { get; set; } = string.Empty;
    public string SheetName { get; set; } = string.Empty;
    
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SpreadsheetId))
            throw new InvalidOperationException("GoogleSheet:SpreadsheetId 組態設定不得為空");
            
        if (string.IsNullOrWhiteSpace(SheetName))
            throw new InvalidOperationException("GoogleSheet:SheetName 組態設定不得為空");
    }
}

// 註冊時啟用驗證
services.AddOptions<GoogleSheetOptions>()
    .BindConfiguration("GoogleSheet")
    .Validate(opts =>
    {
        opts.Validate();
        return true;
    })
    .ValidateOnStart();
```

**最終決策**：採用方案 B（自訂驗證），因為：
- 錯誤訊息可精確指出缺少的配置鍵值
- 符合專案既有的錯誤處理慣例（參考 `ServiceCollectionExtensions.cs` 中 Redis 配置驗證）

**參考資料**：
- [Microsoft Docs: Options validation in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/options#options-validation)

---

## 研究主題 3：環境變數覆寫機制

### 決策：使用階層式命名規範 (Hierarchical Keys)

**理由**：
- .NET Configuration 系統原生支援透過環境變數覆寫巢狀配置
- 使用 `__` (雙底線) 作為階層分隔符號，例如 `ReleaseKit__GoogleSheet__SpreadsheetId` 對應 `ReleaseKit:GoogleSheet:SpreadsheetId`
- 環境變數優先級高於 appsettings.json，符合 12-Factor App 原則

**考慮的替代方案**：
- ❌ **單層環境變數 (Flat Keys)**：無法表達巢狀結構，需額外解析邏輯
- ✅ **階層式命名 (Hierarchical Keys with __)**：原生支援，無需額外程式碼

**實作方式**：
```bash
# 設定環境變數
export ReleaseKit__GoogleSheet__SpreadsheetId="my-spreadsheet-id"
export ReleaseKit__GitLab__AccessToken="glpat-xxxxxxxxxxxxxxxxxxxx"

# 啟動應用程式時，環境變數會自動覆寫 appsettings.json 中的值
```

**陣列與索引處理**：
```bash
# 覆寫陣列元素
export ReleaseKit__GitLab__Projects__0__FetchMode="DateTimeRange"
export ReleaseKit__GitLab__Projects__0__StartDateTime="2025-01-01"
```

**優先順序**：
1. 環境變數（最高優先）
2. User Secrets（開發環境）
3. appsettings.{Environment}.json（環境特定）
4. appsettings.json（基礎配置）

**參考資料**：
- [Microsoft Docs: Configuration in .NET - Environment Variables](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration#environment-variables)

---

## 研究主題 4：可選配置欄位處理

### 決策：使用 Nullable Reference Types (`string?`, `DateTimeOffset?`)

**理由**：
- .NET 9 專案已啟用 Nullable Reference Types (`<Nullable>enable</Nullable>`)
- 可明確區分必要欄位 (`string`) 與可選欄位 (`string?`)
- 編譯器會在存取可為 null 的欄位時提供警告，減少執行時錯誤

**實作方式**：
```csharp
public class GitLabProjectOptions
{
    /// <summary>
    /// 拉取模式 (必要欄位)
    /// </summary>
    public string FetchMode { get; set; } = string.Empty;
    
    /// <summary>
    /// 來源分支 (可選欄位，僅在 FetchMode 為 BranchDiff 時使用)
    /// </summary>
    public string? SourceBranch { get; set; }
    
    /// <summary>
    /// 開始時間 (可選欄位，僅在 FetchMode 為 DateTimeRange 時使用)
    /// </summary>
    public DateTimeOffset? StartDateTime { get; set; }
}
```

**驗證邏輯**：
```csharp
public void Validate()
{
    if (string.IsNullOrWhiteSpace(FetchMode))
        throw new InvalidOperationException("GitLab:Projects:FetchMode 組態設定不得為空");
    
    // 根據 FetchMode 驗證對應欄位
    if (FetchMode == "BranchDiff" && string.IsNullOrWhiteSpace(SourceBranch))
        throw new InvalidOperationException("當 FetchMode 為 BranchDiff 時，SourceBranch 不得為空");
    
    if (FetchMode == "DateTimeRange")
    {
        if (!StartDateTime.HasValue)
            throw new InvalidOperationException("當 FetchMode 為 DateTimeRange 時，StartDateTime 不得為空");
        if (!EndDateTime.HasValue)
            throw new InvalidOperationException("當 FetchMode 為 DateTimeRange 時，EndDateTime 不得為空");
    }
}
```

**考慮的替代方案**：
- ❌ **預設值 (Default Values)**：無法區分「未設定」與「設定為預設值」
- ✅ **Nullable Types**：明確表達可選欄位，編譯器提供型別檢查

**參考資料**：
- [Microsoft Docs: Nullable reference types](https://learn.microsoft.com/en-us/dotnet/csharp/nullable-references)

---

## 研究主題 5：FetchMode 列舉設計

### 決策：使用 Enum 替代字串

**理由**：
- 符合 Constitution 第 VII 條「避免硬編碼」原則
- 型別安全，編譯時檢查，避免拼寫錯誤
- 可透過 Enum.Parse 將配置字串轉換為列舉值

**實作方式**：
```csharp
// 定義列舉
public enum FetchMode
{
    DateTimeRange,
    BranchDiff
}

// 配置類別
public class GitLabProjectOptions
{
    public FetchMode FetchMode { get; set; }
}

// appsettings.json 仍使用字串
{
  "GitLab": {
    "Projects": [
      {
        "FetchMode": "DateTimeRange"  // 會自動轉換為 FetchMode.DateTimeRange
      }
    ]
  }
}
```

**錯誤處理**：
```csharp
public void Validate()
{
    if (!Enum.IsDefined(typeof(FetchMode), FetchMode))
        throw new InvalidOperationException($"無效的 FetchMode 值: {FetchMode}");
}
```

**考慮的替代方案**：
- ❌ **字串常數 (String Constants)**：型別不安全，需手動驗證
- ✅ **Enum**：型別安全，編譯時檢查

**參考資料**：
- [Microsoft Docs: Enumeration types](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/enum)

---

## 研究結論

所有技術未知項目已解決，可進入 Phase 1 設計階段。關鍵決策總結：

| 項目 | 決策 | 理由 |
|------|------|------|
| Options Pattern | IOptions<T> | 符合 Console 應用程式不可變配置需求 |
| 配置驗證 | ValidateOnStart + 自訂驗證 | 提供精確錯誤訊息，符合既有慣例 |
| 環境變數 | 階層式命名 (Hierarchical Keys) | 原生支援，無需額外程式碼 |
| 可選欄位 | Nullable Types (string?, DateTimeOffset?) | 編譯器檢查，減少執行時錯誤 |
| FetchMode | Enum | 型別安全，避免拼寫錯誤 |

---

**Phase 0 Complete**: ✅  
**Next Phase**: Phase 1 - Design & Contracts
