# Quick Start: 配置設定類別與 DI 整合

**Feature**: 配置設定類別與 DI 整合  
**Last Updated**: 2026-01-28

## Overview

本指南說明如何在 Release-Kit 中新增、使用與測試配置類別。

---

## 1. 新增配置類別

### 步驟 1: 建立 Options 類別

在 `src/ReleaseKit.Infrastructure/Configuration/` 目錄建立新的 Options 類別：

```csharp
using System.ComponentModel.DataAnnotations;

namespace ReleaseKit.Infrastructure.Configuration;

/// <summary>
/// Google Sheets 配置選項
/// </summary>
public class GoogleSheetOptions
{
    /// <summary>
    /// Google 試算表 ID
    /// </summary>
    [Required(ErrorMessage = "GoogleSheet:SpreadsheetId 不可為空")]
    public string SpreadsheetId { get; init; } = string.Empty;

    /// <summary>
    /// 工作表名稱
    /// </summary>
    [Required(ErrorMessage = "GoogleSheet:SheetName 不可為空")]
    public string SheetName { get; init; } = string.Empty;

    /// <summary>
    /// 服務帳戶憑證檔案路徑
    /// </summary>
    [Required(ErrorMessage = "GoogleSheet:ServiceAccountCredentialPath 不可為空")]
    public string ServiceAccountCredentialPath { get; init; } = string.Empty;

    /// <summary>
    /// 欄位映射配置
    /// </summary>
    [Required]
    public ColumnMappingOptions ColumnMapping { get; init; } = new();
}
```

### 步驟 2: 註冊至 DI 容器

在 `src/ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs` 的 `AddConfigurationOptions` 方法中註冊：

```csharp
public static IServiceCollection AddConfigurationOptions(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // 註冊 Google Sheets 配置
    services.AddOptions<GoogleSheetOptions>()
        .Bind(configuration.GetSection("GoogleSheet"))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    // 其他配置...

    return services;
}
```

### 步驟 3: 在 appsettings.json 中新增配置區段

```json
{
  "GoogleSheet": {
    "SpreadsheetId": "1a2b3c4d5e6f",
    "SheetName": "Sheet1",
    "ServiceAccountCredentialPath": "/path/to/credentials.json",
    "ColumnMapping": {
      "FeatureColumn": "B",
      "TeamColumn": "D"
    }
  }
}
```

---

## 2. 使用配置類別

### 在服務中注入使用

```csharp
using Microsoft.Extensions.Options;
using ReleaseKit.Infrastructure.Configuration;

namespace ReleaseKit.Infrastructure.GoogleSheets;

public class GoogleSheetService
{
    private readonly GoogleSheetOptions _options;

    public GoogleSheetService(IOptions<GoogleSheetOptions> options)
    {
        _options = options.Value;
    }

    public async Task SyncDataAsync()
    {
        // 使用配置值
        var spreadsheetId = _options.SpreadsheetId;
        var sheetName = _options.SheetName;
        
        // 業務邏輯...
    }
}
```

### 選擇適當的注入介面

| 介面 | 使用場景 | 生命週期 |
|------|---------|---------|
| `IOptions<T>` | 配置不變（Console 應用） | Singleton |
| `IOptionsSnapshot<T>` | 每次請求重新載入（Web 應用） | Scoped |
| `IOptionsMonitor<T>` | 支援熱重載與變更通知 | Singleton |

**本專案使用 `IOptions<T>`** 因為是 Console 應用程式，配置在啟動後不會改變。

---

## 3. 環境變數覆寫

### 覆寫單一屬性

```bash
# Linux/macOS
export GoogleSheet__SpreadsheetId="new-spreadsheet-id"

# Windows PowerShell
$env:GoogleSheet__SpreadsheetId="new-spreadsheet-id"
```

### 覆寫巢狀屬性

```bash
export GoogleSheet__ColumnMapping__FeatureColumn="C"
```

### 覆寫陣列元素

```bash
# 覆寫第一個專案的 ProjectPath
export GitLab__Projects__0__ProjectPath="newgroup/newproject"
```

---

## 4. 測試配置類別

### 單元測試範例

