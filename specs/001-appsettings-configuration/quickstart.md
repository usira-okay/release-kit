# 設定管理快速入門指南

**Feature**: Configuration Settings Infrastructure  
**Date**: 2025-01-28  
**Audience**: ReleaseKit 專案開發者

本指南提供四種常見的設定管理情境與逐步實作指引。

---

## 前置需求

- .NET 9.0 SDK
- 熟悉 C# 基本語法
- 了解 ASP.NET Core 的相依性注入 (DI) 概念

---

## 情境 1: 新增簡單設定區段

**目標**: 為新的外部服務（如 Seq 日誌）新增設定

### 步驟 1: 建立 Options 類別

**檔案路徑**: `/src/ReleaseKit.Console/Options/SeqOptions.cs`

```csharp
namespace ReleaseKit.Console.Options;

/// <summary>
/// Seq 日誌伺服器設定選項
/// </summary>
public class SeqOptions
{
    /// <summary>
    /// Seq 伺服器 URL
    /// </summary>
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>
    /// Seq API 金鑰
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// 是否啟用 Seq 日誌
    /// </summary>
    public bool Enabled { get; set; } = false;
}
```

**✅ 檢查點**:
- 類別名稱以 `Options` 結尾
- 每個屬性有 XML 註解（繁體中文）
- 所有屬性有預設值
- 檔案名稱與類別名稱一致

### 步驟 2: 更新 appsettings.json

**檔案路徑**: `/src/ReleaseKit.Console/appsettings.json`

```json
{
  "Serilog": {
    // ... 現有設定
  },
  "Seq": {
    "ServerUrl": "http://localhost:5341",
    "ApiKey": "",
    "Enabled": false
  }
}
```

**✅ 檢查點**:
- JSON 區段名稱與 Options 類別名稱一致（去除 Options 後綴）
- 屬性名稱使用 PascalCase
- 提供合理的預設值

### 步驟 3: 註冊至 DI 容器

**檔案路徑**: `/src/ReleaseKit.Console/Extensions/ServiceCollectionExtensions.cs`

```csharp
public static IServiceCollection AddConfigurationOptions(
    this IServiceCollection services, 
    IConfiguration configuration)
{
    // 現有註冊
    services.Configure<GitLabOptions>(configuration.GetSection("GitLab"));
    services.Configure<BitbucketOptions>(configuration.GetSection("Bitbucket"));
    services.Configure<UserMappingOptions>(configuration.GetSection("UserMapping"));
    
    // ✨ 新增 Seq 設定註冊
    services.Configure<SeqOptions>(configuration.GetSection("Seq"));

    return services;
}
```

**✅ 檢查點**:
- `GetSection` 參數與 JSON 區段名稱一致
- 註冊在 `AddConfigurationOptions` 方法中
- 使用 `Configure<T>()` 而非手動註冊

### 步驟 4: 注入至服務使用

**範例**: 在任何服務中注入 SeqOptions

```csharp
using Microsoft.Extensions.Options;
using ReleaseKit.Console.Options;

public class LoggingService
{
    private readonly SeqOptions _seqOptions;

    // 透過建構函式注入
    public LoggingService(IOptions<SeqOptions> seqOptions)
    {
        _seqOptions = seqOptions.Value;
    }

    public void InitializeLogging()
    {
        if (_seqOptions.Enabled && !string.IsNullOrEmpty(_seqOptions.ServerUrl))
        {
            Console.WriteLine($"啟用 Seq 日誌: {_seqOptions.ServerUrl}");
        }
    }
}
```

**✅ 檢查點**:
- 注入 `IOptions<SeqOptions>` 而非 `SeqOptions`
- 透過 `.Value` 屬性存取設定值
- 不在 Options 類別中實作業務邏輯

---

## 情境 2: 新增包含集合的設定

**目標**: 新增多個 Azure DevOps 專案的設定

### 步驟 1: 建立主 Options 類別

**檔案路徑**: `/src/ReleaseKit.Console/Options/AzureDevOpsOptions.cs`

```csharp
namespace ReleaseKit.Console.Options;

/// <summary>
/// Azure DevOps 設定選項
/// </summary>
public class AzureDevOpsOptions
{
    /// <summary>
    /// Azure DevOps 組織 URL
    /// </summary>
    public string OrganizationUrl { get; set; } = string.Empty;

    /// <summary>
    /// 個人存取權杖 (PAT)
    /// </summary>
    public string PersonalAccessToken { get; set; } = string.Empty;

    /// <summary>
    /// 專案設定清單
    /// </summary>
    public List<AzureDevOpsProjectOptions> Projects { get; set; } = new();
}
```