```csharp
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReleaseKit.Infrastructure.Configuration;
using Xunit;

namespace ReleaseKit.Infrastructure.Tests.Configuration;

public class GoogleSheetOptionsTests
{
    [Fact]
    public void Bind_ValidConfiguration_ShouldBindCorrectly()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GoogleSheet:SpreadsheetId"] = "test-id",
                ["GoogleSheet:SheetName"] = "TestSheet",
                ["GoogleSheet:ServiceAccountCredentialPath"] = "/test/path.json",
                ["GoogleSheet:ColumnMapping:FeatureColumn"] = "B",
                ["GoogleSheet:ColumnMapping:TeamColumn"] = "D"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<GoogleSheetOptions>()
            .Bind(configuration.GetSection("GoogleSheet"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<GoogleSheetOptions>>().Value;

        // Assert
        options.SpreadsheetId.Should().Be("test-id");
        options.SheetName.Should().Be("TestSheet");
        options.ColumnMapping.FeatureColumn.Should().Be("B");
    }

    [Fact]
    public void Validate_MissingRequiredProperty_ShouldThrowException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GoogleSheet:SpreadsheetId"] = "", // 空值
                ["GoogleSheet:SheetName"] = "TestSheet"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<GoogleSheetOptions>()
            .Bind(configuration.GetSection("GoogleSheet"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Act & Assert
        var act = () => services.BuildServiceProvider(validateScopes: true);
        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*SpreadsheetId*");
    }
}
```

### 測試環境變數覆寫

```csharp
[Fact]
public void Bind_EnvironmentVariableOverride_ShouldUseEnvironmentValue()
{
    // Arrange
    Environment.SetEnvironmentVariable("GoogleSheet__SpreadsheetId", "env-override-id");

    var configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["GoogleSheet:SpreadsheetId"] = "json-id",
            ["GoogleSheet:SheetName"] = "TestSheet"
        })
        .AddEnvironmentVariables()
        .Build();

    var services = new ServiceCollection();
    services.AddOptions<GoogleSheetOptions>()
        .Bind(configuration.GetSection("GoogleSheet"))
        .ValidateDataAnnotations();

    var serviceProvider = services.BuildServiceProvider();

    // Act
    var options = serviceProvider.GetRequiredService<IOptions<GoogleSheetOptions>>().Value;

    // Assert
    options.SpreadsheetId.Should().Be("env-override-id");

    // Cleanup
    Environment.SetEnvironmentVariable("GoogleSheet__SpreadsheetId", null);
}
```

---

## 5. 條件驗證（進階）

### 實作 IValidatableObject

當驗證規則需要跨屬性比對時，實作 `IValidatableObject` 介面：

```csharp
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

public class FetchModeOptions : IValidatableObject
{
    [Required]
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

### 測試條件驗證

```csharp
[Fact]
public void Validate_BranchDiffModeWithoutSourceBranch_ShouldFail()
{
    // Arrange
    var options = new FetchModeOptions
    {
        FetchMode = "BranchDiff",
        SourceBranch = null
    };

    var context = new ValidationContext(options);
    var results = new List<ValidationResult>();

    // Act
    var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

    // Assert
    isValid.Should().BeFalse();
    results.Should().Contain(r => r.MemberNames.Contains(nameof(FetchModeOptions.SourceBranch)));
}
```

---

## 6. 常見問題

### Q1: 配置綁定失敗但沒有錯誤訊息？

**A**: 確認是否使用 `ValidateOnStart()`，此方法會在應用程式啟動時立即驗證配置。

### Q2: 環境變數覆寫不生效？

**A**: 檢查環境變數名稱是否使用雙底線 `__` 作為分隔符，並確認在 `ConfigureAppConfiguration` 中有呼叫 `AddEnvironmentVariables()`。

### Q3: 巢狀物件驗證不執行？

**A**: 確認子物件也有套用 `[Required]` 或 `[ValidateComplexType]` 標記。

### Q4: 如何在測試中模擬配置？

**A**: 使用 `Microsoft.Extensions.Options.Options.Create<T>()` 建立測試用的 IOptions：

```csharp
var options = Options.Create(new GoogleSheetOptions
{
    SpreadsheetId = "test-id",
    SheetName = "TestSheet"
});

var service = new GoogleSheetService(options);
```

---

## 7. 參考資料

- [Microsoft Docs: Options Pattern](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options)
- [Microsoft Docs: Configuration in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration)
- [Data Annotations](https://learn.microsoft.com/en-us/dotnet/api/system.componentmodel.dataannotations)