### 步驟 2: 建立巢狀 Options 類別

**檔案路徑**: `/src/ReleaseKit.Console/Options/AzureDevOpsProjectOptions.cs`

⚠️ **注意**: 獨立檔案，遵循 One Class Per File 原則

```csharp
namespace ReleaseKit.Console.Options;

/// <summary>
/// Azure DevOps 專案設定選項
/// </summary>
public class AzureDevOpsProjectOptions
{
    /// <summary>
    /// 專案名稱
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// 工作項目類型（如 "Bug", "User Story"）
    /// </summary>
    public List<string> WorkItemTypes { get; set; } = new();

    /// <summary>
    /// 目標迭代路徑
    /// </summary>
    public string IterationPath { get; set; } = string.Empty;
}
```

### 步驟 3: 更新 appsettings.json

```json
{
  "AzureDevOps": {
    "OrganizationUrl": "https://dev.azure.com/myorg",
    "PersonalAccessToken": "",
    "Projects": [
      {
        "ProjectName": "BackendAPI",
        "WorkItemTypes": ["Bug", "User Story"],
        "IterationPath": "BackendAPI\\Sprint 1"
      },
      {
        "ProjectName": "FrontendApp",
        "WorkItemTypes": ["Bug", "Task"],
        "IterationPath": "FrontendApp\\Sprint 1"
      }
    ]
  }
}
```

**✅ 檢查點**:
- 集合使用 JSON 陣列 `[]`
- 巢狀物件結構與 Options 類別對應
- 字串中的反斜線使用雙反斜線 `\\` 跳脫

### 步驟 4: 註冊與使用

**註冊** (ServiceCollectionExtensions.cs):
```csharp
services.Configure<AzureDevOpsOptions>(configuration.GetSection("AzureDevOps"));
```

**使用範例**:
```csharp
public class WorkItemService
{
    private readonly AzureDevOpsOptions _options;

    public WorkItemService(IOptions<AzureDevOpsOptions> options)
    {
        _options = options.Value;
    }

    public void ProcessProjects()
    {
        foreach (var project in _options.Projects)
        {
            Console.WriteLine($"處理專案: {project.ProjectName}");
            Console.WriteLine($"  工作項目類型: {string.Join(", ", project.WorkItemTypes)}");
            Console.WriteLine($"  迭代路徑: {project.IterationPath}");
        }
    }
}
```

---

## 情境 3: 新增環境特定覆寫

**目標**: 開發環境使用 localhost，生產環境使用實際 URL

### 步驟 1: 定義基礎設定

**appsettings.json** (所有環境的預設值):
```json
{
  "ExternalApi": {
    "BaseUrl": "https://api.production.com",
    "Timeout": 30,
    "MaxRetries": 3
  }
}
```

### 步驟 2: 開發環境覆寫

**appsettings.Development.json** (只覆寫 BaseUrl):
```json
{
  "ExternalApi": {
    "BaseUrl": "http://localhost:5000"
  }
}
```

**結果**: Development 環境
- `BaseUrl` = `http://localhost:5000` (覆寫)
- `Timeout` = `30` (繼承自基礎設定)
- `MaxRetries` = `3` (繼承自基礎設定)

### 步驟 3: 生產環境覆寫

**appsettings.Production.json** (完整定義):
```json
{
  "ExternalApi": {
    "BaseUrl": "https://api.production.com",
    "Timeout": 60,
    "MaxRetries": 5
  }
}
```

**結果**: Production 環境 - 使用生產專用的逾時與重試設定

### 步驟 4: QA 環境覆寫

**appsettings.Qa.json**:
```json
{
  "ExternalApi": {
    "BaseUrl": "https://api.qa.internal.com",
    "Timeout": 45
  }
}
```

**結果**: QA 環境
- `BaseUrl` = `https://api.qa.internal.com` (覆寫)
- `Timeout` = `45` (覆寫)
- `MaxRetries` = `3` (繼承自基礎設定)

### 環境變數覆寫（Docker / Kubernetes）

**docker-compose.yml**:
```yaml
services:
  release-kit:
    environment:
      - ExternalApi__BaseUrl=https://api.docker.local
      - ExternalApi__Timeout=120
```

**Kubernetes ConfigMap**:
```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: release-kit-config
data:
  ExternalApi__BaseUrl: "https://api.k8s.cluster.local"
  ExternalApi__MaxRetries: "10"
```

**優先級** (由低到高，後者覆蓋前者):
1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. 環境變數
4. User Secrets (僅 Development)

---

## 情境 4: 設定驗證（選用）

**目標**: 確保必填設定在應用程式啟動時存在且有效

### 方法 1: 啟動時驗證（簡單場景）

**適用**: 必填欄位的 null/empty 檢查

**實作位置**: `ServiceCollectionExtensions.cs`

```csharp
public static IServiceCollection AddRedisServices(
    this IServiceCollection services, 
    IConfiguration configuration)
{
    // ✅ 啟動時驗證
    var connectionString = configuration["Redis:ConnectionString"] 
        ?? throw new InvalidOperationException("Redis:ConnectionString 組態設定不得為空");
    
    var instanceName = configuration["Redis:InstanceName"] 
        ?? throw new InvalidOperationException("Redis:InstanceName 組態設定不得為空");

    // 註冊服務
    services.AddSingleton<IConnectionMultiplexer>(sp => 
    {
        var configOptions = ConfigurationOptions.Parse(connectionString);
        return ConnectionMultiplexer.Connect(configOptions);
    });

    return services;
}
```

**優點**:
- ✅ 簡單直接，無需額外套件
- ✅ 錯誤訊息清晰
- ✅ 立即失敗（Fail Fast）

**缺點**:
- ❌ 驗證邏輯與服務註冊混合
- ❌ 不適合複雜的驗證規則

### 方法 2: IValidateOptions<T>（複雜場景）

**適用**: 跨屬性驗證、複雜邏輯驗證

**步驟 1: 建立 Validator**

**檔案路徑**: `/src/ReleaseKit.Console/Validators/GitLabOptionsValidator.cs`

```csharp
using Microsoft.Extensions.Options;
using ReleaseKit.Console.Options;

namespace ReleaseKit.Console.Validators;

/// <summary>
/// GitLab 設定驗證器
/// </summary>
public class GitLabOptionsValidator : IValidateOptions<GitLabOptions>
{
    public ValidateOptionsResult Validate(string? name, GitLabOptions options)
    {
        // 驗證 1: ApiUrl 不得為空
        if (string.IsNullOrEmpty(options.ApiUrl))
        {
            return ValidateOptionsResult.Fail("GitLab ApiUrl 不得為空");
        }

        // 驗證 2: ApiUrl 必須使用 HTTPS
        if (!options.ApiUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Fail("GitLab ApiUrl 必須使用 HTTPS 協定");
        }

        // 驗證 3: AccessToken 不得為空
        if (string.IsNullOrEmpty(options.AccessToken))
        {
            return ValidateOptionsResult.Fail("GitLab AccessToken 不得為空");
        }

        // 驗證 4: 至少需要一個專案
        if (options.Projects.Count == 0)
        {
            return ValidateOptionsResult.Fail("GitLab 設定至少需要一個專案");
        }

        // 驗證 5: 每個專案的必填欄位
        for (int i = 0; i < options.Projects.Count; i++)
        {
            var project = options.Projects[i];
            
            if (string.IsNullOrEmpty(project.ProjectPath))
            {
                return ValidateOptionsResult.Fail($"GitLab 專案 #{i + 1} 的 ProjectPath 不得為空");
            }

            if (string.IsNullOrEmpty(project.TargetBranch))
            {
                return ValidateOptionsResult.Fail($"GitLab 專案 #{i + 1} 的 TargetBranch 不得為空");
            }
        }

        return ValidateOptionsResult.Success;
    }
}
```

**步驟 2: 註冊 Validator**

**ServiceCollectionExtensions.cs**:
```csharp
public static IServiceCollection AddConfigurationOptions(
    this IServiceCollection services, 
    IConfiguration configuration)
{
    // 註冊 Options
    services.Configure<GitLabOptions>(configuration.GetSection("GitLab"));
    
    // ✨ 註冊 Validator
    services.AddSingleton<IValidateOptions<GitLabOptions>, GitLabOptionsValidator>();

    return services;
}
```

**步驟 3: 觸發驗證**

驗證會在首次存取 `IOptions<T>.Value` 時自動執行：

```csharp
public class SomeService
{
    public SomeService(IOptions<GitLabOptions> options)
    {
        // ⚠️ 若驗證失敗，此行會拋出 OptionsValidationException
        var gitlabOptions = options.Value;
    }
}
```

**提前觸發驗證**（建議在 Program.cs）:
```csharp
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddConfigurationOptions(context.Configuration);
        services.AddApplicationServices();
    })
    .Build();

// ✅ 啟動前驗證所有設定
var gitlabOptions = host.Services.GetRequiredService<IOptions<GitLabOptions>>().Value;

await host.RunAsync();
```

### 方法比較

| 方法 | 適用場景 | 優點 | 缺點 |
|------|---------|------|------|
| **Non-nullable Types** | 編譯時檢查 | 零成本、最安全 | 只能檢查 null |
| **啟動時驗證** | 簡單 null/empty 檢查 | 簡單直接 | 與註冊邏輯混合 |
| **IValidateOptions** | 複雜驗證邏輯 | 分離關注點、可測試 | 需額外程式碼 |

---

## 常見問題 (FAQ)

### Q1: 何時使用 IOptions<T> vs IOptionsSnapshot<T> vs IOptionsMonitor<T>？

**A**: 專案為 Console Application，預設使用 `IOptions<T>`

- **IOptions<T>** - 設定於啟動時載入，不支援熱重載
- **IOptionsSnapshot<T>** - 適用於 Web API 的 Scoped 生命週期（每個請求重新載入）
- **IOptionsMonitor<T>** - 支援即時變更通知（appsettings.json 修改時觸發）

### Q2: 如何在開發環境儲存敏感資訊（如 API Token）？

**A**: 使用 User Secrets

```bash
# 設定 GitLab AccessToken
dotnet user-secrets set "GitLab:AccessToken" "glpat-xxxxxxxxxxxxx"

# 設定 Redis 密碼
dotnet user-secrets set "Redis:ConnectionString" "localhost:6379,password=secret"
```

**檔案位置**:
- Windows: `%APPDATA%\Microsoft\UserSecrets\release-kit-secrets-2026\secrets.json`
- macOS/Linux: `~/.microsoft/usersecrets/release-kit-secrets-2026/secrets.json`

⚠️ **User Secrets 只在 Development 環境生效**

### Q3: 如何處理可選的設定區段？

**A**: 使用可空型別或預設值

```csharp
public class OptionalFeatureOptions
{
    /// <summary>
    /// 是否啟用功能
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// 選用的 API URL（Enabled = true 時必填）
    /// </summary>
    public string? ApiUrl { get; set; }
}
```

**服務中判斷**:
```csharp
public class FeatureService
{
    private readonly OptionalFeatureOptions _options;

    public FeatureService(IOptions<OptionalFeatureOptions> options)
    {
        _options = options.Value;
    }

    public void Execute()
    {
        if (!_options.Enabled)
        {
            Console.WriteLine("功能未啟用，跳過執行");
            return;
        }

        if (string.IsNullOrEmpty(_options.ApiUrl))
        {
            throw new InvalidOperationException("功能已啟用但 ApiUrl 未設定");
        }

        // 執行功能
    }
}
```

### Q4: 如何在測試中 Mock Options？

**A**: 使用 `Options.Create<T>()`

```csharp
using Microsoft.Extensions.Options;
using Xunit;

public class ServiceTests
{
    [Fact]
    public void Service_Should_UseConfiguration()
    {
        // Arrange
        var mockOptions = Options.Create(new GitLabOptions
        {
            ApiUrl = "https://gitlab.test.com",
            AccessToken = "test-token",
            Projects = new List<GitLabProjectOptions>
            {
                new() { ProjectPath = "test/project", TargetBranch = "main" }
            }
        });

        var service = new GitLabService(mockOptions);

        // Act & Assert
        Assert.NotNull(service);
    }
}
```

### Q5: 環境變數如何對應巢狀設定？

**A**: 使用雙底線 `__` 或冒號 `:` 分隔

```bash
# Bash (建議使用雙底線)
export GitLab__ApiUrl="https://gitlab.com"
export GitLab__Projects__0__ProjectPath="group/project"
export GitLab__Projects__0__TargetBranch="main"

# PowerShell
$env:GitLab__ApiUrl = "https://gitlab.com"
$env:GitLab__Projects__0__ProjectPath = "group/project"
```

**Docker Compose**:
```yaml
environment:
  - GitLab__ApiUrl=https://gitlab.docker.local
  - GitLab__Projects__0__ProjectPath=mygroup/api
  - GitLab__Projects__0__TargetBranch=develop
```

---

## 下一步

- 📖 閱讀 [data-model.md](./data-model.md) 了解完整的設計規範
- 📖 閱讀 [research.md](./research.md) 了解技術決策的背景
- 🔍 參考現有的 `GitLabOptions.cs` 與 `BitbucketOptions.cs` 作為範本
- ✅ 在 Code Review 時確保新設定遵循本指南的規範

---

## 變更歷史

| 版本 | 日期 | 變更內容 |
|------|------|---------|
| 1.0 | 2025-01-28 | 初始版本 |
